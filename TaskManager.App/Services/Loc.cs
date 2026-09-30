using System.Globalization;
using System.Text.RegularExpressions;

namespace TaskManager.App.Services;

/// <summary>An interface language: the code, the name in the language itself, a short label for the button and the culture for dates and numbers.</summary>
public sealed record Language(string Code, string Name, string Label, CultureInfo Culture);

/// <summary>
/// Translation of the interface into Russian, English, German and Ukrainian. The key is the Russian text, as in the mockup: if there is no
/// translation, the key itself is shown. So any missing translation is visible as a Russian line, not as an empty place.
/// A language change raises <see cref="Changed"/>: the application rebuilds the window and all labels are read again.
/// </summary>
public static class Loc
{
    public static IReadOnlyList<Language> Languages { get; } =
    [
        new("de", "Deutsch", "DE", CultureInfo.GetCultureInfo("de-DE")),
        new("en", "English", "EN", CultureInfo.GetCultureInfo("en-GB")),
        new("ru", "Русский", "RU", CultureInfo.GetCultureInfo("ru-RU")),
        new("ua", "Українська", "UA", CultureInfo.GetCultureInfo("uk-UA")),
    ];

    private static readonly Dictionary<string, string[]> Table = BuildTable();
    private static int s_index = IndexOf("ru");

    public static Language Current => Languages[s_index];

    private static bool IsRussian => Current.Code == "ru";

    /// <summary>The column of the current language in the translation table: a row is [ru, en, de, uk], the order of languages in the menu does not affect it.</summary>
    private static int Column => Current.Code switch { "en" => 1, "de" => 2, "ua" => 3, _ => 0 };

    private static int IndexOf(string? code)
    {
        for (var i = 0; i < Languages.Count; i++)
        {
            if (Languages[i].Code == code)
            {
                return i;
            }
        }

        return -1;
    }

    public static CultureInfo Culture => Current.Culture;

    /// <summary>The language changed.</summary>
    public static event Action? Changed;

    /// <summary>Chooses a language by code ("ru", "en", "de", "ua"). An unknown code means Russian.</summary>
    public static void Set(string? code)
    {
        // Ukrainian used to be saved as "uk".
        var index = IndexOf(code == "uk" ? "ua" : code);
        index = index < 0 ? IndexOf("ru") : index;
        if (index == s_index && Equals(CultureInfo.DefaultThreadCurrentCulture, Culture))
        {
            return;
        }

        var changed = index != s_index;
        s_index = index;

        // Dates, numbers and weekday names are taken from the culture of the language.
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Translation of a Russian string. No translation: returns the string itself.</summary>
    public static string T(string ru)
    {
        if (IsRussian || string.IsNullOrEmpty(ru))
        {
            return ru;
        }

        // In German the Ctrl key is called Strg.
        if (ru.StartsWith("Ctrl", StringComparison.Ordinal))
        {
            return Current.Code == "de" ? "Strg" + ru[4..] : ru;
        }

        return Table.TryGetValue(ru, out var forms) && forms[Column].Length > 0 ? forms[Column] : ru;
    }

    /// <summary>Translation of a template with substitution: <c>Loc.F("Найдено задач: {0}", n)</c>.</summary>
    public static string F(string ruFormat, params object?[] args) => string.Format(Culture, T(ruFormat), args);

    /// <summary>
    /// The right word form for a number. The key is three Russian forms separated by "|" ("задача|задачи|задач"), the translation keeps
    /// the forms of its language in the same order (in English and German the second and third are the same).
    /// </summary>
    public static string Plural(int n, string ruForms)
    {
        var forms = T(ruForms).Split('|');
        var tail = Math.Abs(n) % 100;
        var last = tail % 10;
        var slavic = Current.Code is "ru" or "ua";

        var form = slavic
            ? tail is >= 11 and <= 14 ? 2 : last == 1 ? 0 : last is >= 2 and <= 4 ? 1 : 2
            : n == 1 ? 0 : 1;

        return forms[Math.Min(form, forms.Length - 1)];
    }

    /// <summary>A number together with the right word form: "5 задач", "1 task".</summary>
    public static string Count(int n, string ruForms) => $"{n.ToString(Culture)} {Plural(n, ruForms)}";

    /// <summary>
    /// Translation of an error message from the application core (it is written in Russian and sometimes contains numbers and names).
    /// First an exact match, then templates with substitution.
    /// </summary>
    public static string Message(string? ru)
    {
        if (string.IsNullOrEmpty(ru))
        {
            return string.Empty;
        }

        if (Table.ContainsKey(ru))
        {
            return T(ru);
        }

        foreach ((Regex pattern, string key) in MessagePatterns.All)
        {
            var match = pattern.Match(ru);
            if (!match.Success)
            {
                continue;
            }

            // The core inserts raw enum names ("Todo", "Done") and nested Russian reasons: show both in the interface language.
            var args = match.Groups.Cast<Group>().Skip(1).Select(g => (object?)LocalizeArgument(g.Value)).ToArray();
            return string.Format(Culture, T(key), args);
        }

        return ru;
    }

    private static string LocalizeArgument(string value)
    {
        // A letter first: "1" would otherwise parse as a numeric enum value.
        if (value.Length > 0 && char.IsLetter(value[0])
            && Enum.TryParse<TaskManager.Core.Domain.TaskStatus>(value, out var status) && Enum.IsDefined(status))
        {
            return DisplayNames.StatusName(status);
        }

        // Nested reasons are whole Russian sentences that end with a full stop.
        return value.EndsWith('.') && Table.ContainsKey(value) ? T(value) : value;
    }

    private static Dictionary<string, string[]> BuildTable()
    {
        var table = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var rows in new[] { Translations.Interface, Translations.Dialogs, Translations.Messages, Translations.Demo })
        {
            foreach (var row in rows)
            {
                table[row[0]] = [row[0], row[1], row[2], row[3]];
            }
        }

        return table;
    }
}

