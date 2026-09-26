using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms;

/// <summary>Groups the platform services required by the shared docking engine.</summary>
/// <remarks>
/// The Windows implementation is the default provider. A future Uno backend can supply the
/// same semantic services without changing layout, drag target, or docking state code.
/// </remarks>
internal interface IPlatformServices
{
    IFocusService Focus
    {
        get;
    }
    IKeyboardInputService Keyboard
    {
        get;
    }
    IWindowOrderService WindowOrder
    {
        get;
    }
    IWindowGeometryService WindowGeometry
    {
        get;
    }
    ICoordinateService Coordinates
    {
        get;
    }
    IPointerGestureService PointerGestures
    {
        get;
    }
    IDragInputService CreateDragInputService(FrameworkElement origin);
    IWindowHostService CreateWindowHostService(FrameworkElement owner);
    IOverlayWindowSurface CreateOverlayWindowSurface(Window window, FrameworkElement owner);
    IChildWindowHost CreateChildWindowHost(IChildWindowHostOwner owner);
    IWindowIconSurface CreateWindowIconSurface(Window window);
    IModalWindowHost CreateModalWindowHost(Window window, FrameworkElement owner);
    IWindowSystemCommands CreateWindowSystemCommands(Window window);
}
