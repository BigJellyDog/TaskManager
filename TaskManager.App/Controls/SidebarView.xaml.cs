using TaskManager.App.ViewModels;

namespace TaskManager.App.Controls;

/// <summary>Боковая панель. Создаётся из XAML оболочки, поэтому модель берёт из контейнера.</summary>
public partial class SidebarView : ContentView
{
    public SidebarView()
    {
        InitializeComponent();
        BindingContext = IPlatformApplication.Current?.Services.GetRequiredService<SidebarViewModel>();
    }

    /// <summary>
    /// В узкой панели блока напоминаний нет: нажатие на колокольчик открывает его справа от панели.
    /// Это тот же блок с теми же кнопками, он закрывается, когда напоминаний не остаётся.
    /// </summary>
    private void OnBellTapped(object? sender, TappedEventArgs e)
    {
#if WINDOWS
        if (BindingContext is not SidebarViewModel { Reminders: { IsVisible: true } reminders }
            || Bell.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement anchor
            || Bell.Handler.MauiContext is not { } context)
        {
            return;
        }

        var banner = new ReminderBanner { WidthRequest = 207 };
        var native = Microsoft.Maui.Platform.ElementExtensions.ToPlatform(banner, context);
        native.RequestedTheme = Services.ThemeManager.NativeTheme;

        // Окно без своей рамки и фона: рамку и поверхность рисует сам блок.
        var style = new Microsoft.UI.Xaml.Style(typeof(Microsoft.UI.Xaml.Controls.FlyoutPresenter));
        style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.Control.BackgroundProperty, new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)));
        style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.Control.BorderThicknessProperty, new Microsoft.UI.Xaml.Thickness(0)));
        style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.Control.PaddingProperty, new Microsoft.UI.Xaml.Thickness(0)));

        var flyout = new Microsoft.UI.Xaml.Controls.Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.RightEdgeAlignedBottom,
            Content = native,
            FlyoutPresenterStyle = style,
        };

        // Выполнили последнее напоминание: окно закрывается само.
        void Close(object? s, System.ComponentModel.PropertyChangedEventArgs a)
        {
            if (a.PropertyName == nameof(reminders.IsVisible) && !reminders.IsVisible)
            {
                flyout.Hide();
            }
        }

        reminders.PropertyChanged += Close;
        flyout.Closed += (_, _) => reminders.PropertyChanged -= Close;
        flyout.ShowAt(anchor);
#endif
    }
}
