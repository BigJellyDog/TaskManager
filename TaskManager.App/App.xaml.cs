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

        var settings = services.GetRequiredService<UserSettings>();
        ThemeManager.Apply(settings.Theme);
        settings.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(() => ThemeManager.Apply(settings.Theme));
        RequestedThemeChanged += (_, _) =>
        {
            if (settings.Theme == ThemeChoice.System)
            {
                ThemeManager.Apply(ThemeChoice.System);
            }
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Сначала экран загрузки: пока применяются миграции, интерфейс не должен обращаться к базе.
        var loading = new LoadingPage();
        var window = new Window(loading) { Title = "TaskManager", TitleBar = CreateTitleBar() };

        window.HandlerChanged += (_, _) => _services.GetRequiredService<HotKeyService>().Attach(window);
        window.Created += async (_, _) => await StartAsync(window, loading);
        window.Destroying += (_, _) => _services.GetRequiredService<AppInitializer>().Shutdown();

        return window;
    }

    /// <summary>Заголовок окна как в макете Signal: цвет боковой панели, галочка и моноширинное название.</summary>
    private static TitleBar CreateTitleBar()
    {
        var icon = new Controls.Icon { Kind = "check", Size = 14 };
        icon.SetDynamicResource(Controls.Icon.ColorProperty, "InkMuted");
        var label = new Label { Text = "TaskManager", FontFamily = "Consolas", FontSize = 11, CharacterSpacing = 0.4, VerticalOptions = LayoutOptions.Center };
        label.SetDynamicResource(Label.TextColorProperty, "InkMuted");

        var titleBar = new TitleBar
        {
            HeightRequest = 32,
            Content = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(12, 0), Children = { icon, label }, VerticalOptions = LayoutOptions.Center },
        };
        titleBar.SetDynamicResource(VisualElement.BackgroundColorProperty, "Side");
        titleBar.SetDynamicResource(TitleBar.ForegroundColorProperty, "InkMuted");
        return titleBar;
    }

    private static Task WaitUntilLoadedAsync(VisualElement element)
    {
        if (element.IsLoaded)
        {
            return Task.CompletedTask;
        }

        var loaded = new TaskCompletionSource();
        void OnLoaded(object? sender, EventArgs e)
        {
            element.Loaded -= OnLoaded;
            loaded.TrySetResult();
        }

        element.Loaded += OnLoaded;
        return loaded.Task;
    }

    private async Task StartAsync(Window window, LoadingPage loading)
    {
        try
        {
            await _services.GetRequiredService<AppInitializer>().InitializeAsync();
            var shell = new AppShell();
            window.Page = shell;

            if (_services.GetRequiredService<AppInitializer>().StartupNotice is { } notice)
            {
                // Диалог нельзя показывать, пока оболочка не загрузилась: у окна ещё нет корня XAML, и WinUI падает.
                await WaitUntilLoadedAsync(shell);
                await _services.GetRequiredService<IDialogService>().AlertAsync("Восстановление данных", notice);
            }
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
