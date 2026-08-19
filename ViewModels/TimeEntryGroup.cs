using System.Collections.ObjectModel;

namespace ProjectTimer.ViewModels;

public sealed class TimeEntryGroup : ObservableCollection<TimeEntryListItemViewModel>
{
    public TimeEntryGroup(string name, IEnumerable<TimeEntryListItemViewModel> entries)
        : base(entries)
    {
        Name = name;
    }

    public string Name { get; }
}
