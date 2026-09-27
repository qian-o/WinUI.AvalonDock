using Microsoft.UI.Xaml;

namespace WinUI.AvalonDock.Experiments.ToggleDockingManager;

public sealed partial class DockingApplication : Application
{
    private MainWindow? window;

    internal DockingApplication()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 780));
        window.Activate();
    }
}
