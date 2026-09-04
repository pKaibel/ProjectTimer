using ProjectTimer.Views;

namespace ProjectTimer.Services;

public sealed class UserDialogService : IUserDialogService
{
    public Task ShowErrorAsync(string message)
        => Shell.Current.DisplayAlertAsync("Fehler", message, "OK");

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Löschen", string cancel = "Abbrechen")
        => Shell.Current.DisplayAlertAsync(title, message, accept, cancel);

    public async Task<ProjectDeletionChoice> ChooseProjectDeletionAsync(string projectName, int entryCount)
    {
        var dialog = new ProjectDeletionDialogPage(projectName, entryCount);
        await Shell.Current.Navigation.PushModalAsync(dialog);
        return await dialog.WaitForChoiceAsync();
    }
}
