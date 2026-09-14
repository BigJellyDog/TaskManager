using TaskManager.App.Services;
using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class TaskDetailsPage : ContentPage
{
    private readonly TaskDetailsViewModel _viewModel;

    private readonly HotKeyService _hotKeys;

    public TaskDetailsPage(TaskDetailsViewModel viewModel, HotKeyService hotKeys)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _hotKeys = hotKeys;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _hotKeys.Pressed -= OnHotKey;
    }

    private void OnHotKey(HotKeyAction action)
    {
        switch (action)
        {
            case HotKeyAction.Complete:
                _viewModel.TryComplete();
                break;
            case HotKeyAction.Cancel when _viewModel.BackToTasksCommand.CanExecute(null):
                _viewModel.BackToTasksCommand.Execute(null);
                break;
        }
    }

    // Обновляем при каждом показе: после правки в форме или перехода назад данные карточки могли измениться.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _hotKeys.Pressed += OnHotKey;
        await _viewModel.LoadAsync();
    }
}
