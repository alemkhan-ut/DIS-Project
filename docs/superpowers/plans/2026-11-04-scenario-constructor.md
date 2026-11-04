# Конструктор учебных сценариев — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Правила проекта — `CLAUDE.md`, `Rules/GuideLine.md`, `Rules/OutputFormat.md`** (без тестов, без push, стиль кода, префабы, формат отчёта, UX инспектора).

**Goal:** Система в Unity Editor, где человек без навыков программирования в удобных кастомных инспекторах собирает учебный сценарий из переиспользуемых шагов и проходит его в режимах «Тренировка» и «Экзамен». Прохождение пишется в журнал, который можно заново оценить другим анализатором.

**Architecture:** Шаги, режимы, логгеры, анализаторы и ключи событий хранятся как ScriptableObject-ассеты. Сцена связана с ассетами только через ассеты-ключи событий: компоненты-источники (Button / Trigger / Collision / UnityEvent) публикуют ключ в статическую шину. Ядро исполнения `ScenarioSession` — чистый C#. Сессия пишет записи в логгер по общему контракту `LogRecord`. Анализаторы работают только с готовым `ScenarioLog`. Кастомные IMGUI-инспекторы дают drag-and-drop шагов между стадиями и группами, выбор ассетов из выпадающих списков и всю настройку в одном месте.

**Tech Stack:** Unity 6000.3.21f1, C#, URP 17.3, uGUI (Legacy Text), Input System 1.20, IMGUI Editor (`ReorderableList`, `DragAndDrop`).

**Spec:** `Тестовое.docx` в корне репозитория. Ссылки вида «ТЗ §4» указывают на его разделы.

## Global Constraints

- **Тестов нет** (решение пользователя). Задача проверяется так: консоль Unity без ошибок (`Unity_ReadConsole`) плюс ручной чек-лист задачи в Editor или Play Mode.
- **Git:** ветка `feature/scenario-kit`, локальный коммит в конце каждой задачи. **`git push` и merge в `main` запрещены** до проверки пользователем.
- Сторонние ассеты, плагины, фреймворки и конструкторы не используются, только стандартные пакеты Unity.
- Порядок шагов, группы, способ активации, обработка ошибок, режим, порог экзамена, логгер и анализатор настраиваются в Inspector. C# при этом не правится.
- Шаги не дублируются: все режимы ссылаются на одни и те же ассеты `StepDefinition`.
- Тренировка: 10 баллов за успех с 1-й попытки, 5 — со 2-й, 0 — с 3-й и далее. Максимум = 10 × количество шагов.
- Экзамен: штраф Failed — 10, Skipped — 5. Итог = max(0, 100 − сумма штрафов). Порог нарушений в группе по умолчанию 2, редактируется.
- Действие для шага вне его очереди по умолчанию считается нарушением. Альтернатива `IgnoreUntilActive`: шаг до своей очереди не принимает действия.
- Ввод только через новый Input System (`Keyboard.current`). UI на uGUI с `Text` и `LegacyRuntime.ttf`.
- Код, файлы, сцена и префабы — по `Rules/GuideLine.md` и разделу «Применение гайдлайна» в `CLAUDE.md`. Действуют правила: без `var`, скобки с новой строки, события `…Event`, обработчики `…Handler`, константы `UNDER_SCORE`, одна сущность (в том числе `enum`/`struct`) на файл, методы с данными — `Get…`.
- **Имена полей в задачах даны без префикса.** В коде это `[SerializeField] private _имя` (`sequence` → `_sequence`) со свойством PascalCase на чтение. Имена фиксированы: по ним инспекторы находят `SerializedProperty`. DTO журнала (`LogRecord`, `LogField`, `ScenarioLog`) используют публичные поля PascalCase — это формат JSON-файла.
- Неймспейсы `ScenarioKit.<Область>` (`Events`, `Sources`, `Steps`, `Modes`, `Logging`, `Execution`, `Analysis`, `Validation`, `Demo`), для редактора — `ScenarioKit.EditorTools`.
- Код на английском. UI, инспекторы, валидатор и README — на русском.

## Review Focus (ручные проверки — тестов нет)

1. **Одно событие завершает последний шаг стадии и одновременно является success-событием шага следующей стадии.** Только что активированный шаг не должен его «съесть»: обработка идёт по снимку статусов. Проверка в Task 7.
2. **Действие вне очереди для шага из ещё не начатой экзаменационной группы.** Шаг становится Failed, нарушение засчитывается его группе. Если порог уже достигнут к старту группы, группа пропускается сразу, сессия не зависает. Проверка в Task 7.
3. **Перезапуск посреди прохождения.** В журнал пишется `ScenarioAborted`, события в старую сессию игнорируются. Анализ такого журнала даёт предупреждение, а не исключение. Проверка в Task 16.
4. **Пустая стадия, пустая группа или режим без шагов.** Сессия не зависает, инспектор показывает предупреждение. Проверки в Task 7 и Task 13.
5. **Повреждённый или «чужой» JSON-журнал.** Загрузка и анализ дают предупреждение, а не исключение. Проверка в Task 16.
6. **Инспектор: перетаскивание шага, который уже есть в режиме.** Шаг переносится, а не дублируется. Отмена (Ctrl+Z) возвращает прежнее состояние. Проверка в Task 13.

---

## Структура файлов

```
Assets/ScenarioKit/
  Runtime/                         ScenarioKit.Runtime.asmdef (refs: UnityEngine.UI)
    Events/    ScenarioEventKey, ScenarioStateKey, IWorldState, ScenarioEventBus
    Sources/   ScenarioEventSource, ButtonEventSource, TriggerEventSource, CollisionEventSource,
               UnityEventSource, ToggleStateSource
    Steps/     StepDefinition, StepTracker, StepVerdict, StepVerdictType, EventStep,
               StateRequirement, ViolationRule
    Modes/     ActivationStage, StepGroup, OutOfOrderPolicy, ScenarioMode, TrainingMode, ExamMode
    Logging/   LogField, LogRecord, LogRecordTypes, ScenarioLog, IScenarioLogger, ScenarioLoggerAsset,
               MemoryLoggerAsset, MemoryScenarioLogger, JsonFileLoggerAsset, JsonFileScenarioLogger,
               ScenarioLogStorage
    Execution/ StepStatus, ScenarioSession, ScenarioRunner
    Analysis/  StepScore, AnalysisResult, StepFacts, LogDigest, ScenarioAnalyzer, TrainingAnalyzer,
               ExamAnalyzer, ReportFormatter
    Validation/ ValidationSeverity, ValidationMessage, ScenarioValidator
    ScenarioDefinition
  Editor/                          ScenarioKit.Editor.asmdef (Editor only), namespace ScenarioKit.EditorTools
    Common/    ScenarioKitStyles, AssetPickerField, NamePromptPopup, ScenarioAssetDrawer (базовый generic),
               EventKeyDrawer, StateKeyDrawer, StepDefinitionDrawer, ScenarioModeDrawer,
               LoggerAssetDrawer, AnalyzerDrawer, StepChip, StepDragAndDrop, StepDragData, ScenarioUsageIndex
    Inspectors/ EventStepEditor, ScenarioModeEditor, ScenarioDefinitionEditor, ScenarioRunnerEditor,
               ScenarioEventSourceEditor (базовый), ButtonEventSourceEditor, TriggerEventSourceEditor,
               CollisionEventSourceEditor, UnityEventSourceEditor, ToggleStateSourceEditor
    Windows/   ScenarioLogWindow (Task 17, опционально)
  Demo/
    Scripts/   ScenarioKit.Demo.asmdef (refs: Runtime, Unity.InputSystem, UnityEngine.UI)
               SimplePlayerMover, ResetPose, ScenarioHud, ModeButton
    Prefabs/   Player, Trigger Zone (+ Variants Zone A/B/C/D), Pushable Box, Wall, Marker Sphere,
               Scenario Runner, UI/: Action Button, Action Toggle, Valve Toggle, Mode Button, Scenario Hud
    Data/      Events/, States/, Steps/, Modes/, Loggers/, Analyzers/, DemoScenario.asset
    Scenes/    ScenarioDemo.unity
Rules/         GuideLine.md, OutputFormat.md
README.md
```
(у каждого имени — свой файл `<Имя>.cs`)

