using System.Globalization;
using Microsoft.Extensions.Configuration;
using Serilog;
using TaskManager.App.Configuration;
using TaskManager.App.Pages;
using TaskManager.App.Services;
using TaskManager.App.ViewModels;
using TaskManager.Infrastructure;
using TaskManager.Infrastructure.Reminders;

namespace TaskManager.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		// Шрифты шаблона (OpenSans) не регистрируем: в неупакованном Windows-приложении они не загружаются
		// (ms-appx не находит файл), а на Windows и так подставляется системный шрифт.
		builder.UseMauiApp<App>();

		// Настройки: appsettings.json вшит в сборку, значения читаются через IOptions<T>.
		using (var settings = typeof(MauiProgram).Assembly.GetManifestResourceStream("appsettings.json")
			?? throw new InvalidOperationException("В сборку не вшит appsettings.json."))
		{
			builder.Configuration.AddJsonStream(settings);
		}

		builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
		builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection(ReminderOptions.SectionName));

		// Журнал: файл по дням (14 дней) рядом с базой и окно отладки. ILogger везде остаётся стандартным.
		Directory.CreateDirectory(AppPaths.LogsDirectory);
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
			.WriteTo.File(
				Path.Combine(AppPaths.LogsDirectory, "taskmanager-.log"),
				rollingInterval: RollingInterval.Day,
				retainedFileCountLimit: 14,
				formatProvider: CultureInfo.InvariantCulture)
			.WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
			.CreateLogger();
		builder.Logging.AddSerilog(Log.Logger, dispose: true);

		// Данные: база в папке данных приложения.
		Directory.CreateDirectory(AppPaths.DataDirectory);
		var databaseFileName = builder.Configuration[$"{AppOptions.SectionName}:{nameof(AppOptions.DatabaseFileName)}"]
			?? new AppOptions().DatabaseFileName;
		builder.Services.AddInfrastructure(Path.Combine(AppPaths.DataDirectory, databaseFileName));

		// Сервисы интерфейса.
		builder.Services.AddSingleton<ServiceRunner>();
		builder.Services.AddSingleton<IDialogService, DialogService>();
		builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
		builder.Services.AddSingleton<AppInitializer>();

		// Страницы создаются через DI: так они получают модели представления в конструкторе.
		builder.Services.AddTransient<TasksViewModel>();
		builder.Services.AddTransient<TasksPage>();
		builder.Services.AddTransient<TaskEditViewModel>();
		builder.Services.AddTransient<TaskEditPage>();
		builder.Services.AddTransient<StatisticsPage>();
		builder.Services.AddTransient<DataPage>();

		return builder.Build();
	}
}
