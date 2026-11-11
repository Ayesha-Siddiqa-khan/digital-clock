using System.Windows;

namespace TimeBridge.App.Services;

public interface IDialogService
{
    bool Confirm(string message, string title = "TimeBridge");
    void Error(string message, string title = "TimeBridge");
    void Info(string message, string title = "TimeBridge");
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string message, string title = "TimeBridge") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Error(string message, string title = "TimeBridge") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void Info(string message, string title = "TimeBridge") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
