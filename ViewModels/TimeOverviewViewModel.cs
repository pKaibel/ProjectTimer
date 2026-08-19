using System.Collections.ObjectModel;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class TimeOverviewViewModel : BaseViewModel
{
    private readonly TimeOverviewService _overview;
    private string _title = "Zeitenübersicht";
    private string _subtitle = string.Empty;
    private string _totalDurationText = "0 Stunden 0 Minuten";

    public TimeOverviewViewModel(TimeOverviewService overview)
    {
        _overview = overview;
        ShowDayCommand = new AsyncCommand(() => LoadAsync(OverviewPeriod.Day));
        ShowWeekCommand = new AsyncCommand(() => LoadAsync(OverviewPeriod.Week));
        ShowMonthCommand = new AsyncCommand(() => LoadAsync(OverviewPeriod.Month));
        ShowYearCommand = new AsyncCommand(() => LoadAsync(OverviewPeriod.Year));
    }

    public ObservableCollection<TimeChartBar> Bars { get; } = [];
    public AsyncCommand ShowDayCommand { get; }
    public AsyncCommand ShowWeekCommand { get; }
    public AsyncCommand ShowMonthCommand { get; }
    public AsyncCommand ShowYearCommand { get; }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    public string TotalDurationText
    {
        get => _totalDurationText;
        private set => SetProperty(ref _totalDurationText, value);
    }

    public Task OnAppearingAsync() => Bars.Count == 0 ? LoadAsync(OverviewPeriod.Week) : Task.CompletedTask;

    private async Task LoadAsync(OverviewPeriod period)
    {
        await RunBusyAsync(async () =>
        {
            var overview = await _overview.GetOverviewAsync(period);
            Title = overview.Title;
            Subtitle = overview.Subtitle;
            TotalDurationText = overview.TotalDurationText;
            Bars.Clear();
            foreach (var bar in overview.Bars)
            {
                Bars.Add(bar);
            }
        });
    }
}
