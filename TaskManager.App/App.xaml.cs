using TaskManager.App.Pages;
using TaskManager.App.Services;

namespace TaskManager.App;

public partial class App : Application
{
    // A window narrower than this (in interface units) shows the panel without labels.
    private const double CompactWidth = 1100;

    private readonly IServiceProvider _services;
    private Window? _window;
    private Label? _languageLabel;
    private Controls.Flag? _languageFlag;
    private readonly List<Border> _themeSwatches = [];

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;

        var settings = services.GetRequiredService<UserSettings>();
        Loc.Set(settings.Language);
        ThemeManager.Apply(settings.Theme);
        settings.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            ThemeManager.Apply(settings.Theme);
            UpdateThemeButton();
            Loc.Set(settings.Language);
        });
        Loc.Changed += () => MainThread.BeginInvokeOnMainThread(async () => await OnLanguageChangedAsync());
        ThemeManager.Changed += OnThemeModeChanged;
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
        // The loading screen comes first: while migrations are applied, the interface must not touch the database.
        var loading = new LoadingPage();
        var window = new Window(loading) { Title = "TaskManager", TitleBar = CreateTitleBar() };
        _window = window;

        // Narrow window (about 1024): the side panel folds into icons, as in the mockup.
        window.SizeChanged += (_, _) =>
        {
            var sidebar = _services.GetRequiredService<ViewModels.SidebarViewModel>();
            var compact = window.Width is > 0 and < CompactWidth;
            if (compact != sidebar.IsCompact)
            {
                sidebar.IsCompact = compact;
                if (Shell.Current is { } shell)
                {
                    shell.FlyoutWidth = compact ? 56 : 228;
                }
            }
        };
        window.HandlerChanged += (_, _) =>
        {
            _services.GetRequiredService<HotKeyService>().Attach(window);
            ThemeManager.AttachWindow(window);
        };
        window.Created += async (_, _) =>
        {
#if WINDOWS
            // The window opens on top of the others instead of staying behind the program that started it.
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
            {
                Platforms.Windows.WindowActivator.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(native));
            }
