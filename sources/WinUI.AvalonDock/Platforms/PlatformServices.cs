namespace AvalonDock.Platforms;

/// <summary>
/// Internal platform services used by the ported AvalonDock model.
/// </summary>
internal static class PlatformServices
{
    internal static IFocusService Focus { get; } = new Windows.WindowsFocusService();
    internal static IWindowOrderService WindowOrder { get; } = new Windows.WindowsWindowOrderService();
    internal static IWindowGeometryService WindowGeometry { get; } = new Windows.WindowsWindowGeometryService();
    internal static ICoordinateService Coordinates { get; } = new Windows.WindowsCoordinateService();
    internal static IPointerGestureService PointerGestures { get; } = new Windows.WindowsPointerGestureService();

    internal static IDragInputService CreateDragInputService(Microsoft.UI.Xaml.FrameworkElement origin) =>
        new Windows.WindowsDragInputService(origin);

    internal static IWindowHostService CreateWindowHostService(Microsoft.UI.Xaml.FrameworkElement owner) =>
        new Windows.WindowsWindowHostService(owner);

    internal static IOverlayWindowSurface CreateOverlayWindowSurface(Microsoft.UI.Xaml.Window window, Microsoft.UI.Xaml.FrameworkElement owner) =>
        new Windows.WindowsOverlayWindowSurface(window, owner);

    internal static IChildWindowHost CreateChildWindowHost(Compatibility.HwndHost owner) => new Windows.WindowsChildWindowHost(owner);
    internal static IWindowIconSurface CreateWindowIconSurface(Microsoft.UI.Xaml.Window window) => new Windows.WindowsWindowIconSurface(window);
    internal static IModalWindowHost CreateModalWindowHost(Microsoft.UI.Xaml.Window window, Microsoft.UI.Xaml.FrameworkElement owner) => new Windows.WindowsModalWindowHost(window, owner);
    internal static IWindowSystemCommands CreateWindowSystemCommands(Microsoft.UI.Xaml.Window window) => new Windows.WindowsSystemCommands(window);
}
