using Microsoft.EntityFrameworkCore;
using TimeBridge.Core.Data;
using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

public record AlarmInfo(int ReminderId, int MeetingId, string Title, string ClientName, string ClientCity,
    string ClientTimeZoneId, DateTime StartUtc, int MinutesBefore);

/// <summary>Background reminder engine. Independent of any window, so alarms keep working
/// while the dashboard is minimised or the widget is hidden.</summary>
public class AlarmScheduler : IDisposable
{
    /// <summary>Reminders still unfired this long after the meeting start are skipped as missed.</summary>
    public static readonly TimeSpan MissedGrace = TimeSpan.FromMinutes(30);

    readonly IDbContextFactory<TimeBridgeDbContext> _factory;
    readonly IClock _clock;
    readonly SettingsService _settings;
    CancellationTokenSource? _cts;
    Task? _loop;

    public AlarmScheduler(IDbContextFactory<TimeBridgeDbContext> factory, IClock clock, SettingsService settings)
    { _factory = factory; _clock = clock; _settings = settings; }

    /// <summary>Raised on a background thread; subscribers must marshal to the UI thread.</summary>
    public event Action<AlarmInfo>? AlarmRaised;
    public event EventHandler? RemindersChanged;

    public async Task StartAsync()
    {
        if (_loop != null) return;
        await RecoverAsync();
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try { await CheckAsync(); }
                catch (Exception) when (!ct.IsCancellationRequested) { /* keep ticking; next tick retries */ }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Alarms that were showing when the app last exited are re-armed so they are not lost.</summary>
    public async Task RecoverAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var stale = await db.Reminders.Where(r => r.Status == ReminderStatus.Fired).ToListAsync();
        foreach (var r in stale) r.Status = ReminderStatus.Pending;
        if (stale.Count > 0) await db.SaveChangesAsync();
    }

    /// <summary>Evaluates all reminders against the current clock. Public for tests.</summary>
    public async Task<IReadOnlyList<AlarmInfo>> CheckAsync()
    {
        var now = _clock.UtcNow;
        var raised = new List<AlarmInfo>();
        await using var db = await _factory.CreateDbContextAsync();

        var due = await db.Reminders
            .Include(r => r.Meeting).ThenInclude(m => m.Client)
            .Where(r => (r.Status == ReminderStatus.Pending && r.TriggerUtc <= now)
                     || (r.Status == ReminderStatus.Snoozed && r.SnoozedUntilUtc <= now))
            .ToListAsync();

        foreach (var r in due)
        {
            var m = r.Meeting;
            if (m.Status != MeetingStatus.Scheduled) { r.Status = ReminderStatus.Dismissed; continue; }
            if (now > m.StartUtc + MissedGrace) { r.Status = ReminderStatus.Missed; continue; }
            r.Status = ReminderStatus.Fired;
            r.FiredUtc = now;
            raised.Add(new AlarmInfo(r.Id, m.Id, m.Title, m.Client.Name, m.Client.City, m.Client.TimeZoneId, m.StartUtc, r.MinutesBefore));
        }

        // Meetings that are well past their start become Completed.
        var finished = await db.Meetings
            .Where(m => m.Status == MeetingStatus.Scheduled && m.StartUtc < now - MissedGrace).ToListAsync();
        foreach (var m in finished) m.Status = MeetingStatus.Completed;

        if (due.Count > 0 || finished.Count > 0)
        {
            await db.SaveChangesAsync();
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }
        foreach (var a in raised) AlarmRaised?.Invoke(a);
        return raised;
    }

    public async Task SnoozeAsync(int reminderId, int? minutes = null)
    {
        var mins = minutes ?? _settings.Current.SnoozeMinutes;
        await using var db = await _factory.CreateDbContextAsync();
        var r = await db.Reminders.FirstOrDefaultAsync(x => x.Id == reminderId);
        if (r == null) return;
        r.Status = ReminderStatus.Snoozed;
        r.SnoozedUntilUtc = _clock.UtcNow.AddMinutes(mins);
        await db.SaveChangesAsync();
        RemindersChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DismissAsync(int reminderId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var r = await db.Reminders.FirstOrDefaultAsync(x => x.Id == reminderId);
        if (r == null) return;
        r.Status = ReminderStatus.Dismissed;
        await db.SaveChangesAsync();
        RemindersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
    }
}
