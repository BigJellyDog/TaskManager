using System.Diagnostics;
using System.Text;
using CommunityToolkit.Maui.Storage;
using TaskManager.Core.Domain;

namespace TaskManager.App.Services;

public sealed class FileService : IFileService
{
    // Слишком большой файл для импорта почти наверняка не файл экспорта: не читаем его в память целиком.
    private const long MaxImportBytes = 50 * 1024 * 1024;

    // UTF-8 без BOM: JSON и Markdown так читаются любыми редакторами.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ILogger<FileService> _logger;

    public FileService(ILogger<FileService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> SaveWithDialogAsync(string suggestedFileName, string content, CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream(Utf8NoBom.GetBytes(content));
        var result = await FileSaver.Default.SaveAsync(suggestedFileName, stream, cancellationToken);

        if (result.IsSuccessful)
        {
            return result.FilePath;
        }

        // Пользователь закрыл окно (самый частый случай) или файл не удалось записать: подробности только в журнал.
        _logger.LogInformation(result.Exception, "Файл не сохранён через диалог: {FileName}", suggestedFileName);
        return null;
    }

    public async Task<string> SaveToDocumentsAsync(string fileName, string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TaskManager");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content, Utf8NoBom, cancellationToken);
        return path;
    }

    public async Task<string?> PickAndReadJsonAsync(CancellationToken cancellationToken = default)
    {
        var jsonType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = [".json"],
        });

        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Выберите файл экспорта (JSON)",
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
        // Проводник откроется с выделенным файлом. Windows-only приложение, поэтому explorer.exe доступен всегда.
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }
}
