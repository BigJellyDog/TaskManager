using System.Text.Json;
using TaskManager.App.Configuration;
using TaskManager.Infrastructure.Reminders;

namespace TaskManager.App.Services;

/// <summary>Выбор темы: «Системная» следует за темой Windows. Числа сохраняются в settings.json, новые темы только в конец.</summary>
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
}

/// <summary>
/// Настройки пользователя: хранятся в settings.json рядом с базой и применяются сразу, без перезапуска.
/// Недоступный или повреждённый файл не мешает запуску: берутся значения по умолчанию.
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

    /// <summary>Что-то изменилось: подписчики (страницы, тема, баннер) перечитывают значения.</summary>
    public event EventHandler? Changed;

    /// <summary>Допустимые размеры страницы списка.</summary>
    public static IReadOnlyList<int> PageSizes { get; } = [10, 20, 50];

    /// <summary>Допустимые интервалы проверки напоминаний, минуты.</summary>
    public static IReadOnlyList<int> ReminderMinutesOptions { get; } = [1, 5, 15, 30];

    public int PageSize
    {
        get => _data.PageSize;
        set => Set(ref _data.PageSize, value);
    }

    /// <summary>Скрывать выполненные и отменённые задачи по умолчанию.</summary>
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

    /// <summary>Что открывается на экране «Задачи»: доска (true) или список.</summary>
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

    /// <summary>Язык интерфейса: «ru», «en», «de» или «ua».</summary>
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

    private void ApplyReminderInterval() => _reminderOptions.IntervalSeconds = Math.Max(1, _data.ReminderMinutes) * 60;

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
#pragma warning disable CA1031 // Повреждённый файл настроек не должен мешать запуску.
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
#pragma warning disable CA1031 // Не удалось сохранить: настройка действует до закрытия, ошибку пишем в журнал.
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
        public ThemeChoice Theme = ThemeChoice.Dark;
        public bool DefaultBoard = true;
        public int ReminderMinutes = 1;
        public bool ShowReminderBanner = true;
        public string Language = "ru";
    }
}