## Проверка в каждой задаче

- **Компиляция:** после сохранения файлов дождаться перекомпиляции в Unity (Unity MCP `Unity_ReadConsole`, тип Error/Warning). Ожидается: ни одной ошибки и ни одного предупреждения из `Assets/ScenarioKit`.
- **Коммит:** `git add <файлы задачи>` → `git commit -m "<сообщение>"` в ветке `feature/scenario-kit`, **без push**.
- **Отчёт** пользователю — в формате из `CLAUDE.md`.

---

### Task 1: Ветка и каркас

**Files:**
- Create: `Assets/ScenarioKit/Runtime/ScenarioKit.Runtime.asmdef`, `Editor/ScenarioKit.Editor.asmdef`, `Demo/Scripts/ScenarioKit.Demo.asmdef`
- Commit: `CLAUDE.md`, `docs/superpowers/plans/2026-11-04-scenario-constructor.md`

**Interfaces:**
- Produces: сборки `ScenarioKit.Runtime` (refs `UnityEngine.UI`), `ScenarioKit.Editor` (refs Runtime, `includePlatforms: ["Editor"]`), `ScenarioKit.Demo` (refs Runtime, `Unity.InputSystem`, `UnityEngine.UI`).

- [ ] **Step 1: Спросить пользователя** про незакоммиченные изменения в `main`: удалённый `Assets/Editor/HubForceResolve.cs` и изменённые ProjectSettings. Сделать по ответу.
- [ ] **Step 2: Создать ветку** `git checkout -b feature/scenario-kit`.
- [ ] **Step 3: Создать три asmdef и пустые папки по структуре.** Проверка: консоль без ошибок.
- [ ] **Step 4: Commit** `chore: каркас ScenarioKit, правила проекта и план` (без push).

---

### Task 2: Ключи событий, состояние мира, шина

**Files:** Create `Runtime/Events/ScenarioEventKey.cs`, `ScenarioStateKey.cs`, `IWorldState.cs`, `ScenarioEventBus.cs`

