using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeBridge.App.Services;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

/// <summary>"Daily schedule" page: prayer times for a chosen city, plus the user's routine (sleep, rest, lunch...).</summary>
public partial class ScheduleViewModel : ObservableObject
{
    readonly SettingsService _settings;
    readonly ScheduleService _schedule;
    readonly ClockTicker _ticker;
    readonly IDialogService _dialogs;
    readonly ClientService _clientService;
    List<Client> _clients = new();
    readonly DispatcherTimer _saveDebounce = new() { Interval = TimeSpan.FromMilliseconds(450) };
    bool _loading;
    int _lastMinute = -1;
    List<ScheduleItem> _items = new();

    public ScheduleViewModel(SettingsService settings, ScheduleService schedule, ClockTicker ticker, IDialogService dialogs, ClientService clientService)
    {
        _settings = settings; _schedule = schedule; _ticker = ticker; _dialogs = dialogs; _clientService = clientService;
        _saveDebounce.Tick += async (_, _) => { _saveDebounce.Stop(); await SavePrayerSettingsAsync(); };
        for (var i = 0; i < 5; i++) Prayers.Add(new PrayerRow(i, SchedulePlanner.PrayerNames[i], QueueSave));
    }

    public IReadOnlyList<PrayerCity> Cities => PrayerCities.All;
    public IReadOnlyList<PrayerMethodOption> Methods => PrayerMethods.All;
    public IReadOnlyList<AsrChoice> AsrChoices { get; } = new[]
    {
        new AsrChoice(true, "Hanafi (later Asr)"),
        new AsrChoice(false, "Standard (Shafi'i, Maliki, Hanbali)")
    };
    public IReadOnlyList<ReminderChoice> AlertChoices { get; } =
        ReminderOptions.Minutes.Select(m => new ReminderChoice(m, m == 0 ? "At prayer time" : $"{m} minutes before")).ToList();
    public IReadOnlyList<KindInfo> Kinds => ScheduleKinds.Routine;

    public ObservableCollection<PrayerRow> Prayers { get; } = new();
    public ObservableCollection<TimelineItem> Timeline { get; } = new();
    public ObservableCollection<ScheduleRow> Routine { get; } = new();
    public DaysEditor Days { get; } = new();

    // prayer setup
    [ObservableProperty] string _cityId = "karachi";
    [ObservableProperty] PrayerMethod _method = PrayerMethod.Karachi;
    [ObservableProperty] bool _hanafi = true;
    [ObservableProperty] bool _alertEnabled = true;
    [ObservableProperty] int _alertMinutes;
    [ObservableProperty] string _cityNote = "";
    [ObservableProperty] string _sunriseText = "";
    [ObservableProperty] string _todayText = "";

    // sharing
    public ObservableCollection<ShareTarget> ShareTargets { get; } = new();
    public IReadOnlyList<ShareDayChoice> ShareDays { get; } = new[] { new ShareDayChoice(0, "Today"), new ShareDayChoice(1, "Tomorrow") };
    [ObservableProperty] string _shareTargetKey = "none";
    [ObservableProperty] int _shareDayOffset;
    [ObservableProperty] bool _sharePrayers = true;
    [ObservableProperty] bool _shareRoutine = true;
    [ObservableProperty] string _shareText = "";

    partial void OnShareTargetKeyChanged(string value) => UpdateShare();
    partial void OnShareDayOffsetChanged(int value) => UpdateShare();
    partial void OnSharePrayersChanged(bool value) => UpdateShare();
    partial void OnShareRoutineChanged(bool value) => UpdateShare();

    void UpdateShare()
    {
        if (_loading) return;
        var s = _settings.Current;
        var homeToday = DateOnly.FromDateTime(TimeConversion.FromUtc(_ticker.UtcNow, s.HomeTimeZoneId).Local);
        var target = ShareTargets.FirstOrDefault(t => t.Key == ShareTargetKey);
        ShareText = ScheduleShare.Build(homeToday.AddDays(ShareDayOffset), s, _items, SharePrayers, ShareRoutine, target?.ZoneId, target?.Name);
    }

    async Task LoadShareTargetsAsync()
    {
        _clients = await _clientService.GetAllAsync();
        var keep = ShareTargetKey;
        ShareTargets.Clear();
        ShareTargets.Add(new ShareTarget("none", "No one (my time only)", null, null, "Share with"));
        foreach (var c in _clients)
            ShareTargets.Add(new ShareTarget($"client:{c.Id}", $"{c.Name} · {c.City}", c.TimeZoneId, c.City, "My clients"));
        foreach (var z in TimeZoneCatalog.All().Where(z => z.Group != "All zones" && z.Id != TimeZoneCatalog.PakistanId))
            ShareTargets.Add(new ShareTarget($"zone:{z.Id}", z.Label, z.Id, TimeZoneCatalog.ShortName(z.Id), "Any country / city"));
        ShareTargetKey = ShareTargets.Any(t => t.Key == keep) ? keep : "none";
        UpdateShare();
    }

