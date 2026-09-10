using TaskManager.App.ViewModels;

namespace TaskManager.App.Pages;

public partial class DataPage : ContentPage
{
    public DataPage(DataViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