#endif
            await StartAsync(window, loading);
        };
        window.Destroying += (_, _) => _services.GetRequiredService<AppInitializer>().Shutdown();

        return window;
    }

    /// <summary>The window title bar as in the Signal mockup: a check mark, a monospaced name and the language button "RU ▾" on the right.</summary>
    private TitleBar CreateTitleBar()
    {
        var icon = new Controls.Icon { Kind = "check", Size = 14 };
        icon.SetDynamicResource(Controls.Icon.ColorProperty, "InkMuted");
        var label = new Label { Text = "TaskManager", FontFamily = "Segoe UI Variable", FontSize = 11, CharacterSpacing = 0.4, VerticalOptions = LayoutOptions.Center };
        label.SetDynamicResource(Label.TextColorProperty, "InkMuted");

        _languageFlag = new Controls.Flag { Code = Loc.Current.Code };
        _languageLabel = new Label { FontFamily = "Segoe UI Variable", FontSize = 11, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        _languageLabel.SetDynamicResource(Label.TextColorProperty, "Ink");
        UpdateLanguageLabel();

        // A real button, not a border with a gesture: the title bar treats its whole area as draggable except real
        // buttons. A clickable transparent button covers the whole area; under it there is only a picture that ignores taps.
        static Grid PillButton(View face, Action<VisualElement> open)
        {
            var hit = new Button
            {
                Text = string.Empty,
                BackgroundColor = Colors.Transparent,
                BorderWidth = 1,
                CornerRadius = 4,
                Padding = new Thickness(0),
                HeightRequest = 22,
                MinimumHeightRequest = 0,
            };
            hit.SetDynamicResource(Button.BorderColorProperty, "Line");
            hit.Clicked += (_, _) => open(hit);

            return new Grid
            {
                HeightRequest = 22,
                VerticalOptions = LayoutOptions.Center,
                Children = { face, hit },
            };
        }

        var languageFace = new HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(8, 0),
            InputTransparent = true,
            VerticalOptions = LayoutOptions.Center,
            Children = { _languageFlag, _languageLabel },
        };

        // Theme button: three coloured stripes (background, surface, accent) of the current theme and an arrow.
        var themeFace = new HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(8, 0),
            InputTransparent = true,
            VerticalOptions = LayoutOptions.Center,
        };
        var swatchRow = new HorizontalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        for (var i = 0; i < 3; i++)
        {
            var swatch = new Border { WidthRequest = 8, HeightRequest = 12, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 2 }, StrokeThickness = 0 };
            _themeSwatches.Add(swatch);
            swatchRow.Children.Add(swatch);
        }

        var themeArrow = new Label { Text = "▾", FontFamily = "Segoe UI Variable", FontSize = 11, VerticalOptions = LayoutOptions.Center };
        themeArrow.SetDynamicResource(Label.TextColorProperty, "Ink");
        themeFace.Children.Add(swatchRow);
        themeFace.Children.Add(themeArrow);
        UpdateThemeButton();

        var button = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 14, 0),
            Children = { PillButton(themeFace, OpenThemeMenu), PillButton(languageFace, OpenLanguageMenu) },
        };

        var content = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(12, 0, 0, 0), VerticalOptions = LayoutOptions.Center };
        content.Add(new HorizontalStackLayout { Spacing = 8, Children = { icon, label }, VerticalOptions = LayoutOptions.Center }, 0, 0);
        content.Add(button, 1, 0);

        var titleBar = new TitleBar { HeightRequest = 32, Content = content };
        titleBar.SetDynamicResource(VisualElement.BackgroundColorProperty, "Side");
        titleBar.SetDynamicResource(TitleBar.ForegroundColorProperty, "InkMuted");
        return titleBar;
    }

    private void UpdateLanguageLabel()
    {
        if (_languageLabel is not null)
        {
            _languageFlag!.Code = Loc.Current.Code;
            _languageLabel.Text = Loc.Current.Label + " ▾";
        }
    }

    private void UpdateThemeButton()
    {
        var choice = _services.GetRequiredService<UserSettings>().Theme;
        var colors = ThemeCatalog.Swatch(choice);
        for (var i = 0; i < _themeSwatches.Count; i++)
        {
            _themeSwatches[i].BackgroundColor = Color.FromArgb(colors[i]);
        }
    }

    private void OpenThemeMenu(VisualElement anchor)
    {
        var current = _services.GetRequiredService<UserSettings>().Theme;
        var entries = ThemeCatalog.All
            .Select(t => new Controls.DropEntry(ThemeCatalog.DisplayName(t), Swatch: ThemeCatalog.Swatch(t.Choice)))
            .ToList();
        var selected = ThemeCatalog.All.ToList().FindIndex(t => t.Choice == current);
        Controls.DropMenu.Show(anchor, entries, selected, index => _services.GetRequiredService<UserSettings>().Theme = ThemeCatalog.All[index].Choice, mono: false);
    }

    private void OpenLanguageMenu(VisualElement anchor)
    {
        var entries = Loc.Languages.Select(l => new Controls.DropEntry(l.Name, Hint: l.Label)).ToList();
        var selected = Loc.Languages.ToList().FindIndex(l => l.Code == Loc.Current.Code);
        Controls.DropMenu.Show(anchor, entries, selected, index => _services.GetRequiredService<UserSettings>().Language = Loc.Languages[index].Code, mono: false);
    }

    /// <summary>
    /// The language changed: the window is rebuilt, the pages are created again in the new language, and the open section stays the same.
    /// </summary>
    private async Task OnLanguageChangedAsync()
    {
        UpdateLanguageLabel();
        await RebuildShellAsync();
    }

    // The theme the current shell was built with. Settings call ThemeManager.Apply on every change, so we compare themes.
    private ThemeMode? _shellMode;

    private void OnThemeModeChanged(object? sender, EventArgs e)
    {
        if (_shellMode is { } built && built != ThemeManager.Current)
        {
            MainThread.BeginInvokeOnMainThread(async () => await RebuildShellAsync());
        }
    }

    /// <summary>
    /// Rebuilds the shell with the open section. Needed on a language or theme change: colours that elements got through
    /// triggers (selected item, period switch) do not update by themselves after a theme change, while freshly created ones take the new colours.
    /// </summary>
    private async Task RebuildShellAsync()
    {
        _shellMode = ThemeManager.Current;
        if (_window is null || _window.Page is not AppShell)
        {
            return;
        }

        var location = Shell.Current?.CurrentState?.Location.OriginalString ?? "//tasks";
        var route = location.TrimStart('/').Split('/', '?')[0];

        var shell = new AppShell();
        _window.Page = shell;
        ThemeManager.AttachWindow(_window);
        _services.GetRequiredService<ViewModels.SidebarViewModel>().Relocalize();

        await WaitUntilLoadedAsync(shell);
        shell.FlyoutWidth = _services.GetRequiredService<ViewModels.SidebarViewModel>().IsCompact ? 56 : 228;
        if (route.Length > 0 && route != "tasks")
        {
            await Shell.Current!.GoToAsync("//" + route);
        }
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

    /// <summary>
    /// Creates one spare page of every section, with pauses: the first creation of a page (JIT, markup parsing)
    /// takes noticeably longer than the next ones, and without this the first switch to a tab would lag.
    /// </summary>
    private async Task WarmUpPagesAsync()
    {
        await Task.Delay(1500);

        foreach (var type in new[] { typeof(Pages.StatisticsPage), typeof(Pages.DataPage), typeof(Pages.TrashPage), typeof(Pages.TagsPage), typeof(Pages.SettingsPage) })
        {
            try
            {
                _ = _services.GetRequiredService(type);
            }
#pragma warning disable CA1031 // The warm-up is optional.
            catch (Exception)
#pragma warning restore CA1031
            {
                return;
            }

            await Task.Delay(200);
        }
    }

    private async Task StartAsync(Window window, LoadingPage loading)
    {
        try
        {
            await _services.GetRequiredService<AppInitializer>().InitializeAsync();
            var shell = new AppShell();
            _shellMode = ThemeManager.Current;
            window.Page = shell;
            _ = WarmUpPagesAsync();

            if (_services.GetRequiredService<AppInitializer>().StartupNotice is { } notice)
            {
                // A dialog must not be shown until the shell has loaded: the window has no XAML root yet and WinUI crashes.
                await WaitUntilLoadedAsync(shell);
                await _services.GetRequiredService<IDialogService>().AlertAsync(Loc.T("Восстановление данных"), Loc.Message(notice));
            }
        }
#pragma warning disable CA1031 // A start-up must not fail silently: we show the reason on screen and write it to the log.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _services.GetRequiredService<ILogger<App>>().LogCritical(ex, "Не удалось запустить приложение");
            loading.ShowError(Loc.F("Не удалось запустить приложение: {0}", ex.Message));
        }
    }
}
