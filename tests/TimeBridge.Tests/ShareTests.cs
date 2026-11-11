using TimeBridge.Core.Models;
using TimeBridge.Core.Services;

namespace TimeBridge.Tests;

public class ScheduleShareTests
{
    static readonly DateOnly Wed = new(2026, 11, 11);
    static ScheduleItem Lunch() => new() { Id = 1, Category = ScheduleCategory.Routine, Kind = "Lunch", Name = "Lunch", StartMinute = 13 * 60, EndMinute = 14 * 60 };
    const string LondonZone = "Europe/London";

    [Fact]
    public void Message_lists_routine_ranges_and_prayer_starts_in_pakistan_time()
    {
        var text = ScheduleShare.Build(Wed, new AppSettingsData(), new[] { Lunch() }, true, true);
        Assert.Contains("Wednesday, 11-11-2026", text);
        Assert.Contains("Times are in Pakistan time (UTC+5)", text);
        Assert.Contains("Lunch: 01:00 PM – 02:00 PM", text);
        Assert.Contains("Dhuhr from 12:15 PM", text);
        Assert.DoesNotContain("Sunrise", text);
        Assert.Contains("unavailable", text);
    }

    [Fact]
    public void Client_local_time_is_the_main_time_and_pakistan_time_is_in_brackets()
    {
        var text = ScheduleShare.Build(Wed, new AppSettingsData(), new[] { Lunch() }, true, true, LondonZone, "London");
        Assert.Contains("Times are in London time (UTC+0). Pakistan time is in brackets.", text);
        // 1:00-2:00 PM in Pakistan is 8:00-9:00 AM in London: London first, Pakistan in brackets
        Assert.Contains("Lunch: 08:00 AM – 09:00 AM  (01:00 PM – 02:00 PM Pakistan)", text);
        Assert.Contains("Dhuhr from 07:15 AM  (12:15 PM Pakistan)", text);
    }

    [Fact]
    public void Daylight_saving_is_applied_for_the_reader()
    {
        // 1 July: London is on BST (UTC+1), so 1:00 PM in Pakistan is 9:00 AM there.
        var text = ScheduleShare.Build(new DateOnly(2026, 7, 1), new AppSettingsData(), new[] { Lunch() }, false, true, LondonZone, "London");
        Assert.Contains("London time (UTC+1)", text);
        Assert.Contains("Lunch: 09:00 AM – 10:00 AM", text);
    }

    [Fact]
    public void Any_city_works_without_a_saved_client()
    {
        var text = ScheduleShare.Build(Wed, new AppSettingsData(), new[] { Lunch() }, false, true, "America/New_York", "New York");
        Assert.Contains("New York time (UTC-5)", text);
        Assert.Contains("Lunch: 03:00 AM – 04:00 AM  (01:00 PM – 02:00 PM Pakistan)", text);
    }
    [Fact]
    public void Prayers_and_routine_can_be_excluded()
    {
        var onlyRoutine = ScheduleShare.Build(Wed, new AppSettingsData(), new[] { Lunch() }, false, true);
        Assert.DoesNotContain("Fajr", onlyRoutine);
        var onlyPrayers = ScheduleShare.Build(Wed, new AppSettingsData(), new[] { Lunch() }, true, false);
        Assert.DoesNotContain("Lunch", onlyPrayers);
        Assert.Contains("Fajr", onlyPrayers);
    }

    [Fact]
    public void Health_reminders_are_never_shared()
    {
        var water = new ScheduleItem { Id = 2, Category = ScheduleCategory.Health, Kind = "Water", Name = "Drink water", StartMinute = 9 * 60 };
        var text = ScheduleShare.Build(Wed, new AppSettingsData(), new[] { water }, true, true);
        Assert.DoesNotContain("Drink water", text);
    }

    [Fact]
    public void Empty_selection_says_nothing_scheduled()
    {
        var text = ScheduleShare.Build(Wed, new AppSettingsData(), Array.Empty<ScheduleItem>(), false, true);
        Assert.Contains("Nothing scheduled.", text);
    }

    [Fact]
    public void Reader_on_a_different_date_is_flagged()
    {
        var sleep = new ScheduleItem { Id = 3, Category = ScheduleCategory.Routine, Kind = "Sleep", Name = "Sleep", StartMinute = 2 * 60 + 30, EndMinute = 6 * 60 };
        var text = ScheduleShare.Build(Wed, new AppSettingsData(), new[] { sleep }, false, true, "America/Los_Angeles", "Los Angeles");
        Assert.Contains("(previous day)", text); // 2:30 AM Pakistan is the previous day in Los Angeles
    }
}