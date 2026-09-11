using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class TasksPage : ContentPage
{
    private readonly TasksViewModel _viewModel;

    public TasksPage(TasksViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // Новая страница или новые фильтры: показываем список с начала, а не с прежней прокрутки.
        _viewModel.TasksReplaced += (_, _) =>
        {
            if (_viewModel.Tasks.Count > 0)
            {
                TasksList.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
            }
        };

        // Переключатель Доска/Список перекрашивается вместе с выбором и с темой.
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

    // async void допустим для обработчика события жизненного цикла. Ошибки загрузки обрабатывает сама модель.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
