using Microsoft.UI.Xaml;

namespace WinUI.AvalonDock.Verification.PackageConsumer;

public sealed partial class ConsumerApplication : Application
{
    private Window? window;

    public ConsumerApplication() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Environment.GetCommandLineArgs().Any(value => value is "--runtime-lifetime" or "--xaml-source-lifetime" or "--menu-lifetime" or "--caption-lifetime" or "--composite-lifetime"))
        {
            window = new Window { Title = "Plain WinUI runtime lifetime baseline", Content = new Microsoft.UI.Xaml.Controls.Grid() };
            RuntimeLifetimeSmokeTests.Start(window);
            window.Activate();
            return;
        }
        window = new MainWindow();
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 740));
        window.Activate();
    }
}
