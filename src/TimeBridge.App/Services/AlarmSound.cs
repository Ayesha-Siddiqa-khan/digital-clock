using System.IO;
using System.Media;
using System.Windows.Threading;

namespace TimeBridge.App.Services;

/// <summary>Reference-counted looping alarm sound: stays on while any alarm window is open.</summary>
public sealed class AlarmSound
{
    static readonly string WavPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Alarm01.wav");

    SoundPlayer? _player;
    DispatcherTimer? _beepTimer;
    int _users;

    public void Acquire()
    {
        if (_users++ > 0) return;
        try
        {
            if (File.Exists(WavPath))
            {
                _player = new SoundPlayer(WavPath);
                _player.PlayLooping();
                return;
            }
        }
        catch (Exception ex) { ErrorLog.Write(ex); }

        SystemSounds.Exclamation.Play();
        _beepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _beepTimer.Tick += (_, _) => SystemSounds.Exclamation.Play();
        _beepTimer.Start();
    }

    public void Release()
    {
        if (_users == 0 || --_users > 0) return;
        _player?.Stop(); _player?.Dispose(); _player = null;
        _beepTimer?.Stop(); _beepTimer = null;
    }

    /// <summary>One short chirp for previewing in settings.</summary>
    public static void Preview() => SystemSounds.Exclamation.Play();
}
