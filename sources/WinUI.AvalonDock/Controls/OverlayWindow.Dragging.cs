// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/OverlayWindow.cs.
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Controls;

public partial class OverlayWindow : IOverlayWindow
{
    private readonly IOverlayWindowHost? overlayHost;
    private readonly List<IDropArea> visibleAreas = [];
    private LayoutFloatingWindowControl? floatingWindow;
    private IDropTarget? currentDropTarget;
    private Rect currentPreviewAreaBounds;
    private bool areaVisibilityDirty = true;

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
        currentPreviewAreaBounds = Rect.Empty;
        visibleAreas.Clear();
        areaSnapshot.Clear();
        preparedGroups.Clear();
        duplicateTargetContents.Clear();
        duplicateSourceContents.Clear();
        areaVisibilityDirty = true;
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
            areaVisibilityDirty = true;
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

        areaVisibilityDirty = true;

        if (currentDropTarget is DropTargetBase target && ReferenceEquals(target.Target.Area, OverlayHost.Element(area)))
        {
            ((IOverlayWindow)this).DragLeave(target);
        }

        // DragService 在离开和进入的区域全部协调后统一测量目标，避免此处重复布局。
        Invalidate();
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

        // 本次测量的目标共享同一来源树，以不可变快照避免逐指示器和标签重复遍历。
        LayoutContent[] sourceContents = source.Descendents().OfType<LayoutContent>().ToArray();
        List<DropTargetBase> result = [];
        foreach (IDropTarget original in GetOriginalTargets())
        {
            if (InitializeOriginalTarget(original, sourceContents) is { } target
                && !target.Target.ScreenBounds.IsEmpty && target.Target.ScreenBounds.Width > 0 && target.Target.ScreenBounds.Height > 0)
            {
                result.Add(target);
            }
        }
        OverlayTarget[] measured = result.Select(target => target.Target).Distinct().ToArray();
        bool changed = !targets.SequenceEqual(measured);
        targets = measured;
        if (changed || partsDirty)
        {
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

            partsDirty = false;
        }

        if (changed)
        {
            Invalidate();
        }

        // The glyph may stay at the same position while its pane grows around it.
        // Its preview still needs to follow the pane's current bounds.
        if (currentDropTarget is DropTargetBase active && result.Any(target =>
            target.TabIndex == active.TabIndex && Equals(target.Target, active.Target)))
        {
            UpdateTargetPreview(active, source, false);
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
        currentPreviewAreaBounds = Rect.Empty;
        UpdateTargetPreview(target, source, true);
    }
    private void UpdateTargetPreview(IDropTarget target, LayoutFloatingWindow source, bool force)
    {
        if (preview == null || target is not DropTargetBase nativeTarget)
        {
            return;
        }

        Rect areaBounds = GetPreviewBounds(nativeTarget.Target.Area);
        if (!force && currentPreviewAreaBounds == areaBounds)
        {
            return;
        }

        currentPreviewAreaBounds = areaBounds;
        preview.Data = target.GetPreviewPath(this, source);
        preview.Width = view.Width;
        preview.Height = view.Height;
        preview.Stretch = Microsoft.UI.Xaml.Media.Stretch.None;
        preview.Visibility = preview.Data == null ? Visibility.Collapsed : Visibility.Visible;
        Invalidate();
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
        currentPreviewAreaBounds = Rect.Empty;
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
