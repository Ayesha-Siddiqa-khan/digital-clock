using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TimeBridge.Core.Data;
using TimeBridge.Core.Services;

namespace TimeBridge.Tests;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 11, 7, 0, 0, DateTimeKind.Utc);
    public void Advance(TimeSpan t) => UtcNow += t;
}

/// <summary>Real SQLite (in-memory, kept open) with the real EF migrations applied.</summary>
public sealed class TestDb : IDbContextFactory<TimeBridgeDbContext>, IDisposable
{
    readonly SqliteConnection _conn = new("Data Source=:memory:;Foreign Keys=True");
    public FakeClock Clock { get; } = new();
    public SettingsService Settings { get; }
    public ClientService Clients { get; }
    public MeetingService Meetings { get; }
    public AlarmScheduler Scheduler { get; }
    public ScheduleService Schedule { get; }
    public ReminderEngine Engine { get; }

    public TestDb()
    {
        _conn.Open();
        DatabaseSetup.InitializeAsync(this, Clock).GetAwaiter().GetResult();
        Settings = new SettingsService(this);
        Settings.LoadAsync().GetAwaiter().GetResult();
        Clients = new ClientService(this, Clock);
        Meetings = new MeetingService(this, Clock, Settings);
        Scheduler = new AlarmScheduler(this, Clock, Settings);
        Schedule = new ScheduleService(this);
        Engine = new ReminderEngine(Clock, Settings, Schedule);
    }

    public TimeBridgeDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TimeBridgeDbContext>().UseSqlite(_conn).Options);

    public void Dispose() => _conn.Dispose();
}
