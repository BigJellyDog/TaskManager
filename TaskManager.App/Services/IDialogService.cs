namespace TaskManager.App.Services;

/// <summary>Dialogs for the user. View models work through the interface and know nothing about pages.</summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept = "Да", string cancel = "Нет");
}
