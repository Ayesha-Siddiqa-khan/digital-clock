namespace TimeBridge.Core.Services;

public enum PrayerMethod
{
    Karachi = 0,            // University of Islamic Sciences, Karachi (Fajr 18, Isha 18)
    MuslimWorldLeague = 1,  // Fajr 18, Isha 17
    NorthAmerica = 2,       // ISNA: Fajr 15, Isha 15
    Egypt = 3,              // Egyptian General Authority: Fajr 19.5, Isha 17.5
    Makkah = 4              // Umm al-Qura: Fajr 18.5, Isha 90 min after Maghrib
}

public record PrayerMethodOption(PrayerMethod Method, string Label);

public record PrayerCity(string Id, string Name, string Country, double Latitude, double Longitude,
    string TimeZoneId, PrayerMethod DefaultMethod, bool Hanafi)
{
    public string Label => $"{Country} · {Name}";
}

/// <summary>Cities with coordinates for offline prayer-time calculation.</summary>
public static class PrayerCities
{
    static PrayerCity C(string id, string name, string country, double lat, double lon, string tz, PrayerMethod m, bool hanafi = false) =>
        new(id, name, country, lat, lon, tz, m, hanafi);

    public static readonly IReadOnlyList<PrayerCity> All = new[]
    {
        C("karachi", "Karachi", "Pakistan", 24.8607, 67.0011, "Asia/Karachi", PrayerMethod.Karachi, true),
        C("lahore", "Lahore", "Pakistan", 31.5204, 74.3587, "Asia/Karachi", PrayerMethod.Karachi, true),
        C("islamabad", "Islamabad", "Pakistan", 33.6844, 73.0479, "Asia/Karachi", PrayerMethod.Karachi, true),
        C("peshawar", "Peshawar", "Pakistan", 34.0151, 71.5249, "Asia/Karachi", PrayerMethod.Karachi, true),
        C("quetta", "Quetta", "Pakistan", 30.1798, 66.9750, "Asia/Karachi", PrayerMethod.Karachi, true),
        C("faisalabad", "Faisalabad", "Pakistan", 31.4504, 73.1350, "Asia/Karachi", PrayerMethod.Karachi, true),
        C("multan", "Multan", "Pakistan", 30.1575, 71.5249, "Asia/Karachi", PrayerMethod.Karachi, true),
        C("delhi", "Delhi", "India", 28.6139, 77.2090, "Asia/Kolkata", PrayerMethod.Karachi, true),
        C("dhaka", "Dhaka", "Bangladesh", 23.8103, 90.4125, "Asia/Dhaka", PrayerMethod.Karachi, true),
        C("makkah", "Makkah", "Saudi Arabia", 21.3891, 39.8579, "Asia/Riyadh", PrayerMethod.Makkah),
        C("madinah", "Madinah", "Saudi Arabia", 24.5247, 39.5692, "Asia/Riyadh", PrayerMethod.Makkah),
        C("riyadh", "Riyadh", "Saudi Arabia", 24.7136, 46.6753, "Asia/Riyadh", PrayerMethod.Makkah),
        C("dubai", "Dubai", "UAE", 25.2048, 55.2708, "Asia/Dubai", PrayerMethod.MuslimWorldLeague),
        C("doha", "Doha", "Qatar", 25.2854, 51.5310, "Asia/Qatar", PrayerMethod.Makkah),
        C("istanbul", "Istanbul", "Turkey", 41.0082, 28.9784, "Europe/Istanbul", PrayerMethod.MuslimWorldLeague),
        C("cairo", "Cairo", "Egypt", 30.0444, 31.2357, "Africa/Cairo", PrayerMethod.Egypt),
        C("london", "London", "UK", 51.5072, -0.1276, "Europe/London", PrayerMethod.MuslimWorldLeague),
        C("manchester", "Manchester", "UK", 53.4808, -2.2426, "Europe/London", PrayerMethod.MuslimWorldLeague),
        C("birmingham", "Birmingham", "UK", 52.4862, -1.8904, "Europe/London", PrayerMethod.MuslimWorldLeague),
        C("paris", "Paris", "France", 48.8566, 2.3522, "Europe/Paris", PrayerMethod.MuslimWorldLeague),
        C("berlin", "Berlin", "Germany", 52.5200, 13.4050, "Europe/Berlin", PrayerMethod.MuslimWorldLeague),
        C("amsterdam", "Amsterdam", "Netherlands", 52.3676, 4.9041, "Europe/Amsterdam", PrayerMethod.MuslimWorldLeague),
        C("madrid", "Madrid", "Spain", 40.4168, -3.7038, "Europe/Madrid", PrayerMethod.MuslimWorldLeague),
        C("rome", "Rome", "Italy", 41.9028, 12.4964, "Europe/Rome", PrayerMethod.MuslimWorldLeague),
        C("moscow", "Moscow", "Russia", 55.7558, 37.6173, "Europe/Moscow", PrayerMethod.MuslimWorldLeague),
        C("newyork", "New York", "USA", 40.7128, -74.0060, "America/New_York", PrayerMethod.NorthAmerica),
        C("chicago", "Chicago", "USA", 41.8781, -87.6298, "America/Chicago", PrayerMethod.NorthAmerica),
        C("houston", "Houston", "USA", 29.7604, -95.3698, "America/Chicago", PrayerMethod.NorthAmerica),
        C("losangeles", "Los Angeles", "USA", 34.0522, -118.2437, "America/Los_Angeles", PrayerMethod.NorthAmerica),
        C("toronto", "Toronto", "Canada", 43.6532, -79.3832, "America/Toronto", PrayerMethod.NorthAmerica),
        C("kualalumpur", "Kuala Lumpur", "Malaysia", 3.1390, 101.6869, "Asia/Kuala_Lumpur", PrayerMethod.MuslimWorldLeague),
        C("singapore", "Singapore", "Singapore", 1.3521, 103.8198, "Asia/Singapore", PrayerMethod.MuslimWorldLeague),
        C("jakarta", "Jakarta", "Indonesia", -6.2088, 106.8456, "Asia/Jakarta", PrayerMethod.MuslimWorldLeague),
        C("tokyo", "Tokyo", "Japan", 35.6762, 139.6503, "Asia/Tokyo", PrayerMethod.MuslimWorldLeague),
        C("sydney", "Sydney", "Australia", -33.8688, 151.2093, "Australia/Sydney", PrayerMethod.MuslimWorldLeague),
        C("lagos", "Lagos", "Nigeria", 6.5244, 3.3792, "Africa/Lagos", PrayerMethod.MuslimWorldLeague),
        C("nairobi", "Nairobi", "Kenya", -1.2921, 36.8219, "Africa/Nairobi", PrayerMethod.MuslimWorldLeague),
        C("johannesburg", "Johannesburg", "South Africa", -26.2041, 28.0473, "Africa/Johannesburg", PrayerMethod.MuslimWorldLeague),
    }.OrderBy(c => c.Country).ThenBy(c => c.Name).ToList();

