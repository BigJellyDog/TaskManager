namespace TaskManager.App.Pages;

/// <summary>The screen for the time of start-up: while the database migrations are applied.</summary>
public partial class LoadingPage : ContentPage
{
    public LoadingPage()
    {
        InitializeComponent();
    }

    /// <summary>The start failed: we remove the indicator and show the reason.</summary>
    public void ShowError(string message)
    {
        Indicator.IsRunning = false;
        Indicator.IsVisible = false;
        StatusLabel.Text = message;
    }
}
