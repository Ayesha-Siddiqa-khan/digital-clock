using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TimeBridge.App.ViewModels;

namespace TimeBridge.App.Views;

public partial class WidgetWindow : Window
{
    const double ExpW = 340, ExpH = 326, ColW = 300, ColH = 52;
    const double MinWidth_ = 260, MaxWidth_ = 640;

    readonly WidgetViewModel _vm;
    long _popupClosedTick;
    Point _resizeStartCursor;
    double _resizeStartWidth;

    public WidgetWindow(WidgetViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _vm.PropertyChanged += OnVmChanged;
        ApplyLayout();
        UpdatePinVisual();
        PlaceFromState();
        LocationChanged += (_, _) =>
        {
            _vm.State.Left = Left; _vm.State.Top = Top;
            _vm.SaveSoon();
        };
    }

    void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.IsCollapsed)) ApplyLayout();
        else if (e.PropertyName == nameof(WidgetViewModel.IsPinned)) UpdatePinVisual();
    }

    double Scale => Math.Clamp(_vm.State.Width <= 0 ? ExpW : _vm.State.Width, MinWidth_, MaxWidth_) / ExpW;

    void ApplyLayout()
    {
        var collapsed = _vm.IsCollapsed;
        ExpandedView.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapsedView.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        var s = Scale;
        Width = (collapsed ? ColW : ExpW) * s;
        Height = (collapsed ? ColH : ExpH) * s;
    }

    void UpdatePinVisual()
    {
        PinButton.Content = _vm.IsPinned ? "" : "";
        PinButton.Foreground = _vm.IsPinned ? (Brush)FindResource("Pink") : (Brush)FindResource("Accent");
        PinButton.ToolTip = _vm.IsPinned ? "Pinned on top · click to unpin" : "Not pinned · click to keep on top";
    }

    // ---- placement ----------------------------------------------------------------------

    void PlaceFromState()
    {
        var s = _vm.State;
        if (s.Left is double l && s.Top is double t && IsOnScreen(l, t)) { Left = l; Top = t; return; }
        PlaceDefault();
    }

    bool IsOnScreen(double l, double t)
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var me = new Rect(l, t, Width, Height);
        me.Intersect(screen);
        return !me.IsEmpty && me.Width >= 60 && me.Height >= 30;
    }

    public void PlaceDefault()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 24;
        Top = wa.Top + 24;
    }

    // ---- dragging / click-to-expand -----------------------------------------------------

    void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || e.OriginalSource is Thumb) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    void OnBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        var (l, t) = (Left, Top);
        try { DragMove(); } catch (InvalidOperationException) { }
        // A click (no movement) expands the bar; a drag just moves it.
        if (Math.Abs(Left - l) < 3 && Math.Abs(Top - t) < 3) _vm.ExpandCommand.Execute(null);
    }

    void OnNextMeetingClick(object sender, MouseButtonEventArgs e) => _vm.OpenDashboardCommand.Execute(null);

    // ---- client list --------------------------------------------------------------------

    void OnListClick(object sender, RoutedEventArgs e)
    {
        // Clicking the button while the popup is open first closes it (StaysOpen=False); don't re-open.
        if (Environment.TickCount64 - _popupClosedTick < 250) return;
        _vm.IsClientListOpen = !_vm.IsClientListOpen;
    }

    void OnPopupClosed(object? sender, EventArgs e) => _popupClosedTick = Environment.TickCount64;

    // ---- resizing (keeps the aspect ratio) -----------------------------------------------

    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    Point CursorDip()
    {
        GetCursorPos(out var p);
        var src = PresentationSource.FromVisual(this);
        return src?.CompositionTarget?.TransformFromDevice.Transform(new Point(p.X, p.Y)) ?? new Point(p.X, p.Y);
    }

    void OnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (_resizeStartWidth == 0) { _resizeStartCursor = CursorDip(); _resizeStartWidth = Width; }
        var dx = CursorDip().X - _resizeStartCursor.X;
        var newWindowW = Math.Clamp(_resizeStartWidth + dx, MinWidth_, MaxWidth_);
        _vm.State.Width = newWindowW; // expanded layout: window width == design width * scale
        ApplyLayout();
    }

    void OnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        _resizeStartWidth = 0;
        _vm.State.Height = ExpH * Scale;
        _vm.SaveSoon();
    }
}
