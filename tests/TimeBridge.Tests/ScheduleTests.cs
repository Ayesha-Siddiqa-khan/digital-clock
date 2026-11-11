using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.Tests;

public class PrayerTimeTests
{
    static TimeSpan Tod(DateTime d) => d.TimeOfDay;
    static void Near(TimeSpan expected, DateTime actual, int minutes) =>
        Assert.True(Math.Abs((Tod(actual) - expected).TotalMinutes) <= minutes, $"expected ~{expected}, got {Tod(actual)}");

    [Fact]
    public void London_in_November_matches_published_sunrise_and_sunset()
    {
        // Independent check: solar noon ~11:44 GMT (equation of time +16 min), day length ~9h07m at 51.5N => sunrise ~07:11, sunset ~16:18.
        var t = PrayerTimeCalculator.Calculate(new DateOnly(2026, 11, 11), PrayerCities.Find("london")!, PrayerMethod.MuslimWorldLeague, false);
        Near(new TimeSpan(7, 11, 0), t.Sunrise, 3);
        Near(new TimeSpan(16, 18, 0), t.Maghrib, 3);
        Near(new TimeSpan(11, 44, 0), t.Dhuhr, 3);
    }

    [Fact]
    public void Karachi_in_November_is_plausible()
    {
        var t = PrayerTimeCalculator.Calculate(new DateOnly(2026, 11, 11), PrayerCities.Find("karachi")!, PrayerMethod.Karachi, true);
        Near(new TimeSpan(12, 15, 0), t.Dhuhr, 6);
        Near(new TimeSpan(6, 40, 0), t.Sunrise, 8);
        Near(new TimeSpan(17, 45, 0), t.Maghrib, 8);
        Assert.True(t.Asr > t.Dhuhr.AddHours(2.5) && t.Asr < t.Maghrib);
    }

    [Theory]
    [InlineData("karachi", 11)] [InlineData("london", 6)] [InlineData("london", 12)] [InlineData("newyork", 7)]
    [InlineData("makkah", 3)] [InlineData("sydney", 12)] [InlineData("stockholm", 6)]
    public void Prayers_are_always_in_order(string cityId, int month)
    {
        var city = PrayerCities.Find(cityId) ?? PrayerCities.Find("london")!;
        var t = PrayerTimeCalculator.Calculate(new DateOnly(2026, month, 15), city, city.DefaultMethod, false);
        Assert.True(t.Fajr < t.Sunrise && t.Sunrise < t.Dhuhr && t.Dhuhr < t.Asr && t.Asr < t.Maghrib && t.Maghrib < t.Isha,
            $"{cityId}/{month}: {t}");
    }

    [Fact]
    public void Hanafi_asr_is_later_than_standard()
    {
        var c = PrayerCities.Find("lahore")!;
        var std = PrayerTimeCalculator.Calculate(new DateOnly(2026, 11, 11), c, PrayerMethod.Karachi, false);
        var han = PrayerTimeCalculator.Calculate(new DateOnly(2026, 11, 11), c, PrayerMethod.Karachi, true);
        Assert.True(han.Asr > std.Asr.AddMinutes(30));
    }

    [Fact]
    public void Makkah_method_puts_isha_90_minutes_after_maghrib()
    {
        var t = PrayerTimeCalculator.Calculate(new DateOnly(2026, 11, 11), PrayerCities.Find("makkah")!, PrayerMethod.Makkah, false);
        Assert.Equal(90, (t.Isha - t.Maghrib).TotalMinutes, 0.5);
    }

    [Fact]
    public void Prayer_times_follow_daylight_saving()
    {
        var c = PrayerCities.Find("london")!;
        var winter = PrayerTimeCalculator.Calculate(new DateOnly(2026, 3, 28), c, PrayerMethod.MuslimWorldLeague, false);
        var summer = PrayerTimeCalculator.Calculate(new DateOnly(2026, 3, 30), c, PrayerMethod.MuslimWorldLeague, false);
        Assert.True((summer.Dhuhr - winter.Dhuhr).TotalMinutes > 50); // clocks went forward one hour
    }

