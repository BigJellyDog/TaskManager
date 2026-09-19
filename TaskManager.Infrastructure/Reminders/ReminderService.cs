using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Services;

namespace TaskManager.Infrastructure.Reminders;

/// <summary>
/// Фоновый сервис: раз в интервал создаёт пропущенные копии повторяющихся задач и проверяет дедлайны.
/// Первая проверка выполняется сразу при запуске. Показанные напоминания помнятся только до закрытия приложения:
/// при каждом запуске всё, что ещё актуально, показывается снова. Останавливается через <see cref="CancellationToken"/>:
/// текущая проверка прерывается, ничего не теряется (показанные напоминания записываются в БД в конце проверки).
/// </summary>
public class ReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReminderService> _logger;
    private readonly ReminderOptions _options;

    public ReminderService(
        IServiceScopeFactory scopes,
        TimeProvider clock,
        IOptions<ReminderOptions> options,
        ILogger<ReminderService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _clock = clock;
        _logger = logger;
        _options = options.Value;
    }

    private TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(1, _options.IntervalSeconds));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Сервис напоминаний запущен, интервал проверки {Interval}", Interval);

        try
        {
            await ForgetShownAsync(stoppingToken);

            // Интервал читается перед каждой паузой: смена настройки действует со следующей проверки.
            do
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, _clock, stoppingToken);
            }
            while (true);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Нормальная остановка (Ctrl+C или закрытие приложения): это не ошибка.
        }

        _logger.LogInformation("Сервис напоминаний остановлен");
    }

    /// <summary>Новый запуск: напоминания, показанные в прошлый раз, забываются, и актуальные появятся снова.</summary>
    private async Task ForgetShownAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IReminderRepository>().ClearSentAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Не удалось забыть прежние напоминания: сервис всё равно работает, просто часть из них не повторится.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Не удалось сбросить показанные напоминания при запуске");
        }
    }

    /// <summary>Одна итерация: догон повторяющихся задач и проверка дедлайнов. Ошибка итерации не останавливает сервис.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Новый scope на каждую итерацию: свой DbContext, без накопления отслеживаемых объектов.
            await using var scope = _scopes.CreateAsyncScope();

            var created = await scope.ServiceProvider.GetRequiredService<TaskService>()
                .GenerateDueOccurrencesAsync(cancellationToken);
            if (created > 0)
            {
                _logger.LogInformation("Создано повторяющихся задач по расписанию: {Count}", created);
            }

            var shown = await scope.ServiceProvider.GetRequiredService<ReminderChecker>()
                .CheckAsync(cancellationToken);
            if (shown.Count > 0)
            {
                _logger.LogInformation("Показано напоминаний: {Count}", shown.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Фоновый сервис не должен падать из-за одной неудачной проверки, ошибка пишется в лог.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Ошибка при проверке напоминаний, следующая попытка через {Interval}", Interval);
        }
    }
}
