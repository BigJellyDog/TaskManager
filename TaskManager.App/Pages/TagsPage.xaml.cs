using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class TagsPage : ContentPage
{
    private readonly TagsViewModel _viewModel;

    public TagsPage(TagsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
