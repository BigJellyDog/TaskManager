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
    }

    // Клик по строке открывает задачу. Выбор сразу снимаем, иначе повторный клик по той же строке не сработал бы.
    private async void OnTaskSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection is [TaskRowViewModel row, ..])
        {
            TasksList.SelectedItem = null;
            await _viewModel.OpenTaskAsync(row.Id);
        }
    }

    // async void допустим для обработчика события жизненного цикла. Ошибки загрузки обрабатывает сама модель.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
