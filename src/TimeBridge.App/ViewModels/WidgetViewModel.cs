using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeBridge.App.Services;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

/// <summary>Drives the floating clock widget. Also reused by the dashboard overview for the live clocks.</summary>
public partial class WidgetViewModel : ObservableObject
{
    readonly ClockTicker _ticker;
    readonly SettingsService _settings;
    readonly ClientService _clients;
    readonly MeetingService _meetings;
    readonly WidgetStateService _stateService;
    readonly AlarmScheduler _scheduler;
    readonly WindowManager _windows;
    readonly DispatcherTimer _saveDebounce = new() { Interval = TimeSpan.FromMilliseconds(600) };
    Meeting? _next;

    public WidgetViewModel(ClockTicker ticker, SettingsService settings, ClientService clients, MeetingService meetings,
        WidgetStateService stateService, AlarmScheduler scheduler, WindowManager windows)
    {
        _ticker = ticker; _settings = settings; _clients = clients; _meetings = meetings;
        _stateService = stateService; _scheduler = scheduler; _windows = windows;
        _saveDebounce.Tick += async (_, _) => { _saveDebounce.Stop(); await SaveNowAsync(); };
    }

    public WidgetState State { get; private set; } = new();
    public ObservableCollection<ClientClockItem> Clients { get; } = new();

    [ObservableProperty] bool _isPinned = true;
    [ObservableProperty] bool _isCollapsed;
    [ObservableProperty] bool _isClientListOpen;
    [ObservableProperty] double _widgetOpacity = 0.96;
    [ObservableProperty] ClientClockItem? _selected;

    [ObservableProperty] string _homeLabel = "";
    [ObservableProperty] string _homeShort = "";
    [ObservableProperty] string _homeTime = "";
    [ObservableProperty] string _homeAmPm = "";
    [ObservableProperty] string _homeDate = "";
    [ObservableProperty] string _homeOffset = "";

    [ObservableProperty] string _clientName = "No client";
    [ObservableProperty] string _clientLabel = "Choose a client";
    [ObservableProperty] string _clientShort = "";
    [ObservableProperty] string _clientTime = "--:--";
    [ObservableProperty] string _clientAmPm = "";
    [ObservableProperty] string _clientDate = "";
    [ObservableProperty] string _clientOffset = "";

    [ObservableProperty] bool _hasNextMeeting;
    [ObservableProperty] string _nextTitle = "No upcoming meetings";
    [ObservableProperty] string _nextDetail = "Schedule a client call in the dashboard.";
    [ObservableProperty] string _nextCountdown = "—";

    public string Subtitle => Selected == null ? "No client selected" : $"Showing {Selected.Name}";
    partial void OnSelectedChanged(ClientClockItem? value)
    {
        OnPropertyChanged(nameof(Subtitle));
        if (value == null) return; // transient while the list is rebuilt
        State.SelectedClientId = value.Id;
        foreach (var c in Clients) c.IsSelected = c == value;
        Refresh(_ticker.UtcNow);
        SaveSoon();
    }

    public async Task InitializeAsync()
    {
        State = await _stateService.LoadAsync();
        IsPinned = State.IsPinned;
        IsCollapsed = State.IsCollapsed;
        await ReloadClientsAsync();
        await RefreshNextMeetingAsync();
        ApplySettings();

        _clients.Changed += async (_, _) => await Ui.Run(ReloadClientsAsync);
        _meetings.Changed += async (_, _) => await Ui.Run(RefreshNextMeetingAsync);
        _scheduler.RemindersChanged += async (_, _) => await Ui.Run(RefreshNextMeetingAsync);
        _settings.Changed += async (_, _) => await Ui.Run(() => { ApplySettings(); Refresh(_ticker.UtcNow); });
        _ticker.Tick += Refresh;
        Refresh(_ticker.UtcNow);
    }

    void ApplySettings()
    {
        WidgetOpacity = _settings.Current.WidgetOpacity;
        HomeLabel = TimeZoneCatalog.LabelFor(_settings.Current.HomeTimeZoneId);
        HomeShort = TimeZoneCatalog.ShortName(_settings.Current.HomeTimeZoneId);
    }

