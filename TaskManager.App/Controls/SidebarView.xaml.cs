using TaskManager.App.ViewModels;

namespace TaskManager.App.Controls;

/// <summary>The side panel. Created from the shell XAML, so it takes the model from the container.</summary>
public partial class SidebarView : ContentView
{
    public SidebarView()
    {
        InitializeComponent();
        BindingContext = IPlatformApplication.Current?.Services.GetRequiredService<SidebarViewModel>();
    }

    /// <summary>
    /// The narrow panel has no reminders block: a tap on the bell opens it to the right of the panel.
    /// It is the same block with the same buttons, and it closes when no reminders are left.
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

        // A window without its own border and background: the border and the surface are drawn by the block itself.
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

        // The last reminder was completed: the window closes itself.
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
