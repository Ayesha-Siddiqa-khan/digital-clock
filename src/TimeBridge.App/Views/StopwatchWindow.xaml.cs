using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TimeBridge.App.ViewModels;

namespace TimeBridge.App.Views;

public partial class StopwatchWindow : Window
{
    public StopwatchWindow(StopwatchViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(StopwatchViewModel.IsPinned)) UpdatePin(vm); };
        UpdatePin(vm);
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - 340 - 24;
        Top = wa.Top + 360;
    }

    void UpdatePin(StopwatchViewModel vm) =>
        PinButton.Foreground = (Brush)new BrushConverter().ConvertFromString(vm.IsPinned ? "#F472B6" : "#C7D2FE")!;

    void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || e.OriginalSource is System.Windows.Controls.TextBlock { Parent: System.Windows.Controls.Button }) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    /// <summary>Hiding the window does not stop the stopwatch.</summary>
    void OnHide(object sender, RoutedEventArgs e) => Hide();
}
