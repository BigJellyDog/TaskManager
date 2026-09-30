using TaskManager.App.ViewModels;

namespace TaskManager.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Pages that open on top of the shell by route (not from the side panel).
		Routing.RegisterRoute("taskedit", typeof(TaskManager.App.Pages.TaskEditPage));
		Routing.RegisterRoute("taskdetails", typeof(TaskManager.App.Pages.TaskDetailsPage));

		// The side panel highlights the section and refreshes the counters on every navigation.
		var sidebar = IPlatformApplication.Current!.Services.GetRequiredService<SidebarViewModel>();
		Navigated += async (_, e) =>
		{
			sidebar.SelectRoute(e.Current?.Location.OriginalString);
			await sidebar.RefreshCountsAsync();
		};
	}
}
