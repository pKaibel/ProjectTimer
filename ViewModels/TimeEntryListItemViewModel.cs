using ProjectTimer.Formatting;
using ProjectTimer.Models;

namespace ProjectTimer.ViewModels;

public sealed class TimeEntryListItemViewModel
{
    public TimeEntryListItemViewModel(TimeEntry entry)
    {
        Entry = entry;
    }

    public TimeEntry Entry { get; }
    public string DateText => Entry.Date.ToString("dd.MM.yyyy");
    public string TimeRangeText => $"{Entry.StartAtUtc.ToLocalTime():HH:mm} – {Entry.EndAtUtc.ToLocalTime():HH:mm}";
    public string DurationText => DurationFormatter.Format(Entry.Duration);
    public string? Note => Entry.Note;
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}
