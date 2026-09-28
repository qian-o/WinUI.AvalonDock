// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/DragService.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System;
using System.Collections.Generic;
using System.Linq;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the drag Service.
/// </summary>
internal class DragService
{
    private DockingManager manager;
    private LayoutFloatingWindowControl floatingWindow;

    // A list of hosts that can display an overlaywindow and offer a drop target (docking position)
    private List<IOverlayWindowHost> overlayWindowHosts = new();

    private IOverlayWindowHost? currentHost;
    private IOverlayWindow? currentWindow;
    private List<IDropArea> currentWindowAreas = new();
    private IDropTarget? currentDropTarget;
    private bool isDrag;

    /// <summary>
    /// Initializes a new instance of the <see cref="DragService"/> class.
    /// </summary>
    /// <param name="floatingWindow">The floating Window.</param>
    public DragService(LayoutFloatingWindowControl floatingWindow)
    {
        this.floatingWindow = floatingWindow;
        manager = floatingWindow.Model.Root?.Manager ?? throw new ArgumentException("浮动窗口必须属于停靠管理器。", nameof(floatingWindow));
    }

    /// <summary>
    /// Executes the update Mouse Location operation.
    /// </summary>
    /// <param name="dragPosition">The drag Position.</param>
    internal void UpdateMouseLocation(Point dragPosition)
    {
        if (!isDrag)
        {
            // A previous drag that never reported its end - Windows can drop the modal move loop
            // without a WM_EXITSIZEMOVE when a window crosses monitors of a different DPI - may still
            // have an overlay window on screen. Clearing them here keeps them from accumulating into
            // empty windows that only disappear with the application (issue #587).
            manager?.HideAllOverlayWindows();
            GetOverlayWindowHosts();
            isDrag = true;
        }

        IOverlayWindowHost? newHost = overlayWindowHosts.FirstOrDefault(oh => oh.HitTestScreen(dragPosition) && IsNativeHostVisible(oh, dragPosition));

        if (currentHost != null || currentHost != newHost)
        {
            // is mouse still inside current overlay window host?
            if ((currentHost != null && !currentHost.HitTestScreen(dragPosition)) ||
                currentHost != newHost)
            {
                // esit drop target
                if (currentDropTarget != null)
                {
                    currentWindow?.DragLeave(currentDropTarget);
                }

                currentDropTarget = null;

                // exit area
                currentWindowAreas.ForEach(a =>
                    currentWindow?.DragLeave(a));
                currentWindowAreas.Clear();

                // hide current overlay window
                if (currentWindow != null)
                {
                    currentWindow.DragLeave(floatingWindow);
                }

                if (currentHost != null)
                {
                    currentHost.HideOverlayWindow();
                    GetOverlayWindowHosts();
                }

                currentHost = null;
            }

            if (newHost is not null && currentHost != newHost)
            {
                currentHost = newHost;
                currentWindow = currentHost.ShowOverlayWindow(floatingWindow);
                currentWindow.DragEnter(floatingWindow);

                // Set the target window to topmost
                if (currentHost is LayoutFloatingWindowControl fwc &&
                    (fwc.OwnedByDockingManagerWindow == floatingWindow.OwnedByDockingManagerWindow || fwc.OwnedByDockingManagerWindow))
                {
                    BringWindowToTop2(fwc);
                }
                else if (currentHost is DockingManager dockingManager)
                {
                    BringWindowToTop2(dockingManager);
                }

                GetOverlayWindowHosts();

                BringWindowToTop2(floatingWindow);
                if (currentWindow is Window overlayWindow)
                {
                    BringWindowToTop2(overlayWindow);
                }
            }
        }

        if (currentHost == null || currentWindow is not { } overlay)
        {
            return;
        }

        List<IDropArea> availableAreas = currentHost.GetDropAreas(floatingWindow).ToList();
        List<IDropArea> areasToRemove = currentWindowAreas.Where(area => !availableAreas.Contains(area)
            || !area.DetectionRect.Contains(area.TransformToDeviceDPI(dragPosition))).ToList();
        if (currentDropTarget != null && areasToRemove.Count > 0
            && (currentDropTarget is not DropTargetBase target
                || areasToRemove.Any(area => ReferenceEquals(OverlayHost.Element(area), target.Target.Area))))
        {
            overlay.DragLeave(currentDropTarget);
            currentDropTarget = null;
        }
        foreach (IDropArea area in areasToRemove)
        {
            overlay.DragLeave(area);
            currentWindowAreas.Remove(area);
        }

        List<IDropArea> areasToAdd = availableAreas.Where(area => !currentWindowAreas.Contains(area)
            && area.DetectionRect.Contains(area.TransformToDeviceDPI(dragPosition))).ToList();

        currentWindowAreas.AddRange(areasToAdd);

        areasToAdd.ForEach(a =>
            overlay.DragEnter(a));

        // Rebuild targets only after old views leave and their replacements enter.
        RefreshNativeTarget();
        if (currentDropTarget != null && !currentDropTarget.HitTestScreen(dragPosition))
        {
            overlay.DragLeave(currentDropTarget);
            currentDropTarget = null;
        }

        if (currentDropTarget == null)
        {
            currentWindowAreas.ForEach(wa =>
            {
                if (currentDropTarget != null)
                {
                    return;
                }

                currentDropTarget = overlay.GetTargets().FirstOrDefault(dt => dt.HitTestScreen(dragPosition));

                if (currentDropTarget != null)
                {
                    overlay.DragEnter(currentDropTarget);
                    if (overlay is Window overlayWindow)
                    {
                        BringWindowToTop2(overlayWindow);
                    }

                    return;
                }
            });
        }
    }

