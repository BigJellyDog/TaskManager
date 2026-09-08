namespace TaskManager.App.Services;

public sealed class DialogService : IDialogService
{
    // Страница текущего окна: в окне лежит либо экран загрузки, либо оболочка AppShell.
    private static Page? CurrentPage => Application.Current?.Windows is { Count: > 0 } windows ? windows[0].Page : null;

    public Task AlertAsync(string title, string message) =>
        CurrentPage?.DisplayAlertAsync(title, message, "OK") ?? Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Да", string cancel = "Нет") =>
        CurrentPage?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);
}
