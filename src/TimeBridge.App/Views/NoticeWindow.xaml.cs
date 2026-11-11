using System.Windows;
using System.Windows.Threading;
using TimeBridge.Core.Services;
using Wpf.Ui.Controls;

namespace TimeBridge.App.Views;

/// <summary>Popup for prayer, routine and health-break reminders (a gentle, non-looping notice).</summary>
public partial class NoticeWindow : FluentWindow
{
    static readonly int[] SnoozeChoices = { 5, 10, 15, 30 };
    static int _open;

    readonly DispatcherTimer _autoClose = new() { Interval = TimeSpan.FromMinutes(5) };
    bool _resolved;

    /// <summary>Raised once: snooze minutes, or null when done/dismissed.</summary>
    public event Action<int?>? Resolved;

    public NoticeWindow(NoticeInfo n, string timeText)
    {
        InitializeComponent();
        EmojiText.Text = ScheduleKinds.Emoji(n.Kind);
        CategoryText.Text = n.Category switch { PlanCategory.Prayer => "PRAYER", PlanCategory.Routine => "ROUTINE", _ => "TAKE A BREAK" };
        TitleText.Text = n.Title;
        MessageText.Text = n.Message;
        TimeText.Text = timeText;
        SnoozeBox.ItemsSource = SnoozeChoices.Select(m => $"{m} min").ToList();
        SnoozeBox.SelectedIndex = 1;

        var wa = SystemParameters.WorkArea;
        Left = wa.Right - 420 - 20;
        Top = Math.Max(wa.Top + 20, wa.Bottom - 330 - (_open % 4) * 34);
        _open++;

        _autoClose.Tick += (_, _) => Resolve(null);
        _autoClose.Start();
        Closed += (_, _) => { _open = Math.Max(0, _open - 1); _autoClose.Stop(); Resolve(null); };
    }

    void Resolve(int? snooze)
    {
        if (_resolved) return;
        _resolved = true;
        Resolved?.Invoke(snooze);
        if (IsVisible) Close();
    }

    void OnSnooze(object sender, RoutedEventArgs e) => Resolve(SnoozeChoices[Math.Max(0, SnoozeBox.SelectedIndex)]);
    void OnDone(object sender, RoutedEventArgs e) => Resolve(null);
}
