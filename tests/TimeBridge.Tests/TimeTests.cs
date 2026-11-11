using TimeBridge.Core.Services;

namespace TimeBridge.Tests;

public class TimeConversionTests
{
    const string Pk = "Asia/Karachi";

    [Fact]
    public void Pakistan_5pm_in_London_winter_is_noon()
    {
        var r = TimeConversion.Convert(new DateTime(2026, 11, 11, 17, 0, 0), Pk, "Europe/London");
        Assert.Equal(new DateTime(2026, 11, 11, 12, 0, 0), r.Local);
        Assert.Equal("UTC+0", r.OffsetText);
        Assert.False(r.IsDaylightSaving);
    }

    [Fact]
    public void London_switches_to_BST_after_29_March_2026()
    {
        var before = TimeConversion.Convert(new DateTime(2026, 3, 28, 17, 0, 0), Pk, "Europe/London");
        var after = TimeConversion.Convert(new DateTime(2026, 3, 30, 17, 0, 0), Pk, "Europe/London");
        Assert.Equal(12, before.Local.Hour);
        Assert.Equal(13, after.Local.Hour);
        Assert.True(after.IsDaylightSaving);
        Assert.Equal("UTC+1", after.OffsetText);
    }

    [Fact]
    public void NewYork_offset_changes_with_US_daylight_saving()
    {
        var winter = TimeConversion.FromUtc(new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc), "America/New_York");
        var summer = TimeConversion.FromUtc(new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc), "America/New_York");
        Assert.Equal(TimeSpan.FromHours(-5), winter.Offset);
        Assert.Equal(TimeSpan.FromHours(-4), summer.Offset);
    }

    [Fact]
    public void Day_can_roll_over_between_zones()
    {
        var r = TimeConversion.Convert(new DateTime(2026, 11, 11, 3, 0, 0), Pk, "America/Los_Angeles");
        Assert.Equal(new DateTime(2026, 11, 10, 14, 0, 0), r.Local);
    }

    [Fact]
    public void Nonexistent_local_time_is_rejected()
    {
        Assert.Throws<InvalidLocalTimeException>(() =>
            TimeConversion.ToUtc(new DateTime(2026, 3, 29, 1, 30, 0), "Europe/London"));
    }

    [Fact]
    public void Half_hour_zones_format_offset()
    {
        Assert.Equal("UTC+5:30", TimeConversion.FromUtc(DateTime.UtcNow, "Asia/Kolkata").OffsetText);
    }

    [Theory]
    [InlineData("Asia/Karachi")]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    [InlineData("Europe/Paris")]
    [InlineData("Australia/Sydney")]
    public void Catalog_resolves_curated_zones(string id) => Assert.NotNull(TimeZoneCatalog.TryFind(id));

    [Fact]
    public void Catalog_lists_every_curated_group()
    {
        var groups = TimeZoneCatalog.All().Select(z => z.Group).ToHashSet();
        Assert.Contains("UK", groups);
        Assert.Contains("USA", groups);
        Assert.Contains("Europe", groups);
    }
}

public class FormatTests
{
    [Fact]
    public void Dates_use_DD_MM_YYYY()
    {
        Assert.Equal("11-11-2026", TimeFormats.FormatDate(new DateTime(2026, 11, 11)));
        Assert.Equal("05-03-2026", TimeFormats.FormatDate(new DateTime(2026, 3, 5)));
    }

    [Theory]
    [InlineData("11-11-2026", true)]
    [InlineData("1-1-2026", false)]
    [InlineData("2026-11-11", false)]
    [InlineData("31-02-2026", false)]
    [InlineData("11/11/2026", false)]
    public void Date_parsing_is_strict(string text, bool ok) => Assert.Equal(ok, TimeFormats.TryParseDate(text, out _));

    [Theory]
    [InlineData("5:00 PM", 17, 0)]
    [InlineData("05:00 pm", 17, 0)]
    [InlineData("12:00 AM", 0, 0)]
    [InlineData("12:30 PM", 12, 30)]
    [InlineData("11:59PM", 23, 59)]
    public void Time_parsing_accepts_12_hour(string text, int h, int m)
    {
        Assert.True(TimeFormats.TryParseTime(text, out var t));
        Assert.Equal(new TimeOnly(h, m), t);
    }

    [Fact]
    public void Time_parsing_rejects_24_hour_text() => Assert.False(TimeFormats.TryParseTime("17:00", out _));

    [Fact]
    public void Time_is_formatted_12_hour()
    {
        Assert.Equal("05:00 PM", TimeFormats.FormatTime(new DateTime(2026, 1, 1, 17, 0, 0)));
        Assert.Equal(("7:03:00", "AM"), TimeFormats.FormatClock(new DateTime(2026, 1, 1, 7, 3, 0), true));
    }
}
