using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeBridge.App.Services;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

public record ReminderChoice(int Minutes, string Label);

/// <summary>Meeting planner (form + live time conversion) and meeting list.</summary>
public partial class MeetingsViewModel : ObservableObject
{
    readonly MeetingService _meetings;
    readonly ClientService _clients;
    readonly SettingsService _settings;
    readonly AlarmScheduler _scheduler;
    readonly ClockTicker _ticker;
    readonly IDialogService _dialogs;
    int _lastMinute = -1;

    public MeetingsViewModel(MeetingService meetings, ClientService clients, SettingsService settings,
        AlarmScheduler scheduler, ClockTicker ticker, IDialogService dialogs)
    {
        _meetings = meetings; _clients = clients; _settings = settings;
        _scheduler = scheduler; _ticker = ticker; _dialogs = dialogs;
    }

    public ObservableCollection<Client> ClientChoices { get; } = new();
    public ObservableCollection<MeetingItem> Meetings { get; } = new();
    public IReadOnlyList<ReminderChoice> ReminderChoices { get; } =
        ReminderOptions.Minutes.Select(m => new ReminderChoice(m, ReminderOptions.Label(m))).ToList();

    [ObservableProperty] string _title = "Client catch-up";
    [ObservableProperty] int? _selectedClientId;
    [ObservableProperty] DateOnly? _date;
    [ObservableProperty] TimeOnly _time = new(17, 0);
    [ObservableProperty] int _reminderMinutes = 10;
    [ObservableProperty] string _notes = "";
    [ObservableProperty] int? _editingId;
    [ObservableProperty] bool _showAll;

    [ObservableProperty] string _previewIntro = "";
    [ObservableProperty] string _previewTime = "";
    [ObservableProperty] string _previewDetail = "";
    [ObservableProperty] string _previewError = "";
    [ObservableProperty] string _formError = "";
    [ObservableProperty] string _statusMessage = "";

    public bool IsEditing => EditingId != null;
    public string SaveText => IsEditing ? "Save changes" : "Add meeting & alarm";
    public string FormTitle => IsEditing ? "Edit meeting" : "Plan a client meeting";
    public string HomeLabel => TimeZoneCatalog.ShortName(_settings.Current.HomeTimeZoneId);
    public string HomeFullLabel => TimeZoneCatalog.LabelFor(_settings.Current.HomeTimeZoneId);

