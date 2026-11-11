using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimeBridge.Core.Models;

namespace TimeBridge.Core.Data;

public class TimeBridgeDbContext : DbContext
{
    public TimeBridgeDbContext(DbContextOptions<TimeBridgeDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ScheduleItem> ScheduleItems => Set<ScheduleItem>();
    public DbSet<WidgetState> WidgetState => Set<WidgetState>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // All instants are stored as UTC.
        b.Properties<DateTime>().HaveConversion<UtcConverter>();
        b.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Client>(e =>
        {
            e.ToTable("Clients");
            e.Property(x => x.Name).IsRequired().HasMaxLength(120);
            e.Property(x => x.City).IsRequired().HasMaxLength(120);
            e.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.SortOrder);
        });

        m.Entity<Meeting>(e =>
        {
            e.ToTable("Meetings");
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => x.StartUtc);
            e.HasOne(x => x.Client).WithMany(c => c.Meetings)
                .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Reminder>(e =>
        {
            e.ToTable("Reminders");
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.Status, x.TriggerUtc });
            e.HasOne(x => x.Meeting).WithMany(x => x.Reminders)
                .HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<ScheduleItem>(e =>
        {
            e.ToTable("ScheduleItems");
            e.Property(x => x.Kind).IsRequired().HasMaxLength(40);
            e.Property(x => x.Name).IsRequired().HasMaxLength(120);
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.Category).HasConversion<int>();
            e.Property(x => x.Mode).HasConversion<int>();
            e.HasIndex(x => new { x.Category, x.SortOrder });
        });

        m.Entity<AppSetting>(e =>
        {
            e.ToTable("AppSettings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).IsRequired();
        });

        m.Entity<WidgetState>(e =>
        {
            e.ToTable("WidgetState");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne(x => x.SelectedClient).WithMany()
                .HasForeignKey(x => x.SelectedClientId).OnDelete(DeleteBehavior.SetNull);
            e.HasData(new WidgetState { Id = 1 });
        });
    }
}

public class UtcConverter : ValueConverter<DateTime, DateTime>
{
    public UtcConverter() : base(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
}

public class NullableUtcConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcConverter() : base(
        v => v.HasValue ? v.Value.ToUniversalTime() : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v) { }
}
