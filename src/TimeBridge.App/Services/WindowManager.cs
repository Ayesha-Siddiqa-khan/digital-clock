using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TimeBridge.App.ViewModels;
using TimeBridge.App.Views;

namespace TimeBridge.App.Services;

/// <summary>Creates, shows and hides the app's windows. Windows are created lazily from the container.</summary>
public sealed class WindowManager
{
    readonly IServiceProvider _sp;
    WidgetWindow? _widget;
    MainWindow? _main;
    WidgetEditWindow? _edit;
    StopwatchWindow? _stopwatch;

    public WindowManager(IServiceProvider sp) { _sp = sp; }

    public bool Exiting { get; private set; }
    public bool IsWidgetShown => _widget?.IsVisible == true;
    public event Action<bool>? WidgetVisibilityChanged;

    public void ShowDashboard(int? page = null, bool minimized = false)
    {
        _main ??= new MainWindow(_sp.GetRequiredService<MainViewModel>(), this);
        if (page is int p) _sp.GetRequiredService<MainViewModel>().NavIndex = p;
        if (minimized) { _main.WindowState = WindowState.Minimized; _main.Show(); return; }
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Show();
        _main.Activate();
    }

    public async Task ShowWidgetAsync()
    {
        var vm = _sp.GetRequiredService<WidgetViewModel>();
        if (_widget == null)
        {
            _widget = new WidgetWindow(vm);
            _widget.IsVisibleChanged += (_, _) => WidgetVisibilityChanged?.Invoke(_widget.IsVisible);
        }
        vm.State.IsVisible = true;
        _widget.Show();
        _widget.Activate();
        await vm.SaveNowAsync();
    }

    /// <summary>Hides the widget without stopping the app. <paramref name="keepVisibleOnNextLaunch"/> is true for
    /// Minimise (comes back on next start) and false for Close (stays hidden until restored).</summary>
    public void HideWidget(bool keepVisibleOnNextLaunch)
    {
        var vm = _sp.GetRequiredService<WidgetViewModel>();
        vm.State.IsVisible = keepVisibleOnNextLaunch;
        vm.IsClientListOpen = false;
        _widget?.Hide();
        vm.SaveSoon();
    }

    public void ResetWidgetPosition()
    {
        var vm = _sp.GetRequiredService<WidgetViewModel>();
        vm.State.Left = null; vm.State.Top = null;
        _widget?.PlaceDefault();
        vm.SaveSoon();
    }

    public void OpenWidgetEditor()
    {
        if (_edit != null) { _edit.Activate(); return; }
        _edit = new WidgetEditWindow(_sp.GetRequiredService<WidgetEditViewModel>());
        _edit.Closed += (_, _) => _edit = null;
        _edit.Show();
    }

    /// <summary>Opens the stopwatch window (it is independent of the clock widget).</summary>
    public void ShowStopwatch()
    {
        _stopwatch ??= new StopwatchWindow(_sp.GetRequiredService<StopwatchViewModel>());
        _stopwatch.Show();
        _stopwatch.Activate();
    }

    public void Exit()
    {
        Exiting = true;
        Application.Current.Shutdown();
    }
}


