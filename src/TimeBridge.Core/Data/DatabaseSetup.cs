using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.Core.Data;

public static class DatabaseSetup
{
    /// <summary>%LocalAppData%\TimeBridge\timebridge.db</summary>
    public static string DefaultDatabasePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeBridge");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "timebridge.db");
    }

    public static string ConnectionString(string path) => $"Data Source={path};Foreign Keys=True";

    public static DbContextOptions<TimeBridgeDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<TimeBridgeDbContext>().UseSqlite(connectionString).Options;

    /// <summary>Applies EF Core migrations and seeds first-run data.</summary>
    public static async Task InitializeAsync(IDbContextFactory<TimeBridgeDbContext> factory, IClock clock)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();

        if (!await db.AppSettings.AnyAsync(s => s.Key == SettingKeys.Seeded))
        {
            if (!await db.Clients.AnyAsync())
            {
                var uk = new Client
                {
                    Name = "UK client", City = "London", TimeZoneId = "Europe/London",
                    SortOrder = 0, CreatedUtc = clock.UtcNow
                };
                db.Clients.Add(uk);
                await db.SaveChangesAsync();
                var ws = await db.WidgetState.FirstAsync(w => w.Id == 1);
                ws.SelectedClientId = uk.Id;
            }
            db.AppSettings.Add(new AppSetting { Key = SettingKeys.Seeded, Value = "1" });
            await db.SaveChangesAsync();
        }
    }
}

/// <summary>Used by <c>dotnet ef</c> at design time.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<TimeBridgeDbContext>
{
    public TimeBridgeDbContext CreateDbContext(string[] args) =>
        new(DatabaseSetup.Options("Data Source=design-time.db"));
}
