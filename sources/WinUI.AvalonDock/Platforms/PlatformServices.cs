using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms;

/// <summary>
/// Internal platform services used by the ported AvalonDock model.
/// </summary>
internal static class PlatformServices
{
    private static readonly IPlatformServices Current = new Windows.WindowsPlatformServices();

    internal static IFocusService Focus => Current.Focus;
    internal static IKeyboardInputService Keyboard => Current.Keyboard;
    internal static IWindowOrderService WindowOrder => Current.WindowOrder;
    internal static IWindowGeometryService WindowGeometry => Current.WindowGeometry;
    internal static ICoordinateService Coordinates => Current.Coordinates;
    internal static IPointerGestureService PointerGestures => Current.PointerGestures;

    internal static IDragInputService CreateDragInputService(FrameworkElement origin) => Current.CreateDragInputService(origin);
    internal static IWindowHostService CreateWindowHostService(FrameworkElement owner) => Current.CreateWindowHostService(owner);
    internal static IOverlayWindowSurface CreateOverlayWindowSurface(Window window, FrameworkElement owner) => Current.CreateOverlayWindowSurface(window, owner);
    internal static IChildWindowHost CreateChildWindowHost(IChildWindowHostOwner owner) => Current.CreateChildWindowHost(owner);
    internal static IWindowIconSurface CreateWindowIconSurface(Window window) => Current.CreateWindowIconSurface(window);
    internal static IModalWindowHost CreateModalWindowHost(Window window, FrameworkElement owner) => Current.CreateModalWindowHost(window, owner);
    internal static IWindowSystemCommands CreateWindowSystemCommands(Window window) => Current.CreateWindowSystemCommands(window);
}
