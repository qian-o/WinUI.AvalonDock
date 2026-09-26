// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), pane floating and pane drop paths.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / DockingManager.cs and Controls/AnchorablePaneDropTarget.cs
using AvalonDock.Controls;
using AvalonDock.Core.Events;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager
{
    private LayoutAnchorablePane? draggedPane;
    private LayoutAnchorable[] paneDragContents = [];
    private LayoutAnchorableFloatingWindow? paneDragFloating;
    private IDockingWindowHost? paneDragHost;
    private Point paneDragAnchor;

    internal void BeginPaneDrag(LayoutAnchorablePane pane, FrameworkElement origin)
    {
        if (this is ToggleDockingManager toggle && pane.SelectedContent is LayoutAnchorable tool && tool.CanMove && !IsDetached(tool))
        {
            toggle.BeginZoneDrag(tool, origin, waitForLeave: true);
            return;
        }
        if (dragInput != null || pane.Root?.Manager != this || pane.SelectedContent is not LayoutAnchorable selected
            || !selected.CanMove || IsDetached(selected))
        {
            return;
        }

        BeginContentDrag(selected, origin);
        if (dragInput == null)
        {
            return;
        }

        draggedPane = pane;
        paneDragContents = pane.Children.ToArray();
        if (pane.FindParent<LayoutAnchorableFloatingWindow>() is { } floating
            && floating.Descendents().OfType<LayoutAnchorablePane>().Count() == 1
            && floatingHosts.TryGetValue(floating, out IDockingWindowHost? existing))
        {
            paneDragFloating = floating;
            paneDragHost = existing;
        }
        double scale = origin.XamlRoot?.RasterizationScale ?? 1;
        paneDragAnchor = paneDragHost != null ? paneDragHost.GetPointerAnchor(dragInput.Position)
            : PlatformServices.Coordinates.TryGetScreenBounds(origin, out Rect bounds)
            ? new Point((dragStart.X - bounds.X) / scale, (dragStart.Y - bounds.Y) / scale + 32)
            : new Point(30, 32);
    }

    private void OnPaneDragInput(DragInputUpdate update)
    {
        LayoutAnchorablePane? pane = draggedPane;
        LayoutAnchorable? selected = draggedContent as LayoutAnchorable;
        if (pane == null || selected == null || update.Kind == DragInputUpdateKind.Cancelled
            || selected.Root?.Manager != this || !selected.CanMove
            || paneDragContents.Any(tool => tool.Root?.Manager != this)
            || !paneDragContents.SequenceEqual(pane.Children))
        {
            EndContentDrag();
            return;
        }
        Point point = new(update.Position.X, update.Position.Y);
        if (!dragStarted)
        {
            bool leaveTitle = dragOrigin == null || !PlatformServices.Coordinates.TryGetScreenBounds(dragOrigin, out Rect titleBounds)
                || !titleBounds.Contains(point);
            Size threshold = PlatformServices.PointerGestures.GetDragThreshold(dragOrigin!);
            bool moved = Math.Abs(point.X - dragStart.X) > threshold.Width || Math.Abs(point.Y - dragStart.Y) > threshold.Height;
            if (paneDragHost == null ? !leaveTitle : !moved)
            {
                if (update.Kind == DragInputUpdateKind.Released)
                {
                    EndContentDrag();
                }

                return;
            }
            if (paneDragHost == null)
            {
                (LayoutAnchorableFloatingWindow Model, IDockingWindowHost Host, LayoutAnchorablePane Pane)? result = FloatAnchorablePane(pane);
                if (result == null || dragInput == null)
                {
                    EndContentDrag();
                    return;
                }
                (paneDragFloating, paneDragHost, draggedPane) = result.Value;
                if (controlsByHost.TryGetValue(paneDragHost, out LayoutFloatingWindowControl? control))
                {
                    control.SetDraggingState(true);
                }
            }
            dragStarted = true;
        }
        UpdateDragTarget(selected, point);
        if (update.Kind != DragInputUpdateKind.Released)
        {
            paneDragHost?.MoveWithPointer(update.Position, paneDragAnchor);
            return;
        }
        Action? overlayDrop = CaptureOverlayDrop(point);
        EndContentDrag(overlayDrop != null);
        overlayDrop?.Invoke();
    }

    internal virtual void StartDraggingFloatingWindowForPane(LayoutAnchorablePane paneModel)
    {
        (LayoutAnchorableFloatingWindow Model, IDockingWindowHost Host, LayoutAnchorablePane Pane)? result = FloatAnchorablePane(paneModel);
        result?.Host.BeginMove();
    }

    private (LayoutAnchorableFloatingWindow Model, IDockingWindowHost Host, LayoutAnchorablePane Pane)? FloatAnchorablePane(LayoutAnchorablePane source)
    {
        if (!AllowFloatingWindows || source.Root?.Manager != this || source.Children.Count == 0)
        {
            return null;
        }

        LayoutAnchorable[] children = source.Children.ToArray();
        LayoutAnchorable first = children[0];
        ContentFloatingEventArgs args = new(first);
        ContentFloating?.Invoke(this, args);
        if (args.Cancel)
        {
            return null;
        }

        ContentCancelEventArgs coreArgs = new(first);
        coreContentFloating?.Invoke(this, coreArgs);
        if (coreArgs.Cancel || !AllowFloatingWindows || children.Any(child => !child.CanFloat) || source.Root?.Manager != this
            || !children.SequenceEqual(source.Children))
        {
            return null;
        }

        LayoutFloatingWindowControl? control = CreateFloatingWindowForLayoutAnchorableWithoutParent(source, false);
        if (control == null)
        {
            return null;
        }

        IDockingWindowHost host = control.WindowHost
            ?? throw new InvalidOperationException("A created floating window must have a host.");
        LayoutAnchorableFloatingWindow floating = (LayoutAnchorableFloatingWindow)control.Model;
        LayoutAnchorablePane destination = children[0].Parent as LayoutAnchorablePane
            ?? throw new InvalidOperationException("Floating anchorables must belong to a pane.");
        NotifyFloatingControlCreated(host);
        host.Show();
        ContentFloated?.Invoke(this, new ContentFloatedEventArgs(first));
        coreContentFloated?.Invoke(this, new Core.Events.ContentEventArgs(first));
        return (floating, host, destination);
    }

}