    [Fact]
    public void Every_city_resolves_a_time_zone() =>
        Assert.All(PrayerCities.All, c => Assert.NotNull(TimeZoneCatalog.TryFind(c.TimeZoneId)));
}

public class PlannerTests
{
    static ScheduleItem Health(ScheduleMode mode, int start, int? end, int? interval, int days = 127) => new()
    {
        Id = 1, Category = ScheduleCategory.Health, Kind = "Water", Name = "Water", Mode = mode,
        StartMinute = start, EndMinute = end, IntervalMinutes = interval, DaysMask = days
    };

    static readonly DateOnly Wed = new(2026, 11, 11); // a Wednesday

    [Fact]
    public void Repeating_reminder_fires_every_interval_inside_the_window()
    {
        var s = new AppSettingsData { PrayerAlertEnabled = false };
        var plan = SchedulePlanner.Build(Wed, s, new[] { Health(ScheduleMode.Interval, 9 * 60, 16 * 60, 120) })
            .Where(e => e.Category == PlanCategory.Health).ToList();
        var local = plan.Select(e => TimeConversion.FromUtc(e.StartUtc, s.HomeTimeZoneId).Local.Hour).ToList();
        Assert.Equal(new[] { 11, 13, 15 }, local); // 9:00 + 2h, +4h, +6h
    }

    [Fact]
    public void Fixed_reminder_fires_once_at_its_time()
    {
        var s = new AppSettingsData { PrayerAlertEnabled = false };
        var e = Assert.Single(SchedulePlanner.Build(Wed, s, new[] { Health(ScheduleMode.Fixed, 12 * 60, null, null) }),
            x => x.Category == PlanCategory.Health);
        Assert.Equal(12, TimeConversion.FromUtc(e.StartUtc, s.HomeTimeZoneId).Local.Hour);
    }

    [Fact]
    public void Days_mask_limits_reminders_to_chosen_days()
    {
        var s = new AppSettingsData();
        var weekdaysOnly = Health(ScheduleMode.Fixed, 12 * 60, null, null, days: 0b0011111);
        Assert.Contains(SchedulePlanner.Build(new DateOnly(2026, 11, 13), s, new[] { weekdaysOnly }), e => e.Category == PlanCategory.Health); // Friday
        Assert.DoesNotContain(SchedulePlanner.Build(new DateOnly(2026, 11, 14), s, new[] { weekdaysOnly }), e => e.Category == PlanCategory.Health); // Saturday
    }

    [Fact]
    public void Disabled_items_produce_nothing()
    {
        var i = Health(ScheduleMode.Fixed, 12 * 60, null, null); i.IsEnabled = false;
        Assert.DoesNotContain(SchedulePlanner.Build(Wed, new AppSettingsData(), new[] { i }), e => e.Category == PlanCategory.Health);
    }

    [Fact]
    public void Overnight_routine_block_ends_the_next_morning()
    {
        var sleep = new ScheduleItem { Id = 2, Category = ScheduleCategory.Routine, Kind = "Sleep", Name = "Sleep", StartMinute = 23 * 60, EndMinute = 6 * 60 };
        var e = Assert.Single(SchedulePlanner.Build(Wed, new AppSettingsData(), new[] { sleep }), x => x.Category == PlanCategory.Routine);
        Assert.Equal(7, (e.EndUtc!.Value - e.StartUtc).TotalHours);
    }

    [Fact]
    public void Each_prayer_has_start_end_and_alert_following_settings()
    {
        var s = new AppSettingsData { PrayerAlertMinutesBefore = 10, PrayerAlertMask = 0b00101 }; // Fajr + Asr only
        var prayers = SchedulePlanner.Build(Wed, s, Array.Empty<ScheduleItem>())
            .Where(e => e.Category == PlanCategory.Prayer && e.Kind != "Sunrise").ToList();
        Assert.Equal(5, prayers.Count);
        Assert.All(prayers, p => Assert.True(p.EndUtc > p.StartUtc));
        Assert.Equal(new[] { "Fajr", "Asr" }, prayers.Where(p => p.AlertUtc != null).Select(p => p.Title));
        var fajr = prayers.First();
        Assert.Equal(fajr.StartUtc.AddMinutes(-10), fajr.AlertUtc);
    }

