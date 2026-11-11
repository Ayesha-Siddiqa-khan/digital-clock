using System.Windows;
using System.Windows.Controls;

namespace TimeBridge.App.Controls;

/// <summary>12-hour time entry with an AM/PM selector.</summary>
public partial class TimePicker : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(TimeOnly), typeof(TimePicker),
        new FrameworkPropertyMetadata(new TimeOnly(9, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    bool _updating;

    public TimePicker()
    {
        InitializeComponent();
        HourBox.ItemsSource = Enumerable.Range(1, 12).Select(h => h.ToString("00")).ToList();
        MinuteBox.ItemsSource = Enumerable.Range(0, 60).Select(m => m.ToString("00")).ToList();
        PeriodBox.ItemsSource = new[] { "AM", "PM" };
        Show(Value);
    }

    public TimeOnly Value
    {
        get => (TimeOnly)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var p = (TimePicker)d;
        if (!p._updating) p.Show((TimeOnly)e.NewValue);
    }

    void Show(TimeOnly t)
    {
        _updating = true;
        var h12 = t.Hour % 12 == 0 ? 12 : t.Hour % 12;
        HourBox.SelectedIndex = h12 - 1;
        MinuteBox.SelectedIndex = t.Minute;
        PeriodBox.SelectedIndex = t.Hour >= 12 ? 1 : 0;
        _updating = false;
    }

    void OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || HourBox.SelectedIndex < 0 || MinuteBox.SelectedIndex < 0 || PeriodBox.SelectedIndex < 0) return;
        var h12 = HourBox.SelectedIndex + 1;
        var hour = h12 % 12 + (PeriodBox.SelectedIndex == 1 ? 12 : 0);
        _updating = true;
        Value = new TimeOnly(hour, MinuteBox.SelectedIndex);
        _updating = false;
    }
}
