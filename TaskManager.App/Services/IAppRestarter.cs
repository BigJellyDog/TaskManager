namespace TaskManager.App.Services;

/// <summary>Перезапуск приложения (например, чтобы применить восстановление базы из копии).</summary>
public interface IAppRestarter
{
    /// <summary>Запускает новый экземпляр приложения и закрывает текущий.</summary>
    void Restart();
}
