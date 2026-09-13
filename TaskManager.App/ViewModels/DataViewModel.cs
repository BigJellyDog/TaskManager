using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Export;
using TaskManager.Core.Services;

namespace TaskManager.App.ViewModels;

/// <summary>Одна строка отчёта об импорте: вид, текст и цвет.</summary>
public sealed record ImportIssueRowViewModel(string Kind, string Text, Color Color);

/// <summary>Страница «Данные»: экспорт в JSON и Markdown, импорт из JSON с отчётом.</summary>
public sealed partial class DataViewModel : ViewModelBase
{
    // В отчёте показываем не больше строк, чем помещается разумно: остальное сворачиваем в "и ещё N".
    private const int MaxReportRows = 200;

    private readonly ServiceRunner _runner;
    private readonly IFileService _files;
    private readonly TimeProvider _clock;
    private readonly IDatabaseBackup _backup;
    private readonly IAppRestarter _restarter;

    public DataViewModel(
        ServiceRunner runner,
        IFileService files,
        TimeProvider clock,
        IDatabaseBackup backup,
        IAppRestarter restarter,
        IDialogService dialogs,
        ILogger<DataViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
        _files = files;
        _clock = clock;
        _backup = backup;
        _restarter = restarter;
    }

    public ObservableCollection<ImportIssueRowViewModel> ImportIssues { get; } = [];

