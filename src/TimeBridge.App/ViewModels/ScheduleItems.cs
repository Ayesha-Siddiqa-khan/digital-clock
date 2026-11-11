using CommunityToolkit.Mvvm.ComponentModel;
using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

/// <summary>Seven day toggles (Mon..Sun) backed by the ScheduleItem.DaysMask bit field.</summary>
public partial class DaysEditor : ObservableObject
{
    [ObservableProperty] bool _mon = true;
    [ObservableProperty] bool _tue = true;
    [ObservableProperty] bool _wed = true;
    [ObservableProperty] bool _thu = true;
    [ObservableProperty] bool _fri = true;
    [ObservableProperty] bool _sat = true;
    [ObservableProperty] bool _sun = true;

    public int Mask
    {
        get => (Mon ? 1 : 0) | (Tue ? 2 : 0) | (Wed ? 4 : 0) | (Thu ? 8 : 0) | (Fri ? 16 : 0) | (Sat ? 32 : 0) | (Sun ? 64 : 0);
        set { Mon = (value & 1) != 0; Tue = (value & 2) != 0; Wed = (value & 4) != 0; Thu = (value & 8) != 0; Fri = (value & 16) != 0; Sat = (value & 32) != 0; Sun = (value & 64) != 0; }
    }

    static readonly string[] Names = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public static string Describe(int mask)
    {
        mask &= 127;
        if (mask == 127) return "Every day";
        if (mask == 0b0011111) return "Mon–Fri";
        if (mask == 0b1100000) return "Sat–Sun";
        return string.Join(", ", Enumerable.Range(0, 7).Where(i => (mask & (1 << i)) != 0).Select(i => Names[i]));
    }
}

public record AsrChoice(bool Hanafi, string Label);

public record ShareTarget(string Key, string Label, string? ZoneId, string? Name, string Group);

public record ShareDayChoice(int Offset, string Label);

public record IntervalChoice(int Minutes, string Label);

public static class Minutes
{
    public static TimeOnly ToTime(int minute) => new(minute / 60 % 24, minute % 60);
    public static int FromTime(TimeOnly t) => t.Hour * 60 + t.Minute;
    public static string Format(int minute) => TimeFormats.FormatTime(ToTime(minute));
}

/// <summary>One prayer in the setup table; edits notify the owner so settings are saved.</summary>
public partial class PrayerRow : ObservableObject
{
    readonly Action _changed;
    public PrayerRow(int index, string name, Action changed) { Index = index; Name = name; _changed = changed; }

    public int Index { get; }
    public string Name { get; }
    public static IReadOnlyList<int> OffsetChoices { get; } = Enumerable.Range(-15, 31).ToList();

    [ObservableProperty] string _startText = "";
    [ObservableProperty] string _endText = "";
    [ObservableProperty] string _homeText = "";
    [ObservableProperty] bool _alertEnabled = true;
    [ObservableProperty] int _offset;
    [ObservableProperty] bool _isNow;

    partial void OnAlertEnabledChanged(bool value) => _changed();
    partial void OnOffsetChanged(int value) => _changed();
}

public class TimelineItem
{
    public string Emoji { get; init; } = "";
    public string Title { get; init; } = "";
    public string TimeText { get; init; } = "";
    public string Tag { get; init; } = "";
    public bool IsNow { get; init; }
    public bool IsNext { get; init; }
}

/// <summary>A routine block or health reminder row in a list.</summary>
public class ScheduleRow
{
    public required ScheduleItem Item { get; init; }
    public int Id => Item.Id;
    public string Name => Item.Name;
    public string Emoji => ScheduleKinds.Emoji(Item.Kind);
    public bool IsEnabled { get; set; }
    public string WhenText { get; init; } = "";
    public string DaysText { get; init; } = "";

    public static ScheduleRow From(ScheduleItem i)
    {
        string when;
        if (i.Category == ScheduleCategory.Routine)
            when = $"{Minutes.Format(i.StartMinute)} – {Minutes.Format(i.EndMinute ?? i.StartMinute)}";
        else if (i.Mode == ScheduleMode.Fixed)
            when = $"At {Minutes.Format(i.StartMinute)}";
        else
            when = $"Every {i.IntervalMinutes} min · {Minutes.Format(i.StartMinute)} – {Minutes.Format(i.EndMinute ?? i.StartMinute)}";
        return new ScheduleRow { Item = i, IsEnabled = i.IsEnabled, WhenText = when, DaysText = DaysEditor.Describe(i.DaysMask) };
    }
}
