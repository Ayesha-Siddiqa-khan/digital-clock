using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

namespace TimeBridge.Setup;

/// <summary>Per-user installer: copies TimeBridge.exe to %LocalAppData%\Programs\TimeBridge, creates the desktop icon,
/// Start Menu entry and taskbar pin, and registers an uninstall entry. No admin rights needed.</summary>
static class Program
{
    const string Title = "TimeBridge Setup";

    [STAThread]
    static int Main(string[] args)
    {
        var quiet = args.Contains("/quiet", StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!quiet && MessageBox.Show(
                    "Install TimeBridge?\n\nThis adds a desktop icon, a Start Menu entry and pins TimeBridge to the taskbar.",
                    Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return 1;

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TimeBridge");
            var exe = Path.Combine(dir, "TimeBridge.exe");

            foreach (var p in Process.GetProcessesByName("TimeBridge"))
            {
                try { p.Kill(); p.WaitForExit(5000); } catch { }
            }

            Directory.CreateDirectory(dir);
            using (var rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("TimeBridge.exe")
                            ?? throw new InvalidOperationException("Installer payload is missing."))
            using (var fs = File.Create(exe))
                rs.CopyTo(fs);
            File.WriteAllText(Path.Combine(dir, "uninstall.ps1"), UninstallScript);

            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "TimeBridge.lnk"), exe, dir);
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TimeBridge.lnk"), exe, dir);
            PinToTaskbar(exe, dir);
            Register(exe, dir);

            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            if (!quiet)
                MessageBox.Show("TimeBridge is installed and running.\n\nOpen it from the desktop icon, Start Menu or taskbar. " +
                                "Closing the window keeps it in the taskbar so alarms keep working; use \"Exit TimeBridge\" to quit.",
                                Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }
        catch (Exception ex)
        {
            if (!quiet) MessageBox.Show($"Setup failed:\n\n{ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return 2;
        }
    }

    static void Shortcut(string path, string target, string workDir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(path);
        link.TargetPath = target;
        link.WorkingDirectory = workDir;
        link.IconLocation = target + ",0";
        link.Description = "TimeBridge";
        link.Save();
    }

    /// <summary>Windows offers no supported pin API; place the shortcut in the user's pinned-taskbar folder
    /// and, where still honoured, use the shell verb. Failures are ignored.</summary>
    static void PinToTaskbar(string exe, string dir)
    {
        try
        {
            var pinned = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\TimeBridge.lnk");
            Shortcut(pinned, exe, dir);
        }
        catch { }
        try
        {
            dynamic app = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            dynamic item = app.NameSpace(dir).ParseName("TimeBridge.exe");
            item?.InvokeVerb("taskbarpin");
        }
        catch { }
    }

    static void Register(string exe, string dir)
    {
        using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeBridge");
        k.SetValue("DisplayName", "TimeBridge");
        k.SetValue("DisplayVersion", "1.0.0");
        k.SetValue("Publisher", "TimeBridge");
        k.SetValue("DisplayIcon", exe);
        k.SetValue("InstallLocation", dir);
        k.SetValue("UninstallString",
            $"powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{Path.Combine(dir, "uninstall.ps1")}\"");
    }

    const string UninstallScript = """
        # Removes TimeBridge. Your data in %LocalAppData%\TimeBridge (clients, meetings) is kept.
        $dir = $PSScriptRoot
        Get-Process TimeBridge -ErrorAction SilentlyContinue | Stop-Process -Force
        Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'TimeBridge.lnk') -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'TimeBridge.lnk') -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\TimeBridge.lnk') -Force -ErrorAction SilentlyContinue
        Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name TimeBridge -ErrorAction SilentlyContinue
        Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TimeBridge' -Recurse -Force -ErrorAction SilentlyContinue
        Start-Process cmd.exe -WindowStyle Hidden -ArgumentList "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q `"$dir`""
        """;
}
