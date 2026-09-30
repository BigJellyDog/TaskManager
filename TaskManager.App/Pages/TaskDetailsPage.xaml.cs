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

        // More actions: a grid of two columns, rebuilt when the status changes.
        _viewModel.SecondaryActions.CollectionChanged += (_, _) => BuildSecondaryActions();
        BuildSecondaryActions();
    }

    private void BuildSecondaryActions()
    {
        SecondaryGrid.Children.Clear();
        SecondaryGrid.RowDefinitions.Clear();

        var actions = _viewModel.SecondaryActions;
        for (var i = 0; i < actions.Count; i++)
        {
            if (i % 2 == 0)
            {
                SecondaryGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            var action = actions[i];
            var button = new Controls.SignalButton
            {
                Variant = action.Variant,
                Icon = action.Icon,
                Text = action.Title,
                Command = action.Command,
                IsEnabled = action.IsEnabled,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 34,
            };
            SecondaryGrid.Add(button, i % 2, i / 2);
        }
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

    // We refresh on every show: after an edit in the form or a step back the card data may have changed.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _hotKeys.Pressed += OnHotKey;
        await _viewModel.LoadAsync();
    }
}
