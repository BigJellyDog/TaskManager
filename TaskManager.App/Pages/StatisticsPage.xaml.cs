using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class StatisticsPage : ContentPage
{
    private readonly StatisticsViewModel _viewModel;

    public StatisticsPage(StatisticsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // В узком окне (около 1024) макет оставляет две панели, остальные прячет.
        SizeChanged += (_, _) =>
        {
            var narrow = Width is > 0 and < NarrowWidth;
            DeadlinesPanel.IsVisible = !narrow;
            BottomRow.IsVisible = !narrow;
        };
    }

    private const double NarrowWidth = 1000;

    // Цифры могли измениться на других экранах, поэтому пересчитываем при каждом показе.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
