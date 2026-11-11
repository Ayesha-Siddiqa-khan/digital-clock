using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

public enum PlanCategory { Prayer, Routine, Health }

/// <summary>One occurrence on the day plan. <see cref="AlertUtc"/> is null when no alert is wanted.</summary>
public record PlanEntry(string Key, string Title, string Kind, PlanCategory Category,
    DateTime StartUtc, DateTime? EndUtc, DateTime? AlertUtc, string Message);

public static class SchedulePlanner
{
    public static readonly string[] PrayerNames = { "Fajr", "Dhuhr", "Asr", "Maghrib", "Isha" };

    public static bool RunsOn(int daysMask, DateOnly date) => (daysMask & (1 << (((int)date.DayOfWeek + 6) % 7))) != 0;

    public static PrayerCity CityOf(AppSettingsData s) => PrayerCities.Find(s.PrayerCityId) ?? PrayerCities.Default;

    public static PrayerTimesResult Prayers(DateOnly cityDate, AppSettingsData s) =>
        PrayerTimeCalculator.Calculate(cityDate, CityOf(s), s.PrayerMethod, s.AsrHanafi);

    /// <summary>Everything scheduled for one calendar date. Prayers use the selected city's calendar and
    /// time zone; routine and health items use the home time zone.</summary>
    public static List<PlanEntry> Build(DateOnly date, AppSettingsData s, IEnumerable<ScheduleItem> items)
    {
        var list = new List<PlanEntry>();
        AddPrayers(list, date, s);
        foreach (var item in items.Where(i => i.IsEnabled && RunsOn(i.DaysMask, date)))
            AddItem(list, date, s, item);
        return list.OrderBy(e => e.StartUtc).ToList();
    }

    static void AddPrayers(List<PlanEntry> list, DateOnly date, AppSettingsData s)
    {
        var city = CityOf(s);
        var t = Prayers(date, s);
        var starts = new[] { t.Fajr, t.Dhuhr, t.Asr, t.Maghrib, t.Isha };
        for (var i = 0; i < 5; i++) starts[i] = starts[i].AddMinutes(s.PrayerOffsets.ElementAtOrDefault(i));
        var ends = new[] { t.Sunrise, starts[2], starts[3], starts[4], t.Midnight };

        DateTime? Utc(DateTime local)
        {
            try { return TimeConversion.ToUtc(local, city.TimeZoneId); }
            catch (InvalidLocalTimeException) { return null; }
        }

        if (Utc(t.Sunrise) is { } sr)
            list.Add(new PlanEntry($"sunrise:{date:yyyyMMdd}", "Sunrise", "Sunrise", PlanCategory.Prayer, sr, null, null, "Sunrise"));

        for (var i = 0; i < 5; i++)
        {
            if (Utc(starts[i]) is not { } start) continue;
            var end = Utc(ends[i]);
            DateTime? alert = s.PrayerAlertEnabled && (s.PrayerAlertMask & (1 << i)) != 0
                ? start.AddMinutes(-s.PrayerAlertMinutesBefore) : null;
            var msg = s.PrayerAlertMinutesBefore == 0
                ? $"It is time for {PrayerNames[i]} ({city.Name})."
                : $"{PrayerNames[i]} is in {s.PrayerAlertMinutesBefore} minutes ({city.Name}).";
            list.Add(new PlanEntry($"prayer:{i}:{date:yyyyMMdd}", PrayerNames[i], PrayerNames[i], PlanCategory.Prayer, start, end, alert, msg));
        }
    }

    static void AddItem(List<PlanEntry> list, DateOnly date, AppSettingsData s, ScheduleItem item)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        DateTime? Utc(int minute)
        {
            try { return TimeConversion.ToUtc(day.AddMinutes(minute), s.HomeTimeZoneId); }
            catch (InvalidLocalTimeException) { return null; }
        }

        var kind = (item.Category == ScheduleCategory.Routine ? ScheduleKinds.Routine : ScheduleKinds.Health)
            .FirstOrDefault(k => k.Id == item.Kind);
        var message = string.IsNullOrWhiteSpace(item.Message) ? kind?.DefaultMessage ?? item.Name : item.Message!;
        var category = item.Category == ScheduleCategory.Routine ? PlanCategory.Routine : PlanCategory.Health;

        if (item.Category == ScheduleCategory.Routine || item.Mode == ScheduleMode.Fixed)
        {
            if (Utc(item.StartMinute) is not { } start) return;
            DateTime? end = null;
            if (item.Category == ScheduleCategory.Routine && item.EndMinute is int em)
                end = Utc(em <= item.StartMinute ? em + 1440 : em);
            list.Add(new PlanEntry($"item:{item.Id}:{date:yyyyMMdd}:{item.StartMinute}", item.Name, item.Kind, category, start, end, start, message));
            return;
        }

        // Repeating reminder: first one a full interval after the window opens, then every interval until it closes.
        var interval = Math.Max(5, item.IntervalMinutes ?? 60);
        var windowEnd = item.EndMinute ?? item.StartMinute + 480;
        if (windowEnd <= item.StartMinute) windowEnd += 1440;
        for (var m = item.StartMinute + interval; m <= windowEnd; m += interval)
            if (Utc(m) is { } at)
                list.Add(new PlanEntry($"item:{item.Id}:{date:yyyyMMdd}:{m}", item.Name, item.Kind, category, at, null, at, message));
    }
}