**Interfaces:**
- Produces:
  - `[CreateAssetMenu(menuName="ScenarioKit/Событие")] public class ScenarioEventKey : ScriptableObject`: поле `description` (`[TextArea]`), свойство `Description`.
  - `[CreateAssetMenu(menuName="ScenarioKit/Состояние")] public class ScenarioStateKey : ScriptableObject`: так же.
  - `public interface IWorldState { bool Get(ScenarioStateKey key); }`. Ключ, которого нет, означает `false`.
  - `public static class ScenarioEventBus { event Action<ScenarioEventKey> RaisedEvent; void Raise(ScenarioEventKey key); void SetState(ScenarioStateKey key, bool value); IWorldState State { get; } void ResetState(); }`. `Raise(null)` ничего не делает. Подписчики и состояние сбрасываются в `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.

- [ ] **Step 1: Реализовать типы из блока Interfaces.** Состояние — `Dictionary<ScenarioStateKey,bool>` внутри приватного класса, который реализует `IWorldState`.
- [ ] **Step 2: Проверка.** Консоль без ошибок. Через Create → ScenarioKit создаются «Событие» и «Состояние»; затем удалить созданные ассеты.
- [ ] **Step 3: Commit** `feat: ключи событий, состояние мира и шина событий`.

---

### Task 3: Шаги — `StepDefinition`, `EventStep`

**Files:** Create `Runtime/Steps/StepDefinition.cs`, `StepTracker.cs`, `StepVerdict.cs`, `StepVerdictType.cs`, `EventStep.cs`, `StateRequirement.cs`, `ViolationRule.cs`

**Interfaces:**
- Consumes: `ScenarioEventKey`, `ScenarioStateKey`, `IWorldState`.
- Produces:
  - `public abstract class StepDefinition : ScriptableObject`: поля `id`, `instruction` (`[TextArea]`), свойства `Id`, `Instruction`; `public abstract StepTracker CreateTracker(); public abstract bool IsTriggeredBy(ScenarioEventKey key); public abstract IEnumerable<ScenarioEventKey> SuccessEvents { get; }`. `OnValidate`: если `id` пуст, подставить `name`.
  - `public abstract class StepTracker { public abstract StepVerdict Handle(ScenarioEventKey key, IWorldState world); public abstract void Reset(); }`
  - `public enum StepVerdictType { None, Progress, Completed, Violation }` (свой файл) и `public readonly struct StepVerdict { StepVerdictType Type; string Code; string Message; static None, Progress, Completed; static StepVerdict GetViolation(string code, string message); const string CODE_RULE="rule", CODE_WRONG_ORDER="wrong_order", CODE_STATE="state", CODE_OUT_OF_ORDER="out_of_order"; }`
  - `[CreateAssetMenu(menuName="ScenarioKit/Шаг (по событиям)")] public class EventStep : StepDefinition` с полями:
    - `sequence` (`List<ScenarioEventKey>`)
    - `strictOrder` (bool, по умолчанию true), `wrongOrderMessage` («Нарушен порядок действий»)
    - `requiredStates` (`List<StateRequirement>`), `stateMessage` («Не выполнено условие»)
    - `violations` (`List<ViolationRule>`)
    - `[Serializable] StateRequirement` (поля `key`, `expected` = true) и `[Serializable] ViolationRule` (поля `key`, `message`) — отдельные файлы.

Семантика `EventStep.Handle` (порядок проверок важен):
1. `key` совпал с одним из `violations[i].key` → `Violation(rule, message)`.
2. `key == sequence[progress]`: все `requiredStates` совпадают → `progress++`, затем `Completed` в конце последовательности или `Progress` в середине. Хотя бы одно состояние не совпало → `Violation(state, stateMessage)`.
3. `strictOrder` включён, а `key` — элемент `sequence` с индексом больше `progress` → `Violation(wrong_order, wrongOrderMessage)`.
4. Иначе `None`.

`Reset()` обнуляет `progress`. `IsTriggeredBy(key)` возвращает `sequence.Contains(key)`.

- [ ] **Step 1: Реализовать типы из блока Interfaces.** Трекер — приватный вложенный класс.
- [ ] **Step 2: Проверка.** Консоль без ошибок. EventStep создаётся из меню, поля видны в стандартном инспекторе. Временный ассет удалить.
- [ ] **Step 3: Commit** `feat: шаги сценария (последовательность событий, условия, нарушения)`.

---

### Task 4: Режимы, группы, стадии активации

**Files:** Create `Runtime/Modes/ActivationStage.cs`, `StepGroup.cs`, `OutOfOrderPolicy.cs`, `ScenarioMode.cs`, `TrainingMode.cs`, `ExamMode.cs`; заглушка `Runtime/Analysis/ScenarioAnalyzer.cs` (`public abstract class ScenarioAnalyzer : ScriptableObject {}`, дополняется в Task 8)

**Interfaces:**
- Produces:
  - `[Serializable] ActivationStage`: поле `steps` (`List<StepDefinition>`), свойство `Steps`. Шаги одной стадии активны одновременно, стадии идут по очереди.
  - `[Serializable] StepGroup`: поля `name`, `stages` (`List<ActivationStage>`), свойства `Name`, `Stages`.
  - `public enum OutOfOrderPolicy { RecordViolation, IgnoreUntilActive }`
  - `public abstract class ScenarioMode : ScriptableObject`:
    - поля `displayName`, `outOfOrder` (= RecordViolation), `defaultAnalyzer`;
    - `abstract IReadOnlyList<StepGroup> Groups`, `abstract bool RetryAfterViolation`, `virtual bool ShouldSkipRestOfGroup(int violationsInGroup) => false`;
    - `IReadOnlyList<StepDefinition> GetSteps()` — шаги в порядке групп и стадий, без повторов и `null`.
  - `[CreateAssetMenu(menuName="ScenarioKit/Режим: тренировка")] TrainingMode`: поле `group`; `Groups` = { group }; `RetryAfterViolation` = true.
  - `[CreateAssetMenu(menuName="ScenarioKit/Режим: экзамен")] ExamMode`: поля `groups`, `violationThreshold` (`[Min(1)]`, = 2); `RetryAfterViolation` = false; `ShouldSkipRestOfGroup(n)` = `n >= violationThreshold`.

- [ ] **Step 1: Реализовать типы из блока Interfaces.**
- [ ] **Step 2: Проверка.** Консоль без ошибок. Оба режима создаются из меню, в стандартном инспекторе видны вложенные списки.
- [ ] **Step 3: Commit** `feat: режимы тренировки и экзамена, группы и стадии активации`.

---

### Task 5: Контракт журнала, логгеры, хранение

**Files:** Create в `Runtime/Logging/` файлы по структуре (по одному типу на файл; логгеры-ассеты создают `MemoryScenarioLogger` / `JsonFileScenarioLogger`)

**Interfaces:**
- Produces (здесь публичные поля — это DTO для `JsonUtility`):
  - `[Serializable] LogField { public string Key, Value; }`
  - `[Serializable] LogRecord { public double Time; public string Type, StepId, Result, Message; public List<LogField> Data; public string GetValue(string key); }`. Обязательны только `Time` и `Type`.
  - `LogRecordTypes`: константы `SCENARIO_STARTED, GROUP_STARTED, GROUP_FINISHED, STEP_ACTIVATED, STEP_COMPLETED, VIOLATION, STEP_FAILED, STEP_SKIPPED, SCENARIO_FINISHED, SCENARIO_ABORTED`. Их значения — строки PascalCase (`"ScenarioStarted"` и т. д.). В псевдокоде ниже записи названы по значениям. Анализаторы пропускают типы, которых не знают.
  - `[Serializable] ScenarioLog { public int FormatVersion = 1; public string ScenarioId, ModeName, StartedUtc; public List<string> StepIds; public List<LogRecord> Records; }`
  - `public interface IScenarioLogger { void Write(LogRecord record); ScenarioLog Complete(); }`
  - `public abstract class ScenarioLoggerAsset : ScriptableObject`: поле `displayName`; `public abstract IScenarioLogger Create(ScenarioLog header);`.
  - `[CreateAssetMenu(menuName="ScenarioKit/Логгер: в памяти")] MemoryLoggerAsset`.
  - `[CreateAssetMenu(menuName="ScenarioKit/Логгер: JSON-файл")] JsonFileLoggerAsset`: поле `folderName` = "ScenarioLogs". Делает то же, что Memory, и после **каждой** записи перезаписывает `Application.persistentDataPath/<folderName>/<scenarioId>_<modeName>_<yyyyMMdd_HHmmss>.json`.
  - `ScenarioLogStorage` (static): `GetJson(ScenarioLog)`, `Save(log, path)`, `ScenarioLog Load(string path, List<string> warnings)`, `string GetLatestPath(string folder)`, `IReadOnlyList<string> GetLogPaths(string folder)`, `string DefaultFolderPath`. `Load` при отсутствии файла или ошибке разбора возвращает `null` и предупреждение на русском. `GetLatestPath` для пустой папки возвращает `null`.

- [ ] **Step 1: Реализовать типы из блока Interfaces** (`JsonUtility`, `File`, `Directory`).
- [ ] **Step 2: Проверка.** Консоль без ошибок.
- [ ] **Step 3: Commit** `feat: контракт журнала событий, логгеры в памяти и в JSON`.

---

### Task 6: `ScenarioSession` — активация, стадии, группы

**Files:** Create `Runtime/Execution/StepStatus.cs`, `Runtime/Execution/ScenarioSession.cs`

**Interfaces:**
- Produces:
  - `public enum StepStatus { Pending, Active, Completed, Failed, Skipped }`
  - `public sealed class ScenarioSession`:
    - `ScenarioSession(ScenarioMode mode, IScenarioLogger logger, IWorldState world, Func<double> clock)`
    - `void Start(); void HandleEvent(ScenarioEventKey key); void Abort();`
    - `ScenarioMode Mode; bool IsFinished; string CurrentGroupName; IReadOnlyList<StepDefinition> ActiveSteps; StepStatus GetStatus(StepDefinition step);`
    - `event Action ChangedEvent; event Action<StepDefinition, string> ViolationRaisedEvent; event Action FinishedEvent;`

Алгоритм. Принадлежность шага группе и стадии определяется по первому вхождению в `mode.Groups`. Счётчик нарушений ведётся по каждой группе.

```
Start:        Write(ScenarioStarted); StartGroup(0)
StartGroup(g):  g >= Groups.Count → Finish(); иначе Write(GroupStarted, message=name);
                ShouldSkipRestOfGroup(violations[g]) → SkipRestOfGroup(); иначе stage=-1; AdvanceStage()
AdvanceStage:   повторять: stage++; stage >= stages.Count → Write(GroupFinished); StartGroup(g+1); выход;
                все нетерминальные шаги стадии → Active, tracker.Reset(), Write(StepActivated);
                если активирован хоть один → выход
HandleEvent(k): игнорировать, если сессия не запущена или IsFinished.
                snapshot = (шаг, статус) для всех Pending/Active шагов в порядке GetSteps.
                для каждого шага: если текущий статус != статус в snapshot → пропустить;
                  Active  → verdict = tracker.Handle(k, world): Completed → Completed + Write(StepCompleted);
                            Violation → OnViolation(шаг, verdict.Code, verdict.Message)
                  Pending → outOfOrder == RecordViolation и IsTriggeredBy(k) → OnViolation(шаг, out_of_order, "Этот шаг ещё не доступен")
                после цикла: все шаги текущей стадии терминальны → AdvanceStage(); затем ChangedEvent?.Invoke()