    [RelayCommand]
    void ShareWhatsApp()
    {
        var text = ShareText;
        // wa.me opens WhatsApp (desktop app or WhatsApp Web) with the text ready; you choose who to send it to.
        var url = "https://wa.me/?text=" + Uri.EscapeDataString(text);
        try
        {
            if (url.Length > 1900)
            {
                Clipboard.SetText(text);
                Process.Start(new ProcessStartInfo("https://web.whatsapp.com") { UseShellExecute = true });
                _dialogs.Info("The schedule is long, so it was copied to the clipboard. Paste it into your WhatsApp chat.");
                return;
            }
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) { _dialogs.Error($"Could not open WhatsApp: {ex.Message}"); }
    }

    [RelayCommand]
    void CopyShare()
    {
        try { Clipboard.SetText(ShareText); _dialogs.Info("Schedule copied. Paste it anywhere."); }
        catch (System.Runtime.InteropServices.COMException) { _dialogs.Error("The clipboard is busy. Try again."); }
    }

    // routine form
    [ObservableProperty] int? _editingId;
    [ObservableProperty] string _kind = "Lunch";
    [ObservableProperty] string _name = "Lunch";
    [ObservableProperty] TimeOnly _start = new(13, 0);
    [ObservableProperty] TimeOnly _end = new(14, 0);
    [ObservableProperty] bool _active = true;
    [ObservableProperty] string _error = "";

    public bool IsEditing => EditingId != null;
    public string SaveText => IsEditing ? "Save changes" : "+ Add to my routine";

