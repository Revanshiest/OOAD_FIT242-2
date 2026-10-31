# Server Architecture Module Map

Реализация архитектуры высоконагруженного игрового сервера на базе Модели Акторов. Каждый пункт технического задания привязан к модулю в дереве `Game/Server` (namespace `GameServer.*`).

## 1. Модель Акторов и баланс CAP

- Внешний контур (Endpoint) построен по схеме AP: прием пакета без блокировок, минимальное время обработки.
- Внутренний контур (рабочие потоки и игры) построен по схеме CP: все команды одной игры обрабатываются последовательно на одном закрепленном потоке.
- Актор: `GameWorker` (рабочий поток), `Game` (игра), `GameProxy` (пересылатель). Каждый актор изменяет только собственное состояние и общается асинхронными сообщениями.

## 2. Игра: Scope и конвейер создания объектов

- `Game = Scope + GameCommandQueue`: `GameServer.Games.Game`, `GameServer.Games.Scope` (обертка над словарем `id: IGameItem`).
- Конвейер создания объекта из четырех шагов: `CreateGameItemCommand`, `AssignGameItemTypeCommand`, `SetGameItemPositionCommand`, `SetGameItemVelocityCommand`, собирается в `MacroCommand` через `CreateGameItemPipeline` и IoC-стратегию `Games.CreateItem` (`GameServer.IoC.RegisterIoCDependencyGameItemPipeline`).
- Инвариант отсутствия синхронизации внутри игры: `Scope`, `GameItem`, `GameCommandQueue` не содержат блокировок; доступ монопольно с потока-владельца.

## 3. Входной шлюз и первичный роутер

- `GameServer.Endpoint.Endpoint`: проверка токена (`WhitelistTokenAuthenticator`), поверхностная десериализация заголовка (`InboundMessageParser`), немедленная маршрутизация. Транспорт: `HttpListenerEndpointAdapter` (HTTP POST/GET JSON).
- Контракт сообщения: `key` (hash потока), `id` (игра), `token`, `command`, `args`.
- Первичный роутер без ветвлений: `GameServer.Routing.GeneratedRouterDispatcher` генерирует делегат диспетчеризации через `Expression.Compile` (runtime codegen): `table[(uint)routingKey % K]`, O(1), ни одного if/switch. Разрешение зависимостей через IoC: `GameServer.IoC.RegisterIoCDependencyServerRouter`, ключ `Server.Router.Create`.
- Ответ сервера всегда содержит актуальный `routingKey` для клиента.

## 4. Thread-safe Queue и производитель-потребитель

- `GameServer.Messaging.BoundedMessageQueue<T>`: bounded-очередь на базе `System.Threading.Channels` (FullMode.Wait, SingleReader), инвариант `0 <= count < n` (при `count == n` `TryWrite` возвращает false, блокирующая запись доступна только резервному потоку).
- Разделение интерфейсов ISP: `IMessageQueueWriter<T>` (передается производителю) и `IMessageQueueReader<T>` (передается потребителю).
- Залповое опустошение: `DrainAllAvailable()` забирает все накопившиеся сообщения за один проход.

## 5. Рабочий поток: вторичный роутер и планировщик

- `GameServer.Workers.GameWorker`: фаза маршрутизации (дренаж входной очереди, поиск/создание игры, десериализация команды) и фаза диспетчеризации (`IGameScheduler`).
- `GameServer.Workers.PriorityRoundRobinScheduler`: приоритетные кольца Round-Robin (`GamePriority.High` обслуживается раньше `Normal`, все ярусы проходят каждый цикл — отсутствие голодания), квант команд на активацию (`CommandQuantumPerGameActivation`).
- Состояние await: `Game.SuspendForExternalEvent` исключает игру из ротации до `Resume`; сообщение `Server.Resume` возвращает игру в работу.
- Watchdog: игры в ожидании дольше `MaxExternalAwaitDuration` (5 минут) завершаются (`GameTerminationReason.WatchdogAwaitTimeout`).
- Защита от флуда: token bucket на игру (`CommandRateLimiter`) плюс эвикция при переполнении личной очереди (`MaxQueueLengthPerGame`, `GameTerminationReason.FloodQueueOverflow`).

## 6. Решардинг

- Таблица решардинга: `GameServer.Resharding.ReshardingTable` (`id Game -> id Thread`), учитывается первичным роутером поверх хеш-диспетчеризации.
- Proxy: `GameProxy` остается в карте источника и пересылает запоздавшие сообщения в поток назначения; при переполнении целевой очереди сообщение уходит в резервный поток `BackupTransitThread`, который принимает блокировки на себя (разрыв циклического ожидания, задача об обедающих философах).
- Координатор: `GameMigrationCoordinator` (`BeginThreadMigration`): обновление таблицы + команда `Server.MigrateOut` исходному потоку.
- Алгоритм переноса: барьер `Tick` -> дрены команд -> снимок `GameSnapshot` (`JsonServerSnapshotCodec`) -> `Server.MigrateIn` на целевом потоке -> восстановление `Scope` и постановка в планировщик. Клиент получает новый `hash` в ответе.

## 7. Жизненный цикл: Tick, Soft Stop, Hard Stop

- `Server.Tick`: глобальный маркер (запечатывает весь поток и все его игры) или точечный (`args.gameId`). После маркера клиентские команды в очередь не принимаются.
- `GameServer.Lifecycle.ServerShutdownCoordinator.SoftStop()`: маркеры, ожидание дрена с таймаутом, финальные снимки всех игр в `ISnapshotStore`, штатная остановка потоков.
- `HardStop()`: немедленная остановка потоков без дрена (используется автоматически при превышении таймаута Soft Stop).

## 6.1. Масштабирование и нагрузочное тестирование

- `GameServer.Scaling.WorkerPool`: динамический запуск/остановка рабочих потоков (`StartWorker`/`StopWorker`), постинг сообщений (`TryPost`), статистика (`CollectStatistics`), лимит игр (`MaxActiveGames`).
- Нагрузочные тесты: `Game.Tests/Server/Load/ServerLoadTests` — конфигурация 1 (один рабочий поток) и конфигурация 2 (два потока: единый общий роутер против изолированных роутеров на поток); 20000 сообщений на прогон, метрики выводятся в лог теста.

## Карта соответствия ТЗ

| Раздел ТЗ | Модули |
|-----------|--------|
| 1. CAP, Модель Акторов | `GameServer.Workers.GameWorker`, `GameServer.Games.Game`, `GameServer.Endpoint` |
| 2. Game = Scope + Queue, конвейер | `GameServer.Games.*`, `GameServer.IoC.RegisterIoCDependencyGameItemPipeline` |
| 3. Endpoint, кодогенерация роутера | `GameServer.Endpoint.*`, `GameServer.Routing.GeneratedRouterDispatcher` |
| 4. Thread-safe Queue, ISP, залповое опустошение | `GameServer.Messaging.*` |
| 5. Вторичный роутер, планировщик | `GameServer.Workers.*` |
| 6. Решардинг, Proxy, резервный поток | `GameServer.Resharding.*`, `GameServer.Scaling.WorkerPool` |
| 6.1. Масштабирование, нагрузочные тесты | `GameServer.Scaling.*`, `Game.Tests/Server/Load` |
| 7. Tick, Soft/Hard Stop, Snapshot, миграция | `GameServer.Resharding.*`, `GameServer.Lifecycle.*` |
