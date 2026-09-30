namespace TaskManager.App.Services;

/// <summary>
/// Runs the operation in a separate dependency scope. Our DbContext is scoped, while view models
/// live long, so every user action gets its own clean DbContext: the data is always fresh,
/// and tracked objects do not pile up.
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
