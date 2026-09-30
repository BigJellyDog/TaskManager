using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Services;

namespace TaskManager.Infrastructure.Reminders;

/// <summary>
/// The background service: once per interval it creates the missed copies of recurring tasks and checks deadlines.
/// The first check runs right at start-up. Shown reminders are remembered only until the application closes:
/// at every start everything that is still relevant is shown again. It stops through <see cref="CancellationToken"/>:
/// the current check is interrupted, nothing is lost (shown reminders are written to the database at the end of a check).
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

            // The interval is read before every pause: a change of the setting takes effect from the next check.
            do
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, _clock, stoppingToken);
            }
            while (true);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A normal stop (Ctrl+C or closing the application): this is not an error.
        }

        _logger.LogInformation("Сервис напоминаний остановлен");
    }

    /// <summary>A new start: the reminders shown last time are forgotten, and the relevant ones will appear again.</summary>
    private async Task ForgetShownAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IReminderRepository>().ClearSentAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Failed to forget the previous reminders: the service works anyway, only some of them will not repeat.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Не удалось сбросить показанные напоминания при запуске");
        }
    }

    /// <summary>One iteration: catching up recurring tasks and checking deadlines. An error in an iteration does not stop the service.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A new scope for every iteration: its own DbContext, without accumulating tracked objects.
            await using var scope = _scopes.CreateAsyncScope();

            var created = await scope.ServiceProvider.GetRequiredService<TaskService>()
                .GenerateDueOccurrencesAsync(cancellationToken);
            if (created > 0)
            {
                _logger.LogInformation("Создано повторяющихся задач по расписанию: {Count}", created);
            }

            if (!_options.Enabled)
            {
                return;
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
#pragma warning disable CA1031 // The background service must not crash because of one failed check, the error is written to the log.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Ошибка при проверке напоминаний, следующая попытка через {Interval}", Interval);
        }
    }
}
