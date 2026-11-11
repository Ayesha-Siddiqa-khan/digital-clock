using TimeBridge.Core.Models;

namespace TimeBridge.Core.Services;

public record NoticeInfo(string Key, string Title, string Message, string Kind, PlanCategory Category);

/// <summary>Background engine that raises prayer, routine and health reminders. Like <see cref="AlarmScheduler"/>
/// it runs for the whole app lifetime, independent of any window.</summary>
public class ReminderEngine : IDisposable
{
    /// <summary>Alerts older than this (e.g. after the PC slept) are skipped rather than fired late.</summary>
    public static readonly TimeSpan LateTolerance = TimeSpan.FromMinutes(3);

    readonly IClock _clock;
    readonly SettingsService _settings;
    readonly ScheduleService _schedule;
    readonly HashSet<string> _fired = new();
    readonly List<(DateTime DueUtc, NoticeInfo Notice)> _snoozed = new();
    readonly object _lock = new();
    List<ScheduleItem> _items = new();
    DateTime _itemsLoadedUtc = DateTime.MinValue;
    bool _dirty = true;
    CancellationTokenSource? _cts;
    Task? _loop;

    public ReminderEngine(IClock clock, SettingsService settings, ScheduleService schedule)
    {
        _clock = clock; _settings = settings; _schedule = schedule;
        _schedule.Changed += (_, _) => _dirty = true;
        _settings.Changed += (_, _) => _dirty = true;
    }

    /// <summary>Raised on a background thread; subscribers must marshal to the UI thread.</summary>
    public event Action<NoticeInfo>? NoticeRaised;

    public void Start()
    {
        if (_loop != null) return;
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try { await CheckAsync(); }
                catch (Exception) when (!ct.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Snooze(NoticeInfo notice, int minutes)
    {
        lock (_lock) _snoozed.Add((_clock.UtcNow.AddMinutes(minutes), notice));
    }

    /// <summary>Evaluates the plan against the current clock. Public for tests.</summary>
    public async Task<IReadOnlyList<NoticeInfo>> CheckAsync()
    {
        var now = _clock.UtcNow;
        if (_dirty || now - _itemsLoadedUtc > TimeSpan.FromMinutes(5))
        {
            _dirty = false;
            _items = await _schedule.GetAllAsync();
            _itemsLoadedUtc = now;
        }

        var s = _settings.Current;
        var homeToday = DateOnly.FromDateTime(TimeConversion.FromUtc(now, s.HomeTimeZoneId).Local);
        var raised = new List<NoticeInfo>();

        // Yesterday..tomorrow so alerts that straddle midnight (Fajr alerts, overnight routines) are covered.
        for (var d = -1; d <= 1; d++)
        {
            foreach (var e in SchedulePlanner.Build(homeToday.AddDays(d), s, _items))
            {
                if (e.AlertUtc is not { } at || at > now || now - at > LateTolerance) continue;
                lock (_lock) { if (!_fired.Add(e.Key + "@" + at.Ticks)) continue; }
                raised.Add(new NoticeInfo(e.Key, e.Title, e.Message, e.Kind, e.Category));
            }
        }

        lock (_lock)
        {
            foreach (var due in _snoozed.Where(x => x.DueUtc <= now).ToList())
            {
                _snoozed.Remove(due);
                raised.Add(due.Notice);
            }
            if (_fired.Count > 2000) _fired.Clear(); // only matters on very long sessions; LateTolerance prevents re-fires
        }

        foreach (var n in raised) NoticeRaised?.Invoke(n);
        return raised;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
    }
}
