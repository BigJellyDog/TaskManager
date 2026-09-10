namespace TaskManager.App.Services;

/// <summary>Работа с файлами для экспорта и импорта. Модели представления не знают про системные диалоги.</summary>
public interface IFileService
{
    /// <summary>Окно Windows «Сохранить как…». Возвращает путь к файлу или null, если пользователь отказался.</summary>
    Task<string?> SaveWithDialogAsync(string suggestedFileName, string content, CancellationToken cancellationToken = default);

    /// <summary>Без окна: сохраняет в «Документы\TaskManager». Возвращает путь к файлу.</summary>
    Task<string> SaveToDocumentsAsync(string fileName, string content, CancellationToken cancellationToken = default);

    /// <summary>Окно выбора JSON-файла и чтение его текста. Возвращает null, если пользователь отказался.</summary>
    Task<string?> PickAndReadJsonAsync(CancellationToken cancellationToken = default);

    /// <summary>Показывает файл в Проводнике.</summary>
    void RevealInFolder(string path);
}
