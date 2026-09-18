using System.Globalization;
using System.Text.RegularExpressions;

namespace TaskManager.App.Services;

/// <summary>Язык интерфейса: код, название на самом языке, короткая метка для кнопки и культура для дат и чисел.</summary>
public sealed record Language(string Code, string Name, string Label, CultureInfo Culture);

/// <summary>
/// Перевод интерфейса на русский, английский, немецкий и украинский. Ключ — русский текст, как в макете: если перевода
/// нет, показывается он сам. Так любой пропущенный перевод виден как русская строка, а не как пустое место.
/// Смена языка вызывает <see cref="Changed"/>: приложение пересобирает окно, и все подписи читаются заново.
/// </summary>
public static class Loc
{
    public static IReadOnlyList<Language> Languages { get; } =
    [
        new("ru", "Русский", "RU", CultureInfo.GetCultureInfo("ru-RU")),
        new("en", "English", "EN", CultureInfo.GetCultureInfo("en-GB")),
        new("de", "Deutsch", "DE", CultureInfo.GetCultureInfo("de-DE")),
        new("uk", "Українська", "UK", CultureInfo.GetCultureInfo("uk-UA")),
    ];

    private static readonly Dictionary<string, string[]> Table = BuildTable();
    private static int _index;

    public static Language Current => Languages[_index];

    public static CultureInfo Culture => Current.Culture;

    /// <summary>Язык изменился.</summary>
    public static event Action? Changed;

    /// <summary>Выбирает язык по коду («ru», «en», «de», «uk»). Неизвестный код оставляет русский.</summary>
    public static void Set(string? code)
    {
        var index = Languages.ToList().FindIndex(l => l.Code == code);
        index = index < 0 ? 0 : index;
        if (index == _index && CultureInfo.DefaultThreadCurrentCulture == Culture)
        {
            return;
        }

        var changed = index != _index;
        _index = index;

        // Даты, числа и названия дней недели берутся из культуры языка.
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Перевод русской строки. Нет перевода: возвращает саму строку.</summary>
    public static string T(string ru)
    {
        if (_index == 0 || string.IsNullOrEmpty(ru))
        {
            return ru;
        }

        // Клавиша Ctrl по-немецки называется Strg.
        if (ru.StartsWith("Ctrl", StringComparison.Ordinal))
        {
            return _index == 2 ? "Strg" + ru[4..] : ru;
        }

        return Table.TryGetValue(ru, out var forms) && forms[_index - 1].Length > 0 ? forms[_index - 1] : ru;
    }

    /// <summary>Перевод шаблона с подстановкой: <c>Loc.F("Найдено задач: {0}", n)</c>.</summary>
    public static string F(string ruFormat, params object?[] args) => string.Format(Culture, T(ruFormat), args);

    /// <summary>
    /// Нужная форма слова для числа. Ключ — три русские формы через «|» («задача|задачи|задач»), перевод хранит
    /// формы своего языка в том же порядке (у английского и немецкого вторая и третья совпадают).
    /// </summary>
    public static string Plural(int n, string ruForms)
    {
        var forms = T(ruForms).Split('|');
        var tail = Math.Abs(n) % 100;
        var last = tail % 10;
        var slavic = _index is 0 or 3;

        var form = slavic
            ? tail is >= 11 and <= 14 ? 2 : last == 1 ? 0 : last is >= 2 and <= 4 ? 1 : 2
            : n == 1 ? 0 : 1;

        return forms[Math.Min(form, forms.Length - 1)];
    }

    /// <summary>Число вместе с нужной формой слова: «5 задач», «1 task».</summary>
    public static string Count(int n, string ruForms) => $"{n.ToString(Culture)} {Plural(n, ruForms)}";

    /// <summary>
    /// Перевод сообщения об ошибке из ядра приложения (оно написано по-русски и иногда содержит числа и названия).
    /// Сначала точное совпадение, затем шаблоны с подстановкой.
    /// </summary>
    public static string Message(string? ru)
    {
        if (string.IsNullOrEmpty(ru) || _index == 0)
        {
            return ru ?? string.Empty;
        }

        if (Table.ContainsKey(ru))
        {
            return T(ru);
        }

        foreach (var (pattern, key) in MessagePatterns.All)
        {
            var match = pattern.Match(ru);
            if (!match.Success)
            {
                continue;
            }

            var args = match.Groups.Cast<Group>().Skip(1).Select(g => (object?)g.Value).ToArray();
            return string.Format(Culture, T(key), args);
        }

        return ru;
    }

    private static Dictionary<string, string[]> BuildTable()
    {
        var table = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var rows in new[] { Translations.Interface, Translations.Dialogs, Translations.Messages })
        {
            foreach (var row in rows)
            {
                table[row[0]] = [row[1], row[2], row[3]];
            }
        }

        return table;
    }
}

