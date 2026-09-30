namespace TaskManager.App.Services;

/// <summary>Restart of the application (for example, to apply a database restore from a backup).</summary>
public interface IAppRestarter
{
    /// <summary>Starts a new instance of the application and closes the current one.</summary>
    void Restart();
}
