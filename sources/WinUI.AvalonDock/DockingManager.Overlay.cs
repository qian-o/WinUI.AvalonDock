// Adapted from Dirkster.AvalonDock v5.0.0 overlay/drop target rules (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/OverlayWindow.cs and *DropTarget.cs
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager
{
    private readonly DockingOverlay dockingOverlay = new();

    private DragService? overlayDragService;

    private void UpdateOverlayTarget(Point pointer)
    {
        LayoutFloatingWindow? sourceModel = chromeDragModel ?? (LayoutFloatingWindow?)paneDragFloating ?? tabDragFloating;
        LayoutFloatingWindowControl? source = floatingControls.FirstOrDefault(window => ReferenceEquals(window.Model, sourceModel));
        if (source == null)
        {
            overlayDragService?.Abort();
            overlayDragService = null;
            dragTarget = null;
            return;
        }
        if (!ReferenceEquals(overlayDragService?.FloatingWindow, source))
        {
            overlayDragService?.Abort();
            overlayDragService = new DragService(source);
        }
        overlayDragService.UpdateMouseLocation(pointer);
        DropTargetBase? target = overlayDragService.CurrentDropTarget as DropTargetBase;
        dragTarget = target == null ? null : new DockTarget(target.Target.Model, target.Target.Area);
    }



    private static IEnumerable<T> VisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (T child in VisualChildren<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }
}
