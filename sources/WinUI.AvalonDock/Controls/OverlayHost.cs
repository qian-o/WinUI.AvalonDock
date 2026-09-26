// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager and floating-window overlay hosts.
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Controls;

internal static class OverlayHost
{
    internal static void InvalidateAreas(IOverlayWindowHost host)
    {
        switch (host)
        {
            case DockingManager manager:
                manager.InvalidateDropAreas();
                break;
            case LayoutDocumentFloatingWindowControl documents:
                documents.InvalidateDropAreas();
                break;
            case LayoutAnchorableFloatingWindowControl tools:
                tools.InvalidateDropAreas();
                break;
        }
    }
    internal static FrameworkElement? Element(IOverlayWindowHost host) => host switch
    {
        DockingManager manager => manager,
        LayoutFloatingWindowControl window => window.HostedRoot,
        _ => null
    };
    internal static FrameworkElement? Element(IDropArea area) => area switch
    {
        DropArea<DockingManager> manager => manager.AreaElement,
        DropArea<LayoutDocumentPaneControl> documents => documents.AreaElement,
        DropArea<LayoutAnchorablePaneControl> tools => tools.AreaElement,
        DropArea<LayoutDocumentPaneGroupControl> group => group.AreaElement,
        _ => null
    };
    internal static bool HitTest(IOverlayWindowHost host, Point point) => Element(host) is { } element
        && PlatformServices.Coordinates.TryGetScreenBounds(element, out Rect bounds) && bounds.Contains(point);
}

public partial class LayoutDocumentFloatingWindowControl : IOverlayWindowHost
{
    DockingManager IOverlayWindowHost.Manager => Manager ?? throw new InvalidOperationException("浮动覆盖层宿主必须附接到 DockingManager。");
    bool IOverlayWindowHost.HitTestScreen(Point dragPoint) => OverlayHost.HitTest(this, dragPoint);
    IOverlayWindow IOverlayWindowHost.ShowOverlayWindow(LayoutFloatingWindowControl draggingWindow) => ((IOverlayWindowHost)this).Manager.ShowOverlayWindow(this, draggingWindow);
}

public partial class LayoutAnchorableFloatingWindowControl : IOverlayWindowHost
{
    DockingManager IOverlayWindowHost.Manager => Manager ?? throw new InvalidOperationException("浮动覆盖层宿主必须附接到 DockingManager。");
    bool IOverlayWindowHost.HitTestScreen(Point dragPoint) => OverlayHost.HitTest(this, dragPoint);
    IOverlayWindow IOverlayWindowHost.ShowOverlayWindow(LayoutFloatingWindowControl draggingWindow) => ((IOverlayWindowHost)this).Manager.ShowOverlayWindow(this, draggingWindow);
    void IOverlayWindowHost.HideOverlayWindow()
    {
        InvalidateDropAreas();
        Manager?.HideOverlayWindow(this);
    }
}
