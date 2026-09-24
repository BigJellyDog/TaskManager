using Microsoft.Extensions.Options;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Configuration;
using TaskManager.App.Services;

namespace TaskManager.App.ViewModels;

/// <summary>Настройки: каждый переключатель сразу записывается в <see cref="UserSettings"/> и применяется.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly UserSettings _settings;
    private readonly IOptions<AppOptions> _options;

    public SettingsViewModel(UserSettings settings, IOptions<AppOptions> options)
    {
        _settings = settings;
        _options = options;

        pageSizeIndex = Math.Max(0, UserSettings.PageSizes.ToList().IndexOf(settings.PageSize));
        hideClosed = settings.HideClosed;
        selectedTheme = ThemeCatalog.DisplayName(ThemeCatalog.Of(settings.Theme));
        defaultViewIndex = settings.DefaultBoard ? 0 : 1;
        showBanner = settings.ShowReminderBanner;
        showWindowsToast = settings.ShowWindowsToast;
        selectedReminder = ReminderOptions[Math.Max(0, UserSettings.ReminderMinutesOptions.ToList().IndexOf(settings.ReminderMinutes))];
    }

    public string PageSizeItems => string.Join('|', UserSettings.PageSizes);

    /// <summary>Интервалы проверки напоминаний для выпадающего поля: «1 минута», «5 минут» ...</summary>
    public IList<string> ReminderOptions { get; } =
        UserSettings.ReminderMinutesOptions.Select(m => Loc.T($"{m} {(m == 1 ? "минута" : "минут")}")).ToList();

    /// <summary>Названия языков на самих языках: Русский, English, Deutsch, Українська.</summary>
    public IList<string> LanguageNames { get; } = Loc.Languages.Select(l => l.Name).ToList();

    [ObservableProperty]
    private string selectedLanguage = Loc.Current.Name;

    [ObservableProperty]
    private string selectedReminder = string.Empty;

    /// <summary>Полный путь к файлу базы данных.</summary>
    public string DatabasePath => Path.Combine(AppPaths.DataDirectory, _options.Value.DatabaseFileName);

    [ObservableProperty]
    private int pageSizeIndex;

    [ObservableProperty]
    private bool hideClosed;

    /// <summary>Названия тем на языке интерфейса и образцы их цветов для списка.</summary>
    public IList<string> ThemeNames { get; } = ThemeCatalog.All.Select(ThemeCatalog.DisplayName).ToList();

    public IList<string[]> ThemeSwatches { get; } = ThemeCatalog.All.Select(t => ThemeCatalog.Swatch(t.Choice)).ToList();

    [ObservableProperty]
    private string selectedTheme = string.Empty;

    [ObservableProperty]
    private int defaultViewIndex;

    [ObservableProperty]
    private bool showBanner;

    [ObservableProperty]
    private bool showWindowsToast;

    partial void OnPageSizeIndexChanged(int value) => _settings.PageSize = UserSettings.PageSizes[Math.Clamp(value, 0, UserSettings.PageSizes.Count - 1)];

    partial void OnHideClosedChanged(bool value) => _settings.HideClosed = value;

    partial void OnSelectedThemeChanged(string value)
    {
        var index = ThemeNames.IndexOf(value);
        if (index >= 0)
        {
            _settings.Theme = ThemeCatalog.All[index].Choice;
        }
    }

    partial void OnDefaultViewIndexChanged(int value) => _settings.DefaultBoard = value == 0;

    partial void OnSelectedReminderChanged(string value)
    {
        var index = ReminderOptions.IndexOf(value);
        if (index >= 0)
        {
            _settings.ReminderMinutes = UserSettings.ReminderMinutesOptions[index];
        }
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        var language = Loc.Languages.FirstOrDefault(l => l.Name == value);
        if (language is not null)
        {
            _settings.Language = language.Code;
        }
    }

    partial void OnShowBannerChanged(bool value) => _settings.ShowReminderBanner = value;

    partial void OnShowWindowsToastChanged(bool value) => _settings.ShowWindowsToast = value;

    [RelayCommand]
    private void RevealDatabase()
    {
        var path = DatabasePath;
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        else if (Directory.Exists(AppPaths.DataDirectory))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.DataDirectory}\"") { UseShellExecute = true });
        }
    }
}
