// Adapted from Dirkster.AvalonDock v5.0.0 drag service and floating pane drop targets (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/DragService.cs and *PaneDropTarget.cs
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager
{
    private LayoutFloatingWindow? chromeDragModel;
    private IDockingWindowHost? chromeDragHost;
    private LayoutContent[] chromeDragContents = [];
    private long chromeDragSequence;

    private void OnWindowMove(LayoutFloatingWindow model, IDockingWindowHost host, WindowMoveUpdate update)
    {
        if (update.Phase == WindowMovePhase.Started)
        {
            if (dragInput != null || model.Root?.Manager != this || host.IsClosed)
            {
                return;
            }

            if (chromeDragHost != null || draggedContent != null || dragTarget != null)
            {
                EndContentDrag();
            }

            LayoutContent[] contents = model.Descendents().OfType<LayoutContent>().ToArray();
            if (contents.Length == 0)
            {
                return;
            }

            chromeDragModel = model;
            chromeDragHost = host;
            chromeDragContents = contents;
            chromeDragSequence = update.Sequence;
            draggedContent = contents.FirstOrDefault(content => content.IsActive) ?? contents.FirstOrDefault(content => content.IsSelected) ?? contents[0];
            if (controlsByHost.TryGetValue(host, out LayoutFloatingWindowControl? control))
            {
                control.SetDraggingState(true);
            }
        }
        if (!ReferenceEquals(chromeDragHost, host) || !ReferenceEquals(chromeDragModel, model) || chromeDragSequence != update.Sequence)
        {
            return;
        }

        if (update.Phase == WindowMovePhase.Cancelled || host.IsClosed || !IsLoaded || model.Root?.Manager != this
            || !chromeDragContents.SequenceEqual(model.Descendents().OfType<LayoutContent>()))
        {
            EndContentDrag();
            return;
        }
        LayoutContent selected = draggedContent!;
        UpdateDragTarget(selected, new Point(update.Position.X, update.Position.Y));
        if (update.Phase != WindowMovePhase.Released)
        {
            return;
        }

        Action? overlayDrop = CaptureOverlayDrop(new Point(update.Position.X, update.Position.Y));
        EndContentDrag(overlayDrop != null);
        overlayDrop?.Invoke();
    }

}
