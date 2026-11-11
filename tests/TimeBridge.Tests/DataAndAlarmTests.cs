using Microsoft.EntityFrameworkCore;
using TimeBridge.Core.Data;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.Tests;

public class DatabaseTests
{
    [Fact]
    public async Task Migrations_create_all_tables_and_seed_defaults()
    {
        using var t = new TestDb();
        await using var db = t.CreateDbContext();
        Assert.Single(await db.WidgetState.ToListAsync());
        var client = Assert.Single(await db.Clients.ToListAsync());
        Assert.Equal("Europe/London", client.TimeZoneId);
        Assert.Equal(client.Id, (await db.WidgetState.SingleAsync()).SelectedClientId);
        Assert.Empty(await db.Meetings.ToListAsync());
        Assert.Empty(await db.Reminders.ToListAsync());
    }

    [Fact]
    public void Database_lives_in_LocalAppData()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.StartsWith(local, DatabaseSetup.DefaultDatabasePath());
    }

    [Fact]
    public async Task Widget_state_round_trips()
    {
        using var t = new TestDb();
        var svc = new WidgetStateService(t);
        var s = await svc.LoadAsync();
        s.Left = 120; s.Top = 64; s.Width = 420; s.IsPinned = false; s.IsCollapsed = true; s.IsVisible = false;
        await svc.SaveAsync(s);
        var back = await svc.LoadAsync();
        Assert.Equal((120, 64, 420), (back.Left, back.Top, back.Width));
        Assert.False(back.IsPinned);
        Assert.True(back.IsCollapsed);
        Assert.False(back.IsVisible);
    }

    [Fact]
    public async Task Settings_round_trip()
    {
        using var t = new TestDb();
        await t.Settings.SaveAsync(new AppSettingsData
        {
            HomeTimeZoneId = "Asia/Dubai", ShowSeconds = false, WidgetOpacity = 0.7, DefaultReminderMinutes = 15,
            SnoozeMinutes = 10, AlarmSoundEnabled = false
        });
        var s2 = new SettingsService(t);
        var d = await s2.LoadAsync();
        Assert.Equal("Asia/Dubai", d.HomeTimeZoneId);
        Assert.False(d.ShowSeconds);
        Assert.Equal(0.7, d.WidgetOpacity);
        Assert.Equal(15, d.DefaultReminderMinutes);
        Assert.False(d.AlarmSoundEnabled);
    }
}

public class ClientTests
{
    [Fact]
    public async Task Add_edit_delete_client()
    {
        using var t = new TestDb();
        var c = await t.Clients.AddAsync("Acme", "New York", "America/New_York");
        await t.Clients.UpdateAsync(c.Id, "Acme Inc", "Boston", "America/New_York");
        var got = await t.Clients.GetAsync(c.Id);
        Assert.Equal(("Acme Inc", "Boston"), (got!.Name, got.City));
        await t.Clients.DeleteAsync(c.Id);
        Assert.Null(await t.Clients.GetAsync(c.Id));
    }

    [Theory]
    [InlineData("", "London", "Europe/London")]
    [InlineData("A", "", "Europe/London")]
    [InlineData("A", "London", "Mars/Olympus")]
    public async Task Invalid_clients_are_rejected(string name, string city, string tz)
    {
        using var t = new TestDb();
        await Assert.ThrowsAsync<ValidationException>(() => t.Clients.AddAsync(name, city, tz));
    }

    [Fact]
    public async Task Deleting_client_removes_meetings_and_reminders_and_clears_widget_selection()
    {
        using var t = new TestDb();
        var c = (await t.Clients.GetAllAsync()).Single();
        await t.Meetings.CreateAsync(new MeetingInput("Sync", c.Id, new DateOnly(2026, 11, 12), new TimeOnly(17, 0), 10));
        await t.Clients.DeleteAsync(c.Id);
        await using var db = t.CreateDbContext();
        Assert.Empty(await db.Meetings.ToListAsync());
        Assert.Empty(await db.Reminders.ToListAsync());
        Assert.Null((await db.WidgetState.SingleAsync()).SelectedClientId);
    }
}

public class MeetingTests
{
    static MeetingInput Input(int clientId, int minutes = 10, int day = 12) =>
        new("Client catch-up", clientId, new DateOnly(2026, 11, day), new TimeOnly(17, 0), minutes);

