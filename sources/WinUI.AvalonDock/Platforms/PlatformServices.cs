using AvalonDock.Platforms.Windows;
using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms;

internal static class PlatformServices
{
    internal static WindowsFocusService Focus { get; } = new();
    internal static WindowsKeyboardInputService Keyboard { get; } = new();
    internal static WindowsWindowOrderService WindowOrder { get; } = new();
    internal static WindowsWindowGeometryService WindowGeometry { get; } = new();
    internal static WindowsCoordinateService Coordinates { get; } = new();
    internal static WindowsPointerGestureService PointerGestures { get; } = new();

    internal static IDragInputService CreateDragInputService(FrameworkElement origin) => new WindowsDragInputService(origin);
    internal static IWindowHostService CreateWindowHostService(FrameworkElement owner) => new WindowsWindowHostService(owner);
    internal static IOverlayWindowSurface CreateOverlayWindowSurface(Window window, FrameworkElement owner) => new WindowsOverlayWindowSurface(window, owner);
    internal static IChildWindowHost CreateChildWindowHost(IChildWindowHostOwner owner) => new WindowsChildWindowHost(owner);
    internal static IWindowIconSurface CreateWindowIconSurface(Window window) => new WindowsWindowIconSurface(window);
    internal static IModalWindowHost CreateModalWindowHost(Window window, FrameworkElement owner) => new WindowsModalWindowHost(window, owner);
    internal static IWindowSystemCommands CreateWindowSystemCommands(Window window) => new WindowsSystemCommands(window);
}