    /// <summary>
    /// Executes the drop operation.
    /// </summary>
    /// <param name="dropLocation">The drop Location.</param>
    /// <param name="dropHandled">The drop Handled.</param>
    internal void Drop(Point dropLocation, out bool dropHandled)
    {
        dropHandled = false;

        UpdateMouseLocation(dropLocation);

        ILayoutRoot? root = floatingWindow.Model.Root;

        if (currentHost != null)
        {
            currentHost.HideOverlayWindow();
        }

        if (currentDropTarget != null && currentWindow is { } overlay)
        {
            overlay.DragDrop(currentDropTarget);
            root?.CollectGarbage();
            dropHandled = true;
        }

        currentWindowAreas.ForEach(a => currentWindow?.DragLeave(a));

        if (currentDropTarget != null)
        {
            currentWindow?.DragLeave(currentDropTarget);
        }

        if (currentWindow != null)
        {
            currentWindow.DragLeave(floatingWindow);
        }

        currentWindow = null;
        currentHost = null;
        currentWindowAreas.Clear();
        currentDropTarget = null;
        isDrag = false;

        // The host tracked above is not necessarily the only one that has been asked to show an
        // overlay window during this drag, so every host is cleared (issue #587).
        manager?.HideAllOverlayWindows();
    }

    /// <summary>
    /// Executes the abort operation.
    /// </summary>
    internal void Abort()
    {
        // An abort also runs when the dragged window is closed while the drag is still in progress,
        // where there may be no overlay window to leave any more (issue #587).
        if (currentWindow != null)
        {
            currentWindowAreas.ForEach(a => currentWindow.DragLeave(a));

            if (currentDropTarget != null)
            {
                currentWindow.DragLeave(currentDropTarget);
            }

            currentWindow.DragLeave(floatingWindow);
        }

        currentWindowAreas.Clear();
        currentDropTarget = null;
        currentWindow = null;

        if (currentHost != null)
        {
            currentHost.HideOverlayWindow();
        }

        currentHost = null;
        isDrag = false;

        // The host tracked above is not necessarily the only one that has been asked to show an
        // overlay window during this drag, so every host is cleared (issue #587).
        manager?.HideAllOverlayWindows();
    }

    private void BringWindowToTop2(Window window)
    {
        if (window == null)
        {
            return;
        }

        PlatformServices.WindowOrder.BringToFront(window);
    }

    internal LayoutFloatingWindowControl FloatingWindow => floatingWindow;
    internal IDropTarget? CurrentDropTarget => currentDropTarget;

    private bool IsNativeHostVisible(IOverlayWindowHost host, Point point) =>
        OverlayHost.Element(host) is { IsLoaded: true } element
        && PlatformServices.Coordinates.IsVisibleAt(element, point, floatingWindow.WindowHost);

    private void BringWindowToTop2(FrameworkElement element) => PlatformServices.WindowOrder.BringToFront(element);

    private void RefreshNativeTarget()
    {
        if (currentDropTarget is not DropTargetBase previous || currentWindow is not OverlayWindow window)
        {
            return;
        }
        // WinUI template/transform changes can complete after the original target
        // was measured. Reuse its identity only while the actual native glyph still
        // has those bounds and owns the rendered preview.
        IEnumerable<DropTargetBase> targets = currentWindow.GetTargets().OfType<DropTargetBase>();
        if (targets.Any(target => target.TabIndex == previous.TabIndex && Equals(target.Target, previous.Target))
            && window.IsPresenting(previous.Target))
        {
            return;
        }

        currentWindow.DragLeave(currentDropTarget);
        currentDropTarget = null;
    }

    /// <summary>Gets the original ordered overlay hosts for this drag.</summary>
    private void GetOverlayWindowHosts()
    {
        if (manager?.Layout?.RootPanel?.CanDock == true)
        {
            manager.GetOverlayWindowHostsByZOrder(ref overlayWindowHosts, floatingWindow);
        }
    }
}
