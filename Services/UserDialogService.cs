namespace ProjectTimer.Services;

public sealed class UserDialogService : IUserDialogService
{
    public Task ShowErrorAsync(string message)
        => Shell.Current.DisplayAlertAsync("Fehler", message, "OK");

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Löschen", string cancel = "Abbrechen")
        => Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
}
