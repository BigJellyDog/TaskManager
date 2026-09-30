using CommunityToolkit.Mvvm.ComponentModel;
using TaskManager.App.Services;

namespace TaskManager.App.ViewModels;

/// <summary>
/// The base of view models. Gives a busy indicator and one way to run an action with error
/// handling: expected errors (checks, version conflict) are shown to the user, unexpected ones are written to the log.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private readonly ILogger _logger;

    protected ViewModelBase(IDialogService dialogs, ILogger logger)
    {
        Dialogs = dialogs;
        _logger = logger;
    }

    protected IDialogService Dialogs { get; }

    /// <summary>An operation is running: the interface may show an indicator and block the buttons.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Runs an action. A repeated call while it is working is ignored (protection against a double tap).
    /// Returns true if the action finished without an error.
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
#pragma warning disable CA1031 // At the interface boundary we catch everything: the user will see a message, not a crash of the program.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (!ErrorMessages.IsExpected(ex))
            {
                _logger.LogError(ex, "Необработанная ошибка в модели представления {ViewModel}", GetType().Name);
            }

            await Dialogs.AlertAsync(errorTitle, ErrorMessages.ToUserMessage(ex));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
