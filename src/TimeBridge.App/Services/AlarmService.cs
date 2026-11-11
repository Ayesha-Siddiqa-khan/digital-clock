using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using TimeBridge.App.Views;
using TimeBridge.Core.Services;

namespace TimeBridge.App.Services;

/// <summary>Presents alarms raised by <see cref="AlarmScheduler"/>: Windows toast + always-on-top
/// alarm window + looping sound. Runs for the whole app lifetime, whatever windows are open.</summary>
public sealed class AlarmService
{
    const string ToastGroup = "alarms";

    readonly AlarmScheduler _scheduler;
    readonly SettingsService _settings;
    readonly AlarmSound _sound;
    readonly IClock _clock;
    readonly Dictionary<int, AlarmWindow> _windows = new();
    int _testId;

    public AlarmService(AlarmScheduler scheduler, SettingsService settings, AlarmSound sound, IClock clock)
    { _scheduler = scheduler; _settings = settings; _sound = sound; _clock = clock; }

    public void Start()
    {
        _scheduler.AlarmRaised += a => Application.Current?.Dispatcher.BeginInvoke(() => Show(a));
        try { ToastNotificationManagerCompat.OnActivated += OnToastActivated; }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    public void Show(AlarmInfo alarm)
    {
        if (_windows.ContainsKey(alarm.ReminderId)) return;
        var s = _settings.Current;
        var window = new AlarmWindow(alarm, s.HomeTimeZoneId, s.SnoozeMinutes, _clock);
        var useSound = s.AlarmSoundEnabled;
        if (useSound) _sound.Acquire();

        window.Resolved += snooze =>
        {
            _windows.Remove(alarm.ReminderId);
            if (useSound) _sound.Release();
            RemoveToast(alarm.ReminderId);
            _ = ApplyAsync(alarm.ReminderId, snooze);
        };
        _windows[alarm.ReminderId] = window;
        window.Show();
        window.Activate();
        ShowToast(alarm);
    }

    /// <summary>Raises a demo alarm (not stored in the database).</summary>
    public void Test()
    {
        var s = _settings.Current;
        Show(new AlarmInfo(--_testId, 0, "Test meeting", "Sample client", "London", "Europe/London",
            _clock.UtcNow.AddMinutes(10), 10));
    }

    async Task ApplyAsync(int reminderId, int? snoozeMinutes)
    {
        if (reminderId <= 0) return; // test alarm
        try
        {
            if (snoozeMinutes is int m) await _scheduler.SnoozeAsync(reminderId, m);
            else await _scheduler.DismissAsync(reminderId);
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    static void ShowToast(AlarmInfo a)
    {
        try
        {
            var id = a.ReminderId.ToString();
            var when = a.MinutesBefore == 0 ? "starting now" : $"starts in {a.MinutesBefore} minutes";
            new ToastContentBuilder()
                .AddArgument("rid", id)
                .AddText($"Meeting {when}")
                .AddText(a.Title)
                .AddText($"{a.ClientName} · {a.ClientCity}")
                .AddButton(new ToastButton().SetContent("Snooze").AddArgument("action", "snooze").SetBackgroundActivation())
                .AddButton(new ToastButton().SetContent("Dismiss").AddArgument("action", "dismiss").SetBackgroundActivation())
                .SetToastScenario(ToastScenario.Reminder)
                .AddAudio(new ToastAudio { Silent = true })
                .Show(t => { t.Tag = id; t.Group = ToastGroup; });
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    static void RemoveToast(int reminderId)
    {
        try { ToastNotificationManagerCompat.History.Remove(reminderId.ToString(), ToastGroup); }
        catch { /* toast infrastructure unavailable */ }
    }

    void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        if (!args.TryGetValue("rid", out var ridText) || !int.TryParse(ridText, out var rid)) return;
        args.TryGetValue("action", out var action);
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            int? snooze = action == "snooze" ? _settings.Current.SnoozeMinutes : null;
            if (_windows.TryGetValue(rid, out var w)) w.Resolve(snooze);
            else if (action is "snooze" or "dismiss") _ = ApplyAsync(rid, snooze);
        });
    }
}
