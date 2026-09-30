using TaskManager.App.Services;
using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class TasksPage : ContentPage
{
    private readonly TasksViewModel _viewModel;

    private readonly HotKeyService _hotKeys;

    public TasksPage(TasksViewModel viewModel, HotKeyService hotKeys)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _hotKeys = hotKeys;

        // A new page or new filters: we show the list from the start, not from the previous scroll position.
        _viewModel.TasksReplaced += (_, _) =>
        {
            if (_viewModel.Tasks.Count > 0)
            {
                TasksList.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
            }
        };

        // The Board/List switch is recoloured together with the selection and the theme.
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TasksViewModel.IsBoard))
            {
                UpdateTabs();
            }
        };
        UpdateTabs();
    }

    private void UpdateTabs()
    {
        ApplyTab(BoardTab, BoardIcon, BoardLabel, _viewModel.IsBoard);
        ApplyTab(ListTab, ListIcon, ListLabel, !_viewModel.IsBoard);
    }

    private static void ApplyTab(Border tab, Controls.Icon icon, Label label, bool selected)
    {
        if (selected)
        {
            tab.SetDynamicResource(BackgroundColorProperty, "SurfaceRaised");
            icon.SetDynamicResource(Controls.Icon.ColorProperty, "Accent");
            label.SetDynamicResource(Label.TextColorProperty, "Accent");
        }
        else
        {
            tab.BackgroundColor = Colors.Transparent;
            icon.SetDynamicResource(Controls.Icon.ColorProperty, "InkMuted");
            label.SetDynamicResource(Label.TextColorProperty, "InkMuted");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _hotKeys.Pressed -= OnHotKey;
    }

    private void OnHotKey(HotKeyAction action)
    {
        if (action == HotKeyAction.Search)
        {
            SearchEntry.Focus();
        }
    }

    // async void is acceptable for a life-cycle event handler. Loading errors are handled by the model itself.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _hotKeys.Pressed += OnHotKey;
        await _viewModel.LoadAsync();
    }
}
