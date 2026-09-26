// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager.cs overlay host implementation.
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager : IOverlayWindowHost
{
    DockingManager IOverlayWindowHost.Manager => this;
    bool IOverlayWindowHost.HitTestScreen(Point dragPoint) => OverlayHost.HitTest(this, dragPoint);
    IOverlayWindow IOverlayWindowHost.ShowOverlayWindow(LayoutFloatingWindowControl draggingWindow) => ShowOverlayWindow(this, draggingWindow);
    void IOverlayWindowHost.HideOverlayWindow()
    {
        areas = null;
        HideOverlayWindow(this);
    }

    internal IOverlayWindow ShowOverlayWindow(IOverlayWindowHost host, LayoutFloatingWindowControl draggingWindow) => dockingOverlay.Show(host, draggingWindow);
    internal void HideOverlayWindow(IOverlayWindowHost host) => dockingOverlay.Hide(host);

    internal DropTargetBase InitializeDropTarget(DropTargetBase result, OverlayTarget target, LayoutFloatingWindow model, int tabIndex)
    {
        LayoutContent[] contents = model.Descendents().OfType<LayoutContent>().ToArray();
        result.Target = target;
        result.TabIndex = tabIndex;
        // WinUI may clear root activation while the source child island unloads.
        // Retain the session's active model before releasing native capture and previews.
        result.ActiveContent = draggedContent ?? model.Root?.ActiveContent;
        result.CanCommit = source => ReferenceEquals(source, model) && source.Root?.Manager == this && target.Area.IsLoaded
            && target.Model.Root?.Manager == this && !ReferenceEquals(target.Model.FindParent<LayoutFloatingWindow>(), source)
            && contents.Length > 0 && contents.SequenceEqual(source.Descendents().OfType<LayoutContent>());
        return result;
    }

    internal void CommitDropTarget(IDropTarget target, LayoutFloatingWindow source)
    {
        ILayoutRoot? root = source.Root;
        if (root?.Manager != this)
        {
            return;
        }
        // Native child roots must survive until the upstream target has transferred the whole tree.
        bool previous = synchronizingWindows;
        synchronizingWindows = true;
        try
        {
            target.Drop(source);
            root.CollectGarbage();
        }
        finally
        {
            synchronizingWindows = previous;
            SynchronizeWindowHosts();
        }
    }

    private Action? CaptureOverlayDrop(Point pointer)
    {
        if (overlayDragService == null)
        {
            return null;
        }

        DragService service = overlayDragService;
        return () =>
        {
            try
            {
                service.Drop(pointer, out _);
            }
            finally { service.Abort(); }
        };
    }

}