    partial void OnEditingIdChanged(int? value) { OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(SaveText)); }

    partial void OnKindChanged(string value)
    {
        // Keep the name in step with the chosen activity unless the user typed their own.
        if (IsEditing) return;
        if (string.IsNullOrWhiteSpace(Name) || ScheduleKinds.Routine.Any(k => k.DefaultName == Name))
            Name = ScheduleKinds.Routine.FirstOrDefault(k => k.Id == value)?.DefaultName ?? Name;
    }

    partial void OnCityIdChanged(string value)
    {
        if (_loading) return;
        if (PrayerCities.Find(value) is { } c) { Method = c.DefaultMethod; Hanafi = c.Hanafi; }
        QueueSave();
    }
    partial void OnMethodChanged(PrayerMethod value) { if (!_loading) QueueSave(); }
    partial void OnHanafiChanged(bool value) { if (!_loading) QueueSave(); }
    partial void OnAlertEnabledChanged(bool value) { if (!_loading) QueueSave(); }
    partial void OnAlertMinutesChanged(int value) { if (!_loading) QueueSave(); }

    public async Task InitializeAsync()
    {
        _items = await _schedule.GetAllAsync(ScheduleCategory.Routine);
        Rebuild();
        await LoadShareTargetsAsync();
        _clientService.Changed += async (_, _) => await Ui.Run(LoadShareTargetsAsync);
        _schedule.Changed += async (_, _) => await Ui.Run(async () => { _items = await _schedule.GetAllAsync(ScheduleCategory.Routine); Rebuild(); });
        _settings.Changed += (_, _) => Ui.Run(Rebuild);
        _ticker.Tick += utc =>
        {
            if (utc.Minute == _lastMinute) return;
            _lastMinute = utc.Minute;
            Rebuild();
        };
    }

    void QueueSave()
    {
        if (_loading) return;
        _saveDebounce.Stop(); _saveDebounce.Start();
    }

    async Task SavePrayerSettingsAsync()
    {
        var d = _settings.Current.Clone();
        d.PrayerCityId = CityId; d.PrayerMethod = Method; d.AsrHanafi = Hanafi;
        d.PrayerAlertEnabled = AlertEnabled; d.PrayerAlertMinutesBefore = AlertMinutes;
        d.PrayerAlertMask = Prayers.Aggregate(0, (m, p) => m | (p.AlertEnabled ? 1 << p.Index : 0));
        d.PrayerOffsets = Prayers.Select(p => p.Offset).ToArray();
        await _settings.SaveAsync(d);
    }

    /// <summary>Recomputes prayer rows and the day timeline from current settings and routine.</summary>
    void Rebuild()
    {
        var s = _settings.Current;
        var now = _ticker.UtcNow;
        _loading = true;
        try
        {
            CityId = s.PrayerCityId; Method = s.PrayerMethod; Hanafi = s.AsrHanafi;
            AlertEnabled = s.PrayerAlertEnabled; AlertMinutes = s.PrayerAlertMinutesBefore;

            var city = SchedulePlanner.CityOf(s);
            var cityToday = DateOnly.FromDateTime(TimeConversion.FromUtc(now, city.TimeZoneId).Local);
            var cityOffset = TimeConversion.FromUtc(now, city.TimeZoneId).OffsetText;
            var homeOffset = TimeConversion.FromUtc(now, s.HomeTimeZoneId).OffsetText;
            var sameZone = cityOffset == homeOffset;

            var prayers = SchedulePlanner.Build(cityToday, s, Array.Empty<ScheduleItem>());
            foreach (var row in Prayers)
            {
                var e = prayers.First(p => p.Title == row.Name);
                var st = TimeConversion.FromUtc(e.StartUtc, city.TimeZoneId).Local;
                var en = TimeConversion.FromUtc(e.EndUtc ?? e.StartUtc, city.TimeZoneId).Local;
                row.StartText = TimeFormats.FormatTime(st);
                row.EndText = TimeFormats.FormatTime(en);
                row.HomeText = sameZone ? "" : $"{TimeFormats.FormatTime(TimeConversion.FromUtc(e.StartUtc, s.HomeTimeZoneId).Local)} your time";
                row.AlertEnabled = (s.PrayerAlertMask & (1 << row.Index)) != 0;
                row.Offset = s.PrayerOffsets.ElementAtOrDefault(row.Index);
                row.IsNow = now >= e.StartUtc && now < (e.EndUtc ?? e.StartUtc);
            }
            var sun = prayers.FirstOrDefault(p => p.Kind == "Sunrise");
            SunriseText = sun == null ? "" : $"Sunrise {TimeFormats.FormatTime(TimeConversion.FromUtc(sun.StartUtc, city.TimeZoneId).Local)}  ·  Fajr ends at sunrise";
            CityNote = $"Calculated for {city.Name}, {city.Country} ({cityOffset}) · {PrayerMethods.All.First(m => m.Method == s.PrayerMethod).Label}";

            // timeline for today in the user's own time zone
            var homeToday = DateOnly.FromDateTime(TimeConversion.FromUtc(now, s.HomeTimeZoneId).Local);
            TodayText = $"Today · {TimeFormats.FormatDate(homeToday.ToDateTime(TimeOnly.MinValue))}";
            var plan = SchedulePlanner.Build(homeToday, s, _items).Where(e => e.Category != PlanCategory.Health).ToList();
            Timeline.Clear();
            var nextMarked = false;
            foreach (var e in plan)
            {
                var now1 = now >= e.StartUtc && now < (e.EndUtc ?? e.StartUtc.AddMinutes(1));
                var isNext = !nextMarked && !now1 && e.StartUtc > now;
                if (isNext) nextMarked = true;
                var st = TimeConversion.FromUtc(e.StartUtc, s.HomeTimeZoneId).Local;
                var text = e.EndUtc is { } eu
                    ? $"{TimeFormats.FormatTime(st)} – {TimeFormats.FormatTime(TimeConversion.FromUtc(eu, s.HomeTimeZoneId).Local)}"
                    : TimeFormats.FormatTime(st);
                Timeline.Add(new TimelineItem
                {
                    Emoji = ScheduleKinds.Emoji(e.Kind), Title = e.Title, TimeText = text, IsNow = now1, IsNext = isNext,
                    Tag = e.Category == PlanCategory.Prayer ? "Prayer" : "Routine"
                });
            }

            Routine.Clear();
            foreach (var i in _items) Routine.Add(ScheduleRow.From(i));
        }
        finally { _loading = false; }
        UpdateShare();
    }

    // ---- routine form ---------------------------------------------------------------------

    ScheduleItem BuildItem() => new()
    {
        Id = EditingId ?? 0, Category = ScheduleCategory.Routine, Kind = Kind, Name = Name,
        Mode = ScheduleMode.Fixed, StartMinute = Minutes.FromTime(Start), EndMinute = Minutes.FromTime(End),
        DaysMask = Days.Mask, IsEnabled = Active
    };

    [RelayCommand]
    async Task SaveRoutineAsync()
    {
        Error = "";
        try
        {
            if (EditingId != null) await _schedule.UpdateAsync(BuildItem());
            else await _schedule.AddAsync(BuildItem());
            ResetForm();
        }
        catch (ValidationException ex) { Error = ex.Message; }
    }

    void ResetForm()
    {
        EditingId = null; Error = ""; Active = true; Days.Mask = 127;
        Kind = "Lunch"; Name = "Lunch"; Start = new TimeOnly(13, 0); End = new TimeOnly(14, 0);
    }

    [RelayCommand] void CancelEdit() => ResetForm();

    [RelayCommand]
    void EditRoutine(ScheduleRow? row)
    {
        if (row == null) return;
        var i = row.Item;
        EditingId = i.Id; Kind = i.Kind; Name = i.Name; Start = Minutes.ToTime(i.StartMinute);
        End = Minutes.ToTime(i.EndMinute ?? i.StartMinute); Days.Mask = i.DaysMask; Active = i.IsEnabled; Error = "";
    }

    [RelayCommand]
    async Task DeleteRoutineAsync(ScheduleRow? row)
    {
        if (row == null || !_dialogs.Confirm($"Delete \"{row.Name}\" from your routine?", "Delete")) return;
        await _schedule.DeleteAsync(row.Id);
        if (EditingId == row.Id) ResetForm();
    }

    [RelayCommand]
    async Task ToggleRoutineAsync(ScheduleRow? row)
    {
        if (row != null) await _schedule.SetEnabledAsync(row.Id, !row.Item.IsEnabled);
    }

    [RelayCommand]
    async Task AddSuggestedAsync()
    {
        var n = await _schedule.AddSuggestedRoutineAsync();
        if (n == 0) _dialogs.Info("Your routine already contains the suggested blocks.");
    }
}