    [ObservableProperty]
    private bool includeDeleted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSavedPath))]
    [NotifyCanExecuteChangedFor(nameof(RevealSavedCommand))]
    private string? lastSavedPath;

    [ObservableProperty]
    private string importSummary = string.Empty;

    [ObservableProperty]
    private bool hasImportReport;

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasSavedPath => !string.IsNullOrEmpty(LastSavedPath);

    [RelayCommand]
    private Task ExportJsonAsAsync() => ExportAsync(json: true, withDialog: true);

    [RelayCommand]
    private Task ExportJsonQuickAsync() => ExportAsync(json: true, withDialog: false);

    [RelayCommand]
    private Task ExportMarkdownAsAsync() => ExportAsync(json: false, withDialog: true);

    [RelayCommand]
    private Task ExportMarkdownQuickAsync() => ExportAsync(json: false, withDialog: false);

    [RelayCommand]
    private async Task BackupAsAsync() => await RunAsync(() => BackupAsync(withDialog: true), "Не удалось сделать резервную копию");

    [RelayCommand]
    private async Task BackupQuickAsync() => await RunAsync(() => BackupAsync(withDialog: false), "Не удалось сделать резервную копию");

    [RelayCommand]
    private async Task RestoreAsync() => await RunAsync(RestoreCoreAsync, "Не удалось подготовить восстановление");

    [RelayCommand]
    private async Task SeedDemoAsync() => await RunAsync(SeedDemoCoreAsync, "Не удалось создать демо-данные");

    [RelayCommand]
    private async Task RemoveDemoAsync() => await RunAsync(RemoveDemoCoreAsync, "Не удалось удалить демо-данные");

    private bool CanRevealSaved() => HasSavedPath;

    [RelayCommand(CanExecute = nameof(CanRevealSaved))]
    private void RevealSaved()
    {
        if (LastSavedPath is { } path)
        {
            _files.RevealInFolder(path);
        }
    }

    [RelayCommand]
    private async Task ImportAsync() => await RunAsync(ImportCoreAsync, "Не удалось выполнить импорт");

    private async Task ImportCoreAsync()
    {
        var json = await _files.PickAndReadJsonAsync();
        if (json is null)
        {
            return;
        }

        var report = await _runner.RunAsync(sp => sp.GetRequiredService<ImportService>().ImportJsonAsync(json));
        ShowReport(report);
    }

    private async Task ExportAsync(bool json, bool withDialog) =>
        await RunAsync(
            async () =>
            {
                var content = await _runner.RunAsync(sp =>
                {
                    var export = sp.GetRequiredService<ExportService>();
                    return json ? export.ExportJsonAsync(IncludeDeleted) : export.ExportMarkdownAsync(IncludeDeleted);
                });

                var fileName = $"tasks-{_clock.GetLocalNow().ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture)}.{(json ? "json" : "md")}";
                var path = withDialog
                    ? await _files.SaveWithDialogAsync(fileName, content)
                    : await _files.SaveToDocumentsAsync(fileName, content);

                if (path is null)
                {
                    // Отказ: прежний путь сбрасываем, иначе под сообщением "не сохранён" осталась бы кнопка со старым файлом.
                    LastSavedPath = null;
                    StatusMessage = "Файл не сохранён.";
                    return;
                }

                LastSavedPath = path;
                StatusMessage = $"Сохранено: {path}";
            },
            "Не удалось выполнить экспорт");

    private async Task SeedDemoCoreAsync()
    {
        var exists = await _runner.RunAsync(sp => sp.GetRequiredService<DemoDataService>().HasDemoDataAsync());

        var confirmed = exists
            ? await Dialogs.ConfirmAsync(
                "Демо-данные уже есть",
                "Прежние демо-задачи будут удалены (в корзину) и созданы заново. Ваши собственные задачи не затрагиваются. Продолжить?",
                "Пересоздать",
                "Отмена")
            : await Dialogs.ConfirmAsync(
                "Демо-данные",
                $"К вашим задачам будет добавлено около 25 демо-задач с тегом «{DemoDataService.DemoTag}»: проект с подзадачами и зависимостями, " +
                "просроченные и ближайшие дедлайны, повторения, выполненные задачи с историей. Продолжить?",
                "Добавить",
                "Отмена");
        if (!confirmed)
        {
            return;
        }

        var count = await _runner.RunAsync(async sp =>
        {
            var demo = sp.GetRequiredService<DemoDataService>();
            if (exists)
            {
                await demo.RemoveDemoDataAsync();
            }

            return await demo.SeedAsync();
        });

        LastSavedPath = null;
        StatusMessage = $"Добавлено демо-задач: {count}. Они отмечены тегом «{DemoDataService.DemoTag}».";
    }

    private async Task RemoveDemoCoreAsync()
    {
        var exists = await _runner.RunAsync(sp => sp.GetRequiredService<DemoDataService>().HasDemoDataAsync());
        if (!exists)
        {
            LastSavedPath = null;
            StatusMessage = "Демо-задач нет.";
            return;
        }

        var confirmed = await Dialogs.ConfirmAsync(
            "Удалить демо-данные",
            $"Все задачи с тегом «{DemoDataService.DemoTag}» будут удалены (в корзину). Продолжить?",
            "Удалить",
            "Отмена");
        if (!confirmed)
        {
            return;
        }

        var removed = await _runner.RunAsync(sp => sp.GetRequiredService<DemoDataService>().RemoveDemoDataAsync());
        LastSavedPath = null;
        StatusMessage = $"Удалено демо-задач: {removed}.";
    }

    private async Task BackupAsync(bool withDialog)
    {
        var fileName = $"taskmanager-backup-{_clock.GetLocalNow().ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture)}.db";
        string? path;

        if (withDialog)
        {
            // Копия сначала собирается во временный файл, потом отдаётся окну "Сохранить как".
            var temp = Path.Combine(Path.GetTempPath(), fileName);
            try
            {
                await _backup.CreateBackupAsync(temp);
                path = await _files.SaveFileWithDialogAsync(fileName, temp);
            }
            finally
            {
                File.Delete(temp);
            }
        }
        else
        {
            path = _files.GetDocumentsFilePath(fileName);
            await _backup.CreateBackupAsync(path);
        }

        if (path is null)
        {
            LastSavedPath = null;
            StatusMessage = "Копия не сохранена.";
            return;
        }

        LastSavedPath = path;
        StatusMessage = $"Резервная копия сохранена: {path}";
    }

    private async Task RestoreCoreAsync()
    {
        var path = await _files.PickFilePathAsync("Выберите резервную копию", ".db");
        if (path is null)
        {
            return;
        }

        // Проверка до всякого подтверждения: плохой файл отклоняется с понятной причиной (DomainValidationException).
        var info = await _backup.InspectAsync(path);

        var confirmed = await Dialogs.ConfirmAsync(
            "Восстановление из копии",
            $"В копии задач: {info.TaskCount}. Текущие данные будут заменены данными из копии. " +
            "Прежняя база перед заменой сохранится как страховочная копия. Продолжить?",
            "Заменить",
            "Отмена");
        if (!confirmed)
        {
            return;
        }

        await _backup.StageRestoreAsync(path);

        var restart = await Dialogs.ConfirmAsync(
            "Нужен перезапуск",
            "Копия подготовлена. Чтобы применить её, приложение нужно перезапустить. Перезапустить сейчас?",
            "Перезапустить",
            "Позже");

        if (restart)
        {
            _restarter.Restart();
            return;
        }

        LastSavedPath = null;
        StatusMessage = "Копия будет применена при следующем запуске приложения.";
    }

    private void ShowReport(ImportReport report)
    {
        ImportSummary =
            $"Добавлено: {report.Added}   Пропущено: {report.Skipped.Count}   " +
            $"Ошибок: {report.Errors.Count}   Предупреждений: {report.Warnings.Count}";

        var rows = new List<ImportIssueRowViewModel>();
        rows.AddRange(report.Errors.Select(i => Row("Ошибка", i, AppColors.Error)));
        rows.AddRange(report.Warnings.Select(i => Row("Предупреждение", i, AppColors.Warning)));
        rows.AddRange(report.Skipped.Select(i => Row("Пропущено", i, AppColors.Muted)));

        ImportIssues.Clear();
        foreach (var row in rows.Take(MaxReportRows))
        {
            ImportIssues.Add(row);
        }

        if (rows.Count > MaxReportRows)
        {
            ImportIssues.Add(new ImportIssueRowViewModel(string.Empty, $"… и ещё {rows.Count - MaxReportRows}", AppColors.Muted));
        }

        HasImportReport = true;
    }

    private static ImportIssueRowViewModel Row(string kind, ImportIssue issue, Color color)
    {
        var who = issue.Title is { Length: > 0 } title ? $"#{issue.FileTaskId} «{title.Trim()}»" : $"#{issue.FileTaskId}";
        return new ImportIssueRowViewModel(kind, $"{who}: {issue.Reason}", color);
    }
}
