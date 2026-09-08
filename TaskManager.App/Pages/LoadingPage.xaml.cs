namespace TaskManager.App.Pages;

/// <summary>Экран на время запуска: пока применяются миграции базы данных.</summary>
public partial class LoadingPage : ContentPage
{
    public LoadingPage()
    {
        InitializeComponent();
    }

    /// <summary>Запуск не удался: убираем индикатор и показываем причину.</summary>
    public void ShowError(string message)
    {
        Indicator.IsRunning = false;
        Indicator.IsVisible = false;
        StatusLabel.Text = message;
    }
}
