using TaskManager.App.Services;
using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class TaskEditPage : ContentPage
{
    private readonly TaskEditViewModel _viewModel;
    private readonly HotKeyService _hotKeys;
    private bool _loaded;

    public TaskEditPage(TaskEditViewModel viewModel, HotKeyService hotKeys)
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
            case HotKeyAction.Save when _viewModel.SaveCommand.CanExecute(null):
                _viewModel.SaveCommand.Execute(null);
                break;
            case HotKeyAction.Cancel when _viewModel.CancelCommand.CanExecute(null):
                _viewModel.CancelCommand.Execute(null);
                break;
        }
    }

    // Загружаем один раз: повторное появление страницы не должно затирать то, что пользователь уже ввёл.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _hotKeys.Pressed += OnHotKey;

        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await _viewModel.LoadAsync();
    }
}