    [Fact]
    public async Task Create_converts_pakistan_time_to_utc_and_sets_reminder()
    {
        using var t = new TestDb();
        var c = (await t.Clients.GetAllAsync()).Single();
        var m = await t.Meetings.CreateAsync(Input(c.Id, 15));
        Assert.Equal(new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc), m.StartUtc);
        var r = Assert.Single(m.Reminders);
        Assert.Equal(new DateTime(2026, 11, 12, 11, 45, 0, DateTimeKind.Utc), r.TriggerUtc);
        Assert.Equal(ReminderStatus.Pending, r.Status);
    }

    [Theory]
    [InlineData(0)] [InlineData(5)] [InlineData(10)] [InlineData(15)] [InlineData(30)]
    public async Task All_reminder_options_are_accepted(int minutes)
    {
        using var t = new TestDb();
        var c = (await t.Clients.GetAllAsync()).Single();
        var m = await t.Meetings.CreateAsync(Input(c.Id, minutes));
        Assert.Equal(m.StartUtc.AddMinutes(-minutes), m.Reminders.Single().TriggerUtc);
    }

    [Fact]
    public async Task Invalid_reminder_past_time_and_empty_title_are_rejected()
    {
        using var t = new TestDb();
        var c = (await t.Clients.GetAllAsync()).Single();
        await Assert.ThrowsAsync<ValidationException>(() => t.Meetings.CreateAsync(Input(c.Id, 7)));
        await Assert.ThrowsAsync<ValidationException>(() => t.Meetings.CreateAsync(Input(c.Id, day: 10)));
        await Assert.ThrowsAsync<ValidationException>(() => t.Meetings.CreateAsync(Input(c.Id) with { Title = " " }));
    }

    [Fact]
    public async Task Update_moves_meeting_and_rebuilds_reminder()
    {
        using var t = new TestDb();
        var c = (await t.Clients.GetAllAsync()).Single();
        var m = await t.Meetings.CreateAsync(Input(c.Id, 10));
        await t.Meetings.UpdateAsync(m.Id, Input(c.Id, 5, day: 13));
        var got = await t.Meetings.GetAsync(m.Id);
        Assert.Equal(new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc), got!.StartUtc);
        Assert.Equal(5, got.Reminders.Single().MinutesBefore);
    }

    [Fact]
    public async Task Cancel_removes_from_upcoming_and_disarms_alarm()
    {
        using var t = new TestDb();
        var c = (await t.Clients.GetAllAsync()).Single();
        var m = await t.Meetings.CreateAsync(Input(c.Id));
        Assert.Single(await t.Meetings.GetUpcomingAsync());
        await t.Meetings.CancelAsync(m.Id);
        Assert.Empty(await t.Meetings.GetUpcomingAsync());
        Assert.Equal(ReminderStatus.Dismissed, (await t.Meetings.GetAsync(m.Id))!.Reminders.Single().Status);
        Assert.Single(await t.Meetings.GetAllAsync());
    }

    [Fact]
    public async Task Upcoming_is_sorted_soonest_first()
    {
        using var t = new TestDb();
        var c = (await t.Clients.GetAllAsync()).Single();
        await t.Meetings.CreateAsync(Input(c.Id, day: 14) with { Title = "later" });
        await t.Meetings.CreateAsync(Input(c.Id, day: 12) with { Title = "sooner" });
        var up = await t.Meetings.GetUpcomingAsync();
        Assert.Equal(new[] { "sooner", "later" }, up.Select(m => m.Title));
    }

    [Fact]
    public async Task Meeting_during_DST_gap_is_rejected_with_a_clear_message()
    {
        using var t = new TestDb();
        await t.Settings.SaveAsync(new AppSettingsData { HomeTimeZoneId = "Europe/London" });
        var c = (await t.Clients.GetAllAsync()).Single();
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            t.Meetings.CreateAsync(new MeetingInput("x", c.Id, new DateOnly(2026, 3, 29), new TimeOnly(1, 30), 0)));
        Assert.Contains("does not exist", ex.Message);
    }
}

