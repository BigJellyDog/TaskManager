using CommunityToolkit.Mvvm.ComponentModel;
using TaskManager.App.Services;

namespace TaskManager.App.ViewModels;

/// <summary>
/// Основа для моделей представления. Даёт индикатор занятости и единый способ выполнить действие с обработкой
/// ошибок: ожидаемые (проверки, конфликт версий) показываются пользователем, неожиданные пишутся в журнал.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    protected ViewModelBase(IDialogService dialogs, ILogger logger)
    {
        _dialogs = dialogs;
        _logger = logger;
    }

    /// <summary>Идёт операция: интерфейс может показать индикатор и заблокировать кнопки.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Выполняет действие. Повторный вызов во время работы игнорируется (защита от двойного нажатия).
    /// Возвращает true, если действие завершилось без ошибки.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> action, string errorTitle = "Не удалось выполнить действие")
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await action();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
#pragma warning disable CA1031 // На границе интерфейса перехватываем всё: пользователь увидит сообщение, а не падение программы.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (!ErrorMessages.IsExpected(ex))
            {
                _logger.LogError(ex, "Необработанная ошибка в модели представления {ViewModel}", GetType().Name);
            }

            await _dialogs.AlertAsync(errorTitle, ErrorMessages.ToUserMessage(ex));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
