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
#if WINDOWS
	private static Microsoft.UI.Xaml.FrameworkElement? FindByName(Microsoft.UI.Xaml.DependencyObject parent, string name)
	{
		for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
			if (child is Microsoft.UI.Xaml.FrameworkElement element && element.Name == name)
			{
				return element;
			}

			if (FindByName(child, name) is { } found)
			{
				return found;
			}
		}

		return null;
	}
#endif

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		// Шрифты шаблона (OpenSans) не регистрируем: в неупакованном Windows-приложении они не загружаются
		// (ms-appx не находит файл), а на Windows и так подставляется системный шрифт.
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit();

		// Списки и календари раскрываются системным окном: оно должно быть в теме приложения, а не Windows.
		Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("Theme", (handler, view) =>
		{
#if WINDOWS
			Services.ThemeManager.Register(handler.PlatformView);
#endif
		});
		Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("Theme", (handler, view) =>
		{
#if WINDOWS
			Services.ThemeManager.Register(handler.PlatformView);
#endif
		});
		Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("Theme", (handler, view) =>
		{
#if WINDOWS
			Services.ThemeManager.Register(handler.PlatformView);
#endif
		});

		// Поля ввода без штатной рамки WinUI: рамку и подсветку фокуса рисует наша оболочка (Signal).
		Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("Signal", (handler, view) =>
		{
#if WINDOWS
			handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
			var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
			handler.PlatformView.Background = transparent;
			handler.PlatformView.Resources["CalendarDatePickerBackground"] = transparent;
			handler.PlatformView.Resources["CalendarDatePickerBackgroundPointerOver"] = transparent;
			handler.PlatformView.Resources["CalendarDatePickerBackgroundPressed"] = transparent;
			handler.PlatformView.Resources["CalendarDatePickerBorderBrush"] = transparent;
			handler.PlatformView.Resources["CalendarDatePickerBorderBrushPointerOver"] = transparent;
			handler.PlatformView.Resources["CalendarDatePickerBorderBrushPressed"] = transparent;
			// Значок календаря рисуем сами слева от поля: штатный справа прячем.
			handler.PlatformView.Loaded += (sender, _) =>
			{
				if (sender is Microsoft.UI.Xaml.DependencyObject root && FindByName(root, "CalendarGlyph") is Microsoft.UI.Xaml.UIElement glyph)
				{
					glyph.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
				}
			};
#endif
		});
		Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("Signal", (handler, view) =>
		{
#if WINDOWS
			var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
			handler.PlatformView.Background = transparent;
			handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
		});
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
		// Показ напоминаний подставляется до AddInfrastructure: та регистрирует запись в журнал только если другого нет.
		builder.Services.AddSingleton<ReminderBannerViewModel>();
		builder.Services.AddSingleton<SidebarViewModel>();
		builder.Services.AddSingleton<IReminderNotifier, ReminderNotifier>();
		builder.Services.AddInfrastructure(Path.Combine(AppPaths.DataDirectory, databaseFileName));

		// Сервисы интерфейса.
		builder.Services.AddSingleton<UserSettings>();
		builder.Services.AddSingleton<HotKeyService>();
		builder.Services.AddSingleton<ServiceRunner>();
		builder.Services.AddSingleton<IDialogService, DialogService>();
		builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
		builder.Services.AddSingleton<IFileService, FileService>();
		builder.Services.AddSingleton<IAppRestarter, AppRestarter>();
		builder.Services.AddSingleton<AppInitializer>();

		// Страницы создаются через DI: так они получают модели представления в конструкторе.
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