/// <summary>Templates of core messages with numbers and names: the template key is in the translation table with "{0}", "{1}".</summary>
internal static partial class MessagePatterns
{
    [GeneratedRegex(@"^Нельзя перевести задачу из (.+) в (.+): (.+)$")]
    private static partial Regex TaskTransferRegex();

    [GeneratedRegex(@"^Название тега не может быть длиннее (\d+) символов\.$")]
    private static partial Regex TagNameTooLongRegex();

    [GeneratedRegex(@"^Название задачи не может быть длиннее (\d+) символов\.$")]
    private static partial Regex TaskNameTooLongRegex();

    [GeneratedRegex(@"^Нельзя завершить задачу: не завершены подзадачи \((.+)\)\.$")]
    private static partial Regex IncompleteSubtasksRegex();

    [GeneratedRegex(@"^Вложенность подзадач не может быть больше (\d+) уровней\.$")]
    private static partial Regex SubtaskDepthExceededRegex();

    [GeneratedRegex(@"^Размер страницы должен быть от 1 до (\d+)\.$")]
    private static partial Regex PageSizeRangeRegex();

    [GeneratedRegex(@"^Тег «(.+)» уже есть\. Выберите другое название\.$")]
    private static partial Regex TagAlreadyExistsRegex();

    [GeneratedRegex(@"^Тег «(.+)» используется в задачах, удалить его нельзя\.$")]
    private static partial Regex TagInUseRegex();

    [GeneratedRegex(@"^Нельзя добавить зависимость: она создаёт цикл \((.+)\)\.$")]
    private static partial Regex DependencyCycleRegex();

    [GeneratedRegex(@"^Нельзя перевести задачу в (.+): не выполнены зависимости: (.+)$")]
    private static partial Regex UnmetDependenciesRegex();

    [GeneratedRegex(@"^Id (\d+) повторяется в файле\.$")]
    private static partial Regex DuplicateIdRegex();

    [GeneratedRegex(@"^Вложенность больше (\d+) уровней\.$")]
    private static partial Regex NestingDepthExceededRegex();

    [GeneratedRegex(@"^Версия формата (\d+) не поддерживается \(поддерживается до (\d+)\)\.$")]
    private static partial Regex UnsupportedFormatVersionRegex();

    [GeneratedRegex(@"^Файл не похож на экспорт задач: (.+)$")]
    private static partial Regex InvalidExportFileRegex();

    [GeneratedRegex(@"^Зависимость от задачи #(\d+) пропущена: её нет среди импортированных\.$")]
    private static partial Regex SkippedMissingDependencyRegex();

    [GeneratedRegex(@"^Зависимость от задачи #(\d+) пропущена: она создаёт цикл\.$")]
    private static partial Regex SkippedCyclicDependencyRegex();

    [GeneratedRegex(@"^Родительская задача #(\d+) не найдена в файле\.$")]
    private static partial Regex ParentTaskNotFoundRegex();

    [GeneratedRegex(@"^Данные восстановлены из резервной копии\. Прежняя база сохранена: (.+)$")]
    private static partial Regex DataRestoredFromBackupRegex();

    [GeneratedRegex(@"^Не удалось восстановить данные из копии, приложение работает со старой базой\. Файл копии оставлен: (.+)$")]
    private static partial Regex BackupRestoreFailedRegex();

    public static IReadOnlyList<(Regex Pattern, string Key)> All { get; } =
    [
        (TaskTransferRegex(), "Нельзя перевести задачу из «{0}» в «{1}»: {2}"),
        (TagNameTooLongRegex(), "Название тега не может быть длиннее {0} символов."),
        (TaskNameTooLongRegex(), "Название задачи не может быть длиннее {0} символов."),
        (IncompleteSubtasksRegex(), "Нельзя завершить задачу: не завершены подзадачи ({0})."),
        (SubtaskDepthExceededRegex(), "Вложенность подзадач не может быть больше {0} уровней."),
        (PageSizeRangeRegex(), "Размер страницы должен быть от 1 до {0}."),
        (TagAlreadyExistsRegex(), "Тег «{0}» уже есть. Выберите другое название."),
        (TagInUseRegex(), "Тег «{0}» используется в задачах, удалить его нельзя."),
        (DependencyCycleRegex(), "Нельзя добавить зависимость: она создаёт цикл ({0})."),
        (UnmetDependenciesRegex(), "Нельзя перевести задачу в «{0}»: не выполнены зависимости: {1}"),
        (DuplicateIdRegex(), "Id {0} повторяется в файле."),
        (NestingDepthExceededRegex(), "Вложенность больше {0} уровней."),
        (UnsupportedFormatVersionRegex(), "Версия формата {0} не поддерживается (поддерживается до {1})."),
        (InvalidExportFileRegex(), "Файл не похож на экспорт задач: {0}"),
        (SkippedMissingDependencyRegex(), "Зависимость от задачи #{0} пропущена: её нет среди импортированных."),
        (SkippedCyclicDependencyRegex(), "Зависимость от задачи #{0} пропущена: она создаёт цикл."),
        (ParentTaskNotFoundRegex(), "Родительская задача #{0} не найдена в файле."),
        (DataRestoredFromBackupRegex(), "Данные восстановлены из резервной копии. Прежняя база сохранена: {0}"),
        (BackupRestoreFailedRegex(), "Не удалось восстановить данные из копии, приложение работает со старой базой. Файл копии оставлен: {0}"),
    ];
}