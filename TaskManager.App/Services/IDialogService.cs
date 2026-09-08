namespace TaskManager.App.Services;

/// <summary>Диалоги для пользователя. Модели представления работают через интерфейс и не знают про страницы.</summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept = "Да", string cancel = "Нет");
}
