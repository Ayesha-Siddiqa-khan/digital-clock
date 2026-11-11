using Microsoft.Win32;

namespace TimeBridge.App.Services;

/// <summary>Per-user "start with Windows" via the HKCU Run key (no admin rights needed).</summary>
public static class StartupService
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "TimeBridge";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue(ValueName, $"\"{exe}\" --minimized");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }
}
