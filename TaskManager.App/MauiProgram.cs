using System.Globalization;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Serilog;
using TaskManager.App.Configuration;
using TaskManager.App.Pages;
using TaskManager.App.Services;
using TaskManager.App.ViewModels;
using TaskManager.Core.Abstractions;
using TaskManager.Infrastructure;
using TaskManager.Infrastructure.Reminders;

namespace TaskManager.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		// We do not bundle our own fonts: Windows uses the system one (Segoe UI Variable).
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit();

		Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("Signal", (handler, view) =>
		{
#if WINDOWS
			handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
			var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
			handler.PlatformView.Resources["TextControlBackground"] = transparent;
			handler.PlatformView.Resources["TextControlBackgroundPointerOver"] = transparent;
			handler.PlatformView.Resources["TextControlBackgroundFocused"] = transparent;
			handler.PlatformView.Resources["TextControlBorderBrush"] = transparent;
			handler.PlatformView.Resources["TextControlBorderBrushPointerOver"] = transparent;
			handler.PlatformView.Resources["TextControlBorderBrushFocused"] = transparent;
#endif
		});
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Signal", (handler, view) =>
		{
#if WINDOWS
			handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
			var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
			handler.PlatformView.Resources["TextControlBackground"] = transparent;
			handler.PlatformView.Resources["TextControlBackgroundPointerOver"] = transparent;
			handler.PlatformView.Resources["TextControlBackgroundFocused"] = transparent;
			handler.PlatformView.Resources["TextControlBorderBrush"] = transparent;
			handler.PlatformView.Resources["TextControlBorderBrushPointerOver"] = transparent;
			handler.PlatformView.Resources["TextControlBorderBrushFocused"] = transparent;
#endif
		});

		// Settings: appsettings.json is embedded into the assembly, values are read through IOptions<T>.
		using (var settings = typeof(MauiProgram).Assembly.GetManifestResourceStream("appsettings.json")
			?? throw new InvalidOperationException("В сборку не вшит appsettings.json."))
		{
			builder.Configuration.AddJsonStream(settings);
		}

		builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
		builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection(ReminderOptions.SectionName));

		// Logging: a file per day (14 days) next to the database and the debug window. ILogger stays standard everywhere.
		Directory.CreateDirectory(AppPaths.LogsDirectory);
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
			// MAUI writes "Error loading font" for the name of a system font (it comes from Windows, not from the package): the font is still substituted correctly.
			.MinimumLevel.Override("Microsoft.Maui.FontManager", Serilog.Events.LogEventLevel.Fatal)
			.WriteTo.File(
				Path.Combine(AppPaths.LogsDirectory, "taskmanager-.log"),
				rollingInterval: RollingInterval.Day,
				retainedFileCountLimit: 14,
				formatProvider: CultureInfo.InvariantCulture)
			.WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
			.CreateLogger();
		builder.Logging.AddSerilog(Log.Logger, dispose: true);

		// Data: the database in the application data folder.
		Directory.CreateDirectory(AppPaths.DataDirectory);
		var databaseFileName = builder.Configuration[$"{AppOptions.SectionName}:{nameof(AppOptions.DatabaseFileName)}"]
			?? new AppOptions().DatabaseFileName;
		// The reminder display is registered before AddInfrastructure: that one registers a log writer only if there is no other.
		builder.Services.AddSingleton<ReminderBannerViewModel>();
		builder.Services.AddSingleton<SidebarViewModel>();
		builder.Services.AddSingleton<IReminderNotifier, ReminderNotifier>();
		builder.Services.AddInfrastructure(Path.Combine(AppPaths.DatabaseDirectory, databaseFileName));

		// Interface services.
		builder.Services.AddSingleton<UserSettings>();
		builder.Services.AddSingleton<HotKeyService>();
		builder.Services.AddSingleton<ServiceRunner>();
		builder.Services.AddSingleton<IDialogService, DialogService>();
		builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
		builder.Services.AddSingleton<IFileService, FileService>();
		builder.Services.AddSingleton<IAppRestarter, AppRestarter>();
		builder.Services.AddSingleton<AppInitializer>();

		// Pages are created through DI: this is how they get their view models in the constructor.
		builder.Services.AddTransient<TasksViewModel>();
		builder.Services.AddTransient<TasksPage>();
		builder.Services.AddTransient<TaskEditViewModel>();
		builder.Services.AddTransient<TaskEditPage>();
		builder.Services.AddTransient<TaskDetailsViewModel>();
		builder.Services.AddTransient<TaskDetailsPage>();
		builder.Services.AddTransient<StatisticsViewModel>();
		builder.Services.AddTransient<StatisticsPage>();
		builder.Services.AddTransient<TrashViewModel>();
		builder.Services.AddTransient<TrashPage>();
		builder.Services.AddTransient<TagsViewModel>();
		builder.Services.AddTransient<TagsPage>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<DataViewModel>();
		builder.Services.AddTransient<DataPage>();

		return builder.Build();
	}
}
