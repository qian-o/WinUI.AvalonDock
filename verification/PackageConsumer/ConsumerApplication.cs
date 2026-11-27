using AvalonDock;
using Microsoft.UI.Xaml;

namespace WinUI.AvalonDock.Verification.PackageConsumer;

public sealed partial class ConsumerApplication : Application
{
    private Window? window;
    private readonly string output = Environment.GetCommandLineArgs().ElementAtOrDefault(1) ?? Path.Combine(AppContext.BaseDirectory, "result.log");

    public ConsumerApplication()
    {
        UnhandledException += (_, e) => File.WriteAllText(output, e.Exception.ToString());
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DockingManager manager = new();
        window = new Window { Title = "AvalonDock package check", Content = manager };
        manager.Loaded += OnLoaded;
        window.Activate();
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        DockingManager manager = (DockingManager)sender;
        manager.Loaded -= OnLoaded;
        try
        {
            await PackageSmokeTests.RunAsync(manager);
            File.WriteAllText(output, "PASS");
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            File.WriteAllText(output, error.ToString());
        }
        finally
        {
            manager.Dispose();
            window!.Close();
            Exit();
        }
    }
}
