using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class StatisticsPage : ContentPage
{
    private readonly StatisticsViewModel _viewModel;

    public StatisticsPage(StatisticsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Цифры могли измениться на других экранах, поэтому пересчитываем при каждом показе.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
