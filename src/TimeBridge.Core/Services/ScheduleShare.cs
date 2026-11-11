using System.Globalization;
using System.Text;
using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

/// <summary>Builds the plain-text day schedule that is shared (e.g. over WhatsApp) so clients know when the user is busy.
/// When a target time zone is given, the times are shown in the TARGET's local time (the reader never has to convert),
/// with the sender's home time in brackets.</summary>
public static class ScheduleShare
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="targetZoneId">Time zone of the reader (null = share in home time only).</param>
    /// <param name="targetName">Place name shown to the reader, e.g. "London".</param>
    public static string Build(DateOnly date, AppSettingsData s, IEnumerable<ScheduleItem> items,
        bool includePrayers, bool includeRoutine, string? targetZoneId = null, string? targetName = null)
    {
        var plan = SchedulePlanner.Build(date, s, items)
            .Where(e => e.Kind != "Sunrise" &&
                        ((e.Category == PlanCategory.Prayer && includePrayers) ||
                         (e.Category == PlanCategory.Routine && includeRoutine)))
            .ToList();

        var homeId = s.HomeTimeZoneId;
        var homeName = homeId == TimeZoneCatalog.PakistanId ? "Pakistan" : TimeZoneCatalog.ShortName(homeId);
        var hasTarget = TimeZoneCatalog.TryFind(targetZoneId) != null;
        var noonUtc = TimeConversion.ToUtc(date, new TimeOnly(12, 0), homeId);
        var dayText = $"{date.ToDateTime(TimeOnly.MinValue).ToString("dddd", Inv)}, {TimeFormats.FormatDate(date.ToDateTime(TimeOnly.MinValue))}";

        var sb = new StringBuilder();
        if (hasTarget)
        {
            var name = string.IsNullOrWhiteSpace(targetName) ? TimeZoneCatalog.ShortName(targetZoneId!) : targetName;
            sb.AppendLine($"📅 My schedule · {dayText} ({homeName} date)");
            sb.AppendLine($"Times are in {name} time ({TimeConversion.FromUtc(noonUtc, targetZoneId!).OffsetText}). {homeName} time is in brackets.");
        }
        else
        {
            sb.AppendLine($"📅 My schedule · {dayText}");
            sb.AppendLine($"Times are in {homeName} time ({TimeConversion.FromUtc(noonUtc, homeId).OffsetText})");
        }
        sb.AppendLine();

        if (plan.Count == 0)
        {
            sb.AppendLine("Nothing scheduled.");
            return sb.ToString().TrimEnd();
        }

        foreach (var e in plan)
        {
            var emoji = ScheduleKinds.Emoji(e.Kind);
            var prayer = e.Category == PlanCategory.Prayer;
            var homeText = Range(e.StartUtc, prayer ? null : e.EndUtc, homeId, null);
            if (!hasTarget)
            {
                sb.AppendLine(prayer ? $"{emoji} {e.Title} from {homeText}" : $"{emoji} {e.Title}: {homeText}");
                continue;
            }
            var homeStartDate = TimeConversion.FromUtc(e.StartUtc, homeId).Local.Date;
            var targetText = Range(e.StartUtc, prayer ? null : e.EndUtc, targetZoneId!, homeStartDate);
            sb.AppendLine(prayer
                ? $"{emoji} {e.Title} from {targetText}  ({homeText} {homeName})"
                : $"{emoji} {e.Title}: {targetText}  ({homeText} {homeName})");
        }

        sb.AppendLine();
        sb.Append("I may be unavailable during these times. Sent from TimeBridge");
        return sb.ToString();
    }

    /// <summary>"01:00 PM – 02:00 PM", flagged when the start falls on a different date than <paramref name="compareDate"/>.</summary>
    static string Range(DateTime startUtc, DateTime? endUtc, string zoneId, DateTime? compareDate)
    {
        var start = TimeConversion.FromUtc(startUtc, zoneId).Local;
        var text = TimeFormats.FormatTime(start);
        if (endUtc is { } eu) text += " – " + TimeFormats.FormatTime(TimeConversion.FromUtc(eu, zoneId).Local);
        if (compareDate is { } cd && start.Date != cd)
            text += start.Date > cd ? " (next day)" : " (previous day)";
        return text;
    }
}
