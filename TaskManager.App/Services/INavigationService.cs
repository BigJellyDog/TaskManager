namespace TaskManager.App.Services;

/// <summary>Navigation between screens. View models use it and know nothing about Shell.</summary>
public interface INavigationService
{
    /// <summary>Opens a page by route, for example "taskedit?id=5".</summary>
    Task GoToAsync(string route);

    /// <summary>Goes back to the previous page.</summary>
    Task GoBackAsync();
}
