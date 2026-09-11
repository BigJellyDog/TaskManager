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
}
