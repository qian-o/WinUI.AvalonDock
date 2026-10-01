using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager
{
    private readonly List<DockTarget> dragTargets = [];
    private IDragInputSession? dragInput;
    private DockTarget? dragTarget;
    private LayoutContent? draggedContent;
    private FrameworkElement? dragOrigin;
    private Point dragStart;
    private bool dragStarted;
    private IDockingWindowHost? tabDragHost;
    private LayoutFloatingWindow? tabDragFloating;
    private Point tabDragAnchor;

    internal IDisposable RegisterDockTarget(ILayoutGroup model, FrameworkElement view)
    {
        DockTarget target = new(model, view);
        dragTargets.Add(target);
        return new CallbackDisposable(() =>
        {
            dragTargets.Remove(target);
            if (ReferenceEquals(dragTarget?.Model, model) && ReferenceEquals(dragTarget?.View, view))
            {
                dockingOverlay.Hide();
                dragTarget = null;
            }
        });
    }

    internal void BeginContentDrag(LayoutContent content, FrameworkElement origin, Point? pressPosition = null)
    {
        if (this is ToggleDockingManager toggle && content is LayoutAnchorable tool && tool.CanMove && !IsDetached(tool))
        {
            toggle.BeginZoneDrag(tool, origin, waitForLeave: true, pressPosition: pressPosition);
            return;
        }
        if (dragInput is not null || chromeDragHost != null || content.Root?.Manager != this)
        {
            return;
        }
        if (content is LayoutDocument { CanMove: false } or LayoutAnchorable { CanMove: false })
        {
            return;
        }
        draggedContent = content;
        dragOrigin = origin;
        dragStarted = false;
        dragInput = PlatformServices.CreateDragInputService(origin).TryBegin(OnDragInput, pressPosition);
        if (dragInput is null)
        {
            draggedContent = null;
            dragOrigin = null;
            return;
        }
        dragStart = new Point(dragInput.Position.X, dragInput.Position.Y);
        if (content.FindParent<LayoutFloatingWindow>() is { } floating && floatingHosts.TryGetValue(floating, out IDockingWindowHost? host)
            && controlsByHost.TryGetValue(host, out LayoutFloatingWindowControl? control))
        {
            control.SetDraggingState(true);
        }
    }

    internal void CancelPendingContentDrag(LayoutContent content, FrameworkElement origin)
    {
        if (ReferenceEquals(draggedContent, content) && ReferenceEquals(dragOrigin, origin) && tabDragHost == null)
        {
            EndContentDrag();
        }
    }

    private void OnDragInput(DragInputUpdate update)
    {
        if (draggedPane != null)
        {
            OnPaneDragInput(update);
            return;
        }
        LayoutContent? content = draggedContent;
        if (content is null)
        {
            return;
        }
        Point point = new(update.Position.X, update.Position.Y);
        if (!ReferenceEquals(content.Root, Layout) || update.Kind == DragInputUpdateKind.Cancelled
            || content is LayoutDocument { CanMove: false } or LayoutAnchorable { CanMove: false })
        {
            EndContentDrag();
            return;
        }
        if (!dragStarted)
        {
            Size threshold = PlatformServices.PointerGestures.GetDragThreshold(dragOrigin!);
            dragStarted = Math.Abs(point.X - dragStart.X) > threshold.Width || Math.Abs(point.Y - dragStart.Y) > threshold.Height;
            // WPF's document tab resets its pending press on MouseLeave. Native HWND
            // capture can suppress PointerExited, so observe the full tab boundary
            // on the captured pointer update before the threshold has been crossed.
            if (!dragStarted && update.Kind == DragInputUpdateKind.Moved && content is LayoutDocument
                && dragOrigin is LayoutDocumentTabItem or TabViewItem { Header: LayoutDocumentTabItem })
            {
                FrameworkElement boundary = dragOrigin;
                if (dragOrigin is LayoutDocumentTabItem header
                    && header.FindVisualAncestor<TabViewItem>() is { } tab)
                {
                    boundary = tab;
                }
                if (PlatformServices.Coordinates.TryGetScreenBounds(boundary, out Rect tabBounds)
                    && !tabBounds.Contains(point))
                {
                    EndContentDrag();
                    return;
                }
            }
        }
        if (dragStarted)
        {
            if (tabDragHost == null && content.CanFloat && AllowFloatingWindows
                && dragTargets.FirstOrDefault(target => ReferenceEquals(target.Model, content.Parent)) is { } source
                && !TryHitTabStrip(source, content, point, out _))
            {
                double scale = dragOrigin?.XamlRoot?.RasterizationScale ?? 1;
                LayoutFloatingWindow? existingModel = content.FindParent<LayoutFloatingWindow>();
                IDockingWindowHost? existingHost = existingModel != null && floatingHosts.TryGetValue(existingModel, out IDockingWindowHost? known) ? known : null;
                tabDragAnchor = existingHost != null && existingModel is not null && existingModel.Descendents().OfType<LayoutContent>().Count() == 1
                    ? existingHost.GetPointerAnchor(new DragInputPosition(dragStart.X, dragStart.Y, DragCoordinateSpace.DesktopPhysicalPixels))
                    : new Point(30, 32);
                double originalLeft = content.FloatingLeft;
                double originalTop = content.FloatingTop;
                Point floatingPosition = PlatformServices.Coordinates.ToLayoutPosition(
                    new Point(point.X - tabDragAnchor.X * scale, point.Y - tabDragAnchor.Y * scale));
                content.FloatingLeft = floatingPosition.X;
                content.FloatingTop = floatingPosition.Y;
                LayoutFloatingWindowControl? floatingControl = StartFloatingContent(content);
                LayoutFloatingWindow? floating = content.FindParent<LayoutFloatingWindow>();
                if (floatingControl == null || dragInput == null || floating == null || !floatingHosts.TryGetValue(floating, out tabDragHost))
                {
                    if (floatingControl == null)
                    {
                        content.FloatingLeft = originalLeft;
                        content.FloatingTop = originalTop;
                    }
                    EndContentDrag();
                    return;
                }
                tabDragFloating = floating;
                if (controlsByHost.TryGetValue(tabDragHost, out LayoutFloatingWindowControl? control))
                {
                    control.SetDraggingState(true);
                }
            }
            UpdateDragTarget(content, point);
            if (update.Kind == DragInputUpdateKind.Moved)
            {
                tabDragHost?.MoveWithPointer(update.Position, tabDragAnchor);
            }
        }
        if (update.Kind != DragInputUpdateKind.Released)
        {
            return;
        }
        DockTarget? target = dragTarget;
        FrameworkElement? origin = dragOrigin;
        bool active = dragStarted;
        bool alreadyFloating = tabDragHost != null;
        Action? overlayDrop = CaptureOverlayDrop(point);
        EndContentDrag(overlayDrop != null);
        if (!active)
        {
            return;
        }
        if (overlayDrop != null)
        {
            overlayDrop();
        }
        else if (target == null && !alreadyFloating && origin != null && content.CanFloat && AllowFloatingWindows)
        {
            Point position = PlatformServices.Coordinates.ToLayoutPosition(point);
            content.FloatingLeft = position.X;
            content.FloatingTop = position.Y;
            StartDraggingFloatingWindowForContent(content, false);
        }
    }

    private void UpdateDragTarget(LayoutContent content, Point point)
    {
        if (chromeDragModel != null || paneDragFloating != null || tabDragFloating != null)
        {
            UpdateOverlayTarget(point);
            return;
        }
        DockTarget? selected = null;
        foreach (DockTarget target in dragTargets.ToArray())
        {
            if (target.Model.Root?.Manager != this || content is LayoutDocument && target.Model is LayoutAnchorablePane
                || !ReferenceEquals(target.Model, content.Parent))
            {
                continue;
            }
            if (PlatformServices.Coordinates.TryGetScreenBounds(target.View, out Rect screen)
                && (screen.Contains(point) || ReferenceEquals(target.Model, content.Parent)
                    && TryHitTabStrip(target, content, point, out _))
                && PlatformServices.Coordinates.IsVisibleAt(target.View, point))
            {
                selected = target;
                break;
            }
        }
        dragTarget = null;
        if (selected != null && TryHitTabStrip(selected, content, point, out int tabIndex))
        {
            if (draggedPane == null && chromeDragModel == null && ReferenceEquals(content.Parent, selected.Model))
            {
                overlayDragService?.Abort();
                overlayDragService = null;
                dockingOverlay.Hide();
                dragTarget = selected;
                ReorderDraggedTab(content, selected.Model, tabIndex);
                return;
            }
        }
        if (chromeDragModel == null && (!content.CanFloat || !AllowFloatingWindows))
        {
            dockingOverlay.Hide();
            return;
        }
        UpdateOverlayTarget(point);
    }

    private static bool TryHitTabStrip(DockTarget target, LayoutContent content, Point point, out int index)
    {
        index = -1;
        if (target.View is not TabView tabs)
        {
            return false;
        }

        List<(int Index, Rect Bounds)> headers = new();
        for (int itemIndex = 0; itemIndex < tabs.TabItems.Count; itemIndex++)
        {
            if (tabs.TabItems[itemIndex] is TabViewItem { IsHitTestVisible: true, Opacity: > 0 } tab
                && PlatformServices.Coordinates.TryGetScreenBounds(tab, out Rect bounds))
            {
                headers.Add((itemIndex, bounds));
            }
        }
        if (headers.Count == 0)
        {
            return false;
        }

        double top = headers.Min(header => header.Bounds.Top);
        double bottom = headers.Max(header => header.Bounds.Bottom);
        bool sourceDocumentStrip = target.Model is LayoutDocumentPane && ReferenceEquals(content.Parent, target.Model);
        double verticalBuffer = sourceDocumentStrip ? (bottom - top) / 2 : 0;
        if (point.Y < top - verticalBuffer || point.Y > bottom + verticalBuffer)
        {
            return false;
        }

        int draggedIndex = target.Model.IndexOfChild(content);
        double draggedWidth = headers.FirstOrDefault(header => header.Index == draggedIndex).Bounds.Width;
        foreach ((int Index, Rect Bounds) header in headers)
        {
            Rect hitBounds = sourceDocumentStrip && draggedWidth > 0
                ? new Rect(header.Bounds.Left, header.Bounds.Top, draggedWidth, header.Bounds.Height)
                : header.Bounds;
            if (!hitBounds.Contains(point))
            {
                continue;
            }

            index = header.Index;
            return true;
        }
        Rect last = headers[^1].Bounds;
        Rect trailingBounds = tabs.FlowDirection == FlowDirection.RightToLeft
            ? new Rect(last.Left - last.Width, last.Top, last.Width, last.Height)
            : new Rect(last.Right, last.Top, last.Width, last.Height);
        if (!PlatformServices.Coordinates.TryGetScreenBounds(tabs, out Rect paneBounds))
        {
            return false;
        }

        if (trailingBounds.Left >= paneBounds.Left && trailingBounds.Right <= paneBounds.Right
            && trailingBounds.Contains(point))
        {
            index = tabs.TabItems.Count;
            return true;
        }
        if (ReferenceEquals(content.Parent, target.Model) && point.X >= paneBounds.Left && point.X <= paneBounds.Right)
        {
            // An empty portion of the source strip is still a reorder gesture, not an edge split.
            index = draggedIndex;
            return true;
        }
        return false;
    }

    private static void ReorderDraggedTab(LayoutContent content, ILayoutGroup group, int index)
    {
        bool permitted = group switch
        {
            LayoutDocumentPane documents => documents.CanRepositionItems
                && (documents.Parent is not LayoutDocumentPaneGroup parent || parent.CanRepositionItems),
            LayoutAnchorablePane tools => tools.CanRepositionItems
                && (tools.Parent is not LayoutAnchorablePaneGroup parent || parent.CanRepositionItems),
            _ => false
        };
        if (!permitted || group is not ILayoutPane pane)
        {
            return;
        }

        int oldIndex = group.IndexOfChild(content);
        int newIndex = Math.Min(index, group.ChildrenCount - 1);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        pane.MoveChild(oldIndex, newIndex);
        content.IsSelected = true;
        content.IsActive = true;
    }

    private void EndContentDrag(bool keepOverlaySource = false)
    {
        // Tab and pane drags move their native host outside the system move loop.
        // Complete the floating control's original WM_EXITSIZEMOVE persistence phase here.
        foreach (IDockingWindowHost? host in new[] { tabDragHost, paneDragHost }.OfType<IDockingWindowHost>().Distinct())
        {
            if (!host.IsClosed && controlsByHost.TryGetValue(host, out LayoutFloatingWindowControl? control))
            {
                control.UpdatePositionAndSizeOfPanes();
            }
        }

        DragService? service = overlayDragService;
        overlayDragService = null;
        if (!keepOverlaySource)
        {
            service?.Abort();
        }

        IDragInputSession? input = dragInput;
        foreach (LayoutFloatingWindowControl? control in floatingControls.Where(control => control.IsDragging).ToArray())
        {
            control.SetDraggingState(false);
        }

        dragInput = null;
        draggedContent = null;
        dragOrigin = null;
        dragStarted = false;
        draggedPane = null;
        paneDragContents = [];
        paneDragHost = null;
        paneDragFloating = null;
        chromeDragHost = null;
        chromeDragModel = null;
        chromeDragContents = [];
        tabDragHost = null;
        tabDragFloating = null;
        dragTarget = null;
        if (!keepOverlaySource)
        {
            dockingOverlay.Hide();
        }

        input?.Dispose();
    }

    /// <summary>
    /// Restores keyboard focus after a drop rebuilt one or more pane presenters. Rebuilding a pane
    /// creates a new TabViewItem and can otherwise let the target pane's existing selection reclaim
    /// activation after the commit has already selected the dropped content.
    /// </summary>
    internal void FocusDroppedContent(LayoutContent content)
    {
        if (!ReferenceEquals(content.Root, Layout))
        {
            return;
        }

        FrameworkElement? paneView = dragTargets.FirstOrDefault(target => ReferenceEquals(target.Model, content.Parent))?.View;
        if (paneView is { IsLoaded: false })
        {
            paneView.Loaded += OnPaneLoaded;
            paneView.Unloaded += OnPaneUnloaded;
            return;
        }
        void OnPaneLoaded(object sender, RoutedEventArgs args)
        {
            OnPaneUnloaded(sender, args);
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => FocusDroppedContent(content));
        }
        void OnPaneUnloaded(object sender, RoutedEventArgs args)
        {
            paneView?.Loaded -= OnPaneLoaded;
            if (paneView is not null)
            {
                paneView.Unloaded -= OnPaneUnloaded;
            }
        }
        FrameworkElement? FindHeader()
        {
            foreach (DockTarget target in dragTargets)
            {
                if (target.View is not TabView tabs || !ReferenceEquals(target.Model, content.Parent))
                {
                    continue;
                }

                tabs.ApplyTemplate();
                if (tabs is TabControlEx { IsTabStripCollapsed: true })
                {
                    return GetLayoutItemFromModel(content)?.View;
                }

                foreach (TabViewItem tab in tabs.TabItems.OfType<TabViewItem>())
                {
                    if (ReferenceEquals(tab.Tag, content))
                    {
                        return tab.Header as FrameworkElement;
                    }
                }
            }
            return null;
        }
        FrameworkElement? header = FindHeader();
        if (header is null)
        {
            return;
        }

        if (header.IsLoaded)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusHeader);
            return;
        }

        // Wait for the actual new visual to become focusable instead of racing dispatcher turns.
        header.Loaded += OnHeaderLoaded;
        header.Unloaded += OnHeaderUnloaded;

        void OnHeaderLoaded(object sender, RoutedEventArgs args)
        {
            Detach();
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusHeader);
        }

        void OnHeaderUnloaded(object sender, RoutedEventArgs args) => Detach();

        void Detach()
        {
            header.Loaded -= OnHeaderLoaded;
            header.Unloaded -= OnHeaderUnloaded;
        }

        void FocusHeader()
        {
            if (!ReferenceEquals(content.Root, Layout) || !ReferenceEquals(FindHeader(), header))
            {
                return;
            }

            TabViewItem? tabItem = null;
            TabView? tabView = null;
            for (DependencyObject? parent = header; parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is TabViewItem item)
                {
                    tabItem = item;
                }

                if (parent is TabView view)
                {
                    tabView = view;
                    break;
                }
            }
            if (content.Parent is ILayoutContentSelector selector)
            {
                selector.SelectedContentIndex = selector.IndexOf(content);
            }
            if (tabView is not null && tabItem is not null)
            {
                tabView.SelectedItem = tabItem;
            }

            content.IsSelected = true;
            PlatformServices.Coordinates.ActivateWindow(header);
            if (ReferenceEquals(header, GetLayoutItemFromModel(content)?.View))
            {
                if (FocusManager.FindFirstFocusableElement(header) is Control focusable)
                {
                    focusable.Focus(FocusState.Programmatic);
                }
            }
            else if (tabItem is not null)
            {
                tabItem.Focus(FocusState.Programmatic);
            }
            else
            {
                header.Focus(FocusState.Programmatic);
            }

            content.IsActive = true;
        }
    }

    private sealed record DockTarget(ILayoutGroup Model, FrameworkElement View);

    private sealed class CallbackDisposable(Action release) : IDisposable
    {
        private Action? callback = release;

        public void Dispose() => Interlocked.Exchange(ref callback, null)?.Invoke();
    }
}
