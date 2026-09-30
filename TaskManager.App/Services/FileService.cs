using System.Diagnostics;
using System.Text;
using CommunityToolkit.Maui.Storage;
using TaskManager.Core.Domain;

namespace TaskManager.App.Services;

public sealed class FileService(ILogger<FileService> logger) : IFileService
{
    // A file that is too large for an import is almost certainly not an export file: we do not read it into memory whole.
    private const long MaxImportBytes = 50 * 1024 * 1024;

    // UTF-8 without BOM: JSON and Markdown are read this way by any editor.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<string?> SaveWithDialogAsync(string suggestedFileName, string content, CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream(Utf8NoBom.GetBytes(content));
        var result = await FileSaver.Default.SaveAsync(suggestedFileName, stream, cancellationToken);

        if (result.IsSuccessful)
        {
            return result.FilePath;
        }

        // The user closed the window (the most common case) or the file could not be written: details go only to the log.
        logger.LogInformation(result.Exception, "Файл не сохранён через диалог: {FileName}", suggestedFileName);
        return null;
    }

    public async Task<string> SaveToDocumentsAsync(string fileName, string content, CancellationToken cancellationToken = default)
    {
        var path = GetDocumentsFilePath(fileName);
        await File.WriteAllTextAsync(path, content, Utf8NoBom, cancellationToken);
        return path;
    }

    public string GetDocumentsFilePath(string fileName)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TaskManager");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    public async Task<string?> PickFilePathAsync(string title, params string[] extensions)
    {
        var fileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = extensions,
        });

        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title, FileTypes = fileType });
        return file?.FullPath;
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var result = await FolderPicker.Default.PickAsync(cancellationToken);
        return result.IsSuccessful ? result.Folder.Path : null;
    }

    public async Task<string?> SaveFileWithDialogAsync(string suggestedFileName, string sourceFilePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(sourceFilePath);
        var result = await FileSaver.Default.SaveAsync(suggestedFileName, stream, cancellationToken);

        if (result.IsSuccessful)
        {
            return result.FilePath;
        }

        logger.LogInformation(result.Exception, "Файл не сохранён через диалог: {FileName}", suggestedFileName);
        return null;
    }

    public async Task<string?> PickAndReadJsonAsync(CancellationToken cancellationToken = default)
    {
        var jsonType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = [".json"],
        });

        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = Loc.T("Выберите файл экспорта (JSON)"),
            FileTypes = jsonType,
        });

        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        if (stream.Length > MaxImportBytes)
        {
            throw new DomainValidationException("Файл слишком большой для импорта (больше 50 МБ).");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public void RevealInFolder(string path)
    {
        // Explorer opens with the file selected. A Windows-only application, so explorer.exe is always available.
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }
}
