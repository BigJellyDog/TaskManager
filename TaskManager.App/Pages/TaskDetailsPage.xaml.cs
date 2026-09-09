using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class TaskDetailsPage : ContentPage
{
    private readonly TaskDetailsViewModel _viewModel;

    public TaskDetailsPage(TaskDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Обновляем при каждом показе: после правки в форме или перехода назад данные карточки могли измениться.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