Finish:       Write(ScenarioFinished); IsFinished = true; FinishedEvent?.Invoke()
Abort:        если не IsFinished → Write(ScenarioAborted); IsFinished = true
```

`OnViolation` в этой задаче — заглушка, её заполняет Task 7.

- [ ] **Step 1: Реализовать `ScenarioSession` по алгоритму.** Время записи = `clock()`, `stepId` = `Id` шага.
- [ ] **Step 2: Проверка.** Консоль без ошибок. Поведение проверяется вручную в Task 7.
- [ ] **Step 3: Commit** `feat: исполнение сценария — одновременная, последовательная и смешанная активация`.

---

### Task 7: `ScenarioSession` — нарушения; ручная проверка ядра

**Files:** Modify `Runtime/Execution/ScenarioSession.cs`; временный `Assets/ScenarioKit/Editor/SessionSmokeCheck.cs` (удаляется в конце задачи)

**Interfaces:**
- Produces: записи `Violation` (`result` = код, `message` = причина), `StepFailed`, `StepSkipped`, `GroupFinished` (`result="threshold"` при досрочном завершении); событие `ViolationRaisedEvent`.

```
OnViolation(step, code, msg): Write(Violation); ViolationRaisedEvent?.Invoke(step, msg)
  mode.RetryAfterViolation → если step Active: tracker.Reset()
  иначе → step Failed, Write(StepFailed); violations[группа шага]++;
          группа шага == текущая и ShouldSkipRestOfGroup(...) → SkipRestOfGroup()
SkipRestOfGroup: нетерминальные шаги текущей группы → Skipped + Write(StepSkipped);
                 Write(GroupFinished, result="threshold"); StartGroup(g+1)