    [Fact]
    public void Prayer_offset_moves_the_prayer()
    {
        var plain = SchedulePlanner.Build(Wed, new AppSettingsData(), Array.Empty<ScheduleItem>()).First(e => e.Title == "Dhuhr");
        var shifted = SchedulePlanner.Build(Wed, new AppSettingsData { PrayerOffsets = new[] { 0, 7, 0, 0, 0 } }, Array.Empty<ScheduleItem>()).First(e => e.Title == "Dhuhr");
        Assert.Equal(7, (shifted.StartUtc - plain.StartUtc).TotalMinutes);
    }

    [Fact]
    public void Changing_city_changes_the_plan()
    {
        var k = SchedulePlanner.Build(Wed, new AppSettingsData { PrayerCityId = "karachi" }, Array.Empty<ScheduleItem>()).First(e => e.Title == "Fajr");
        var l = SchedulePlanner.Build(Wed, new AppSettingsData { PrayerCityId = "london", PrayerMethod = PrayerMethod.MuslimWorldLeague }, Array.Empty<ScheduleItem>()).First(e => e.Title == "Fajr");
        Assert.True(Math.Abs((k.StartUtc - l.StartUtc).TotalHours) > 1);
    }
}

public class ScheduleServiceTests
{
    [Fact]
    public async Task Validates_input()
    {
        using var t = new TestDb();
        await Assert.ThrowsAsync<ValidationException>(() => t.Schedule.AddAsync(new ScheduleItem { Category = ScheduleCategory.Routine, Name = "", StartMinute = 60, EndMinute = 120 }));
        await Assert.ThrowsAsync<ValidationException>(() => t.Schedule.AddAsync(new ScheduleItem { Category = ScheduleCategory.Routine, Name = "Nap", StartMinute = 60, EndMinute = 60 }));
        await Assert.ThrowsAsync<ValidationException>(() => t.Schedule.AddAsync(new ScheduleItem { Category = ScheduleCategory.Health, Name = "W", Mode = ScheduleMode.Interval, StartMinute = 540, EndMinute = 600, IntervalMinutes = 120 }));
        await Assert.ThrowsAsync<ValidationException>(() => t.Schedule.AddAsync(new ScheduleItem { Category = ScheduleCategory.Health, Name = "W", StartMinute = 540, DaysMask = 0 }));
    }

    [Fact]
    public async Task Add_update_toggle_delete()
    {
        using var t = new TestDb();
        var a = await t.Schedule.AddAsync(new ScheduleItem { Category = ScheduleCategory.Health, Kind = "Walk", Name = "Walk", StartMinute = 720 });
        a.Name = "Long walk"; a.StartMinute = 730;
        await t.Schedule.UpdateAsync(a);
        await t.Schedule.SetEnabledAsync(a.Id, false);
        var got = Assert.Single(await t.Schedule.GetAllAsync(ScheduleCategory.Health));
        Assert.Equal(("Long walk", 730, false), (got.Name, got.StartMinute, got.IsEnabled));
        await t.Schedule.DeleteAsync(a.Id);
        Assert.Empty(await t.Schedule.GetAllAsync());
    }