/// <summary>Шаблоны сообщений ядра с числами и названиями: ключ шаблона лежит в таблице переводов с «{0}», «{1}».</summary>
internal static class MessagePatterns
{
    public static IReadOnlyList<(Regex Pattern, string Key)> All { get; } =
    [
        (new Regex(@"^Нельзя перевести задачу из (.+) в (.+): (.+)$"), "Нельзя перевести задачу из {0} в {1}: {2}"),
        (new Regex(@"^Название тега не может быть длиннее (\d+) символов\.$"), "Название тега не может быть длиннее {0} символов."),
        (new Regex(@"^Название задачи не может быть длиннее (\d+) символов\.$"), "Название задачи не может быть длиннее {0} символов."),
        (new Regex(@"^Нельзя завершить задачу: не завершены подзадачи \((.+)\)\.$"), "Нельзя завершить задачу: не завершены подзадачи ({0})."),
        (new Regex(@"^Вложенность подзадач не может быть больше (\d+) уровней\.$"), "Вложенность подзадач не может быть больше {0} уровней."),
        (new Regex(@"^Размер страницы должен быть от 1 до (\d+)\.$"), "Размер страницы должен быть от 1 до {0}."),
        (new Regex(@"^Тег «(.+)» уже есть\. Выберите другое название\.$"), "Тег «{0}» уже есть. Выберите другое название."),
        (new Regex(@"^Тег «(.+)» используется в задачах, удалить его нельзя\.$"), "Тег «{0}» используется в задачах, удалить его нельзя."),
        (new Regex(@"^Нельзя добавить зависимость: она создаёт цикл \((.+)\)\.$"), "Нельзя добавить зависимость: она создаёт цикл ({0})."),
        (new Regex(@"^Нельзя перевести задачу в (.+): не выполнены зависимости: (.+)$"), "Нельзя перевести задачу в {0}: не выполнены зависимости: {1}"),
        (new Regex(@"^Id (\d+) повторяется в файле\.$"), "Id {0} повторяется в файле."),
        (new Regex(@"^Вложенность больше (\d+) уровней\.$"), "Вложенность больше {0} уровней."),
        (new Regex(@"^Версия формата (\d+) не поддерживается \(поддерживается до (\d+)\)\.$"), "Версия формата {0} не поддерживается (поддерживается до {1})."),
        (new Regex(@"^Файл не похож на экспорт задач: (.+)$"), "Файл не похож на экспорт задач: {0}"),
        (new Regex(@"^Зависимость от задачи #(\d+) пропущена: её нет среди импортированных\.$"), "Зависимость от задачи #{0} пропущена: её нет среди импортированных."),
        (new Regex(@"^Зависимость от задачи #(\d+) пропущена: она создаёт цикл\.$"), "Зависимость от задачи #{0} пропущена: она создаёт цикл."),
        (new Regex(@"^Родительская задача #(\d+) не найдена в файле\.$"), "Родительская задача #{0} не найдена в файле."),
        (new Regex(@"^Данные восстановлены из резервной копии\. Прежняя база сохранена: (.+)$"), "Данные восстановлены из резервной копии. Прежняя база сохранена: {0}"),
        (new Regex(@"^Не удалось восстановить данные из копии, приложение работает со старой базой\. Файл копии оставлен: (.+)$"), "Не удалось восстановить данные из копии, приложение работает со старой базой. Файл копии оставлен: {0}"),
    ];
}