public class AlarmSchedulerTests
{
    static async Task<(TestDb t, Meeting m)> Setup(int reminderMinutes = 10)
    {
        var t = new TestDb();
        var c = (await t.Clients.GetAllAsync()).Single();
        // Meeting at 12:00 UTC on 12 Nov 2026; clock starts 11 Nov 07:00 UTC.
        var m = await t.Meetings.CreateAsync(new MeetingInput("Call", c.Id, new DateOnly(2026, 11, 12), new TimeOnly(17, 0), reminderMinutes));
        return (t, m);
    }

    [Fact]
    public async Task Alarm_fires_at_reminder_time_not_before()
    {
        var (t, m) = await Setup(10);
        using var _ = t;
        t.Clock.UtcNow = m.StartUtc.AddMinutes(-11);
        Assert.Empty(await t.Scheduler.CheckAsync());
        t.Clock.UtcNow = m.StartUtc.AddMinutes(-10);
        var fired = Assert.Single(await t.Scheduler.CheckAsync());
        Assert.Equal("Call", fired.Title);
        Assert.Equal("London", fired.ClientCity);
        Assert.Empty(await t.Scheduler.CheckAsync()); // does not fire twice
    }

    [Fact]
    public async Task Zero_minute_reminder_fires_at_start()
    {
        var (t, m) = await Setup(0);
        using var _ = t;
        t.Clock.UtcNow = m.StartUtc.AddSeconds(-1);
        Assert.Empty(await t.Scheduler.CheckAsync());
        t.Clock.UtcNow = m.StartUtc;
        Assert.Single(await t.Scheduler.CheckAsync());
    }

    [Fact]
    public async Task Alarm_raises_event_without_any_window()
    {
        var (t, m) = await Setup(5);
        using var _ = t;
        AlarmInfo? seen = null;
        t.Scheduler.AlarmRaised += a => seen = a;
        t.Clock.UtcNow = m.StartUtc.AddMinutes(-5);
        await t.Scheduler.CheckAsync();
        Assert.NotNull(seen);
    }

    [Fact]
    public async Task Snooze_refires_later_and_dismiss_stops_it()
    {
        var (t, m) = await Setup(10);
        using var _ = t;
        t.Clock.UtcNow = m.StartUtc.AddMinutes(-10);
        var a = (await t.Scheduler.CheckAsync()).Single();

        await t.Scheduler.SnoozeAsync(a.ReminderId, 5);
        t.Clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Empty(await t.Scheduler.CheckAsync());
        t.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Single(await t.Scheduler.CheckAsync());

        await t.Scheduler.DismissAsync(a.ReminderId);
        t.Clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Empty(await t.Scheduler.CheckAsync());
    }

    [Fact]
    public async Task Cancelled_meeting_never_alarms()
    {
        var (t, m) = await Setup(10);
        using var _ = t;
        await t.Meetings.CancelAsync(m.Id);
        t.Clock.UtcNow = m.StartUtc;
        Assert.Empty(await t.Scheduler.CheckAsync());
    }

    [Fact]
    public async Task Long_overdue_reminders_are_marked_missed_not_fired()
    {
        var (t, m) = await Setup(10);
        using var _ = t;
        t.Clock.UtcNow = m.StartUtc.AddHours(2);
        Assert.Empty(await t.Scheduler.CheckAsync());
        var got = await t.Meetings.GetAsync(m.Id);
        Assert.Equal(ReminderStatus.Missed, got!.Reminders.Single().Status);
        Assert.Equal(MeetingStatus.Completed, got.Status);
    }

    [Fact]
    public async Task Unacknowledged_alarm_is_rearmed_after_restart()
    {
        var (t, m) = await Setup(10);
        using var _ = t;
        t.Clock.UtcNow = m.StartUtc.AddMinutes(-10);
        Assert.Single(await t.Scheduler.CheckAsync()); // shown, never dismissed
        await t.Scheduler.RecoverAsync();              // app restarts
        Assert.Single(await t.Scheduler.CheckAsync());
    }

    [Fact]
    public async Task Background_loop_raises_alarms_on_its_own()
    {
        var (t, m) = await Setup(5);
        using var _ = t;
        var tcs = new TaskCompletionSource<AlarmInfo>();
        t.Scheduler.AlarmRaised += a => tcs.TrySetResult(a);
        t.Clock.UtcNow = m.StartUtc.AddMinutes(-5);
        await t.Scheduler.StartAsync();
        var done = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        t.Scheduler.Dispose();
        Assert.Same(tcs.Task, done);
    }
}
