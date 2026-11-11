using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeBridge.App.Services;
using TimeBridge.Core.Data;
using TimeBridge.Core.Services;

namespace TimeBridge.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    readonly SettingsService _settings;
    readonly AlarmService _alarms;
    readonly WindowManager _windows;

    public SettingsViewModel(SettingsService settings, AlarmService alarms, WindowManager windows)
    { _settings = settings; _alarms = alarms; _windows = windows; }

    public IReadOnlyList<ZoneOption> Zones { get; } = TimeZoneCatalog.All();
    public IReadOnlyList<ReminderChoice> ReminderChoices { get; } =
        ReminderOptions.Minutes.Select(m => new ReminderChoice(m, ReminderOptions.Label(m))).ToList();
    public IReadOnlyList<int> SnoozeChoices { get; } = new[] { 1, 2, 5, 10, 15, 30 };
    public string DatabasePath { get; } = DatabaseSetup.DefaultDatabasePath();
    public string Version { get; } = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    [ObservableProperty] string _homeZoneId = TimeZoneCatalog.PakistanId;
    [ObservableProperty] bool _showSeconds = true;
    [ObservableProperty] double _opacity = 0.96;
    [ObservableProperty] int _defaultReminder = 10;
    [ObservableProperty] int _snoozeMinutes = 5;
    [ObservableProperty] bool _alarmSound = true;
    [ObservableProperty] bool _startWithWindows;
    [ObservableProperty] string _status = "";

    public void Initialize() => Load(_settings.Current);

    void Load(AppSettingsData d)
    {
        HomeZoneId = d.HomeTimeZoneId; ShowSeconds = d.ShowSeconds; Opacity = d.WidgetOpacity;
        DefaultReminder = d.DefaultReminderMinutes; SnoozeMinutes = d.SnoozeMinutes;
        AlarmSound = d.AlarmSoundEnabled; StartWithWindows = d.StartWithWindows;
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        await _settings.SaveAsync(new AppSettingsData
        {
            HomeTimeZoneId = HomeZoneId, ShowSeconds = ShowSeconds, WidgetOpacity = Math.Round(Opacity, 2),
            DefaultReminderMinutes = DefaultReminder, SnoozeMinutes = SnoozeMinutes,
            AlarmSoundEnabled = AlarmSound, StartWithWindows = StartWithWindows
        });
        StartupService.Apply(StartWithWindows);
        Status = "Settings saved.";
    }

    [RelayCommand] void TestAlarm() => _alarms.Test();
    [RelayCommand] void ShowWidget() => _ = _windows.ShowWidgetAsync();
    [RelayCommand] void ResetWidget() => _windows.ResetWidgetPosition();

    [RelayCommand]
    void OpenDataFolder()
    {
        var dir = Path.GetDirectoryName(DatabasePath)!;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }
}
