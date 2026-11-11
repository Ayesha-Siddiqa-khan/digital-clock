using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeBridge.App.Services;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public const int PageOverview = 0, PageMeetings = 1, PageClients = 2, PageSettings = 3;

    readonly MeetingService _meetingService;
    readonly SettingsService _settings;
    readonly AlarmScheduler _scheduler;
    readonly ClockTicker _ticker;
    readonly WindowManager _windows;
    int _lastMinute = -1;

    public MainViewModel(WidgetViewModel clock, MeetingsViewModel meetings, ClientsViewModel clients,
        SettingsViewModel settings, MeetingService meetingService, SettingsService settingsService,
        AlarmScheduler scheduler, ClockTicker ticker, WindowManager windows)
    {
        Clock = clock; Meetings = meetings; Clients = clients; Settings = settings;
        _meetingService = meetingService; _settings = settingsService; _scheduler = scheduler;
        _ticker = ticker; _windows = windows;
    }

    public WidgetViewModel Clock { get; }
    public MeetingsViewModel Meetings { get; }
    public ClientsViewModel Clients { get; }
    public SettingsViewModel Settings { get; }
    public ObservableCollection<MeetingItem> Upcoming { get; } = new();

    [ObservableProperty] int _navIndex;
    [ObservableProperty] bool _widgetShown;
    [ObservableProperty] int _upcomingCount;

    public async Task InitializeAsync()
    {
        Settings.Initialize();
        await Meetings.InitializeAsync();
        await Clients.InitializeAsync();
        await RefreshUpcomingAsync();
        _meetingService.Changed += async (_, _) => await Ui.Run(RefreshUpcomingAsync);
        _scheduler.RemindersChanged += async (_, _) => await Ui.Run(RefreshUpcomingAsync);
        _windows.WidgetVisibilityChanged += shown => WidgetShown = shown;
        WidgetShown = _windows.IsWidgetShown;
        _ticker.Tick += utc =>
        {
            if (utc.Minute == _lastMinute) return;
            _lastMinute = utc.Minute;
            _ = RefreshUpcomingAsync();
        };
    }

    async Task RefreshUpcomingAsync()
    {
        var home = _settings.Current.HomeTimeZoneId;
        var now = _ticker.UtcNow;
        var all = await _meetingService.GetUpcomingAsync();
        UpcomingCount = all.Count;
        Upcoming.Clear();
        foreach (var m in all.Take(6)) Upcoming.Add(MeetingItem.From(m, home, now));
    }

    [RelayCommand] void OpenStopwatch() => _windows.ShowStopwatch();
    [RelayCommand] void ExitApp() => _windows.Exit();
    [RelayCommand] void Navigate(string? index) { if (int.TryParse(index, out var i)) NavIndex = i; }

    [RelayCommand]
    void EditMeeting(MeetingItem? item)
    {
        if (item == null) return;
        Meetings.BeginEdit(item);
        NavIndex = PageMeetings;
    }

    [RelayCommand] void ToggleWidget()
    {
        if (_windows.IsWidgetShown) _windows.HideWidget(keepVisibleOnNextLaunch: false);
        else _ = _windows.ShowWidgetAsync();
    }
}


