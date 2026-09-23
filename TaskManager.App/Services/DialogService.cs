namespace TaskManager.App.Services;

public sealed class DialogService : IDialogService
{
    // Страница текущего окна: в окне лежит либо экран загрузки, либо оболочка AppShell.
    private static Page? CurrentPage => Application.Current?.Windows is { Count: > 0 } windows ? windows[0].Page : null;

    public Task AlertAsync(string title, string message)
    {
#if WINDOWS
        // Своё окно в теме приложения: штатный DisplayAlert рисует Windows по системной теме.
        return Platforms.Windows.SignalDialog.ShowAsync(Loc.T(title), Loc.Message(message), "OK", null);
#else
        return CurrentPage?.DisplayAlertAsync(Loc.T(title), Loc.Message(message), "OK") ?? Task.CompletedTask;
#endif
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Да", string cancel = "Нет")
    {
#if WINDOWS
        return Platforms.Windows.SignalDialog.ShowAsync(Loc.T(title), Loc.Message(message), Loc.T(accept), Loc.T(cancel));
#else
        return CurrentPage?.DisplayAlertAsync(Loc.T(title), Loc.Message(message), Loc.T(accept), Loc.T(cancel)) ?? Task.FromResult(false);
#endif
    }
}
