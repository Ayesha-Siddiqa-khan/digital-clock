using System.ComponentModel;
using TimeBridge.App.Services;
using TimeBridge.App.ViewModels;
using Wpf.Ui.Controls;

namespace TimeBridge.App.Views;

public partial class MainWindow : FluentWindow
{
    readonly WindowManager _windows;

    public MainWindow(MainViewModel vm, WindowManager windows)
    {
        InitializeComponent();
        DataContext = vm;
        _windows = windows;
    }

    /// <summary>Closing the dashboard keeps TimeBridge (and its alarms) running in the tray.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_windows.Exiting)
        {
            e.Cancel = true;
            WindowState = System.Windows.WindowState.Minimized; // stays in the taskbar; alarms keep running
        }
        base.OnClosing(e);
    }
}


