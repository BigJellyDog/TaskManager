namespace TaskManager.App.Services;

/// <summary>Work with files for export and import. View models know nothing about system dialogs.</summary>
public interface IFileService
{
    /// <summary>The Windows "Save as…" window. Returns the file path, or null if the user cancelled.</summary>
    Task<string?> SaveWithDialogAsync(string suggestedFileName, string content, CancellationToken cancellationToken = default);

    /// <summary>Without a window: saves to "Documents\TaskManager". Returns the file path.</summary>
    Task<string> SaveToDocumentsAsync(string fileName, string content, CancellationToken cancellationToken = default);

    /// <summary>A window to choose a JSON file, and reading its text. Returns null if the user cancelled.</summary>
    Task<string?> PickAndReadJsonAsync(CancellationToken cancellationToken = default);

    /// <summary>A window to choose a file with the given extensions (for example ".db"). Returns the path, or null if the user cancelled.</summary>
    Task<string?> PickFilePathAsync(string title, params string[] extensions);

    /// <summary>The "Save as…" window for an already prepared file. Returns the path, or null if the user cancelled.</summary>
    Task<string?> SaveFileWithDialogAsync(string suggestedFileName, string sourceFilePath, CancellationToken cancellationToken = default);

    /// <summary>The path of a file in "Documents\TaskManager" (the folder is created, the file is not).</summary>
    string GetDocumentsFilePath(string fileName);

    /// <summary>A window to choose a folder. Returns the path, or null if the user cancelled.</summary>
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);

    /// <summary>Shows a file in Explorer.</summary>
    void RevealInFolder(string path);
}
