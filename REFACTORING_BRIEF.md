# REFACTORING_BRIEF

Операционный бриф для рефакторинга этого репозитория по эвристикам AGENTS.md. Прочитай полностью до первого изменения кода.

## 1. Команды верификации (весь цикл выполняется локально, без настройки окружения)

| Действие | Команда |
|----------|---------|
| Восстановление зависимостей | `dotnet restore Game.sln` |
| Сборка | `dotnet build Game.sln --configuration Release` |
| Тесты | `dotnet test Game.Tests/Game.Tests.csproj --configuration Release --no-build` |
| Проверка форматирования (шаг Checkstyle в CI) | `dotnet format --verify-no-changes` |
| Покрытие | `dotnet test Game.Tests/Game.Tests.csproj --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory coverage` |

SDK закреплён через `global.json` (10.0.300, rollForward latestFeature). TargetFramework: net10.0. Тестовый стек: xUnit 2.9.2, Moq 4.20.72.

## 2. Baseline (зафиксирован 2026-10-25)

- Сборка: успешна, 2 предупреждения CS8625 (`Game.Tests/commands/SendCommandTest.cs`, строки 42 и 49 — null в non-nullable ссылочный тип).
- Тесты: 97 из 97 проходят.
- `dotnet format --verify-no-changes`: отклонений нет.
- Покрытие: строк 96,27%, ветвей 85,41%. Порог CI — 90% по строкам с `fail_below_min: true`: при падении ниже 90% CI краснеет.
- Класс без покрытия: `Game.RegisterIoCDependencyGameObjectRepository` (0 из 5 строк) — единственная «дыра».

## 3. Исправлено в подготовке (не чинить повторно)

1. Коллизия регистров путей: в индексе были `Game.Tests/IoC/RegisterIoCDependencySendCommandTests.cs` и `Game.Tests/Ioc/RegisterIoCDependencySendCommandTests.cs` с идентичным содержимым; на Windows второй файл не выгружался из-за коллизии. Дубль `Ioc/` удалён, коммит `52ca664`. Корректный путь — `Game.Tests/IoC/`.
2. CI падал на шаге «Build Project»: workflow ставил .NET SDK 8.0.x при target net10.0 (ошибка NETSDK1045). Версия поднята до 10.0.x и добавлен `global.json`, коммит `23542df`. Локально вся цепочка (restore, format, build, test) зелёная; после пуша CI должен позеленеть.

## 4. Жёсткие ограничения

1. `Common/core.dll` и `Common/scopes.dll` — внешние сборки курса (namespace `App`, `App.Scopes`; типы `Ioc`, `InitCommand`, скоупы, `ICommand`). Это чёрные ящики: не рефакторить, не заменять, API считать фиксированным контрактом.
2. Строковые ключи IoC, приходящие извне (`IoC.Register`, `IoC.Scope.Create`, `IoC.Scope.Current.Set`, `Game.CommandsReceiver`, `Actions.StartIoC`, `Actions.StopIoC`), переименовывать нельзя. Ключи, объявленные внутри репозитория (`Commands.Move`, `Commands.Rotate`, `Commands.Send`, `Adapters.IMovingObject`, `Properties.Position`, `Macro.Move`, `Macro.Rotate`, `Specs.Move`, `Specs.Rotate`, `Repository.GameObject`, `Commands.Game_MoveCommand`, `Adapters.Game_IMovingObjectAdapter`), — межкомпонентный контракт: менять только синхронно по всему коду и тестам.
3. Поведение, зафиксированное тестами (например, `IndexOutOfRangeException` при `Ioc.Resolve<ICommand>("Commands.Move")` без аргументов в `RegisterIoCDependencyMoveCommandTests`), менять только осознанно, с правкой теста в том же коммите.
4. CI должен оставаться зелёным: `dotnet format --verify-no-changes`, успешная сборка, тесты, покрытие строк не ниже 90%.
5. При изменении контрактов генератора адаптеров обнови `adapter_generator_architecture.md` (описывает пайплайн и правила именования ключей).

## 5. Точки рефакторинга (выявлены при подготовке)

| Место | Проблема |
|-------|----------|
| `Game/IMassageReciever.cs` | Имя файла с двойной опечаткой, внутри объявлен интерфейс `IMessageReceiver`. Переименовать файл в `IMessageReceiver.cs` — санкционировано |
| `Game/Angle.cs` | Мутабельная `private static int denominator` (разделяемое изменяемое состояние), `Numerator` с `private set`, выражение `% denominator` дублируется |
| `Game/Vector.cs` | Параметр конструктора с заглавной буквы (`int[] Coordinates`), внутренний `int[]` мутабелен, нет защитных проверок на пустой/нулевой вход |
| `Game/IoC/RegisterIoCDependencyMoveCommand.cs` | Небезопасный каст `(IMovingObject)adapter`, обращение `args[0]` без guard clause |
| `Game/IoC/RegisterIoCDependencyGameObjectRepository.cs` | 0% покрытия тестами |
| `Game.Tests/commandTests/` и `Game.Tests/commands/` | Две папки тестов команд с дублирующим назначением — слить в `commands` |
| `Game.Tests/commands/SendCommandTest.cs:42,49` | Предупреждения CS8625 (nullability) |
| `Game/AdapterGenerator/AdapterSourceBuilder.cs`, `AdapterGenerator.cs` | `foreach` и пошаговое накопление — по разделу 8 AGENTS.md подлежит LINQ-конвейерам; `StringBuilder` — инфраструктура генерации кода, а не доменное состояние, применять правило с границей из самих эвристик |

## 6. Рекомендуемый порядок работы

1. Гигиена: переименование `IMassageReciever.cs`, слияние тестовых папок, устранение CS8625. Атомарный коммит, тесты после каждого шага.
2. Value-типы: `Vector` и `Angle` — иммутабельность (раздел 8), покрытие граничных классов эквивалентности (T1, T5).
3. IoC-регистрации: guard clauses (G3), вынос строковых ключей в именованные константы (G25), устранение небезопасных кастов.
4. AdapterGenerator: LINQ-ификация, именование.
5. Дописать тесты на `RegisterIoCDependencyGameObjectRepository` до покрытия.

После каждого шага: `dotnet test`, `dotnet format --verify-no-changes`; коммитить атомарно, не смешивать этапы.

## 7. Состояние git

Подготовительные коммиты (`52ca664`, `23542df`, коммит с AGENTS.md и этим брифом) сделаны локально в main и не запушены. Автор — временная identity `Vladimir <vladimir@local>`: перед пушем задай реальный email (`git config user.email`) и при необходимости перепиши автора (`git commit --amend --reset-author` на вершине или `git rebase --exec` по трём коммитам).
