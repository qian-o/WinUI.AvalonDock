using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsPlatformServices : IPlatformServices
{
    public IFocusService Focus { get; } = new WindowsFocusService();
    public IKeyboardInputService Keyboard { get; } = new WindowsKeyboardInputService();
    public IWindowOrderService WindowOrder { get; } = new WindowsWindowOrderService();
    public IWindowGeometryService WindowGeometry { get; } = new WindowsWindowGeometryService();
    public ICoordinateService Coordinates { get; } = new WindowsCoordinateService();
    public IPointerGestureService PointerGestures { get; } = new WindowsPointerGestureService();

    public IDragInputService CreateDragInputService(FrameworkElement origin) => new WindowsDragInputService(origin);
    public IWindowHostService CreateWindowHostService(FrameworkElement owner) => new WindowsWindowHostService(owner);
    public IOverlayWindowSurface CreateOverlayWindowSurface(Window window, FrameworkElement owner) => new WindowsOverlayWindowSurface(window, owner);
    public IChildWindowHost CreateChildWindowHost(IChildWindowHostOwner owner) => new WindowsChildWindowHost(owner);
    public IWindowIconSurface CreateWindowIconSurface(Window window) => new WindowsWindowIconSurface(window);
    public IModalWindowHost CreateModalWindowHost(Window window, FrameworkElement owner) => new WindowsModalWindowHost(window, owner);
    public IWindowSystemCommands CreateWindowSystemCommands(Window window) => new WindowsSystemCommands(window);
}
