namespace TimeBridge.Core.Services;

public class InvalidLocalTimeException : Exception
{
    public InvalidLocalTimeException(string message) : base(message) { }
}

public record ZonedMoment(DateTime Local, TimeSpan Offset, string TimeZoneId, bool IsDaylightSaving)
{
    public string OffsetText => TimeFormats.FormatOffset(Offset);
}

public static class TimeConversion
{
    /// <summary>Converts a wall-clock time in <paramref name="zoneId"/> to a UTC instant.
    /// Throws if the time does not exist (DST spring-forward gap). Ambiguous times resolve to standard time.</summary>
    public static DateTime ToUtc(DateTime wallClock, string zoneId)
    {
        var tz = TimeZoneCatalog.Find(zoneId);
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(unspecified))
            throw new InvalidLocalTimeException(
                $"{TimeFormats.FormatDateTime(unspecified)} does not exist in {tz.Id} because clocks move forward that day. Choose a different time.");
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    public static DateTime ToUtc(DateOnly date, TimeOnly time, string zoneId) => ToUtc(date.ToDateTime(time), zoneId);

    public static ZonedMoment FromUtc(DateTime utc, string zoneId)
    {
        var tz = TimeZoneCatalog.Find(zoneId);
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        return new ZonedMoment(local, tz.GetUtcOffset(utc), tz.Id, tz.IsDaylightSavingTime(utc));
    }

    /// <summary>Wall-clock time in one zone expressed in another.</summary>
    public static ZonedMoment Convert(DateTime wallClock, string fromZoneId, string toZoneId) =>
        FromUtc(ToUtc(wallClock, fromZoneId), toZoneId);
}
