using ProjectTimer.Services;

namespace ProjectTimer.Views;

public partial class ProjectDeletionDialogPage : ContentPage
{
    private readonly TaskCompletionSource<ProjectDeletionChoice> _choiceSource = new();

    public ProjectDeletionDialogPage(string projectName, int entryCount)
    {
        InitializeComponent();
        var entriesText = entryCount == 1 ? "1 Zeiteintrag" : $"{entryCount} Zeiteinträge";
        DeleteMessageLabel.Text = $"„{projectName}“ und {entriesText} werden gelöscht.";
    }

    public Task<ProjectDeletionChoice> WaitForChoiceAsync() => _choiceSource.Task;

    private async void OnExportBackupClicked(object sender, EventArgs e) => await CompleteAsync(ProjectDeletionChoice.ExportBackup);

    private async void OnDeleteWithoutBackupClicked(object sender, EventArgs e) => await CompleteAsync(ProjectDeletionChoice.DeleteWithoutBackup);

    private async void OnCancelClicked(object sender, EventArgs e) => await CompleteAsync(ProjectDeletionChoice.Cancel);

    private async Task CompleteAsync(ProjectDeletionChoice choice)
    {
        if (_choiceSource.TrySetResult(choice))
        {
            await Navigation.PopModalAsync();
        }
    }

    protected override void OnDisappearing()
    {
        _choiceSource.TrySetResult(ProjectDeletionChoice.Cancel);
        base.OnDisappearing();
    }
}
