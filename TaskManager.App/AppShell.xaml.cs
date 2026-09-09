namespace TaskManager.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Страницы, которые открываются поверх оболочки по маршруту (не из боковой панели).
		Routing.RegisterRoute("taskedit", typeof(TaskManager.App.Pages.TaskEditPage));
		Routing.RegisterRoute("taskdetails", typeof(TaskManager.App.Pages.TaskDetailsPage));
	}
}
