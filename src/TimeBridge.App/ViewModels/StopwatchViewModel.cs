using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TimeBridge.App.ViewModels;

/// <summary>Stopwatch independent of the clocks and alarms. It only runs after Start is clicked and keeps
/// running when its window is hidden (the view model lives for the whole app session).</summary>
public partial class StopwatchViewModel : ObservableObject
{
    readonly Stopwatch _sw = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    TimeSpan _lastLap;

    public StopwatchViewModel() { _timer.Tick += (_, _) => Update(); }

    public ObservableCollection<string> Laps { get; } = new();

    [ObservableProperty] string _timeText = "00:00:00.00";
    [ObservableProperty] bool _isRunning;
    [ObservableProperty] bool _isPinned = true;

    public string StartPauseText => IsRunning ? "Pause" : _sw.Elapsed > TimeSpan.Zero ? "Resume" : "Start";
    public bool HasTime => _sw.Elapsed > TimeSpan.Zero;

    static string Format(TimeSpan t) =>
        $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}";

    void Update()
    {
        TimeText = Format(_sw.Elapsed);
        OnPropertyChanged(nameof(HasTime));
    }

    void Refresh()
    {
        Update();
        OnPropertyChanged(nameof(StartPauseText));
    }

    [RelayCommand]
    void StartPause()
    {
        if (_sw.IsRunning) { _sw.Stop(); _timer.Stop(); IsRunning = false; }
        else { _sw.Start(); _timer.Start(); IsRunning = true; }
        Refresh();
    }

    [RelayCommand]
    void Lap()
    {
        var e = _sw.Elapsed;
        if (e == TimeSpan.Zero) return;
        Laps.Insert(0, $"#{Laps.Count + 1}     +{Format(e - _lastLap)}     {Format(e)}");
        _lastLap = e;
    }

    [RelayCommand]
    void Reset()
    {
        _sw.Reset(); _timer.Stop(); IsRunning = false;
        Laps.Clear(); _lastLap = TimeSpan.Zero;
        Refresh();
    }

    [RelayCommand] void TogglePin() => IsPinned = !IsPinned;
}
