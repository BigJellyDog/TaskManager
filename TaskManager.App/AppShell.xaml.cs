using TaskManager.App.ViewModels;

namespace TaskManager.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Страницы, которые открываются поверх оболочки по маршруту (не из боковой панели).
		Routing.RegisterRoute("taskedit", typeof(TaskManager.App.Pages.TaskEditPage));
		Routing.RegisterRoute("taskdetails", typeof(TaskManager.App.Pages.TaskDetailsPage));

		// Боковая панель подсвечивает раздел и обновляет счётчики при каждом переходе.
		var sidebar = IPlatformApplication.Current!.Services.GetRequiredService<SidebarViewModel>();
		Navigated += async (_, e) =>
		{
			sidebar.SelectRoute(e.Current?.Location.OriginalString);
			await sidebar.RefreshCountsAsync();
		};
	}
}
