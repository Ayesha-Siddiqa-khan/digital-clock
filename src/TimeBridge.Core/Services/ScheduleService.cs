using Microsoft.EntityFrameworkCore;
using TimeBridge.Core.Data;
using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

/// <summary>CRUD for routine blocks and health reminders.</summary>
public class ScheduleService
{
    readonly IDbContextFactory<TimeBridgeDbContext> _factory;
    public ScheduleService(IDbContextFactory<TimeBridgeDbContext> factory) => _factory = factory;

    public event EventHandler? Changed;

    public async Task<List<ScheduleItem>> GetAllAsync(ScheduleCategory? category = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.ScheduleItems.AsNoTracking().AsQueryable();
        if (category is { } c) q = q.Where(i => i.Category == c);
        var list = await q.ToListAsync();
        return list.OrderBy(i => i.SortOrder).ThenBy(i => i.StartMinute).ThenBy(i => i.Id).ToList();
    }

    static void Validate(ScheduleItem i)
    {
        if (string.IsNullOrWhiteSpace(i.Name)) throw new ValidationException("Name is required.");
        if (i.StartMinute is < 0 or >= 1440) throw new ValidationException("Start time is not valid.");
        if (i.EndMinute is < 0 or >= 1440) throw new ValidationException("End time is not valid.");
        if ((i.DaysMask & 127) == 0) throw new ValidationException("Choose at least one day.");
        if (i.Category == ScheduleCategory.Routine)
        {
            if (i.EndMinute is null) throw new ValidationException("Choose an end time.");
            if (i.EndMinute == i.StartMinute) throw new ValidationException("Start and end time must be different.");
        }
        else if (i.Mode == ScheduleMode.Interval)
        {
            if (i.IntervalMinutes is null or < 5) throw new ValidationException("Repeat interval must be at least 5 minutes.");
            if (i.EndMinute is null) throw new ValidationException("Choose when the repeating window ends.");
            if (i.EndMinute <= i.StartMinute) throw new ValidationException("The window must end after it starts.");
            if (i.StartMinute + i.IntervalMinutes > i.EndMinute)
                throw new ValidationException("The interval is longer than the window, so it would never fire.");
        }
    }

    public async Task<ScheduleItem> AddAsync(ScheduleItem item)
    {
        item.Id = 0;
        Validate(item);
        await using var db = await _factory.CreateDbContextAsync();
        item.SortOrder = (await db.ScheduleItems.Select(i => (int?)i.SortOrder).MaxAsync() ?? -1) + 1;
        item.Name = item.Name.Trim();
        db.ScheduleItems.Add(item);
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    public async Task UpdateAsync(ScheduleItem item)
    {
        Validate(item);
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.ScheduleItems.FirstOrDefaultAsync(i => i.Id == item.Id) ?? throw new ValidationException("Item not found.");
        row.Kind = item.Kind; row.Name = item.Name.Trim(); row.Message = item.Message; row.Mode = item.Mode;
        row.StartMinute = item.StartMinute; row.EndMinute = item.EndMinute; row.IntervalMinutes = item.IntervalMinutes;
        row.DaysMask = item.DaysMask; row.IsEnabled = item.IsEnabled;
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetEnabledAsync(int id, bool enabled)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.ScheduleItems.FirstOrDefaultAsync(i => i.Id == id);
        if (row == null) return;
        row.IsEnabled = enabled;
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.ScheduleItems.FirstOrDefaultAsync(i => i.Id == id);
        if (row == null) return;
        db.ScheduleItems.Remove(row);
        await db.SaveChangesAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    static ScheduleItem R(string kind, string name, int start, int end) => new()
    {
        Category = ScheduleCategory.Routine, Kind = kind, Name = name, StartMinute = start, EndMinute = end
    };

    /// <summary>Suggested everyday routine; adds only blocks whose name does not exist yet.</summary>
    public Task<int> AddSuggestedRoutineAsync() => AddMissingAsync(ScheduleCategory.Routine, new[]
    {
        R("Breakfast", "Breakfast", 8 * 60, 8 * 60 + 30),
        R("Lunch", "Lunch", 13 * 60, 14 * 60),
        R("Rest", "Afternoon rest", 15 * 60 + 30, 16 * 60),
        R("Dinner", "Dinner", 20 * 60, 21 * 60),
        R("Sleep", "Sleep", 23 * 60, 6 * 60),
    });

    static ScheduleItem H(string kind, string name, ScheduleMode mode, int start, int? end, int? interval, int days) => new()
    {
        Category = ScheduleCategory.Health, Kind = kind, Name = name, Mode = mode,
        StartMinute = start, EndMinute = end, IntervalMinutes = interval, DaysMask = days
    };

    const int Weekdays = 0b0011111;

    /// <summary>Suggested plan for a 9:00-16:00 desk day; adds only reminders whose name does not exist yet.</summary>
    public Task<int> AddSuggestedHealthPlanAsync() => AddMissingAsync(ScheduleCategory.Health, new[]
    {
        H("Water", "Drink water", ScheduleMode.Interval, 9 * 60, 16 * 60, 60, Weekdays),
        H("ScreenBreak", "Screen break", ScheduleMode.Interval, 9 * 60, 16 * 60, 120, Weekdays),
        H("Walk", "Take a walk", ScheduleMode.Fixed, 12 * 60, null, null, Weekdays),
        H("Meal", "Lunch time", ScheduleMode.Fixed, 13 * 60, null, null, Weekdays),
        H("Stretch", "Stretch", ScheduleMode.Fixed, 15 * 60, null, null, Weekdays),
    });

    async Task<int> AddMissingAsync(ScheduleCategory category, ScheduleItem[] items)
    {
        var existing = (await GetAllAsync(category)).Select(i => i.Name.ToLowerInvariant()).ToHashSet();
        var added = 0;
        foreach (var i in items.Where(i => !existing.Contains(i.Name.ToLowerInvariant())))
        {
            await AddAsync(i);
            added++;
        }
        return added;
    }
}
