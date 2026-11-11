using System.Windows.Threading;
using TimeBridge.Core.Services;

namespace TimeBridge.App.Services;

/// <summary>One UI-thread timer that drives every live clock.</summary>
public sealed class ClockTicker
{
    readonly DispatcherTimer _timer;
    readonly IClock _clock;

    public ClockTicker(IClock clock)
    {
        _clock = clock;
        _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Tick?.Invoke(_clock.UtcNow);
    }

    public event Action<DateTime>? Tick;
    public DateTime UtcNow => _clock.UtcNow;
    public void Start() => _timer.Start();
}
