namespace TimeBridge.App.Controls;

/// <summary>Static choices (-15..+15 minutes) for the per-prayer adjustment drop-down.</summary>
public static class PrayerOffsets
{
    public static IReadOnlyList<int> Choices { get; } = Enumerable.Range(-15, 31).ToList();
}
