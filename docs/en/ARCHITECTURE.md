# Architecture

[Back to README](../../README.md) · [Deutsch](../de/ARCHITEKTUR.md)

## 1. Overview

Three source projects and one test project. Dependencies point only inwards:

```
TaskManager.App  ──►  TaskManager.Infrastructure  ──►  TaskManager.Core
 (MAUI, MVVM)          (EF Core, SQLite, backup)         (domain, services)
                                  ▲
TaskManager.Tests ────────────────┘   (also references Core)
```

- **Core** has no reference to EF Core, MAUI or any UI type. It defines what the application does and the
  interfaces it needs (`ITaskRepository`, `IUnitOfWork`, `IReminderNotifier`, `IDatabaseBackup`, …).
- **Infrastructure** implements those interfaces with EF Core and SQLite.
- **App** is the Windows MAUI application. It talks to Core services through a scope per operation and never to
  the `DbContext` directly.

## 2. Core

| Folder | Content |
|--------|---------|
| `Domain` | `TaskItem` (the aggregate), `Tag`, `TaskDependency`, `TaskHistoryEntry`, `SentReminder`, the enums (`Priority`, `TaskStatus`, `RecurrenceFrequency`, `RecurrenceMode`, `ReminderKind`), `RecurrenceRule`, the status machine and the domain exceptions |
| `Abstractions` | repository, unit-of-work, notifier and backup interfaces; `ConcurrencyConflictException` |
| `Services` | `TaskService`, `TrashService`, `TagService`, `StatisticsService`, `ReminderChecker`, `ReminderPolicy`, `DemoDataService`, `RecurrenceCalculator`, `DependencyGraph` |
| `Specifications` | composable query predicates (`TaskFilter` → specification) used by the repositories |
| `Querying` | `TaskFilter`, `TaskQuery`, `TaskSort`, `PagedResult<T>` |
| `Export` | `ExportService`, `ImportService`, `MarkdownRenderer`, the versioned JSON document and the import report |

Key rules that live here, and only here:

- **Status machine** (`TaskStateMachine`): the single place that says which transitions are allowed. Done and
  cancelled tasks are left only by *reopen* (done → in progress, cancelled → to do).
- **Subtasks**: depth is limited to three levels (`TaskItem.MaxDepth`); a task cannot be done while a subtask is
  open; moving a task checks the height of the moved subtree.
- **Dependencies**: a task cannot move to *to do*, *in progress* or *done* while a dependency is unfinished; a
  *to do* task with an unfinished dependency becomes *blocked* and is released again automatically. Adding a
  dependency runs a cycle check (`DependencyGraph`).
- **Recurrence**: `RecurrenceCalculator` computes the next occurrence (daily, weekly with weekdays, monthly,
  yearly, every *N*). `GenerateDueOccurrencesAsync` catches up on missed occurrences with an anchor date.
- **Reminders**: `ReminderPolicy` maps the time left to *due in 24 hours*, *due in 1 hour* or *overdue*;
  `ReminderChecker` shows the ones not yet shown and remembers them.
- **History**: every change is written as a `TaskHistoryEntry` by the service, not by the UI.
- **Time** always comes from an injected `TimeProvider`.

## 3. Infrastructure

- **`TaskManagerDbContext`** with one `IEntityTypeConfiguration` per entity and five code-first migrations
  (initial schema, search text, recurrence anchor, successor flag, sent reminders).
- **Soft delete**: `TaskItem.IsDeleted` plus a global query filter. Normal queries never see deleted tasks; the
  trash repository uses `IgnoreQueryFilters()`. Rows that depend on a task (`TaskDependency`) inherit the filter
  through their navigation, which matters when purging: the delete of dependency rows must ignore the filters,
  too.
- **Optimistic concurrency**: `TaskItem.RowVersion` is a concurrency token; a stale edit raises
  `ConcurrencyConflictException`, which the UI explains and answers by reloading.
- **Dates** are `DateTimeOffset` values stored as UTC ticks (`DateTimeOffsetToUtcTicksConverter`), so sorting
  and range queries work in SQLite.
- **Search** uses a denormalised `SearchText` column (title and description in one normalised string, so search is case-insensitive).
- **`SqliteTuningInterceptor`** sets `synchronous = NORMAL` and `temp_store = MEMORY` on every connection;
  foreign keys are enforced by SQLite (`ON DELETE RESTRICT` for the parent link and for the depended-on side of a
  dependency). Purging therefore detaches the tasks being removed from each other before deleting them.
- **`SqliteDatabaseBackup`**: a consistent copy of a running database; `InspectAsync` validates a file
  (integrity check, schema, migrations newer than this version are refused); a restore is *staged* next to the
  database and applied at the next start before anything opens the file, with a safety copy of the old one.
- **`ReminderService`** is a `BackgroundService`: once per interval it generates due recurring tasks and checks
  deadlines, in its own scope per run. `ReminderOptions.Enabled` switches only the deadline check off (the
  *Never* setting); recurring tasks keep being generated.
- Everything is registered by `AddInfrastructure(databasePath)`.

## 4. Application (MAUI)

### 4.1 Structure

