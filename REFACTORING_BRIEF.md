# REFACTORING_BRIEF

Операционный бриф репозитория OOAD_FIT242-2. Зафиксирован полный рефакторинг по эвристикам AGENTS.md (октябрь 2026). Документ сохраняет команды верификации, ограничения и итоговое состояние.

## 1. Команды верификации

| Действие | Команда |
|----------|---------|
| Восстановление зависимостей | `dotnet restore Game.sln` |
| Сборка | `dotnet build Game.sln --configuration Release` |
| Тесты | `dotnet test Game.Tests/Game.Tests.csproj --configuration Release --no-build` |
| Проверка форматирования (шаг Checkstyle в CI) | `dotnet format --verify-no-changes` |
| Покрытие | `dotnet test Game.Tests/Game.Tests.csproj --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory coverage` |

SDK закреплён через `global.json` (10.0.300, rollForward latestFeature). TargetFramework: net10.0. Тестовый стек: xUnit 2.9.2, Moq 4.20.72. Тесты выполняются последовательно (`xunit.runner.json`, `parallelizeTestCollections: false`) — IoC-контейнер внешних DLL является глобальным статическим состоянием, параллельные коллекции делают прогоны недетерминированными.

## 2. Метрики

| Метрика | До рефакторинга | После рефакторинга |
|---------|-----------------|--------------------|
| Сборка | 2 предупреждения CS8625 | 0 предупреждений |
| Тесты | 97 (с недетерминированными падениями на параллельных коллекциях) | 108, детерминированные |
| `dotnet format --verify-no-changes` | чисто | чисто |
| Покрытие строк | 96,27% | 97,4%+ |
| Классы без покрытия | RegisterIoCDependencyGameObjectRepository (0%) | нет |

## 3. Выполненные этапы (атомарные коммиты в порядке следования)

1. Гигиена: файл `IMassageReciever.cs` переименован в `IMessageReceiver.cs` по имени типа, тестовые папки слиты, исключение SendCommand переформулировано (`Message receiver is busy`), дубликат null-теста удалён, предупреждения nullable устранены.
2. Структура: команды в `Game/Commands` (namespace `Game.Commands`), регистрации в `Game.IoC`, доменные интерфейсы в `Game`, имена файлов равны именам типов, общая фикстура `IoCTestScope`, единый `namespace Game.Tests`, последовательный запуск тестовых коллекций.
3. Value-типы: `Vector` и `Angle` — иммутабельные sealed-типы; мутабельное статическое поле `Angle.denominator` заменено константой, координаты `Vector` копируются защитно, реализованы `IEquatable<T>`, добавлены граничные тесты.
4. IoC-слой: guard clauses через `StrategyArguments.Get`, строковые ключи вынесены в `IocDependencyKeys`, удалены лишние касты в `StartCommand`, все классы sealed.
5. Генератор адаптеров: `AdapterSourceBuilder` переписан декларативным LINQ-конвейером, генерируемый код — sealed-классы с primary constructor, `FrozenSet` в компиляторе, сообщения инфраструктуры на английском, тесты и `adapter_generator_architecture.md` синхронизированы.
6. Покрытие: тесты на `RegisterIoCDependencyGameObjectRepository` и ветку ошибки компиляции адаптера.

## 4. Жёсткие ограничения (актуальны для любых последующих изменений)

1. `Common/core.dll` и `Common/scopes.dll` — внешние сборки курса (namespace `App`, `App.Scopes`; типы `Ioc`, `InitCommand`, скоупы, `ICommand`). Чёрные ящики: не рефакторить, не заменять, API считать фиксированным контрактом.
2. Строковые ключи IoC, приходящие извне (`IoC.Register`, `IoC.Scope.Create`, `IoC.Scope.Current.Set`, `Game.CommandsReceiver`), переименовывать нельзя. Ключи репозитория объявлены константами в `Game/IocDependencyKeys.cs` и должны меняться только там.
3. Регистрации в скоупах DLL накапливаются по цепочке тестов одного класса — поведение тестов зависит от порядка их выполнения; новые тесты должны быть самодостаточными и не полагаться на регистрации из других тестов.
4. Конструктор `MacroCommand` принимает `ICommand[]`: параметр-интерфейс (`IReadOnlyList<ICommand>`) попадает под критерии `CommandScanner` и ломает генерацию адаптеров.
5. CI должен оставаться зелёным: `dotnet format --verify-no-changes`, сборка, тесты, покрытие строк не ниже 90%.

## 5. Состояние git

Все коммиты локальные, не запушены. Автор — временная identity `Vladimir <vladimir@local>`: перед пушем задать реальный email (`git config user.email`) и переписать автора при необходимости.
