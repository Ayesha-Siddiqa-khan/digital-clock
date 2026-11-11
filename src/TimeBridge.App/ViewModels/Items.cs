using CommunityToolkit.Mvvm.ComponentModel;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

/// <summary>A client with a live-updating local time (used by the widget list and the clients page).</summary>
public partial class ClientClockItem : ObservableObject
{
    public ClientClockItem(Client client) { Client = client; }

    public Client Client { get; }
    public int Id => Client.Id;
    public string Name => Client.Name;
    public string City => Client.City;
    public string Label => $"{Client.Name} · {Client.City}";
    public string ZoneLabel => TimeZoneCatalog.LabelFor(Client.TimeZoneId);

    [ObservableProperty] string _timeText = "";
    [ObservableProperty] string _dateText = "";
    [ObservableProperty] string _offsetText = "";
    [ObservableProperty] bool _isSelected;

    public void Update(DateTime utc)
    {
        var m = TimeConversion.FromUtc(utc, Client.TimeZoneId);
        TimeText = TimeFormats.FormatTime(m.Local, seconds: true);
        DateText = TimeFormats.FormatDate(m.Local);
        OffsetText = m.OffsetText;
    }
}

/// <summary>Display model for one meeting row.</summary>
public class MeetingItem
{
    public required Meeting Meeting { get; init; }
    public int Id => Meeting.Id;
    public string Title => Meeting.Title;
    public string ClientLabel { get; init; } = "";
    public string HomeWhen { get; init; } = "";
    public string ClientWhen { get; init; } = "";
    public string ReminderText { get; init; } = "";
    public string StatusText { get; init; } = "";
    public string Countdown { get; init; } = "";
    public bool CanEdit { get; init; }
    public bool CanCancel { get; init; }
    public bool IsCancelled => Meeting.Status == MeetingStatus.Cancelled;

    public static MeetingItem From(Meeting m, string homeZoneId, DateTime nowUtc)
    {
        var home = TimeConversion.FromUtc(m.StartUtc, homeZoneId);
        var client = TimeConversion.FromUtc(m.StartUtc, m.Client.TimeZoneId);
        var reminder = m.Reminders.OrderBy(r => r.Id).FirstOrDefault();
        var scheduled = m.Status == MeetingStatus.Scheduled;
        return new MeetingItem
        {
            Meeting = m,
            ClientLabel = $"{m.Client.Name} · {m.Client.City}",
            HomeWhen = $"{TimeFormats.FormatDate(home.Local)}  {TimeFormats.FormatTime(home.Local)}  ({TimeZoneCatalog.ShortName(homeZoneId)})",
            ClientWhen = $"{TimeFormats.FormatDate(client.Local)}  {TimeFormats.FormatTime(client.Local)}  ({m.Client.City}, {client.OffsetText})",
            ReminderText = reminder == null ? "No alarm"
                : reminder.MinutesBefore == 0 ? "Alarm at start" : $"Alarm {reminder.MinutesBefore} min before",
            StatusText = m.Status.ToString(),
            Countdown = scheduled && m.StartUtc > nowUtc ? TimeFormats.FormatCountdown(m.StartUtc - nowUtc) : "",
            CanEdit = scheduled && m.StartUtc > nowUtc,
            CanCancel = scheduled,
        };
    }
}
