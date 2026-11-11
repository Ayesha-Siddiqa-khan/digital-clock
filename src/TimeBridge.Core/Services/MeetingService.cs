using Microsoft.EntityFrameworkCore;
using TimeBridge.Core.Data;
using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

public static class ReminderOptions
{
    public static readonly int[] Minutes = { 0, 5, 10, 15, 30 };
    public static string Label(int m) => m == 0 ? "At meeting time" : $"{m} minutes before";
}

/// <summary>Meeting details entered by the user. Date and time are home-zone (Pakistan) wall-clock values.</summary>
public record MeetingInput(string Title, int ClientId, DateOnly Date, TimeOnly Time, int ReminderMinutes, string? Notes = null);

public class MeetingService
{
    readonly IDbContextFactory<TimeBridgeDbContext> _factory;
    readonly IClock _clock;
    readonly SettingsService _settings;

    public MeetingService(IDbContextFactory<TimeBridgeDbContext> factory, IClock clock, SettingsService settings)
    { _factory = factory; _clock = clock; _settings = settings; }

    public event EventHandler? Changed;

    DateTime ResolveStartUtc(MeetingInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) throw new ValidationException("Meeting name is required.");
        if (!ReminderOptions.Minutes.Contains(input.ReminderMinutes))
            throw new ValidationException("Reminder must be 0, 5, 10, 15 or 30 minutes before.");
        DateTime utc;
        try { utc = TimeConversion.ToUtc(input.Date, input.Time, _settings.Current.HomeTimeZoneId); }
        catch (InvalidLocalTimeException e) { throw new ValidationException(e.Message); }
        if (utc <= _clock.UtcNow) throw new ValidationException("Meeting time must be in the future.");
        return utc;
    }

    static Reminder NewReminder(int minutes, DateTime startUtc) => new()
    {
        MinutesBefore = minutes, TriggerUtc = startUtc.AddMinutes(-minutes), Status = ReminderStatus.Pending
    };

    public async Task<Meeting> CreateAsync(MeetingInput input)
    {
        var start = ResolveStartUtc(input);
        await using var db = await _factory.CreateDbContextAsync();
        if (!await db.Clients.AnyAsync(c => c.Id == input.ClientId)) throw new ValidationException("Choose a client.");
        var now = _clock.UtcNow;
        var m = new Meeting
        {
            Title = input.Title.Trim(), ClientId = input.ClientId, StartUtc = start, Notes = input.Notes?.Trim(),
            Status = MeetingStatus.Scheduled, CreatedUtc = now, UpdatedUtc = now,
            Reminders = { NewReminder(input.ReminderMinutes, start) }
        };
        db.Meetings.Add(m);
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
        return m;
    }

    public async Task UpdateAsync(int id, MeetingInput input)
    {
        var start = ResolveStartUtc(input);
        await using var db = await _factory.CreateDbContextAsync();
        var m = await db.Meetings.Include(x => x.Reminders).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ValidationException("Meeting not found.");
        if (!await db.Clients.AnyAsync(c => c.Id == input.ClientId)) throw new ValidationException("Choose a client.");
        m.Title = input.Title.Trim(); m.ClientId = input.ClientId; m.StartUtc = start;
        m.Notes = input.Notes?.Trim(); m.Status = MeetingStatus.Scheduled; m.UpdatedUtc = _clock.UtcNow;
        db.Reminders.RemoveRange(m.Reminders);
        m.Reminders.Clear();
        m.Reminders.Add(NewReminder(input.ReminderMinutes, start));
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task CancelAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var m = await db.Meetings.Include(x => x.Reminders).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return;
        m.Status = MeetingStatus.Cancelled; m.UpdatedUtc = _clock.UtcNow;
        foreach (var r in m.Reminders.Where(r => r.Status is ReminderStatus.Pending or ReminderStatus.Snoozed or ReminderStatus.Fired))
            r.Status = ReminderStatus.Dismissed;
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var m = await db.Meetings.FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return;
        db.Meetings.Remove(m);
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<Meeting?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Meetings.AsNoTracking().Include(m => m.Client).Include(m => m.Reminders)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    /// <summary>Scheduled meetings that have not started yet, soonest first.</summary>
    public async Task<List<Meeting>> GetUpcomingAsync(int? take = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var now = _clock.UtcNow;
        IQueryable<Meeting> q = db.Meetings.AsNoTracking().Include(m => m.Client).Include(m => m.Reminders)
            .Where(m => m.Status == MeetingStatus.Scheduled && m.StartUtc >= now)
            .OrderBy(m => m.StartUtc);
        if (take is int t) q = q.Take(t);
        return await q.ToListAsync();
    }

    /// <summary>All meetings, newest first (history and cancelled included).</summary>
    public async Task<List<Meeting>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Meetings.AsNoTracking().Include(m => m.Client).Include(m => m.Reminders)
            .OrderByDescending(m => m.StartUtc).ToListAsync();
    }
}
