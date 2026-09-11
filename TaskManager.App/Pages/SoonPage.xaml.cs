namespace TaskManager.App.Pages;

/// <summary>Заглушка для разделов, которые ещё не сделаны (корзина, теги, настройки).</summary>
public partial class SoonPage : ContentPage
{
    public SoonPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var route = Shell.Current.CurrentState.Location.OriginalString.TrimStart('/');
        TitleLabel.Text = route switch
        {
            "trash" => "Корзина",
            "tags" => "Теги",
            "settings" => "Настройки",
            _ => route,
        };
    }
}
