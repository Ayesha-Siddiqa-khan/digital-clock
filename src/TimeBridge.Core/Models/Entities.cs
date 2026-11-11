namespace TimeBridge.Core.Models;

public enum MeetingStatus { Scheduled = 0, Cancelled = 1, Completed = 2 }

public enum ReminderStatus { Pending = 0, Fired = 1, Snoozed = 2, Dismissed = 3, Missed = 4 }

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    /// <summary>IANA identifier (e.g. Europe/London). Windows identifiers are also accepted.</summary>
    public string TimeZoneId { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public List<Meeting> Meetings { get; set; } = new();
}

public class Meeting
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public DateTime StartUtc { get; set; }
    public string? Notes { get; set; }
    public MeetingStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<Reminder> Reminders { get; set; } = new();
}

public class Reminder
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public int MinutesBefore { get; set; }
    public DateTime TriggerUtc { get; set; }
    public DateTime? SnoozedUntilUtc { get; set; }
    public DateTime? FiredUtc { get; set; }
    public ReminderStatus Status { get; set; }
}

public enum ScheduleCategory { Routine = 0, Health = 1 }

public enum ScheduleMode { Fixed = 0, Interval = 1 }

/// <summary>A routine block (sleep, rest, lunch, ...) or a health reminder (water, walk, screen break, ...).
/// Times are minutes after midnight in the user's home time zone.</summary>
public class ScheduleItem
{
    public int Id { get; set; }
    public ScheduleCategory Category { get; set; }
    public string Kind { get; set; } = "Custom";
    public string Name { get; set; } = "";
    public string? Message { get; set; }
    public ScheduleMode Mode { get; set; }
    public int StartMinute { get; set; }
    /// <summary>Routine: when the block ends. Interval reminders: end of the active window.</summary>
    public int? EndMinute { get; set; }
    public int? IntervalMinutes { get; set; }
    /// <summary>Bit 0 = Monday ... bit 6 = Sunday.</summary>
    public int DaysMask { get; set; } = 127;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>Single-row table (Id = 1).</summary>
public class WidgetState
{
    public int Id { get; set; } = 1;
    public double? Left { get; set; }
    public double? Top { get; set; }
    /// <summary>Expanded widget width in DIPs; height follows the fixed aspect ratio.</summary>
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 326;
    public bool IsPinned { get; set; } = true;
    public bool IsCollapsed { get; set; }
    public bool IsVisible { get; set; } = true;
    public int? SelectedClientId { get; set; }
    public Client? SelectedClient { get; set; }
}
