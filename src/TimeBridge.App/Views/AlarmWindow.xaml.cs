using System.Windows;
using System.Windows.Threading;
using TimeBridge.Core.Services;
using Wpf.Ui.Controls;

namespace TimeBridge.App.Views;

public partial class AlarmWindow : FluentWindow
{
    static readonly int[] SnoozeChoices = { 1, 5, 10, 15, 30 };

    readonly AlarmInfo _alarm;
    readonly IClock _clock;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    bool _resolved;

    /// <summary>Raised exactly once: snooze minutes, or null when dismissed.</summary>
    public event Action<int?>? Resolved;

    public int ReminderId => _alarm.ReminderId;

    public AlarmWindow(AlarmInfo alarm, string homeZoneId, int defaultSnooze, IClock clock)
    {
        InitializeComponent();
        _alarm = alarm;
        _clock = clock;

        TitleText.Text = alarm.Title;
        var home = TimeConversion.FromUtc(alarm.StartUtc, homeZoneId);
        var client = TimeConversion.FromUtc(alarm.StartUtc, alarm.ClientTimeZoneId);
        HomeLabel.Text = TimeZoneCatalog.LabelFor(homeZoneId);
        HomeValue.Text = $"{TimeFormats.FormatTime(home.Local)}  ·  {TimeFormats.FormatDate(home.Local)}";
        ClientLabel.Text = $"{alarm.ClientName} · {alarm.ClientCity}";
        ClientValue.Text = $"{TimeFormats.FormatTime(client.Local)}  ·  {TimeFormats.FormatDate(client.Local)}  ·  {client.OffsetText}";

        SnoozeBox.ItemsSource = SnoozeChoices.Select(m => $"{m} min").ToList();
        var idx = Array.IndexOf(SnoozeChoices, defaultSnooze);
        SnoozeBox.SelectedIndex = idx >= 0 ? idx : 1;

        // Bottom-right of the work area, above the taskbar.
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - 440 - 20;
        Top = wa.Bottom - 420;

        _timer.Tick += (_, _) => UpdateCountdown();
        UpdateCountdown();
        _timer.Start();
        Closed += (_, _) => { _timer.Stop(); Resolve(null); };
    }

    void UpdateCountdown()
    {
        var delta = _alarm.StartUtc - _clock.UtcNow;
        CountdownText.Text = delta > TimeSpan.Zero
            ? $"Starts {TimeFormats.FormatCountdown(delta)}"
            : delta > TimeSpan.FromSeconds(-30) ? "Starting now" : $"Started {TimeFormats.FormatCountdown(-delta).Replace("in ", "")} ago";
    }

    /// <summary>Resolves the alarm (idempotent) and closes the window.</summary>
    public void Resolve(int? snoozeMinutes)
    {
        if (_resolved) return;
        _resolved = true;
        Resolved?.Invoke(snoozeMinutes);
        if (IsLoaded || IsVisible) Close();
    }

    void OnSnooze(object sender, RoutedEventArgs e) => Resolve(SnoozeChoices[Math.Max(0, SnoozeBox.SelectedIndex)]);
    void OnDismiss(object sender, RoutedEventArgs e) => Resolve(null);
}
