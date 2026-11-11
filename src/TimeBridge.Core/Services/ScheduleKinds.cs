namespace TimeBridge.Core.Services;

public record KindInfo(string Id, string Label, string Emoji, string DefaultName, string DefaultMessage);

/// <summary>Built-in activity types for routine blocks and health reminders.</summary>
public static class ScheduleKinds
{
    public static readonly IReadOnlyList<KindInfo> Routine = new[]
    {
        new KindInfo("Sleep", "Sleep", "🌙", "Sleep", "Time to wind down and sleep."),
        new KindInfo("Rest", "Rest / nap", "☕", "Rest", "Take a rest."),
        new KindInfo("Breakfast", "Breakfast", "🍳", "Breakfast", "Time for breakfast."),
        new KindInfo("Lunch", "Lunch", "🍽", "Lunch", "Time for lunch."),
        new KindInfo("Dinner", "Dinner", "🍴", "Dinner", "Time for dinner."),
        new KindInfo("Exercise", "Exercise", "🏃", "Exercise", "Time to exercise."),
        new KindInfo("Work", "Work", "💻", "Work", "Work block starts."),
        new KindInfo("Custom", "Custom", "⏰", "Activity", ""),
    };

    public static readonly IReadOnlyList<KindInfo> Health = new[]
    {
        new KindInfo("Water", "Drink water", "💧", "Drink water", "Time to drink a glass of water."),
        new KindInfo("Meal", "Eat a meal", "🍽", "Eat something", "Time to eat. Step away from the screen."),
        new KindInfo("Walk", "Walk", "🚶", "Take a walk", "Stand up and walk for a few minutes."),
        new KindInfo("Eyes", "Rest your eyes", "👀", "Rest your eyes", "Look at something 6 metres away for 20 seconds."),
        new KindInfo("Stretch", "Stretch", "🧘", "Stretch", "Stand up and stretch your neck, shoulders and back."),
        new KindInfo("ScreenBreak", "Screen break", "🖥", "Screen break", "You have been on the screen for a while. Take a break."),
        new KindInfo("Custom", "Custom", "⏰", "Reminder", ""),
    };

    public static string Emoji(string kind) =>
        kind switch
        {
            "Fajr" or "Dhuhr" or "Asr" or "Maghrib" or "Isha" => "🕌",
            "Sunrise" => "🌅",
            _ => Routine.Concat(Health).FirstOrDefault(k => k.Id == kind)?.Emoji ?? "⏰"
        };
}
