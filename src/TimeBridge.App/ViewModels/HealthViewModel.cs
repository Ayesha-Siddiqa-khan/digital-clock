using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeBridge.App.Services;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

/// <summary>"Health breaks" page: water, meals, walks and screen-break reminders, at fixed times or repeating in a window.</summary>
public partial class HealthViewModel : ObservableObject
{
    readonly ScheduleService _schedule;
    readonly IDialogService _dialogs;
    readonly AlarmService _alarms;

    public HealthViewModel(ScheduleService schedule, IDialogService dialogs, AlarmService alarms)
    { _schedule = schedule; _dialogs = dialogs; _alarms = alarms; }

    public IReadOnlyList<KindInfo> Kinds => ScheduleKinds.Health;
    public IReadOnlyList<IntervalChoice> Intervals { get; } =
        new[] { 15, 20, 30, 45, 60, 90, 120, 180, 240 }.Select(m => new IntervalChoice(m, m % 60 == 0 ? $"{m / 60} hour(s)" : $"{m} minutes")).ToList();
    public ObservableCollection<ScheduleRow> Items { get; } = new();
    public DaysEditor Days { get; } = new() { Sat = false, Sun = false };

    [ObservableProperty] int? _editingId;
    [ObservableProperty] string _kind = "Water";
    [ObservableProperty] string _name = "Drink water";
    [ObservableProperty] string _message = "";
    [ObservableProperty] bool _repeating = true;
    [ObservableProperty] TimeOnly _start = new(9, 0);
    [ObservableProperty] TimeOnly _end = new(16, 0);
    [ObservableProperty] int _interval = 60;
    [ObservableProperty] bool _active = true;
    [ObservableProperty] string _error = "";

    public bool IsEditing => EditingId != null;
    public string SaveText => IsEditing ? "Save changes" : "+ Add reminder";
    public string StartLabel => Repeating ? "Window starts" : "Remind me at";
    public string Summary => Repeating
        ? $"Reminds you every {Interval} minutes between {TimeFormats.FormatTime(Start)} and {TimeFormats.FormatTime(End)}."
        : $"Reminds you once at {TimeFormats.FormatTime(Start)}.";

    partial void OnEditingIdChanged(int? value) { OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(SaveText)); }
    partial void OnRepeatingChanged(bool value) { OnPropertyChanged(nameof(StartLabel)); OnPropertyChanged(nameof(Summary)); }
    partial void OnStartChanged(TimeOnly value) => OnPropertyChanged(nameof(Summary));
    partial void OnEndChanged(TimeOnly value) => OnPropertyChanged(nameof(Summary));
    partial void OnIntervalChanged(int value) => OnPropertyChanged(nameof(Summary));

    partial void OnKindChanged(string value)
    {
        if (IsEditing) return;
        var info = ScheduleKinds.Health.FirstOrDefault(k => k.Id == value);
        if (info == null) return;
        if (string.IsNullOrWhiteSpace(Name) || ScheduleKinds.Health.Any(k => k.DefaultName == Name)) Name = info.DefaultName;
    }

    public async Task InitializeAsync()
    {
        await ReloadAsync();
        _schedule.Changed += async (_, _) => await Ui.Run(ReloadAsync);
    }

    async Task ReloadAsync()
    {
        var items = await _schedule.GetAllAsync(ScheduleCategory.Health);
        Items.Clear();
        foreach (var i in items) Items.Add(ScheduleRow.From(i));
    }

    ScheduleItem BuildItem() => new()
    {
        Id = EditingId ?? 0, Category = ScheduleCategory.Health, Kind = Kind, Name = Name,
        Message = string.IsNullOrWhiteSpace(Message) ? null : Message.Trim(),
        Mode = Repeating ? ScheduleMode.Interval : ScheduleMode.Fixed,
        StartMinute = Minutes.FromTime(Start), EndMinute = Repeating ? Minutes.FromTime(End) : null,
        IntervalMinutes = Repeating ? Interval : null, DaysMask = Days.Mask, IsEnabled = Active
    };

    [RelayCommand]
    async Task SaveAsync()
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
        EditingId = null; Error = ""; Active = true; Message = "";
        Days.Mask = 0b0011111;
        Kind = "Water"; Name = "Drink water"; Repeating = true;
        Start = new TimeOnly(9, 0); End = new TimeOnly(16, 0); Interval = 60;
    }

    [RelayCommand] void CancelEdit() => ResetForm();

    [RelayCommand]
    void Edit(ScheduleRow? row)
    {
        if (row == null) return;
        var i = row.Item;
        EditingId = i.Id; Kind = i.Kind; Name = i.Name; Message = i.Message ?? "";
        Repeating = i.Mode == ScheduleMode.Interval;
        Start = Minutes.ToTime(i.StartMinute);
        End = Minutes.ToTime(i.EndMinute ?? i.StartMinute + 60);
        Interval = i.IntervalMinutes ?? 60;
        Days.Mask = i.DaysMask; Active = i.IsEnabled; Error = "";
    }

    [RelayCommand]
    async Task DeleteAsync(ScheduleRow? row)
    {
        if (row == null || !_dialogs.Confirm($"Delete the reminder \"{row.Name}\"?", "Delete reminder")) return;
        await _schedule.DeleteAsync(row.Id);
        if (EditingId == row.Id) ResetForm();
    }

    [RelayCommand]
    async Task ToggleAsync(ScheduleRow? row)
    {
        if (row != null) await _schedule.SetEnabledAsync(row.Id, !row.Item.IsEnabled);
    }

    [RelayCommand]
    async Task StarterPlanAsync()
    {
        var n = await _schedule.AddSuggestedHealthPlanAsync();
        if (n == 0) _dialogs.Info("The starter plan is already in your list.");
    }

    [RelayCommand]
    void TestReminder()
    {
        var info = ScheduleKinds.Health.FirstOrDefault(k => k.Id == Kind);
        _alarms.ShowNotice(new NoticeInfo("test", string.IsNullOrWhiteSpace(Name) ? "Reminder" : Name,
            string.IsNullOrWhiteSpace(Message) ? info?.DefaultMessage ?? "" : Message, Kind, PlanCategory.Health));
    }
}
