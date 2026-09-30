using TaskManager.App.ViewModels;

namespace TaskManager.App.Controls;

/// <summary>The reminder banner. Shows the shared model that the background service fills.</summary>
public partial class ReminderBanner : ContentView
{
    public ReminderBanner()
    {
        InitializeComponent();

        // The control is created from the shell XAML and has no dependency injection, so we take the model from the container.
        BindingContext = IPlatformApplication.Current?.Services.GetRequiredService<ReminderBannerViewModel>();
    }
}
