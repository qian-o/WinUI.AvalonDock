using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Platforms;

internal interface ICoordinateService
{
    bool TryGetScreenBounds(FrameworkElement element, out Rect bounds);
    bool IsVisibleAt(FrameworkElement element, Point position, IDockingWindowHost? excludedHost = null);
    Point ToHostLogical(FrameworkElement element, Point position);
    /// <summary>Converts physical desktop coordinates to the supplied element's local XAML units.</summary>
    Point ToElementLocal(FrameworkElement element, Point position);
    /// <summary>Returns the current physical desktop pointer position for source-style pane geometry checks.</summary>
    bool TryGetPointerPosition(out Point position);
    bool IsPointerOver(FrameworkElement element);
    bool ActivateWindow(FrameworkElement element);
}
