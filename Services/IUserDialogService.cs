namespace ProjectTimer.Services;

public interface IUserDialogService
{
    Task ShowErrorAsync(string message);
    Task<bool> ConfirmAsync(string title, string message, string accept = "Löschen", string cancel = "Abbrechen");
}