    partial void OnEditingIdChanged(int? value) { OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(SaveText)); OnPropertyChanged(nameof(FormTitle)); }
    partial void OnDateChanged(DateOnly? value) => UpdatePreview();
    partial void OnTimeChanged(TimeOnly value) => UpdatePreview();
    partial void OnSelectedClientIdChanged(int? value) => UpdatePreview();
    partial void OnShowAllChanged(bool value) => _ = ReloadMeetingsAsync();

    public async Task InitializeAsync()
    {
        ReminderMinutes = _settings.Current.DefaultReminderMinutes;
        ResetDateToToday();
        await ReloadClientsAsync();
        await ReloadMeetingsAsync();

        _clients.Changed += async (_, _) => await Ui.Run(async () => { await ReloadClientsAsync(); await ReloadMeetingsAsync(); });
        _meetings.Changed += async (_, _) => await Ui.Run(ReloadMeetingsAsync);
        _scheduler.RemindersChanged += async (_, _) => await Ui.Run(ReloadMeetingsAsync);
        _settings.Changed += (_, _) => Ui.Run(() => { OnPropertyChanged(nameof(HomeLabel)); OnPropertyChanged(nameof(HomeFullLabel)); UpdatePreview(); });
        _ticker.Tick += utc =>
        {
            if (utc.Minute == _lastMinute) return;
            _lastMinute = utc.Minute;
            _ = ReloadMeetingsAsync(); // refresh countdowns
        };
    }

    void ResetDateToToday()
    {
        var home = TimeConversion.FromUtc(_ticker.UtcNow, _settings.Current.HomeTimeZoneId).Local;
        Date = DateOnly.FromDateTime(home);
        var next = home.AddHours(1);
        Time = new TimeOnly(next.Hour, 0);
    }

    async Task ReloadClientsAsync()
    {
        var keep = SelectedClientId;
        var list = await _clients.GetAllAsync();
        ClientChoices.Clear();
        foreach (var c in list) ClientChoices.Add(c);
        SelectedClientId = list.Any(c => c.Id == keep) ? keep : list.FirstOrDefault()?.Id;
        UpdatePreview();
    }

    public async Task ReloadMeetingsAsync()
    {
        var home = _settings.Current.HomeTimeZoneId;
        var now = _ticker.UtcNow;
        var list = ShowAll ? await _meetings.GetAllAsync() : await _meetings.GetUpcomingAsync();
        Meetings.Clear();
        foreach (var m in list) Meetings.Add(MeetingItem.From(m, home, now));
    }

    void UpdatePreview()
    {
        PreviewError = "";
        var client = ClientChoices.FirstOrDefault(c => c.Id == SelectedClientId);
        PreviewIntro = $"When it is {TimeFormats.FormatTime(Time)} in {HomeLabel}";
        if (Date is not { } d || client == null)
        {
            PreviewTime = "—";
            PreviewDetail = client == null ? "Add a client first." : "Enter a valid date (DD-MM-YYYY).";
            return;
        }
        try
        {
            var homeId = _settings.Current.HomeTimeZoneId;
            var conv = TimeConversion.Convert(d.ToDateTime(Time), homeId, client.TimeZoneId);
            PreviewTime = TimeFormats.FormatTime(conv.Local);
            var dayShift = (DateOnly.FromDateTime(conv.Local).DayNumber - d.DayNumber) switch
            {
                0 => "", 1 => "  (next day)", -1 => "  (previous day)", var n => $"  ({n:+#;-#} days)"
            };
            PreviewDetail = $"{client.Name} · {client.City} · {TimeFormats.FormatDate(conv.Local)} · {conv.OffsetText}{(conv.IsDaylightSaving ? " (DST)" : "")}{dayShift}";
        }
        catch (InvalidLocalTimeException ex)
        {
            PreviewTime = "—";
            PreviewDetail = "";
            PreviewError = ex.Message;
        }
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        FormError = ""; StatusMessage = "";
        if (SelectedClientId is not int clientId) { FormError = "Choose a client."; return; }
        if (Date is not { } date) { FormError = "Enter a valid date in DD-MM-YYYY format."; return; }
        var input = new MeetingInput(Title, clientId, date, Time, ReminderMinutes, string.IsNullOrWhiteSpace(Notes) ? null : Notes);
        try
        {
            if (EditingId is int id) await _meetings.UpdateAsync(id, input);
            else await _meetings.CreateAsync(input);
            StatusMessage = IsEditing ? "Meeting updated." : $"Meeting added. {ReminderOptions.Label(ReminderMinutes)} alarm is set.";
            EditingId = null;
            Notes = "";
            if (_scheduler != null) await _scheduler.CheckAsync(); // an alarm due right now should fire immediately
        }
        catch (ValidationException ex) { FormError = ex.Message; }
    }

    [RelayCommand]
    void CancelEdit()
    {
        EditingId = null; FormError = ""; StatusMessage = ""; Notes = "";
    }

    public void BeginEdit(MeetingItem item)
    {
        var m = item.Meeting;
        var home = TimeConversion.FromUtc(m.StartUtc, _settings.Current.HomeTimeZoneId).Local;
        EditingId = m.Id;
        Title = m.Title;
        SelectedClientId = m.ClientId;
        Date = DateOnly.FromDateTime(home);
        Time = TimeOnly.FromDateTime(home);
        ReminderMinutes = m.Reminders.OrderBy(r => r.Id).FirstOrDefault()?.MinutesBefore ?? _settings.Current.DefaultReminderMinutes;
        Notes = m.Notes ?? "";
        FormError = ""; StatusMessage = "";
    }

    [RelayCommand] void Edit(MeetingItem? item) { if (item != null) BeginEdit(item); }

    [RelayCommand]
    async Task CancelMeetingAsync(MeetingItem? item)
    {
        if (item == null) return;
        if (!_dialogs.Confirm($"Cancel \"{item.Title}\"? Its alarm will be switched off.", "Cancel meeting")) return;
        await _meetings.CancelAsync(item.Id);
        if (EditingId == item.Id) CancelEdit();
    }

    [RelayCommand]
    async Task DeleteMeetingAsync(MeetingItem? item)
    {
        if (item == null) return;
        if (!_dialogs.Confirm($"Permanently delete \"{item.Title}\"? This cannot be undone.", "Delete meeting")) return;
        await _meetings.DeleteAsync(item.Id);
        if (EditingId == item.Id) CancelEdit();
    }

    [RelayCommand]
    void CopyTime()
    {
        var client = ClientChoices.FirstOrDefault(c => c.Id == SelectedClientId);
        if (client == null || Date is not { } d) return;
        try
        {
            var conv = TimeConversion.Convert(d.ToDateTime(Time), _settings.Current.HomeTimeZoneId, client.TimeZoneId);
            var text = $"{Title}\n{TimeFormats.FormatDate(d.ToDateTime(Time))} {TimeFormats.FormatTime(Time)} {HomeFullLabel}\n" +
                       $"= {TimeFormats.FormatDate(conv.Local)} {TimeFormats.FormatTime(conv.Local)} {client.City} ({conv.OffsetText})";
            Clipboard.SetText(text);
            StatusMessage = "Time copied to clipboard.";
        }
        catch (Exception ex) when (ex is InvalidLocalTimeException or System.Runtime.InteropServices.COMException)
        {
            FormError = ex.Message;
        }
    }
}
