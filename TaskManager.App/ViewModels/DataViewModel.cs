using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Export;

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

    public DataViewModel(
        ServiceRunner runner,
        IFileService files,
        TimeProvider clock,
        IDialogService dialogs,
        ILogger<DataViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
        _files = files;
        _clock = clock;
    }

    public ObservableCollection<ImportIssueRowViewModel> ImportIssues { get; } = [];

    [ObservableProperty]
    private bool includeDeleted;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSavedPath))]
    [NotifyCanExecuteChangedFor(nameof(RevealSavedCommand))]
    private string? lastSavedPath;

    [ObservableProperty]
    private string importSummary = string.Empty;

    [ObservableProperty]
    private bool hasImportReport;

    public bool HasSavedPath => !string.IsNullOrEmpty(LastSavedPath);

    [RelayCommand]
    private Task ExportJsonAsAsync() => ExportAsync(json: true, withDialog: true);

    [RelayCommand]
    private Task ExportJsonQuickAsync() => ExportAsync(json: true, withDialog: false);

    [RelayCommand]
    private Task ExportMarkdownAsAsync() => ExportAsync(json: false, withDialog: true);

    [RelayCommand]
    private Task ExportMarkdownQuickAsync() => ExportAsync(json: false, withDialog: false);

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
