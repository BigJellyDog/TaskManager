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
        themeIndex = (int)settings.Theme;
        defaultViewIndex = settings.DefaultBoard ? 0 : 1;
        reminderIndex = Math.Max(0, UserSettings.ReminderMinutesOptions.ToList().IndexOf(settings.ReminderMinutes));
        showBanner = settings.ShowReminderBanner;
    }

    public string PageSizeItems => string.Join('|', UserSettings.PageSizes);

    public string ReminderItems => string.Join('|', UserSettings.ReminderMinutesOptions.Select(m => $"{m} мин"));

    /// <summary>Полный путь к файлу базы данных.</summary>
    public string DatabasePath => Path.Combine(AppPaths.DataDirectory, _options.Value.DatabaseFileName);

    [ObservableProperty]
    private int pageSizeIndex;

    [ObservableProperty]
    private bool hideClosed;

    [ObservableProperty]
    private int themeIndex;

    [ObservableProperty]
    private int defaultViewIndex;

    [ObservableProperty]
    private int reminderIndex;

    [ObservableProperty]
    private bool showBanner;

    partial void OnPageSizeIndexChanged(int value) => _settings.PageSize = UserSettings.PageSizes[Math.Clamp(value, 0, UserSettings.PageSizes.Count - 1)];

    partial void OnHideClosedChanged(bool value) => _settings.HideClosed = value;

    partial void OnThemeIndexChanged(int value) => _settings.Theme = (ThemeChoice)Math.Clamp(value, 0, 2);

    partial void OnDefaultViewIndexChanged(int value) => _settings.DefaultBoard = value == 0;

    partial void OnReminderIndexChanged(int value) =>
        _settings.ReminderMinutes = UserSettings.ReminderMinutesOptions[Math.Clamp(value, 0, UserSettings.ReminderMinutesOptions.Count - 1)];

    partial void OnShowBannerChanged(bool value) => _settings.ShowReminderBanner = value;

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
