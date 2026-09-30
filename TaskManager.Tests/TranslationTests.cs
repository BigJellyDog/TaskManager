using System.Text.RegularExpressions;
using TaskManager.App.Services;

namespace TaskManager.Tests;

/// <summary>
/// The translation tables of the interface (the application's Translations*.cs files are linked into the test project as they are):
/// a row = [Russian key, English, German, Ukrainian].
/// </summary>
public sealed partial class TranslationTests
{
    private static readonly (string Name, string[][] Rows)[] Tables =
    [
        (nameof(Translations.Interface), Translations.Interface),
        (nameof(Translations.Dialogs), Translations.Dialogs),
        (nameof(Translations.Messages), Translations.Messages),
        (nameof(Translations.Demo), Translations.Demo),
    ];

    public static TheoryData<string> TableNames => new(Tables.Select(t => t.Name));

    private static string[][] Rows(string name) => Tables.Single(t => t.Name == name).Rows;

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    [Theory]
    [MemberData(nameof(TableNames))]
    public void EveryRow_HasFourFilledColumns(string table)
    {
        foreach (var row in Rows(table))
        {
            Assert.True(row.Length == 4, $"{table}: в строке «{row.FirstOrDefault()}» {row.Length} столбцов вместо 4");
            Assert.All(row, cell => Assert.False(string.IsNullOrWhiteSpace(cell), $"{table}: пустой перевод в строке «{row[0]}»"));
        }
    }

    [Theory]
    [MemberData(nameof(TableNames))]
    public void Translation_KeepsPlaceholdersAndPluralForms(string table)
    {
        foreach (var row in Rows(table))
        {
            var placeholders = Placeholder().Matches(row[0]).Select(m => m.Value).Order().ToList();
            for (var i = 1; i < row.Length; i++)
            {
                Assert.True(
                    placeholders.SequenceEqual(Placeholder().Matches(row[i]).Select(m => m.Value).Order()),
                    $"{table}: в переводе №{i} строки «{row[0]}» другие подстановки {{n}}");
            }

            // Rows with forms through "|" (день|дня|дней, Доска|Список): the number of forms is the same in all languages.
            var forms = row[0].Count(c => c == '|');
            for (var i = 1; i < row.Length; i++)
            {
                Assert.True(row[i].Count(c => c == '|') == forms, $"{table}: в переводе №{i} строки «{row[0]}» другое число форм через «|»");
            }
        }
    }

    [Fact]
    public void SameKeyInSeveralRows_HasTheSameTranslation()
    {
        // The tables are merged into a dictionary and the last row wins: diverging translations of one key are a hidden error.
        foreach (var group in Tables.SelectMany(t => t.Rows).GroupBy(r => r[0]).Where(g => g.Count() > 1))
        {
            var first = group.First();
            Assert.All(group, row => Assert.True(row.SequenceEqual(first), $"ключ «{group.Key}» переведён по-разному в разных строках"));
        }
    }

    [Fact]
    public void ThemeNames_AreTranslated()
    {
        var all = Tables.SelectMany(t => t.Rows).GroupBy(r => r[0]).ToDictionary(g => g.Key, g => g.First());

        foreach (var name in new[] { "Системная", "Тёмная", "Светлая", "Аврора", "Аврора светлая", "Токийская ночь", "Solarized светлая" })
        {
            Assert.True(all.ContainsKey(name), $"нет перевода названия темы «{name}»");
        }

        Assert.Equal("Aurora", all["Аврора"][1]);
        Assert.Equal("Dark", all["Тёмная"][1]);
    }
}