    public static PrayerCity? Find(string? id) => All.FirstOrDefault(c => c.Id == id);
    public static PrayerCity Default => Find("karachi")!;
}

public static class PrayerMethods
{
    public static readonly IReadOnlyList<PrayerMethodOption> All = new[]
    {
        new PrayerMethodOption(PrayerMethod.Karachi, "University of Islamic Sciences, Karachi"),
        new PrayerMethodOption(PrayerMethod.MuslimWorldLeague, "Muslim World League"),
        new PrayerMethodOption(PrayerMethod.NorthAmerica, "ISNA (North America)"),
        new PrayerMethodOption(PrayerMethod.Egypt, "Egyptian General Authority"),
        new PrayerMethodOption(PrayerMethod.Makkah, "Umm al-Qura (Makkah)"),
    };

    internal static (double Fajr, double Isha, int IshaMinutes) Parameters(PrayerMethod m) => m switch
    {
        PrayerMethod.Karachi => (18, 18, 0),
        PrayerMethod.MuslimWorldLeague => (18, 17, 0),
        PrayerMethod.NorthAmerica => (15, 15, 0),
        PrayerMethod.Egypt => (19.5, 17.5, 0),
        PrayerMethod.Makkah => (18.5, 0, 90),
        _ => (18, 18, 0)
    };
}

/// <summary>Prayer times as local (city) wall-clock times, plus Sunrise and the Islamic midnight.</summary>
public record PrayerTimesResult(DateTime Fajr, DateTime Sunrise, DateTime Dhuhr, DateTime Asr,
    DateTime Maghrib, DateTime Isha, DateTime Midnight);

/// <summary>Astronomical prayer-time calculation (same method as PrayTimes.org), fully offline.</summary>
public static class PrayerTimeCalculator
{
    static double Rad(double d) => d * Math.PI / 180;
    static double Deg(double r) => r * 180 / Math.PI;
    static double Sin(double d) => Math.Sin(Rad(d));
    static double Cos(double d) => Math.Cos(Rad(d));
    static double Tan(double d) => Math.Tan(Rad(d));
    static double FixAngle(double a) { a %= 360; return a < 0 ? a + 360 : a; }
    static double FixHour(double h) { h %= 24; return h < 0 ? h + 24 : h; }

