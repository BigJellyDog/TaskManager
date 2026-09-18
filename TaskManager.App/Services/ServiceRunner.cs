namespace TaskManager.App.Services;

/// <summary>
/// Выполняет операцию в отдельной области (scope) зависимостей. DbContext у нас scoped, а модели представления
/// живут долго, поэтому каждое действие пользователя получает свой чистый DbContext: данные всегда свежие,
/// а отслеживаемые объекты не накапливаются.
/// </summary>
public sealed class ServiceRunner(IServiceScopeFactory scopes)
{
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = scopes.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task RunAsync(Func<IServiceProvider, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = scopes.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }
}