    async Task ReloadClientsAsync()
    {
        var list = await _clients.GetAllAsync();
        Clients.Clear();
        foreach (var c in list) Clients.Add(new ClientClockItem(c));

        Selected = Clients.FirstOrDefault(c => c.Id == State.SelectedClientId) ?? Clients.FirstOrDefault();
        State.SelectedClientId = Selected?.Id;
        foreach (var c in Clients) c.IsSelected = c == Selected;
        Refresh(_ticker.UtcNow);
    }

    public async Task RefreshNextMeetingAsync()
    {
        _next = (await _meetings.GetUpcomingAsync(1)).FirstOrDefault();
        Refresh(_ticker.UtcNow);
    }

    void Refresh(DateTime utc)
    {
        var s = _settings.Current;
        var home = TimeConversion.FromUtc(utc, s.HomeTimeZoneId);
        (HomeTime, HomeAmPm) = TimeFormats.FormatClock(home.Local, s.ShowSeconds);
        HomeDate = TimeFormats.FormatDate(home.Local);
        HomeOffset = home.OffsetText;

        foreach (var c in Clients) c.Update(utc);

        if (Selected is { } sel)
        {
            var m = TimeConversion.FromUtc(utc, sel.Client.TimeZoneId);
            ClientName = sel.Name;
            ClientLabel = $"{sel.Name} · {sel.City}";
            ClientShort = sel.City;
            (ClientTime, ClientAmPm) = TimeFormats.FormatClock(m.Local, s.ShowSeconds);
            ClientDate = TimeFormats.FormatDate(m.Local);
            ClientOffset = m.OffsetText;
        }
        else
        {
            ClientName = "No client"; ClientLabel = "Choose a client"; ClientShort = "";
            ClientTime = "--:--"; ClientAmPm = ""; ClientDate = ""; ClientOffset = "";
        }

        if (_next is { } n && n.StartUtc > utc.AddMinutes(-1))
        {
            HasNextMeeting = true;
            var h = TimeConversion.FromUtc(n.StartUtc, s.HomeTimeZoneId);
            var c = TimeConversion.FromUtc(n.StartUtc, n.Client.TimeZoneId);
            NextTitle = n.Title;
            NextDetail = $"{TimeFormats.FormatDate(h.Local)} {TimeFormats.FormatTime(h.Local)} {HomeShort}  ·  {TimeFormats.FormatTime(c.Local)} {n.Client.City}";
            NextCountdown = TimeFormats.FormatCountdown(n.StartUtc - utc);
        }
        else
        {
            HasNextMeeting = false;
            NextTitle = "No upcoming meetings";
            NextDetail = "Schedule a client call in the dashboard.";
            NextCountdown = "—";
            if (_next != null) _ = RefreshNextMeetingAsync(); // meeting started; look for the next one
        }
    }

    partial void OnIsPinnedChanged(bool value) { State.IsPinned = value; SaveSoon(); }
    partial void OnIsCollapsedChanged(bool value) { State.IsCollapsed = value; SaveSoon(); }

    [RelayCommand] void TogglePin() => IsPinned = !IsPinned;
    [RelayCommand] void ToggleCollapse() => IsCollapsed = !IsCollapsed;
    [RelayCommand] void Expand() => IsCollapsed = false;
    [RelayCommand] void ToggleClientList() => IsClientListOpen = !IsClientListOpen;
    [RelayCommand] void Edit() { IsClientListOpen = false; _windows.OpenWidgetEditor(); }
    [RelayCommand] void OpenDashboard() => _windows.ShowDashboard();
    [RelayCommand] void Minimise() => _windows.HideWidget(keepVisibleOnNextLaunch: true);
    [RelayCommand] void Close() => _windows.HideWidget(keepVisibleOnNextLaunch: false);

    [RelayCommand]
    void SelectClient(ClientClockItem? item)
    {
        if (item == null) return;
        SelectClientById(item.Id);
        IsClientListOpen = false;
    }

    public void SelectClientById(int id)
    {
        var item = Clients.FirstOrDefault(c => c.Id == id);
        if (item != null) Selected = item;
    }

    public void SaveSoon() { _saveDebounce.Stop(); _saveDebounce.Start(); }

    public async Task SaveNowAsync()
    {
        try { await _stateService.SaveAsync(State); }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }
}
