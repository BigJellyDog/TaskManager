# Development guide

[Back to README](../../README.md) · [Deutsch](../de/ENTWICKLUNG.md)

## 1. Setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Install the MAUI workload: `dotnet workload install maui`.
3. Clone the repository and restore: `dotnet restore TaskManager.sln`.

## 2. Build, run, test

```powershell
# build everything (what the CI does)
dotnet build TaskManager.sln -c Release

# run the app
dotnet run --project TaskManager.App -f net10.0-windows10.0.19041.0

# run the tests
dotnet test TaskManager.Tests/TaskManager.Tests.csproj
```

In Rider or Visual Studio: open the solution, select `TaskManager.App`, Run.

A running app locks `TaskManager.App.exe`; stop it before building again, or the build ends with a "file is
being used by another process" error.

The tests cover Core and Infrastructure: domain rules, services, repositories against SQLite (in memory and in
temporary files), backup and restore, migrations (a test fails if the model changed without a migration) and the
translation tables, which are linked into the test project as source (`TaskManager.Tests.csproj`, `Linked`).

## 3. Where the app keeps its data while you develop

The same place as in daily use: `%LOCALAPPDATA%\TaskManager` (database, `settings.json`, logs). To start from a
clean state, close the app and delete `taskmanager.db*` there; it is created again on the next start. *Data →
Fill with demo data* gives you a full set of sample tasks.

## 4. Database migrations

Install the EF Core tool once: `dotnet tool install --global dotnet-ef`.

```powershell
# add a migration after changing a model or a configuration
dotnet ef migrations add <Name> --project TaskManager.Infrastructure --output-dir Persistence/Migrations
```

`TaskManagerDbContextFactory` provides the design-time context. Migrations are applied when the app starts. The restore
check recognises migrations automatically: a backup made by a newer version (with an unknown migration) is refused.

## 5. Conventions

- **Layering**: Core must not reference EF Core, MAUI or UI types. Infrastructure implements Core interfaces.
  The app talks to Core services through `ServiceRunner`, never to the `DbContext`.
- **Time**: inject `TimeProvider`; do not call `DateTime.Now` in logic. Tests use `FakeTimeProvider` (it only
  moves forward).
- **Rules in the domain**: validation and state changes belong to `TaskItem` and the services, with a message
  in Russian (the translation key); the UI shows it translated.
- **MVVM**: state and commands in the view model; code-behind only for view concerns. Run data access through
  `ViewModelBase.RunAsync`.
- **Errors**: catch at the operation level, log with `ILogger`, show a translated message. Never let an
  exception end the app.
- **Colours** only through theme tokens (`DynamicResource`), never as literals in a page. Elements that colour
  themselves in code subscribe to `ThemeManager.Changed` with an instance method (the event is weak).
- **Comments and XML documentation** are written in English. The only Russian text in the code are translation keys and the
  user-visible messages built from them.
- **Commits** are small and described in English (`feat:`, `fix:`, `docs:`, `test:`).

## 6. How to extend

### Add a screen

Create the page in `Pages` and its view model in `ViewModels` (inherit `ViewModelBase`), register both in
`MauiProgram` (`AddTransient`), add a `ShellContent` with a route in `AppShell.xaml` and an item in
`SidebarViewModel`. Load data in `OnAppearing` through the view model.

### Add or change a text

Put the Russian text in the markup with `{loc:Extension 'text'}` or in code with `Loc.T("text")`, and add a row
`["text", "English", "Deutsch", "Українська"]` to the matching table in `Services/Translations.*.cs`. The
translation test fails if a row is incomplete or its placeholders differ.

### Add a language

Add a `Language` to `Loc.Languages`, a column to every translation row (the test lists what is missing) and the
culture for dates and numbers.

### Add a theme

Append a value to `ThemeChoice` (`UserSettings.cs`; the numbers are stored in `settings.json`, so only append)
and to `ThemeMode`, add a row to `ThemeCatalog.All` (name as a Russian key plus the three preview colours), the
ten palette colours and the second accent `A2` to `ThemeCatalog.Palette`, and a translation for the name. Light
themes are listed in `ThemeManager.IsLight`.

### Add a column or a table

Change the entity and its configuration, add a migration (section 4), update the repository and, if the value
should be exported, `ExportService` / `ImportService` and the export version.

## 7. Continuous integration

`.github/workflows/build.yml` builds the solution on `windows-latest` with .NET 10 and the MAUI workload and
runs the tests for pushes to the main branch and for pull requests.

## 8. Status and roadmap

Done: the full task workflow (statuses, subtasks, dependencies, recurrence, history), reminders, trash, tags,
statistics, export and import, backup and restore, four languages, nine themes, 192 automated tests.

Ideas for later: a Windows installer or MSIX package, UI tests for the MAUI layer, more export formats,
saved filters, attachments.
