// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/OverlayWindow.cs.
using AvalonDock.Layout;
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

public partial class OverlayWindow : IOverlayWindow
{
    private readonly IOverlayWindowHost? overlayHost;
    private readonly List<IDropArea> visibleAreas = [];
    private LayoutFloatingWindowControl? floatingWindow;
    private IDropTarget? currentDropTarget;

    internal OverlayWindow(IOverlayWindowHost host) : this(OverlayHost.Element(host)
        ?? throw new ArgumentException("The overlay host has no connected content.", nameof(host)), host is LayoutFloatingWindowControl)
    {
        overlayHost = host;
    }

    internal LayoutFloatingWindowControl? DraggingWindow => floatingWindow;
    internal IOverlayWindowHost? Host => overlayHost;
    private void ClearDragAreas()
    {
        currentDropTarget?.DragLeave();
        currentDropTarget = null;
        visibleAreas.Clear();
        // Upstream HideOverlay retains the dragged window until the drop and leave calls finish.
    }

    void IOverlayWindow.DragEnter(LayoutFloatingWindowControl floatingWindow)
    {
        if (overlayHost == null || !ReferenceEquals(floatingWindow.Model?.Root?.Manager, overlayHost.Manager))
        {
            return;
        }

        if (!ReferenceEquals(this.floatingWindow, floatingWindow))
        {
            ClearDragAreas();
        }

        this.floatingWindow = floatingWindow;
    }
    void IOverlayWindow.DragLeave(LayoutFloatingWindowControl floatingWindow)
    {
        if (!ReferenceEquals(this.floatingWindow, floatingWindow))
        {
            return;
        }

        Hide();
        this.floatingWindow = null;
    }
    void IOverlayWindow.DragEnter(IDropArea area)
    {
        if (overlayHost == null || floatingWindow == null || OverlayHost.Element(area) is not { } element
            || !ReferenceEquals(element.XamlRoot, destination.XamlRoot)
            || !ReferenceEquals(element is DockingManager manager ? manager : (element as ILayoutControl)?.Model?.Root?.Manager, overlayHost.Manager))
        {
            return;
        }

        if (!visibleAreas.Contains(area))
        {
            visibleAreas.Add(area);
        }

        ApplyOriginalArea(area);
        Invalidate();
    }
    void IOverlayWindow.DragLeave(IDropArea area)
    {
        if (!visibleAreas.Remove(area))
        {
            return;
        }

        if (currentDropTarget is DropTargetBase target && ReferenceEquals(target.Target.Area, OverlayHost.Element(area)))
        {
            ((IOverlayWindow)this).DragLeave(target);
        }

        ((IOverlayWindow)this).GetTargets().ToArray();
    }
    IEnumerable<IDropTarget> IOverlayWindow.GetTargets()
    {
        if (floatingWindow?.Model is not LayoutFloatingWindow source || overlayHost == null
            || !ReferenceEquals(source.Root?.Manager, overlayHost.Manager) || !IsVisible || IsClosed || RenderFailure != null)
        {
            return [];
        }

        if (!PrepareOriginalTargets())
        {
            return [];
        }

        DropTargetBase[] result = GetOriginalTargets().Select(InitializeOriginalTarget).OfType<DropTargetBase>()
            .Where(target => !target.Target.ScreenBounds.IsEmpty && target.Target.ScreenBounds.Width > 0 && target.Target.ScreenBounds.Height > 0).ToArray();
        OverlayTarget[] measured = result.Select(target => target.Target).Distinct().ToArray();
        bool changed = !targets.SequenceEqual(measured);
        targets = measured;
        parts.Clear();
        foreach (OverlayTarget? target in measured)
        {
            foreach (FrameworkElement part in TemplateParts())
            {
                if (IsNativeTargetVisible(part) && GetNativeScreenArea(part) == target.ScreenBounds)
                {
                    part.Tag = target.Type;
                    parts[target] = part;
                    break;
                }
            }
        }

        if (changed)
        {
            Invalidate();
        }

        return result;
    }
    void IOverlayWindow.DragEnter(IDropTarget target)
    {
        if (floatingWindow?.Model is not LayoutFloatingWindow source)
        {
            return;
        }

        currentDropTarget?.DragLeave();
        currentDropTarget = target;
        target.DragEnter();
        SetActive((target as DropTargetBase)?.Target);
        if (preview != null && target.GetPreviewPath(this, source) is { } geometry)
        {
            preview.Data = geometry;
            preview.Width = view.Width;
            preview.Height = view.Height;
            preview.Stretch = Microsoft.UI.Xaml.Media.Stretch.None;
            preview.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            Invalidate();
        }
    }
    void IOverlayWindow.DragLeave(IDropTarget target)
    {
        target.DragLeave();
        // The pinned OverlayWindow always hides the preview on target leave.
        // SetActive(null) can return early when this target has no native glyph.
        if (preview != null)
        {
            preview.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            Invalidate();
        }
        if (!ReferenceEquals(currentDropTarget, target))
        {
            return;
        }

        currentDropTarget = null;
        SetActive(null);
    }
    void IOverlayWindow.DragDrop(IDropTarget target)
    {
        if (floatingWindow?.Model is LayoutFloatingWindow source && overlayHost != null
            && ReferenceEquals(source.Root?.Manager, overlayHost.Manager))
        {
            overlayHost.Manager.CommitDropTarget(target, source);
        }
    }

}
