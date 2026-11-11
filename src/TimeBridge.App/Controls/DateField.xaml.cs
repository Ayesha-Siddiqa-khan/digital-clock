using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TimeBridge.Core.Services;

namespace TimeBridge.App.Controls;

/// <summary>Date entry that always uses DD-MM-YYYY (WPF's DatePicker follows the OS culture).</summary>
public partial class DateField : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(DateOnly?), typeof(DateField),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    bool _updating;

    public DateField() { InitializeComponent(); }

    public DateOnly? Value
    {
        get => (DateOnly?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var f = (DateField)d;
        if (f._updating) return;
        f._updating = true;
        var v = (DateOnly?)e.NewValue;
        f.Box.Text = v is { } dv ? dv.ToString(TimeFormats.DateFormat, System.Globalization.CultureInfo.InvariantCulture) : "";
        if (v is { } cv) { f.Cal.SelectedDate = cv.ToDateTime(TimeOnly.MinValue); f.Cal.DisplayDate = cv.ToDateTime(TimeOnly.MinValue); }
        f.Box.Foreground = (Brush)f.FindResource("TextFillColorPrimaryBrush");
        f._updating = false;
    }

    void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        if (TimeFormats.TryParseDate(Box.Text, out var d))
        {
            Value = d;
            Box.Foreground = (Brush)FindResource("TextFillColorPrimaryBrush");
        }
        else
        {
            Value = null;
            Box.Foreground = Box.Text.Length == 0 ? (Brush)FindResource("TextFillColorPrimaryBrush") : (Brush)FindResource("DangerBrush");
        }
        _updating = false;
    }

    void OnLostFocus(object sender, RoutedEventArgs e)
    {
        // Snap text back to the canonical format of the last valid value.
        if (Value is { } v)
        {
            _updating = true;
            Box.Text = v.ToString(TimeFormats.DateFormat, System.Globalization.CultureInfo.InvariantCulture);
            _updating = false;
        }
    }

    void OnCalendarClick(object sender, RoutedEventArgs e) => Pop.IsOpen = true;

    void OnCalendarPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_updating || Cal.SelectedDate is not { } d) return;
        Value = DateOnly.FromDateTime(d);
        Pop.IsOpen = false;
    }
}
