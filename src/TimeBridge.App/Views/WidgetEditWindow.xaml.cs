using TimeBridge.App.ViewModels;
using Wpf.Ui.Controls;

namespace TimeBridge.App.Views;

public partial class WidgetEditWindow : FluentWindow
{
    public WidgetEditWindow(WidgetEditViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.Saved += Close;
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
