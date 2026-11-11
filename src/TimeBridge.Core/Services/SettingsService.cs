using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TimeBridge.Core.Data;
using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

public static class SettingKeys
{
    public const string Seeded = "Seeded";
    public const string HomeTimeZoneId = "HomeTimeZoneId";
    public const string ShowSeconds = "ShowSeconds";
    public const string WidgetOpacity = "WidgetOpacity";
    public const string DefaultReminderMinutes = "DefaultReminderMinutes";
    public const string SnoozeMinutes = "SnoozeMinutes";
    public const string AlarmSoundEnabled = "AlarmSoundEnabled";
    public const string StartWithWindows = "StartWithWindows";
    public const string PrayerCityId = "PrayerCityId";
    public const string PrayerMethod = "PrayerMethod";
    public const string AsrHanafi = "AsrHanafi";
    public const string PrayerAlertEnabled = "PrayerAlertEnabled";
    public const string PrayerAlertMinutes = "PrayerAlertMinutes";
    public const string PrayerAlertMask = "PrayerAlertMask";
    public const string PrayerOffsets = "PrayerOffsets";
}

public class AppSettingsData
{
    public string HomeTimeZoneId { get; set; } = TimeZoneCatalog.PakistanId;
    public bool ShowSeconds { get; set; } = true;
    public double WidgetOpacity { get; set; } = 0.96;
    public int DefaultReminderMinutes { get; set; } = 10;
    public int SnoozeMinutes { get; set; } = 5;
    public bool AlarmSoundEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }

    // Prayer times (indexes 0..4 = Fajr, Dhuhr, Asr, Maghrib, Isha)
    public string PrayerCityId { get; set; } = "karachi";
    public PrayerMethod PrayerMethod { get; set; } = PrayerMethod.Karachi;
    public bool AsrHanafi { get; set; } = true;
    public bool PrayerAlertEnabled { get; set; } = true;
    public int PrayerAlertMinutesBefore { get; set; }
    public int PrayerAlertMask { get; set; } = 31;
    public int[] PrayerOffsets { get; set; } = new int[5];

    public AppSettingsData Clone()
    {
        var c = (AppSettingsData)MemberwiseClone();
        c.PrayerOffsets = (int[])PrayerOffsets.Clone();
        return c;
    }
}

public class SettingsService
{
    readonly IDbContextFactory<TimeBridgeDbContext> _factory;
    public SettingsService(IDbContextFactory<TimeBridgeDbContext> factory) => _factory = factory;

    public AppSettingsData Current { get; private set; } = new();
    public event EventHandler? Changed;

    public async Task<AppSettingsData> LoadAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var map = await db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
        var d = new AppSettingsData();
        var inv = CultureInfo.InvariantCulture;
        if (map.TryGetValue(SettingKeys.HomeTimeZoneId, out var h) && TimeZoneCatalog.TryFind(h) != null) d.HomeTimeZoneId = h;
        if (map.TryGetValue(SettingKeys.ShowSeconds, out var s) && bool.TryParse(s, out var sb)) d.ShowSeconds = sb;
        if (map.TryGetValue(SettingKeys.WidgetOpacity, out var o) && double.TryParse(o, NumberStyles.Float, inv, out var od))
            d.WidgetOpacity = Math.Clamp(od, 0.4, 1.0);
        if (map.TryGetValue(SettingKeys.DefaultReminderMinutes, out var r) && int.TryParse(r, out var ri)
            && ReminderOptions.Minutes.Contains(ri)) d.DefaultReminderMinutes = ri;
        if (map.TryGetValue(SettingKeys.SnoozeMinutes, out var sn) && int.TryParse(sn, out var sni)) d.SnoozeMinutes = Math.Clamp(sni, 1, 60);
        if (map.TryGetValue(SettingKeys.AlarmSoundEnabled, out var a) && bool.TryParse(a, out var ab)) d.AlarmSoundEnabled = ab;
        if (map.TryGetValue(SettingKeys.StartWithWindows, out var w) && bool.TryParse(w, out var wb)) d.StartWithWindows = wb;
        if (map.TryGetValue(SettingKeys.PrayerCityId, out var pc) && PrayerCities.Find(pc) != null) d.PrayerCityId = pc;
        if (map.TryGetValue(SettingKeys.PrayerMethod, out var pm) && Enum.TryParse<PrayerMethod>(pm, out var pme)) d.PrayerMethod = pme;
        if (map.TryGetValue(SettingKeys.AsrHanafi, out var ah) && bool.TryParse(ah, out var ahb)) d.AsrHanafi = ahb;
        if (map.TryGetValue(SettingKeys.PrayerAlertEnabled, out var pae) && bool.TryParse(pae, out var paeb)) d.PrayerAlertEnabled = paeb;
        if (map.TryGetValue(SettingKeys.PrayerAlertMinutes, out var pam) && int.TryParse(pam, out var pami) && ReminderOptions.Minutes.Contains(pami)) d.PrayerAlertMinutesBefore = pami;
        if (map.TryGetValue(SettingKeys.PrayerAlertMask, out var pmask) && int.TryParse(pmask, out var pmaski)) d.PrayerAlertMask = pmaski & 31;
        if (map.TryGetValue(SettingKeys.PrayerOffsets, out var po))
        {
            var parts = po.Split(',');
            if (parts.Length == 5 && parts.All(x => int.TryParse(x, out _)))
                d.PrayerOffsets = parts.Select(x => Math.Clamp(int.Parse(x), -60, 60)).ToArray();
        }
        Current = d;
        return d;
    }

    public async Task SaveAsync(AppSettingsData d)
    {
        var inv = CultureInfo.InvariantCulture;
        var values = new Dictionary<string, string>
        {
            [SettingKeys.HomeTimeZoneId] = d.HomeTimeZoneId,
            [SettingKeys.ShowSeconds] = d.ShowSeconds.ToString(),
            [SettingKeys.WidgetOpacity] = d.WidgetOpacity.ToString("0.##", inv),
            [SettingKeys.DefaultReminderMinutes] = d.DefaultReminderMinutes.ToString(inv),
            [SettingKeys.SnoozeMinutes] = d.SnoozeMinutes.ToString(inv),
            [SettingKeys.AlarmSoundEnabled] = d.AlarmSoundEnabled.ToString(),
            [SettingKeys.StartWithWindows] = d.StartWithWindows.ToString(),
            [SettingKeys.PrayerCityId] = d.PrayerCityId,
            [SettingKeys.PrayerMethod] = d.PrayerMethod.ToString(),
            [SettingKeys.AsrHanafi] = d.AsrHanafi.ToString(),
            [SettingKeys.PrayerAlertEnabled] = d.PrayerAlertEnabled.ToString(),
            [SettingKeys.PrayerAlertMinutes] = d.PrayerAlertMinutesBefore.ToString(inv),
            [SettingKeys.PrayerAlertMask] = d.PrayerAlertMask.ToString(inv),
            [SettingKeys.PrayerOffsets] = string.Join(",", d.PrayerOffsets.Take(5)),
        };
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.AppSettings.ToDictionaryAsync(x => x.Key);
        foreach (var (k, v) in values)
        {
            if (existing.TryGetValue(k, out var row)) row.Value = v;
            else db.AppSettings.Add(new AppSetting { Key = k, Value = v });
        }
        await db.SaveChangesAsync();
        Current = d;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
