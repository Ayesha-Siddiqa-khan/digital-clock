using System.Windows;

namespace TimeBridge.App.Services;

public static class Ui
{
    /// <summary>Runs <paramref name="work"/> on the UI thread (inline if already there).</summary>
    public static Task Run(Func<Task> work)
    {
        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) return work();
        return d.InvokeAsync(work).Task.Unwrap();
    }

    public static Task Run(Action work) => Run(() => { work(); return Task.CompletedTask; });
}
