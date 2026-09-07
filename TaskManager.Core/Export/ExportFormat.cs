using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskManager.Core.Export;

/// <summary>Общие настройки формата JSON: одни и те же для экспорта и импорта.</summary>
public static class ExportFormat
{
    /// <summary>Текущая версия формата. Увеличивается, если формат меняется несовместимо.</summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Перечисления пишутся словами (<c>"High"</c>, <c>"InProgress"</c>), а не числами: файл читаем человеком
    /// и не ломается, если порядок значений поменяется.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },

        // Кириллица в файле остаётся кириллицей, а не Да.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
