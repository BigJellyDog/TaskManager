using System.Text.Json;
using TaskManager.App.Configuration;
using TaskManager.Infrastructure.Reminders;

namespace TaskManager.App.Services;

/// <summary>The choice of theme: "System" follows the Windows theme. The numbers are saved in settings.json, new themes only at the end.</summary>
public enum ThemeChoice
{
    System,
    Light,
    Dark,
    Dracula,
    Nord,
    Gruvbox,
    Tokyo,
    Solar,
    AuroraDark,
    AuroraLight,
}

/// <summary>
/// User settings: stored in settings.json next to the database and applied at once, without a restart.
/// An unavailable or damaged file does not prevent start-up: the default values are used.
/// </summary>
public sealed class UserSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, IncludeFields = true };

    private readonly string _path;
    private readonly ReminderOptions _reminderOptions;
    private readonly ILogger<UserSettings> _logger;
    private Data _data = new();

    public UserSettings(Microsoft.Extensions.Options.IOptions<ReminderOptions> reminderOptions, ILogger<UserSettings> logger)
    {
        _path = Path.Combine(AppPaths.DataDirectory, "settings.json");
        _reminderOptions = reminderOptions.Value;
        _logger = logger;
        Load();
        ApplyReminderInterval();
    }

    /// <summary>Something changed: subscribers (pages, theme, banner) read the values again.</summary>
    public event EventHandler? Changed;

    /// <summary>Allowed page sizes of the list.</summary>
    public static IReadOnlyList<int> PageSizes { get; } = [10, 20, 50];

    /// <summary>Allowed intervals of the reminder check, minutes. 0 means "never": reminders are not shown.</summary>
    public static IReadOnlyList<int> ReminderMinutesOptions { get; } = [1, 5, 15, 30, 0];

    public int PageSize
    {
        get => _data.PageSize;
        set => Set(ref _data.PageSize, value);
    }

    /// <summary>Hide done and cancelled tasks by default.</summary>
    public bool HideClosed
    {
        get => _data.HideClosed;
        set => Set(ref _data.HideClosed, value);
    }

    public ThemeChoice Theme
    {
        get => _data.Theme;
        set => Set(ref _data.Theme, value);
    }

    /// <summary>What opens on the "Tasks" screen: the board (true) or the list.</summary>
    public bool DefaultBoard
    {
        get => _data.DefaultBoard;
        set => Set(ref _data.DefaultBoard, value);
    }

    public int ReminderMinutes
    {
        get => _data.ReminderMinutes;
        set
        {
            Set(ref _data.ReminderMinutes, value);
            ApplyReminderInterval();
        }
    }

    /// <summary>The interface language: "ru", "en", "de" or "ua".</summary>
    public string Language
    {
        get => _data.Language;
        set => Set(ref _data.Language, value);
    }

    public bool ShowReminderBanner
    {
        get => _data.ShowReminderBanner;
        set => Set(ref _data.ShowReminderBanner, value);
    }

    /// <summary>Also show reminders as a Windows system notification.</summary>
    public bool ShowWindowsToast
    {
        get => _data.ShowWindowsToast;
        set => Set(ref _data.ShowWindowsToast, value);
    }

    private void ApplyReminderInterval()
    {
        // "Never" switches off showing reminders, while the loop of the background service (recurring tasks) keeps running every minute.
        _reminderOptions.Enabled = _data.ReminderMinutes > 0;
        _reminderOptions.IntervalSeconds = Math.Max(1, _data.ReminderMinutes) * 60;
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                _data = JsonSerializer.Deserialize<Data>(File.ReadAllText(_path), JsonOptions) ?? new Data();
            }
        }
#pragma warning disable CA1031 // A damaged settings file must not prevent start-up.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Не удалось прочитать настройки из {Path}, взяты значения по умолчанию", _path);
            _data = new Data();
        }

        if (!PageSizes.Contains(_data.PageSize))
        {
            _data.PageSize = 20;
        }

        if (!ReminderMinutesOptions.Contains(_data.ReminderMinutes))
        {
            _data.ReminderMinutes = 1;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_data, JsonOptions));
        }
#pragma warning disable CA1031 // Saving failed: the setting stays in effect until closing, the error is written to the log.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Не удалось сохранить настройки в {Path}", _path);
        }
    }

    private sealed class Data
    {
        public int PageSize = 20;
        public bool HideClosed;
        public ThemeChoice Theme = ThemeChoice.AuroraDark;
        public bool DefaultBoard = true;
        public int ReminderMinutes = 1;
        public bool ShowReminderBanner = true;
        public bool ShowWindowsToast = true;
        public string Language = "ru";
    }
}
