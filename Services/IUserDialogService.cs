namespace ProjectTimer.Services;

public interface IUserDialogService
{
    Task ShowErrorAsync(string message);
    Task<bool> ConfirmAsync(string title, string message, string accept = "Löschen", string cancel = "Abbrechen");
    Task<ProjectDeletionChoice> ChooseProjectDeletionAsync(string projectName, int entryCount);
}

public enum ProjectDeletionChoice
{
    Cancel,
    ExportBackup,
    DeleteWithoutBackup
}