```

- [ ] **Step 1: Реализовать `OnViolation` и `SkipRestOfGroup`.**
- [ ] **Step 2: Ручная проверка ядра.** Это одноразовый скрипт, не тест. Создать пункт меню `Tools/ScenarioKit/Проверка ядра (временно)`: он в памяти собирает шаги и режимы через `ScriptableObject.CreateInstance` и `SerializedObject`, прогоняет сценарии и печатает в консоль `OK`/`FAIL` с журналом. Запустить через Unity MCP `Unity_ManageMenuItem`. Сценарии:
  - смешанная активация `[1-4][5][6][7][8][9,10][11,12]`: после k1..k4 активен только s5; после k8 активны {s9, s10};
  - стадии `[sA(kX)] [sB(kX)]`: событие kX → sA Completed, sB Active (Review Focus 1);
  - тренировка: нарушение → шаг остаётся активным, соседние тоже;
  - экзамен, порог 2: группа [s1..s4], нарушения у s1 и s4 → s2, s3 Skipped, стартует группа 2;
  - экзамен, порог 1: событие шага группы 2 во время группы 1 → после группы 1 группа 2 сразу пропускается, сессия завершена (Review Focus 2);
  - пустая стадия, пустая группа, режим без шагов → не зависает (Review Focus 4);
  - `IgnoreUntilActive`: событие ожидающего шага не даёт записи `Violation`.

  Все строки должны быть `OK`.
- [ ] **Step 3: Удалить `SessionSmokeCheck.cs`.** Проверка: консоль без ошибок.
- [ ] **Step 4: Commit** `feat: обработка нарушений — повтор в тренировке, Failed/Skipped и порог в экзамене`.

---

### Task 8: Анализаторы и отчёт

**Files:** Create `Runtime/Analysis/StepScore.cs`, `AnalysisResult.cs`, `StepFacts.cs`, `LogDigest.cs`, `TrainingAnalyzer.cs`, `ExamAnalyzer.cs`, `ReportFormatter.cs`; Modify `ScenarioAnalyzer.cs`

**Interfaces:**
- Produces:
  - `[Serializable] StepScore { public string stepId, status; public int attempts, points; }`, status ∈ `Completed | Failed | Skipped | NotCompleted`.
  - `AnalysisResult { public string analyzerName; public int score, maxScore; public List<StepScore> steps; public List<string> warnings; }`
  - `public abstract class ScenarioAnalyzer : ScriptableObject`: поле `displayName`; `public abstract AnalysisResult Analyze(ScenarioLog log);`.
  - `LogDigest.Create(ScenarioLog) -> LogDigest { StepIds; IsFinished; Warnings; StepFacts GetFacts(id) }`, где `StepFacts { violations, violationsBeforeCompletion; completed, failed, skipped }`. Предупреждения:
    - журнал пуст или `null` → «Журнал пуст»;
    - нет `StepIds` → «В журнале нет списка шагов — используются шаги из записей»;
    - нет `ScenarioFinished` → «Прохождение не завершено»;
    - неизвестный `stepId` → одно предупреждение на каждый id.
  - `[CreateAssetMenu(menuName="ScenarioKit/Анализатор: тренировка")] TrainingAnalyzer`: поле `pointsByAttempt` = {10, 5}. Попытка = `violationsBeforeCompletion + 1`; баллы — элемент массива по индексу попытки или 0. `maxScore = pointsByAttempt[0] × шагов`.
  - `[CreateAssetMenu(menuName="ScenarioKit/Анализатор: экзамен")] ExamAnalyzer`: поля `baseScore` = 100, `failedPenalty` = 10, `skippedPenalty` = 5. Статус определяется по порядку: Failed (`failed || violations > 0`) → Skipped → Completed → иначе Skipped с предупреждением. `score = max(0, base − Σ)`.
  - `ReportFormatter.GetResultText(AnalysisResult)`: строка `Итог: {score} / {maxScore} ({analyzerName})`, затем строки шагов `{stepId}: {status}, попыток {attempts}, баллы {points}`, затем `⚠ {warning}`.
  - `ReportFormatter.GetLogText(ScenarioLog)`: строка на запись `[{time:0.0} c] {type} {stepId} {result} {message}`.

- [ ] **Step 1: Реализовать типы из блока Interfaces.**
- [ ] **Step 2: Проверка.** Консоль без ошибок. Повторить временный пункт меню из Task 7 (снова создать и удалить) с контрольными примерами ТЗ §7. Тренировка: s1, s2 с одним нарушением, s3 с двумя → 10 + 5 + 0 = 15 из 30. Экзамен: два Failed + два Skipped → 70. Тот же журнал через `ScenarioLogStorage.Save/Load` даёт тот же итог. Журнал с `type="CustomThing"` и без `StepIds` → предупреждения, без исключения.
- [ ] **Step 3: Commit** `feat: анализаторы тренировки и экзамена, форматирование отчёта`.

---

### Task 9: `ScenarioDefinition` и валидатор

**Files:** Create `Runtime/ScenarioDefinition.cs`, `Runtime/Validation/ValidationSeverity.cs`, `ValidationMessage.cs`, `ScenarioValidator.cs`

**Interfaces:**
- Produces:
  - `[CreateAssetMenu(menuName="ScenarioKit/Сценарий")] ScenarioDefinition`: поля `scenarioId`, `steps` (библиотека шагов), `modes`.
  - `enum ValidationSeverity { Error, Warning }`; `readonly struct ValidationMessage { Severity; Text; Object Context; }`. `Context` — шаг или режим, к которому относится сообщение; по нему инспектор подсвечивает строку.
  - `ScenarioValidator.GetMessages(ScenarioDefinition) -> List<ValidationMessage>` и `ScenarioValidator.GetModeMessages(ScenarioMode, IReadOnlyList<StepDefinition> library /* может быть null */)`.

| Правило | Уровень |
|---|---|
| пустой или повторяющийся `id` шага | Error |
| режим ссылается на шаг не из библиотеки | Error |
| шаг встречается в режиме дважды | Error |
| шаг библиотеки не входит в режим | Warning |
| пустая стадия, группа без стадий, `null` в списке | Warning |
| `EventStep` с пустой последовательностью | Error |
| одно success-событие у двух шагов | Warning |
| в сценарии нет режимов | Error |

- [ ] **Step 1: Реализовать типы.**
- [ ] **Step 2: Проверка.** Консоль без ошибок.
- [ ] **Step 3: Commit** `feat: ассет сценария и валидатор настроек`.

---

### Task 10: Источники событий и `ScenarioRunner`

**Files:** Create `Runtime/Sources/*.cs`, `Runtime/Execution/ScenarioRunner.cs`

**Interfaces:**
- Produces:
  - `abstract ScenarioEventSource : MonoBehaviour { protected void Raise(ScenarioEventKey key); }`: точка расширения для новых источников.
  - `ButtonEventSource [RequireComponent(Button)]`: поле `onClick`.
  - `TriggerEventSource`: поля `onEnter`, `onExit`, `requiredTag` = "Player".
  - `CollisionEventSource`: поля `onCollision`, `requiredTag` = "Player".
  - `UnityEventSource`: поле `key`; методы `Raise()`, `RaiseIfTrue(bool)`, `RaiseIfFalse(bool)` для вызова из любого UnityEvent.
  - `ToggleStateSource [RequireComponent(Toggle)]`: поле `state`; пишет `isOn` в шину в `Start` и при каждом изменении.
  - `ScenarioRunner : MonoBehaviour`:
    - поля `scenario`, `logger`, `analyzerOverride` (пусто → анализатор режима), `analyzers` (список для переоценки), `autoStartMode`, `onBeforeSessionStart` (UnityEvent);
    - свойства `Session`, `LastLog`, `LastResult`, `Scenario`, `Analyzers`;
    - `event Action<ScenarioSession> SessionStartedEvent; event Action<ScenarioLog, AnalysisResult> SessionFinishedEvent;`
    - `void StartMode(ScenarioMode)`: прерывает текущую сессию (`Abort` + `Complete`), вызывает `ResetState` и `onBeforeSessionStart`, создаёт сессию. Заголовок журнала: `scenarioId`, `displayName` режима, UTC-время, `StepIds`. Сообщения валидатора уходят в `Debug.LogWarning`.
    - `void SimulateEvent(ScenarioEventKey key)` → `ScenarioEventBus.Raise(key)` (для инспектора).
    - `AnalysisResult Analyze(ScenarioLog, ScenarioAnalyzer)`: при `null` возвращает результат с предупреждением.
    - После `FinishedEvent`: `LastLog = Complete()`, `LastResult = Analyze(...)`, `Debug.Log("Сценарий завершён: X/Y")`, затем `SessionFinishedEvent`.
    - Подписка на шину в `OnEnable`, отписка в `OnDisable`. Время сессии = `Time.timeAsDouble − start`.

- [ ] **Step 1: Реализовать типы.**
- [ ] **Step 2: Проверка.** Консоль без ошибок. Временная сцена: Canvas с Button + ButtonEventSource и Runner со сценарием из одного шага и `autoStartMode`. В Play клик даёт «Сценарий завершён: 10/10». Временную сцену и ассеты удалить.
- [ ] **Step 3: Commit** `feat: источники событий Unity и раннер сценария`.

---

### Task 11: Основа редактора — пикеры ассетов, чипы шагов, drag-and-drop

**Files:** Create в `Editor/Common/` файлы по структуре (по одному типу на файл)

**Interfaces:**
- Produces:
  - `ScenarioKitStyles` (static): цвета статусов (Pending — серый, Active — синий, Completed — зелёный, Failed — красный, Skipped — жёлтый), стиль чипа, заголовки секций `Section(string title)`. Цвета адаптируются под светлую и тёмную тему через `EditorGUIUtility.isProSkin`.
  - `AssetPickerField.Draw<T>(Rect rect, SerializedProperty prop, GUIContent label, string createFolder) where T : ScriptableObject`. Внутри: `ObjectField`, который принимает drag из Project, и кнопка ▾ со списком `GenericMenu` всех ассетов `T` (`AssetDatabase.FindAssets("t:T")`, отсортированы по имени, путь подписан серым). Внизу списка — «＋ Создать…». Для абстрактного `T` создание открывает подменю конкретных наследников из `TypeCache.GetTypesDerivedFrom<T>()`. Есть и вариант для layout: `DrawLayout<T>(...)`.
  - `NamePromptPopup : PopupWindowContent`: поле имени, кнопки «Создать» и «Отмена», Enter подтверждает.
  - `ScenarioAssetDrawer<T> : PropertyDrawer` (абстрактный generic) и по одному маленькому наследнику с `[CustomPropertyDrawer(typeof(T), true)]` на каждый тип: `EventKeyDrawer`, `StateKeyDrawer`, `StepDefinitionDrawer`, `ScenarioModeDrawer`, `LoggerAssetDrawer`, `AnalyzerDrawer`. Все рисуются через `AssetPickerField`, поэтому удобный выбор работает везде, в том числе в стандартных инспекторах uGUI-компонентов. Папка для создания по умолчанию — `Assets/ScenarioKit/Demo/Data/<Events|States|Steps|Modes|Loggers|Analyzers>`.
  - `StepChip.Draw(Rect, StepDefinition, StepStatus? status, bool selected)`: плашка «`id` — instruction», обрезанная по ширине, с цветом по статусу. Двойной клик выделяет ассет (`Selection.activeObject`).
  - `StepDragAndDrop`: `BeginDrag(StepDefinition step, object sourceList, int index)` выставляет `DragAndDrop.objectReferences` и `SetGenericData(DRAG_DATA_KEY, StepDragData)`. `bool TryAcceptDrop(Rect zone, out List<StepDefinition> steps, out (object list, int index)? source)` принимает и внутренние перетаскивания, и `StepDefinition` из окна Project.
  - `ScenarioUsageIndex` (static, кэш сбрасывается через `EditorApplication.projectChanged`): `GetStepsUsing(ScenarioEventKey)`, `GetStepsUsing(ScenarioStateKey)`, `GetModesUsing(StepDefinition) -> (mode, groupName, stageIndex)[]`, `GetScenariosUsing(ScenarioMode)`.

- [ ] **Step 1: Реализовать типы.**
- [ ] **Step 2: Проверка.**
  - Консоль без ошибок.
  - В стандартном инспекторе `ButtonEventSource` поле события показывает ▾ со списком ключей.
  - «＋ Создать…» создаёт ключ в `Data/Events`, и он сразу выбирается.
  - Работает перетаскивание ключа из Project в поле.
- [ ] **Step 3: Commit** `feat(editor): пикеры ассетов, чипы шагов и drag-and-drop`.

---

### Task 12: Инспектор шага и источников событий

**Files:** Create `Editor/Inspectors/EventStepEditor.cs`, `ScenarioEventSourceEditor.cs` (базовый) и пять наследников по структуре

**Interfaces:**
- Consumes: `AssetPickerField`, `ScenarioKitStyles`, `ScenarioUsageIndex`.
- Produces: `[CustomEditor(typeof(EventStep))] EventStepEditor`. Его можно встроить в другой инспектор через `Editor.CreateCachedEditor` — это использует Task 14.

Макет `EventStepEditor` (секции сворачиваются, состояние хранится в `SessionState`):

1. **Шаг:** поле `id` с кнопкой «↺ из имени файла»; поле `instruction` (TextArea, 2–4 строки) с подписью «Текст задания для пользователя».
2. **Шаг выполнен, когда** — `ReorderableList` по `sequence`:
   - каждая строка: номер «1.», «2.»… и `AssetPickerField<ScenarioEventKey>`; между строками подпись «затем»;
   - при двух и более элементах появляется переключатель «Строгий порядок» и поле «Сообщение при нарушении порядка».
3. **Только если** — `ReorderableList` по `requiredStates`: строка `[состояние ▾] [включено/выключено ▾]` и поле «Сообщение, если условие не выполнено».
4. **Считается нарушением** — `ReorderableList` по `violations`: строка `[событие ▾] [текст сообщения]`.
5. **Итог:** `HelpBox` с фразой, собранной из полей. Пример: «Шаг выполнен, когда: Зона A → Зона B. Только если: Клапан = включено. Нарушение: Тревога — «…»». Пустая последовательность даёт красное «Не указано событие выполнения».
6. **Где используется:** строки `режим → группа → стадия` из `ScenarioUsageIndex`, клик выделяет режим.

Редакторы источников: базовый `ScenarioEventSourceEditor` (общая отрисовка ключей и «Используется шагами») и `[CustomEditor]`-наследники для `ButtonEventSource`, `TriggerEventSource`, `CollisionEventSource`, `UnityEventSource`, `ToggleStateSource`. Поля рисуются через пикер. Под каждым ключом подпись «Используется шагами: …» или жёлтое «Этот ключ не используется ни одним шагом». В `TriggerEventSource` — предупреждение, если у коллайдера не включён `isTrigger`. В `UnityEventSource` — подсказка, как подключить метод из UnityEvent.

- [ ] **Step 1: Реализовать редакторы.** Все правки — через `SerializedObject`, чтобы работал Undo.
- [ ] **Step 2: Проверка.**
  - Консоль без ошибок.
  - Создать EventStep: добавить два события перетаскиванием из Project; переставить их drag-ом; фраза «Итог» обновляется; Ctrl+Z откатывает правки.
  - Инспекторы источников показывают «Используется шагами».
- [ ] **Step 3: Commit** `feat(editor): инспектор шага и источников событий`.

---

### Task 13: Инспектор режима — группы, стадии, перетаскивание шагов

**Files:** Create `Editor/Inspectors/ScenarioModeEditor.cs`

**Interfaces:**
- Consumes: `StepChip`, `StepDragAndDrop`, `AssetPickerField`, `ScenarioValidator.GetModeMessages`, `ScenarioUsageIndex.GetScenariosUsing`.
- Produces: `[CustomEditor(typeof(ScenarioMode), true)] ScenarioModeEditor`. Он покрывает `TrainingMode`, `ExamMode` и будущих наследников со стандартными полями `group`/`groups`. Встраивается в Task 14.

Макет:

1. **Валидация:** `HelpBox` на каждое сообщение `GetModeMessages`. Библиотекой служит сценарий, который ссылается на этот режим (через `ScenarioUsageIndex`).
2. **Настройки:** поле «Название»; «Действие вне очереди» — popup с вариантами «Считать нарушением» / «Шаг неактивен до своей очереди»; «Анализатор по умолчанию» через пикер. Для экзамена — «Порог нарушений в группе» (`IntSlider` 1..10) с подписью «После N нарушений оставшиеся шаги группы пропускаются».
3. **Палитра шагов** — горизонтальный перенос чипов: шаги библиотеки, которых ещё нет в режиме. Чип перетаскивается в любую стадию. Если все шаги использованы, вместо палитры серая подпись «Все шаги сценария распределены».
4. **Группы.** У экзамена — `ReorderableList` групп: шапка группы с полем имени, цветной полосой, кнопкой ⋮ и сворачиванием. У тренировки — одна группа без шапки.
5. **Стадии внутри группы:**
   - Стадия — рамка с заголовком «Стадия N — одновременно (k)» или «Стадия N» для одного шага. Между стадиями стрелка «↓ затем».
   - Внутри стадии — чипы шагов. Их можно переставлять внутри стадии, переносить в другую стадию или группу (перенос, а не копия) и бросать из Project или палитры.
   - Перетаскивание на зону «＋ Перетащите шаг сюда, чтобы создать новую стадию» в конце группы создаёт стадию.
   - Сами стадии переставляются за ручку ≡.
   - Удаление шага — ✕ на чипе; шаг возвращается в палитру.
6. **Быстрые действия** (меню ⋮ стадии и группы):
   - стадия: «Разбить на отдельные стадии (по очереди)», «Объединить со следующей (одновременно)», «Удалить стадию»;
   - группа: «Все шаги одновременно», «Все шаги по очереди», «Удалить пустые стадии», «Удалить группу»;
   - для экзамена ещё кнопка «＋ Группа».
7. **Play Mode:** если активный `ScenarioRunner` выполняет этот режим, чипы окрашиваются по `GetStatus`, у активной группы появляется метка «▶ идёт». Инспектор перерисовывается по `Session.ChangedEvent`.

Все изменения структуры идут через `Undo.RecordObject(mode, "…")` с последующим `EditorUtility.SetDirty`. Перенос шага — одна операция Undo (удаление из источника + вставка). Перед вставкой шаг удаляется из всех стадий режима, поэтому дубль в режиме невозможен (Review Focus 6).

- [ ] **Step 1: Реализовать редактор.** Список групп — на `ReorderableList` с переменной высотой элементов (`elementHeightCallback`). Стадии и чипы раскладываются через `Rect`.
- [ ] **Step 2: Проверка.**
  - Консоль без ошибок.
  - На временных ассетах: перетащить шаг из стадии 1 в стадию 3 — он перенесён, а не скопирован.
  - Бросить шаг на «новую стадию» — стадия создана.
  - «Все по очереди» → 1 шаг на стадию; «Объединить со следующей» → снова одновременно.
  - Ctrl+Z откатывает каждое действие.
  - Шаг из Project, который уже есть в режиме, переносится, а не дублируется.
  - Пустая стадия даёт жёлтое предупреждение (Review Focus 4).
- [ ] **Step 3: Commit** `feat(editor): инспектор режима — группы и стадии с перетаскиванием шагов`.

---

### Task 14: Инспектор сценария (центр управления) и раннера

**Files:** Create `Editor/Inspectors/ScenarioDefinitionEditor.cs`, `Editor/Inspectors/ScenarioRunnerEditor.cs`

**Interfaces:**
- Consumes: `EventStepEditor`, `ScenarioModeEditor` (встраиваются через `Editor.CreateCachedEditor`), `ScenarioValidator.GetMessages`, `StepChip`, `AssetPickerField`, `ReportFormatter`, `ScenarioLogStorage`.

`ScenarioDefinitionEditor`:
1. **Шапка:** `scenarioId`; сводка «12 шагов · 3 режима»; общий статус — зелёное «Ошибок не найдено» или счётчики ошибок и предупреждений.
2. **Валидация:** `HelpBox` на сообщение с кнопкой «Показать», которая выделяет `Context`.
3. **Шаги (библиотека)** — `ReorderableList`:
   - строка: чип, иконка ⚠ при проблемах и подпись «в режимах: Тренировка, Экзамен» серым;
   - drag из Project добавляет шаги;
   - «＋ Новый шаг» — подменю типов шагов (`TypeCache`) → `NamePromptPopup` → ассет в `Data/Steps` с `id` = имя;
   - «–» убирает шаг только из библиотеки, ассет не удаляется;
   - выбранный шаг раскрывается ниже во встроенном `EventStepEditor` (секция «Редактирование: <id>»).
4. **Режимы** — `ReorderableList`: строка с названием, типом (Тренировка/Экзамен) и числом групп. Кнопки «＋ Тренировка», «＋ Экзамен», «＋ Существующий» (пикер). Выбранный режим раскрывается во встроенном `ScenarioModeEditor`.

`ScenarioRunnerEditor`:
1. **Настройка:**
   - «Сценарий» через пикер;
   - «Режим при запуске» — popup «(выбор на экране)» + режимы сценария;
   - «Логгер» через пикер;
   - «Анализатор» — popup «(анализатор режима)» + все анализаторы;
   - «Анализаторы для переоценки» — `ReorderableList` с пикерами;
   - `onBeforeSessionStart` штатным `PropertyField`.
2. **Play Mode — «Управление прохождением»:**
   - кнопки «▶ <режим>» для каждого режима и «■ Прервать»;
   - текущая группа и чипы активных шагов;
   - «Симуляция события»: пикер ключа и кнопка «Отправить» (`SimulateEvent`);
   - лента последних 10 записей журнала.
3. **После прохождения:** `GetResultText` в `HelpBox`; popup анализатора + «Переоценить»; «Загрузить последний журнал»; «Открыть папку журналов» (`EditorUtility.RevealInFinder(ScenarioLogStorage.DefaultFolderPath)`).

- [ ] **Step 1: Реализовать оба редактора.** Перерисовка в Play Mode — по `ChangedEvent` и `SessionFinishedEvent` через `Repaint`.
- [ ] **Step 2: Проверка.**
  - Консоль без ошибок.
  - Из инспектора временного сценария создать шаг и режим, раскрыть их встроенные инспекторы, отредактировать, не уходя с ассета.
  - В Play Mode в инспекторе раннера запустить режим и пройти его кнопкой «Отправить» без сцены. Результат отображается, «Переоценить» другим анализатором меняет итог.
- [ ] **Step 3: Commit** `feat(editor): центр управления сценарием и инспектор раннера`.

---

### Task 15: Демо-данные сценария

**Files:** Create ассеты в `Assets/ScenarioKit/Demo/Data/`. Создавать их через новые инспекторы или Unity MCP (`Unity_RunCommand` с `SerializedObject`).

Ключи событий (`Data/Events/`): `Btn_Power`, `Btn_Alarm`, `Btn_Pump`, `Btn_Check`, `Btn_Signal`, `Btn_Report`, `Toggle_Light_On`, `Valve_Closed`, `ZoneA_Enter`, `ZoneB_Enter`, `ZoneC_Enter`, `ZoneD_Enter`, `Box_Hit`, `Wall_Hit`, `Marker_Hit`. Состояние (`Data/States/`): `Valve_Open`.

| # | id | instruction | sequence | условие / нарушения |
|---|---|---|---|---|
| 1 | power_on | Нажмите «Питание» | Btn_Power | нарушение Btn_Alarm → «Нажата «Тревога» вместо «Питания»» |
| 2 | light_on | Включите переключатель «Свет» | Toggle_Light_On | — (UnityEvent) |
| 3 | enter_zone_c | Войдите в зону C | ZoneC_Enter | — (Trigger) |
| 4 | push_box | Толкните ящик, не задевая стену | Box_Hit | нарушение Wall_Hit → «Вы задели стену» (Collision) |
| 5 | route_a_b | Пройдите зону A, затем зону B | ZoneA_Enter, ZoneB_Enter | строгий порядок; «Сначала зона A, потом B» (**составной**) |
| 6 | pump_start | Запустите насос (клапан должен быть открыт) | Btn_Pump | требуется Valve_Open = вкл; «Клапан закрыт» (**контекстный**) |
| 7 | check | Нажмите «Проверка» | Btn_Check | — |
| 8 | touch_marker | Коснитесь сферы-маркера | Marker_Hit | — |
| 9 | enter_zone_d | Войдите в зону D | ZoneD_Enter | — |
| 10 | signal | Подайте сигнал кнопкой «Сигнал» | Btn_Signal | нарушение Btn_Alarm → «Нажата «Тревога» вместо «Сигнала»» |
| 11 | report | Нажмите «Отчёт» | Btn_Report | — |
| 12 | close_valve | Закройте клапан | Valve_Closed | — |

Режимы:
- `Training_Mixed` («Тренировка»): стадии `[1,2,3,4] [5] [6] [7] [8] [9,10] [11,12]` (ТЗ §3.2); анализатор — Training.
- `Training_Sequential` («Тренировка (по порядку)»): 12 стадий по одному шагу, `IgnoreUntilActive`.
- `Exam` («Экзамен»), порог 2: «Подготовка» `[1,2,3,4]`; «Запуск» `[5] [6] [7] [8]`; «Завершение» `[9,10] [11,12]`; анализатор — Exam.

Также: `Analyzers/TrainingAnalyzer`, `Analyzers/ExamAnalyzer`, `Loggers/MemoryLogger`, `Loggers/JsonFileLogger`. `DemoScenario` (`scenarioId = "demo"`) содержит все 12 шагов и 3 режима.

- [ ] **Step 1: Создать ассеты по таблицам.**
- [ ] **Step 2: Проверка.** Инспектор DemoScenario показывает «Ошибок не найдено», палитры всех трёх режимов пусты. В Play Mode через симуляцию в инспекторе раннера (Task 14):
  - тренировка: `power_on` с 1-й попытки, `push_box` — Wall_Hit затем Box_Hit, `signal` — Alarm×2 затем Signal, остальные чисто → по трём шагам 10 + 5 + 0 = 15, итог 105 / 120;
  - экзамен: Alarm, Wall_Hit → «Подготовка» завершена досрочно, остальное чисто → 70 / 100;
  - `route_a_b`: ZoneB до ZoneA → «Сначала зона A, потом B»; `pump_start` без Valve_Open → «Клапан закрыт».
- [ ] **Step 3: Commit** `feat: демонстрационный сценарий — 12 шагов, тренировка, экзамен`.

---

### Task 16: Демо-сцена, HUD, игрок

**Files:** Create `Demo/Scripts/SimplePlayerMover.cs`, `ResetPose.cs`, `ScenarioHud.cs`, `ModeButton.cs`; префабы в `Demo/Prefabs/`; `Demo/Scenes/ScenarioDemo.unity`; Modify `ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Produces:
  - `SimplePlayerMover [RequireComponent(Rigidbody)]`: поле `speed` = 5; `Rigidbody` кешируется в `Awake`; WASD и стрелки через `Keyboard.current`, движение через `Rigidbody.linearVelocity` в `FixedUpdate`.
  - `ResetPose.ResetToInitial()`: восстанавливает позу из `Awake` и обнуляет скорость `Rigidbody`, если он есть.
  - `ModeButton [RequireComponent(Button)]`: `Setup(ScenarioMode mode, Action<ScenarioMode> onSelected)`, подпись `mode.DisplayName`.
  - `ScenarioHud`:
    - поля `runner`, `modeButtonPrefab` (`ModeButton`), `modeButtonsRoot` и ссылки на `Text`/`Button`/`Dropdown`/панели;
    - стартовая панель: кнопки режимов создаются через `Instantiate(modeButtonPrefab, modeButtonsRoot)`;
    - строка «Режим / группа» и список активных заданий;
    - сообщение о нарушении, гаснет через 3 с (`HideMessageCoroutine`);
    - панель результатов: `GetResultText`, Dropdown анализаторов, кнопки «Переоценить», «Загрузить последний журнал» (с предупреждениями), «Показать журнал» (ScrollRect с `GetLogText`) и «Пройти заново»;
    - обработчики по гайдлайну: `SessionStartedEventHandler`, `SessionFinishedEventHandler`, `ChangedEventHandler`, `ViolationRaisedEventHandler`.

Префабы (`Demo/Prefabs/`, корень назван как файл, дочерние объекты — осмысленно):
- `Player`: Capsule, tag `Player`, Rigidbody (freeze rotation), `SimplePlayerMover`, `ResetPose`.
- `Trigger Zone`: Cube 3×1×3, BoxCollider isTrigger, полупрозрачный материал, `TriggerEventSource`. Prefab Variants `Zone A` … `Zone D` различаются только ключом события и цветом.
- `Pushable Box`: Rigidbody, `CollisionEventSource` (Box_Hit), `ResetPose`.
- `Wall`: Cube 0.5×2×6, `CollisionEventSource` (Wall_Hit).
- `Marker Sphere`: `CollisionEventSource` (Marker_Hit).
- `Scenario Runner`: `ScenarioRunner`; логгер — JsonFileLogger, анализаторы — [Training, Exam], режим при запуске — «(выбор на экране)».
- `UI/Action Button`: Button + Text + `ButtonEventSource`. Экземпляры в сцене отличаются подписью и ключом.
- `UI/Action Toggle`: Toggle + Text + `UnityEventSource`. Вариант `UI/Valve Toggle` добавляет `ToggleStateSource`.
- `UI/Mode Button`: Button + Text + `ModeButton`.
- `UI/Scenario Hud`: Canvas (`CanvasScaler` Scale With Screen Size 1920×1080, match 0.5) + `ScenarioHud`; панели «Start Panel», «Status Panel», «Message Panel», «Results Panel», «Log Panel»; колонка «Action Panel» с вложенными экземплярами `Action Button` и `Action Toggle`.

Сцена `ScenarioDemo` (кроме экземпляров префабов только `Main Camera`, `Directional Light`, `Global Volume`, `EventSystem`, `Floor`):
- Floor — Plane 20×20. Камера (0, 18, −8), поворот (65, 0, 0).
- Экземпляры и позиции:
  - Player (0, 1, −8);
  - Zone A (−6, 0.5, 0), Zone B (−6, 0.5, 6), Zone C (6, 0.5, −4), Zone D (6, 0.5, 6);
  - Pushable Box (0, 0.5, 0), Wall (3, 1, 0), Marker Sphere (−2, 0.5, −4);
  - Scenario Runner, Scenario Hud.
- Action Panel (override в экземплярах — только подпись и ключ):
  - кнопки «Питание», «Тревога», «Насос», «Проверка», «Сигнал», «Отчёт»;
  - «Свет» → `RaiseIfTrue` (Toggle_Light_On);
  - «Клапан» (Valve Toggle) → состояние Valve_Open и `RaiseIfFalse` (Valve_Closed).
- В `onBeforeSessionStart` раннера (ссылки на объекты сцены задаются в экземпляре): `ResetPose` для игрока и ящика, `Toggle.SetIsOnWithoutNotify(false)` для обоих тумблеров.

- [ ] **Step 1: Скрипты.**
- [ ] **Step 2: Собрать префабы, затем сцену из их экземпляров.** Сохранить и добавить сцену в Build Settings первой. Проверить иерархию: нет объектов вида `Cube (1)` или `GameObject`.
- [ ] **Step 3: Ручная проверка в Play Mode** (Unity MCP: play, `Unity_Camera_Capture`, `Unity_ReadConsole`):
  - тренировка: 4 задания одновременно, сообщения о нарушениях, повтор шага, итог;
  - экзамен: досрочное завершение группы, итог 70 по сценарию из Task 15;
  - переоценка другим анализатором и просмотр журнала;
  - «Загрузить последний журнал» после перезапуска Play Mode;
  - «Пройти заново» посреди прохождения: в журнале `ScenarioAborted`, переоценка такого журнала даёт «Прохождение не завершено» без исключения (Review Focus 3);
  - испорченный вручную JSON в папке журналов → предупреждение при загрузке (Review Focus 5);
  - консоль чистая.
- [ ] **Step 4: Commit** `feat: демонстрационная сцена с HUD, журналом и переоценкой`.

---

### Task 17 (опционально): окно журналов в Editor

**Files:** Create `Editor/Windows/ScenarioLogWindow.cs`

Окно открывается через меню `Tools/ScenarioKit/Журналы прохождений`:
- слева — список файлов из `ScenarioLogStorage.GetLogPaths(DefaultFolderPath)` с датой и режимом;
- справа — пикер анализатора, кнопка «Оценить», результат (`GetResultText`) и журнал (`GetLogText`) с фильтром по типу записи.

- [ ] **Step 1: Реализовать окно.** Проверка: выбрать журнал из Task 16, переключить анализатор → итог пересчитан без Play Mode.
- [ ] **Step 2: Commit** `feat(editor): окно просмотра и переоценки журналов`.

---

### Task 18: README

**Files:** Create `README.md`

README пишется для проверяющего и для человека без навыков программирования. Разделы (ТЗ §9), к ключевым местам — скриншоты инспекторов (`Unity_Camera_Capture` не подходит, снимать окно Editor вручную или пропустить):
1. **Быстрый старт:** открыть сцену → Play → выбрать режим; управление WASD.
2. **Настройка без кода, по инспекторам:**
   - центр управления сценарием;
   - создание и редактирование шага;
   - события, условия и нарушения;
   - перетаскивание шагов между стадиями и группами;
   - быстрые действия «одновременно / по очереди»;
   - порог экзамена;
   - «вне очереди»;
   - выбор логгера и анализатора на раннере;
   - подключение кнопки или зоны к событию;
   - симуляция событий и переоценка в инспекторе раннера.
3. **Контрольные примеры ТЗ §7:** как воспроизвести каждый в сцене.
4. **Техническое описание:**
   - ответственность частей: события → сессия → журнал → анализатор;
   - как добавить новый тип шага (`StepDefinition` + `StepTracker`, инспектор подхватит его в «＋ Новый шаг»), режим (`ScenarioMode`), логгер, анализатор, источник;
   - контракт журнала.
5. **Принятые решения:**
   - «вне очереди» в тренировке = попытка шага, в экзамене = Failed своей группы;
   - экзаменационный анализатор считает шаг с нарушениями Failed;
   - обработка событий по снимку статусов;
   - автотестов нет — сознательное решение, проверка ручная.
6. **Что улучшил(а) бы дальше:** перекрывающиеся группы экзамена, автотесты ядра, PlayMode-тесты источников, условия «ИЛИ», таймауты шагов, локализация.
7. **Затраченное время:** по фактическому учёту; ориентир — `git log --format='%ad %s' --date=iso`.

- [ ] **Step 1: Написать README.** Каждый путь и пункт меню проверить по проекту.
- [ ] **Step 2: Финальная проверка.** Консоль чистая; `git status` чист; `git log --oneline main..feature/scenario-kit` — по коммиту на задачу. **Ничего не пушить**: сообщить пользователю, что ветка готова к проверке.
- [ ] **Step 3: Commit** `docs: README — инструкция, архитектура, дальнейшие улучшения`.
