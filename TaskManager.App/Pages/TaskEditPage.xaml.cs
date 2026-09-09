using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class TaskEditPage : ContentPage
{
    private readonly TaskEditViewModel _viewModel;
    private bool _loaded;

    public TaskEditPage(TaskEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Загружаем один раз: повторное появление страницы не должно затирать то, что пользователь уже ввёл.
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await _viewModel.LoadAsync();
    }
}
