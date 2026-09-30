using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms;

/// <summary>
/// Internal platform services used by the ported AvalonDock model.
/// </summary>
internal static class PlatformServices
{
    private static readonly IPlatformServices current = new Windows.WindowsPlatformServices();

    internal static IFocusService Focus => current.Focus;
    internal static IKeyboardInputService Keyboard => current.Keyboard;
    internal static IWindowOrderService WindowOrder => current.WindowOrder;
    internal static IWindowGeometryService WindowGeometry => current.WindowGeometry;
    internal static ICoordinateService Coordinates => current.Coordinates;
    internal static IPointerGestureService PointerGestures => current.PointerGestures;

    internal static IDragInputService CreateDragInputService(FrameworkElement origin) => current.CreateDragInputService(origin);
    internal static IWindowHostService CreateWindowHostService(FrameworkElement owner) => current.CreateWindowHostService(owner);
    internal static IOverlayWindowSurface CreateOverlayWindowSurface(Window window, FrameworkElement owner) => current.CreateOverlayWindowSurface(window, owner);
    internal static IChildWindowHost CreateChildWindowHost(IChildWindowHostOwner owner) => current.CreateChildWindowHost(owner);
    internal static IWindowIconSurface CreateWindowIconSurface(Window window) => current.CreateWindowIconSurface(window);
    internal static IModalWindowHost CreateModalWindowHost(Window window, FrameworkElement owner) => current.CreateModalWindowHost(window, owner);
    internal static IWindowSystemCommands CreateWindowSystemCommands(Window window) => current.CreateWindowSystemCommands(window);
}
