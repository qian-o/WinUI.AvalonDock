// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/IDropTarget.cs.
using AvalonDock.Layout;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

internal interface IDropTarget
{
    DropTargetType Type
    {
        get;
    }
    Geometry? GetPreviewPath(OverlayWindow overlayWindow, LayoutFloatingWindow floatingWindowModel);
    bool HitTestScreen(Point dragPoint);
    void Drop(LayoutFloatingWindow floatingWindow);
    void DragEnter();
    void DragLeave();
}
