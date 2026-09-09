namespace TaskManager.App.Services;

/// <summary>Переходы между экранами. Модели представления используют его и не знают про Shell.</summary>
public interface INavigationService
{
    /// <summary>Открывает страницу по маршруту, например "taskedit?id=5".</summary>
    Task GoToAsync(string route);

    /// <summary>Возвращает на предыдущую страницу.</summary>
    Task GoBackAsync();
}
