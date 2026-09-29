using Microsoft.UI.Xaml;
using WinRT;

namespace WinUI.AvalonDock.Experiments.Mvvm;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ComWrappersSupport.InitializeComWrappers();

        Application.Start(initialization =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
            _ = new DockingApplication();
        });
    }
}
