using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class StatisticsPage : ContentPage
{
    private readonly StatisticsViewModel _viewModel;

    public StatisticsPage(StatisticsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // In a narrow window (about 1024) the layout keeps two panels and hides the others.
        SizeChanged += (_, _) =>
        {
            var narrow = Width is > 0 and < NarrowWidth;
            DeadlinesPanel.IsVisible = !narrow;
            BottomRow.IsVisible = !narrow;
        };
    }

    private const double NarrowWidth = 1000;

    // The figures may have changed on other screens, so we recalculate on every show.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
