using System.Globalization;

namespace TimeBridge.Core.Services;

/// <summary>Single place for the app's display formats: DD-MM-YYYY dates and 12-hour AM/PM times.</summary>
public static class TimeFormats
{
    public const string DateFormat = "dd-MM-yyyy";
    public const string TimeFormat = "hh:mm tt";
    public const string TimeFormatSeconds = "hh:mm:ss tt";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] TimeInputFormats = { "hh:mm tt", "h:mm tt", "hh:mmtt", "h:mmtt" };

    public static string FormatDate(DateTime dt) => dt.ToString(DateFormat, Inv);
    public static string FormatTime(DateTime dt, bool seconds = false) =>
        dt.ToString(seconds ? TimeFormatSeconds : TimeFormat, Inv);
    public static string FormatDateTime(DateTime dt) => $"{FormatDate(dt)} {FormatTime(dt)}";
    public static string FormatTime(TimeOnly t) => t.ToString(TimeFormat, Inv);

    public static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text?.Trim(), DateFormat, Inv, DateTimeStyles.None, out date);

    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text?.Trim(), TimeInputFormats, Inv, DateTimeStyles.None, out time);

    /// <summary>Big-clock display split into "7:03:00" and "AM" (no leading zero on the hour).</summary>
    public static (string Time, string AmPm) FormatClock(DateTime local, bool seconds) =>
        (local.ToString(seconds ? "h:mm:ss" : "h:mm", Inv), local.ToString("tt", Inv));

    public static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        return offset.Minutes == 0 ? $"UTC{sign}{offset.Hours}" : $"UTC{sign}{offset.Hours}:{offset.Minutes:00}";
    }

    public static string FormatCountdown(TimeSpan span)
    {
        if (span <= TimeSpan.Zero) return "now";
        if (span.TotalDays >= 1) return $"in {(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"in {(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1) return $"in {(int)span.TotalMinutes}m";
        return "in <1m";
    }
}
