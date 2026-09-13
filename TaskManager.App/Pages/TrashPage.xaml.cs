using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class TrashPage : ContentPage
{
    private readonly TrashViewModel _viewModel;

    public TrashPage(TrashViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Корзина пополняется на других экранах, поэтому перечитываем при каждом показе.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
