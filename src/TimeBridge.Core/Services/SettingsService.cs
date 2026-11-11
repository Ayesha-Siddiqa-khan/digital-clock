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