    [Fact]
    public async Task Suggested_plans_are_added_once()
    {
        using var t = new TestDb();
        Assert.Equal(5, await t.Schedule.AddSuggestedHealthPlanAsync());
        Assert.Equal(0, await t.Schedule.AddSuggestedHealthPlanAsync());
        Assert.Equal(5, await t.Schedule.AddSuggestedRoutineAsync());
        Assert.Equal(10, (await t.Schedule.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Prayer_settings_round_trip_and_survive_clone()
    {
        using var t = new TestDb();
        var d = t.Settings.Current.Clone();
        d.PrayerCityId = "london"; d.PrayerMethod = PrayerMethod.MuslimWorldLeague; d.AsrHanafi = false;
        d.PrayerAlertMinutesBefore = 10; d.PrayerAlertMask = 0b10101; d.PrayerOffsets = new[] { 1, -2, 3, 0, 5 };
        await t.Settings.SaveAsync(d);
        var back = await new SettingsService(t).LoadAsync();
        Assert.Equal(("london", PrayerMethod.MuslimWorldLeague, false, 10, 21), (back.PrayerCityId, back.PrayerMethod, back.AsrHanafi, back.PrayerAlertMinutesBefore, back.PrayerAlertMask));
        Assert.Equal(new[] { 1, -2, 3, 0, 5 }, back.PrayerOffsets);
        Assert.NotSame(d.PrayerOffsets, d.Clone().PrayerOffsets);
    }
}

public class ReminderEngineTests
{
    static async Task<TestDb> WithHealthReminder()
    {
        var t = new TestDb();
        await t.Settings.SaveAsync(new AppSettingsData { PrayerAlertEnabled = false });
        await t.Schedule.AddAsync(new ScheduleItem { Category = ScheduleCategory.Health, Kind = "Walk", Name = "Take a walk", StartMinute = 12 * 60 });
        return t;
    }

    // 12:00 in Karachi on 11 Nov 2026 = 07:00 UTC
    static DateTime NoonPkt => new(2026, 11, 11, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Fires_at_the_scheduled_time_exactly_once()
    {
        using var t = await WithHealthReminder();
        t.Clock.UtcNow = NoonPkt.AddSeconds(-5);
        Assert.Empty(await t.Engine.CheckAsync());
        t.Clock.UtcNow = NoonPkt;
        var n = Assert.Single(await t.Engine.CheckAsync());
        Assert.Equal("Take a walk", n.Title);
        t.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Empty(await t.Engine.CheckAsync());
    }

    [Fact]
    public async Task Stale_alerts_after_sleep_are_not_fired_late()
    {
        using var t = await WithHealthReminder();
        t.Clock.UtcNow = NoonPkt.AddMinutes(20);
        Assert.Empty(await t.Engine.CheckAsync());
    }

    [Fact]
    public async Task Snooze_repeats_the_notice_later()
    {
        using var t = await WithHealthReminder();
        t.Clock.UtcNow = NoonPkt;
        var n = (await t.Engine.CheckAsync()).Single();
        t.Engine.Snooze(n, 10);
        t.Clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Empty(await t.Engine.CheckAsync());
        t.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Single(await t.Engine.CheckAsync());
    }

    [Fact]
    public async Task Disabled_reminders_stay_silent_and_edits_apply_immediately()
    {
        using var t = await WithHealthReminder();
        var item = (await t.Schedule.GetAllAsync()).Single();
        await t.Schedule.SetEnabledAsync(item.Id, false);
        t.Clock.UtcNow = NoonPkt;
        Assert.Empty(await t.Engine.CheckAsync());
    }

    [Fact]
    public async Task Prayer_alert_fires_at_prayer_time_for_the_selected_city()
    {
        using var t = new TestDb();
        var s = t.Settings.Current.Clone();
        s.PrayerAlertEnabled = true; s.PrayerAlertMinutesBefore = 0; s.PrayerAlertMask = 0b00010; // Dhuhr only
        await t.Settings.SaveAsync(s);
        var dhuhr = SchedulePlanner.Build(new DateOnly(2026, 11, 11), s, Array.Empty<ScheduleItem>()).First(e => e.Title == "Dhuhr");
        t.Clock.UtcNow = dhuhr.StartUtc.AddSeconds(-2);
        Assert.Empty(await t.Engine.CheckAsync());
        t.Clock.UtcNow = dhuhr.StartUtc;
        var n = Assert.Single(await t.Engine.CheckAsync());
        Assert.Equal(("Dhuhr", PlanCategory.Prayer), (n.Title, n.Category));
    }
}