    static double Julian(int y, int m, int d)
    {
        if (m <= 2) { y -= 1; m += 12; }
        var a = Math.Floor(y / 100.0);
        var b = 2 - a + Math.Floor(a / 4);
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
    }

    static (double Decl, double Eqt) Sun(double jd)
    {
        var d = jd - 2451545.0;
        var g = FixAngle(357.529 + 0.98560028 * d);
        var q = FixAngle(280.459 + 0.98564736 * d);
        var l = FixAngle(q + 1.915 * Sin(g) + 0.020 * Sin(2 * g));
        var e = 23.439 - 0.00000036 * d;
        var ra = FixHour(Deg(Math.Atan2(Cos(e) * Sin(l), Cos(l))) / 15);
        return (Deg(Math.Asin(Sin(e) * Sin(l))), q / 15 - ra);
    }

    static double MidDay(double jd, double t) => FixHour(12 - Sun(jd + t).Eqt);

    static double AngleTime(double jd, double lat, double angle, double t, bool ccw)
    {
        var (decl, _) = Sun(jd + t);
        var noon = MidDay(jd, t);
        var x = (-Sin(angle) - Sin(decl) * Sin(lat)) / (Cos(decl) * Cos(lat));
        if (x < -1 || x > 1) return double.NaN;
        var diff = Deg(Math.Acos(x)) / 15;
        return noon + (ccw ? -diff : diff);
    }

    static double AsrTime(double jd, double lat, double factor, double t)
    {
        var (decl, _) = Sun(jd + t);
        var angle = -Deg(Math.Atan(1 / (factor + Tan(Math.Abs(lat - decl)))));
        return AngleTime(jd, lat, angle, t, false);
    }

    public static PrayerTimesResult Calculate(DateOnly date, double lat, double lon, TimeZoneInfo tz,
        PrayerMethod method, bool hanafi)
    {
        var (fajrAngle, ishaAngle, ishaMinutes) = PrayerMethods.Parameters(method);
        var jd = Julian(date.Year, date.Month, date.Day) - lon / (15 * 24);
        var offsetHours = tz.GetUtcOffset(DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Unspecified)).TotalHours;

        double fajr = 5, sunrise = 6, dhuhr = 12, asr = 13, sunset = 18, isha = 18;
        for (var i = 0; i < 2; i++)
        {
            fajr = AngleTime(jd, lat, fajrAngle, fajr / 24, true);
            sunrise = AngleTime(jd, lat, 0.833, sunrise / 24, true);
            dhuhr = MidDay(jd, dhuhr / 24);
            asr = AsrTime(jd, lat, hanafi ? 2 : 1, asr / 24);
            sunset = AngleTime(jd, lat, 0.833, sunset / 24, false);
            isha = ishaMinutes > 0 ? sunset + ishaMinutes / 60.0 : AngleTime(jd, lat, ishaAngle, isha / 24, false);
            // seed the next pass with sane values if a polar-ish day produced NaN
            if (double.IsNaN(fajr)) fajr = 5;
            if (double.IsNaN(isha)) isha = 18;
            if (double.IsNaN(sunrise)) sunrise = 6;
            if (double.IsNaN(sunset)) sunset = 18;
        }

        var shift = offsetHours - lon / 15;
        fajr += shift; sunrise += shift; dhuhr += shift; asr += shift; sunset += shift;
        if (ishaMinutes == 0) isha += shift; else isha = sunset + ishaMinutes / 60.0;

        // High latitudes: Fajr/Isha can be missing or implausibly far from sunrise/sunset (angle-based night portion).
        var night = FixHour(sunrise - sunset);
        if (ishaMinutes == 0)
        {
            var fajrDiff = fajrAngle / 60 * night;
            if (double.IsNaN(fajr) || FixHour(sunrise - fajr) > fajrDiff) fajr = sunrise - fajrDiff;
            var ishaDiff = ishaAngle / 60 * night;
            if (double.IsNaN(isha) || FixHour(isha - sunset) > ishaDiff) isha = sunset + ishaDiff;
        }

        var midnight = sunset + night / 2;
        var day = date.ToDateTime(TimeOnly.MinValue);
        DateTime At(double h) => day.AddHours(h);
        var ishaDt = At(isha);
        if (ishaDt < At(sunset)) ishaDt = ishaDt.AddDays(1);
        return new PrayerTimesResult(At(fajr), At(sunrise), At(dhuhr), At(asr), At(sunset), ishaDt, At(midnight));
    }

    public static PrayerTimesResult Calculate(DateOnly date, PrayerCity city, PrayerMethod method, bool hanafi) =>
        Calculate(date, city.Latitude, city.Longitude, TimeZoneCatalog.Find(city.TimeZoneId), method, hanafi);
}
