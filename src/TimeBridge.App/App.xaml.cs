using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeBridge.App.Services;
using TimeBridge.App.ViewModels;
using TimeBridge.Core.Data;
using TimeBridge.Core.Services;

namespace TimeBridge.App;

public partial class App : Application
{
    const string MutexName = @"Local\TimeBridge.SingleInstance";
    const string ShowEventName = @"Local\TimeBridge.ShowDashboard";

    Mutex? _mutex;
    EventWaitHandle? _showEvent;
    TaskbarIcon? _tray;
    bool _started;
    bool _ownsMutex;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var isFirst);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _ownsMutex = isFirst;
        if (!isFirst)
        {
            // Another instance owns the tray + alarms: ask it to show its dashboard, then leave.
            _showEvent.Set();
            Shutdown();
            return;
        }

        var minimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
        var dbIndex = Array.FindIndex(e.Args, a => a.Equals("--db", StringComparison.OrdinalIgnoreCase));
        var dbPath = dbIndex >= 0 && dbIndex + 1 < e.Args.Length ? e.Args[dbIndex + 1] : DatabaseSetup.DefaultDatabasePath();

        try
        {
            Services = BuildServices(dbPath);

            var clock = Services.GetRequiredService<IClock>();
            await DatabaseSetup.InitializeAsync(Services.GetRequiredService<IDbContextFactory<TimeBridgeDbContext>>(), clock);
            var settings = Services.GetRequiredService<SettingsService>();
            await settings.LoadAsync();
            StartupService.Apply(settings.Current.StartWithWindows);

            var widgetVm = Services.GetRequiredService<WidgetViewModel>();
            await widgetVm.InitializeAsync();
            await Services.GetRequiredService<MainViewModel>().InitializeAsync();

            Services.GetRequiredService<ClockTicker>().Start();
            Services.GetRequiredService<AlarmService>().Start();
            await Services.GetRequiredService<AlarmScheduler>().StartAsync();

            var windows = Services.GetRequiredService<WindowManager>();
            CreateTrayIcon(windows);
            ListenForSecondInstance(windows);

            if (widgetVm.State.IsVisible) await windows.ShowWidgetAsync();
            windows.ShowDashboard(minimized: minimized); // autostart (--minimized) keeps a minimised taskbar button
            _started = true;
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            MessageBox.Show($"TimeBridge could not start.\n\n{ex.Message}\n\nDetails were written to {ErrorLog.Directory}.",
                "TimeBridge", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    static ServiceProvider BuildServices(string dbPath)
    {
        var sc = new ServiceCollection();
        sc.AddDbContextFactory<TimeBridgeDbContext>(o => o.UseSqlite(DatabaseSetup.ConnectionString(dbPath)));

        sc.AddSingleton<IClock, SystemClock>();
        sc.AddSingleton<SettingsService>();
        sc.AddSingleton<ClientService>();
        sc.AddSingleton<MeetingService>();
        sc.AddSingleton<WidgetStateService>();
        sc.AddSingleton<AlarmScheduler>();

        sc.AddSingleton<IDialogService, DialogService>();
        sc.AddSingleton<ClockTicker>();
        sc.AddSingleton<AlarmSound>();
        sc.AddSingleton<AlarmService>();
        sc.AddSingleton<WindowManager>();

        sc.AddSingleton<WidgetViewModel>();
        sc.AddSingleton<MeetingsViewModel>();
        sc.AddSingleton<ClientsViewModel>();
        sc.AddSingleton<SettingsViewModel>();
        sc.AddSingleton<StopwatchViewModel>();
        sc.AddSingleton<MainViewModel>();
        sc.AddTransient<WidgetEditViewModel>();
        return sc.BuildServiceProvider();
    }

    void CreateTrayIcon(WindowManager windows)
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Open dashboard" };
        open.Click += (_, _) => windows.ShowDashboard();
        var widget = new MenuItem();
        widget.Click += (_, _) =>
        {
            if (windows.IsWidgetShown) windows.HideWidget(keepVisibleOnNextLaunch: false);
            else _ = windows.ShowWidgetAsync();
        };
        var stopwatch = new MenuItem { Header = "Stopwatch" };
        stopwatch.Click += (_, _) => windows.ShowStopwatch();
        var exit = new MenuItem { Header = "Exit TimeBridge" };
        exit.Click += (_, _) => windows.Exit();
        menu.Items.Add(open);
        menu.Items.Add(widget);
        menu.Items.Add(stopwatch);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        menu.Opened += (_, _) => widget.Header = windows.IsWidgetShown ? "Hide widget" : "Show widget";

        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _tray = new TaskbarIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            ToolTipText = "TimeBridge — alarms are running",
            ContextMenu = menu,
            MenuActivation = PopupActivationMode.RightClick,
        };
        _tray.TrayMouseDoubleClick += (_, _) => windows.ShowDashboard();
    }

    void ListenForSecondInstance(WindowManager windows)
    {
        var thread = new Thread(() =>
        {
            while (_showEvent!.WaitOne())
                Dispatcher.BeginInvoke(() => windows.ShowDashboard());
        }) { IsBackground = true, Name = "TimeBridge.ShowListener" };
        thread.Start();
    }

    void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLog.Write(e.Exception);
        e.Handled = true;
        if (_started)
            MessageBox.Show($"Something went wrong: {e.Exception.Message}\n\nDetails: {ErrorLog.Directory}",
                "TimeBridge", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_started)
            {
                // Persist final widget position/size/pin state before closing.
                Task.Run(() => Services.GetRequiredService<WidgetViewModel>().SaveNowAsync()).Wait(TimeSpan.FromSeconds(3));
                Services.GetRequiredService<AlarmScheduler>().Dispose();
            }
            _tray?.Dispose();
            if (_ownsMutex) _mutex?.ReleaseMutex();
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
        base.OnExit(e);
    }
}



