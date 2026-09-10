using TaskManager.App.ViewModels;

namespace TaskManager.App.Controls;

/// <summary>Баннер напоминаний. Показывает общую модель, которую пополняет фоновый сервис.</summary>
public partial class ReminderBanner : ContentView
{
    public ReminderBanner()
    {
        InitializeComponent();

        // Контрол создаётся из XAML оболочки, у него нет внедрения зависимостей, поэтому берём модель из контейнера.
        BindingContext = IPlatformApplication.Current?.Services.GetRequiredService<ReminderBannerViewModel>();
    }
}
