namespace TimeBridge.Core.Services;

public record ZoneOption(string Id, string Label, string Group);

/// <summary>Curated common zones (UK, USA, Europe, Asia-Pacific, ...) plus every zone the OS knows.</summary>
public static class TimeZoneCatalog
{
    public const string PakistanId = "Asia/Karachi";

    static readonly (string Id, string Label, string Group)[] Curated =
    {
        ("Asia/Karachi", "Pakistan · Karachi", "Pakistan"),
        ("Europe/London", "UK · London", "UK"),
        ("America/New_York", "USA · New York (Eastern)", "USA"),
        ("America/Chicago", "USA · Chicago (Central)", "USA"),
        ("America/Denver", "USA · Denver (Mountain)", "USA"),
        ("America/Phoenix", "USA · Phoenix (no DST)", "USA"),
        ("America/Los_Angeles", "USA · Los Angeles (Pacific)", "USA"),
        ("America/Anchorage", "USA · Anchorage", "USA"),
        ("Pacific/Honolulu", "USA · Honolulu", "USA"),
        ("America/Toronto", "Canada · Toronto", "Americas"),
        ("America/Vancouver", "Canada · Vancouver", "Americas"),
        ("Europe/Dublin", "Europe · Dublin", "Europe"),
        ("Europe/Paris", "Europe · Paris", "Europe"),
        ("Europe/Berlin", "Europe · Berlin", "Europe"),
        ("Europe/Amsterdam", "Europe · Amsterdam", "Europe"),
        ("Europe/Madrid", "Europe · Madrid", "Europe"),
        ("Europe/Rome", "Europe · Rome", "Europe"),
        ("Europe/Zurich", "Europe · Zurich", "Europe"),
        ("Europe/Stockholm", "Europe · Stockholm", "Europe"),
        ("Europe/Athens", "Europe · Athens", "Europe"),
        ("Europe/Istanbul", "Europe · Istanbul", "Europe"),
        ("Europe/Moscow", "Europe · Moscow", "Europe"),
        ("Asia/Dubai", "UAE · Dubai", "Middle East"),
        ("Asia/Riyadh", "Saudi Arabia · Riyadh", "Middle East"),
        ("Asia/Kolkata", "India · Kolkata", "Asia-Pacific"),
        ("Asia/Dhaka", "Bangladesh · Dhaka", "Asia-Pacific"),
        ("Asia/Singapore", "Singapore", "Asia-Pacific"),
        ("Asia/Hong_Kong", "Hong Kong", "Asia-Pacific"),
        ("Asia/Shanghai", "China · Shanghai", "Asia-Pacific"),
        ("Asia/Tokyo", "Japan · Tokyo", "Asia-Pacific"),
        ("Asia/Seoul", "South Korea · Seoul", "Asia-Pacific"),
        ("Australia/Sydney", "Australia · Sydney", "Asia-Pacific"),
        ("Australia/Perth", "Australia · Perth", "Asia-Pacific"),
        ("Pacific/Auckland", "New Zealand · Auckland", "Asia-Pacific"),
        ("Africa/Johannesburg", "South Africa · Johannesburg", "Africa"),
        ("Africa/Cairo", "Egypt · Cairo", "Africa"),
        ("Africa/Lagos", "Nigeria · Lagos", "Africa"),
        ("Africa/Nairobi", "Kenya · Nairobi", "Africa"),
        ("America/Sao_Paulo", "Brazil · Sao Paulo", "Americas"),
        ("America/Mexico_City", "Mexico · Mexico City", "Americas"),
        ("UTC", "UTC", "Other"),
    };

    /// <summary>Resolves an IANA or Windows identifier. Returns null when unknown.</summary>
    public static TimeZoneInfo? TryFind(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }

    public static TimeZoneInfo Find(string id) =>
        TryFind(id) ?? throw new ArgumentException($"Unknown time zone '{id}'.", nameof(id));

    public static IReadOnlyList<ZoneOption> All()
    {
        var list = new List<ZoneOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in Curated)
            if (TryFind(c.Id) != null && seen.Add(c.Id)) list.Add(new ZoneOption(c.Id, c.Label, c.Group));
        foreach (var tz in TimeZoneInfo.GetSystemTimeZones().OrderBy(z => z.BaseUtcOffset).ThenBy(z => z.Id))
            if (seen.Add(tz.Id)) list.Add(new ZoneOption(tz.Id, $"{tz.Id} ({tz.DisplayName.Split(')')[0].TrimStart('(')})", "All zones"));
        return list;
    }

    /// <summary>Short place name, e.g. "Karachi", "London", "New York".</summary>
    public static string ShortName(string id)
    {
        var label = LabelFor(id);
        var i = label.IndexOf(" · ", StringComparison.Ordinal);
        if (i >= 0)
        {
            label = label[(i + 3)..];
            var p = label.IndexOf(" (", StringComparison.Ordinal);
            return p >= 0 ? label[..p] : label;
        }
        var slash = id.LastIndexOf('/');
        return (slash >= 0 ? id[(slash + 1)..] : id).Replace('_', ' ');
    }

    public static string LabelFor(string id) =>
        Curated.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)).Label ?? id;
}
