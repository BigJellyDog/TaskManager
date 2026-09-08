using TaskManager.App.Pages;
using TaskManager.App.Services;

namespace TaskManager.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Сначала экран загрузки: пока применяются миграции, интерфейс не должен обращаться к базе.
        var loading = new LoadingPage();
        var window = new Window(loading) { Title = "TaskManager" };

        window.Created += async (_, _) => await StartAsync(window, loading);
        window.Destroying += (_, _) => _services.GetRequiredService<AppInitializer>().Shutdown();

        return window;
    }

    private async Task StartAsync(Window window, LoadingPage loading)
    {
        try
        {
            await _services.GetRequiredService<AppInitializer>().InitializeAsync();
            window.Page = new AppShell();
        }
#pragma warning disable CA1031 // Запуск не должен падать молча: показываем причину на экране и пишем в журнал.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _services.GetRequiredService<ILogger<App>>().LogCritical(ex, "Не удалось запустить приложение");
            loading.ShowError($"Не удалось запустить приложение: {ex.Message}");
        }
    }
}
