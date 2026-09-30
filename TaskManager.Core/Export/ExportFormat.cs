using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskManager.Core.Export;

/// <summary>Common settings of the JSON format: the same for export and import.</summary>
public static class ExportFormat
{
    /// <summary>The current format version. Increased when the format changes incompatibly.</summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Enumerations are written as words (<c>"High"</c>, <c>"InProgress"</c>), not as numbers: the file is human-readable
    /// and does not break if the order of the values changes.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },

        // Cyrillic stays Cyrillic in the file instead of being written as \uXXXX escapes.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
