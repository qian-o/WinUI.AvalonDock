// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/IOverlayWindowHost.cs.
using Windows.Foundation;

namespace AvalonDock.Controls;

internal interface IOverlayWindowHost
{
    DockingManager Manager
    {
        get;
    }
    bool HitTestScreen(Point dragPoint);
    IOverlayWindow ShowOverlayWindow(LayoutFloatingWindowControl draggingWindow);
    void HideOverlayWindow();
    IEnumerable<IDropArea> GetDropAreas(LayoutFloatingWindowControl draggingWindow);
}