| Folder | Content |
|--------|---------|
| `Pages` | one XAML page per section plus the task card and the task form |
| `ViewModels` | one view model per page, row view models, `SidebarViewModel`, `ReminderBannerViewModel` |
| `Controls` | reusable controls (see 4.4) |
| `Services` | application services (see 4.2) |
| `Platforms/Windows` | WinUI code: pop-ups, dialogs, calendar, window activation, system notifications |
| `Resources/Styles` | global styles and the two XAML theme dictionaries |
| `Configuration` | `AppPaths`, `AppOptions` |

Pages and view models are created through dependency injection. View models inherit `ViewModelBase`, which gives
them `RunAsync` (busy flag, one run at a time, expected errors shown as a message, unexpected ones logged) and
the dialog service.

### 4.2 Application services

| Service | Role |
|---------|------|
| `ServiceRunner` | opens a DI scope per operation and runs a Core service in it |
| `AppInitializer` | at start: applies a staged restore, migrates the database, starts the reminder service; at exit stops it |
| `UserSettings` | `settings.json`: page size, theme, language, view, reminder options; raises `Changed` |
| `Loc` | translation lookup and the current language (see 4.5) |
| `ThemeManager`, `ThemeCatalog`, `GlowBrushes` | themes (see 4.3) |
| `DialogService` | message and confirmation windows; on Windows it opens `SignalDialog` in the theme of the app |
| `FileService` | save and open dialogs, the *Documents\TaskManager* folder, folder picker |
| `HotKeyService` | window shortcuts (`Ctrl+1…6`, `Ctrl+N`, `Alt+←`) and forwarding of the page ones |
| `ShellNavigationService`, `AppRestarter` | navigation by route; restart after a restore or when the database moves |

### 4.3 Theming

Colours are tokens (`Bg`, `Surface`, `Line`, `Ink`, `InkMuted`, `Accent`, `OnAccent`, `A2`, the priority and
status colours, …). Two themes are XAML dictionaries (`DarkTheme`, `LightTheme`); the others are built from the
table in `ThemeCatalog`. Styles refer to tokens with `DynamicResource`, so replacing the dictionary recolours
the application. Three details:

- **Gradients and glow** (page background, panel gradient, the primary button, the selected menu item) are
  `GlowBrushes`: each brush exists once and is recoloured in place on a theme change, which is more reliable
  than replacing a brush resource.
- **Triggers** (selected menu item, period switch, check boxes) keep the colour they had when they last fired,
  so a theme change **rebuilds the window** (same mechanism as a language change) and the open section stays.
- **Windows pop-ups** (lists, calendar, dialogs) are WinUI elements; `ThemeManager`
  hands them the matching `ElementTheme`, and the lists, the calendar and the dialogs are drawn by the app
  itself (`SignalMenu`, `SignalCalendar`, `SignalDialog`) so they use the same tokens.

Adding a theme needs only a new row in `ThemeCatalog` (see the development guide).

### 4.4 Controls

`SignalButton` (primary / secondary / ghost / danger, pill shape, gradient and glow), `DropField` and
`DropMenu` (themed drop-down), `DateField` with the Signal calendar, `SegmentedChoice`, `SwitchToggle`,
`Icon` (Lucide paths drawn as geometry), `Chips` (status and priority), `PriorityBars`, `BarMeter`, `Segments`,
`DayChart` (the *Done by day* chart, one `GraphicsView` canvas), `BoardColumn`, `SidebarView` and
`ReminderBanner`.

### 4.5 Localization

Four languages: Russian, English, German, Ukrainian. The tables are plain arrays in `Services/Translations.*.cs`;
a row is `[Russian key, English, German, Ukrainian]`, and the Russian text is the key. `Loc.T(key)` returns the
text in the current language, or the key itself when nothing is found, so a missing translation shows up as a
Russian line instead of an empty one. Texts with numbers use `{0}`; plural forms are written as
`one|few|many` and chosen by `Loc.Count`. Messages from Core are translated by pattern (`Loc.Message`). A
language change rebuilds the window. Dates and numbers use the culture of the language.

A test links the translation files into the test project and checks that every row has four filled columns, that
placeholders and plural forms match between the languages, and that no key is translated in two ways.

## 5. Quality decisions

- **Layering is enforced by the project references**, not by convention: Core cannot even compile a use of EF
  Core.
- **Domain rules live in the domain** and are tested without a database; persistence is tested against real
  SQLite (in memory and in temporary files).
- **No UI thread work with the database**: view models call Core through `ServiceRunner` on background tasks and
  update bound properties on the UI thread.
- **Destructive actions are reversible or confirmed**: delete goes to the trash, purge and empty ask first, a
  restore keeps a safety copy, import skips duplicates and reports everything it did.
- **A crash in the background does not end the app**: a failed reminder run is logged and tried again at the
  next interval.
- **Analyzers on** (`AnalysisLevel latest-recommended`, code style enforced in build); 192 automated tests.

## 6. Known limits

- Windows only (MAUI with the WinUI head); no installer or auto-update yet.
- One user and one database; no sync.
- The system file and folder dialogs (save as, choose a file or a folder) use the Windows look; every other
  pop-up is drawn in the theme of the app.
- The MAUI layer itself (pages, view models, controls) has no automated UI tests; the logic that matters sits in
  Core and Infrastructure, which are covered.
- The window is rebuilt on a theme or language change, so unsaved text in an open form is not kept; the settings
  page is where both are changed.
