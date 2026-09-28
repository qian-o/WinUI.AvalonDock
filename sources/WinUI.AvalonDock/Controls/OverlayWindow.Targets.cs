// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/OverlayWindow.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace AvalonDock.Controls;

public partial class OverlayWindow
{
    private readonly List<LayoutContent> duplicateTargetContents = [];
    private readonly List<LayoutContent> duplicateSourceContents = [];
    private Grid? gridDockingManagerDropTargets;
    private Grid? gridAnchorablePaneDropTargets;
    private Grid? gridDocumentPaneDropTargets;
    private Grid? gridDocumentPaneFullDropTargets;
    private FrameworkElement? dockingManagerDropTargetBottom;
    private FrameworkElement? dockingManagerDropTargetTop;
    private FrameworkElement? dockingManagerDropTargetLeft;
    private FrameworkElement? dockingManagerDropTargetRight;
    private FrameworkElement? anchorablePaneDropTargetBottom;
    private FrameworkElement? anchorablePaneDropTargetTop;
    private FrameworkElement? anchorablePaneDropTargetLeft;
    private FrameworkElement? anchorablePaneDropTargetRight;
    private FrameworkElement? anchorablePaneDropTargetInto;
    private FrameworkElement? documentPaneDropTargetBottom;
    private FrameworkElement? documentPaneDropTargetTop;
    private FrameworkElement? documentPaneDropTargetLeft;
    private FrameworkElement? documentPaneDropTargetRight;
    private FrameworkElement? documentPaneDropTargetInto;
    private FrameworkElement? documentPaneDropTargetBottomAsAnchorablePane;
    private FrameworkElement? documentPaneDropTargetTopAsAnchorablePane;
    private FrameworkElement? documentPaneDropTargetLeftAsAnchorablePane;
    private FrameworkElement? documentPaneDropTargetRightAsAnchorablePane;
    private FrameworkElement? documentPaneFullDropTargetBottom;
    private FrameworkElement? documentPaneFullDropTargetTop;
    private FrameworkElement? documentPaneFullDropTargetLeft;
    private FrameworkElement? documentPaneFullDropTargetRight;
    private FrameworkElement? documentPaneFullDropTargetInto;
    private void BindOriginalTargetParts()
    {
        gridDockingManagerDropTargets = GetTemplateChild("PART_DockingManagerDropTargets") as Grid;
        gridAnchorablePaneDropTargets = GetTemplateChild("PART_AnchorablePaneDropTargets") as Grid;
        gridDocumentPaneDropTargets = GetTemplateChild("PART_DocumentPaneDropTargets") as Grid;
        gridDocumentPaneFullDropTargets = GetTemplateChild("PART_DocumentPaneFullDropTargets") as Grid;
        dockingManagerDropTargetBottom = GetTemplateChild("PART_DockingManagerDropTargetBottom") as FrameworkElement;
        dockingManagerDropTargetTop = GetTemplateChild("PART_DockingManagerDropTargetTop") as FrameworkElement;
        dockingManagerDropTargetLeft = GetTemplateChild("PART_DockingManagerDropTargetLeft") as FrameworkElement;
        dockingManagerDropTargetRight = GetTemplateChild("PART_DockingManagerDropTargetRight") as FrameworkElement;
        anchorablePaneDropTargetBottom = GetTemplateChild("PART_AnchorablePaneDropTargetBottom") as FrameworkElement;
        anchorablePaneDropTargetTop = GetTemplateChild("PART_AnchorablePaneDropTargetTop") as FrameworkElement;
        anchorablePaneDropTargetLeft = GetTemplateChild("PART_AnchorablePaneDropTargetLeft") as FrameworkElement;
        anchorablePaneDropTargetRight = GetTemplateChild("PART_AnchorablePaneDropTargetRight") as FrameworkElement;
        anchorablePaneDropTargetInto = GetTemplateChild("PART_AnchorablePaneDropTargetInto") as FrameworkElement;
        documentPaneDropTargetBottom = GetTemplateChild("PART_DocumentPaneDropTargetBottom") as FrameworkElement;
        documentPaneDropTargetTop = GetTemplateChild("PART_DocumentPaneDropTargetTop") as FrameworkElement;
        documentPaneDropTargetLeft = GetTemplateChild("PART_DocumentPaneDropTargetLeft") as FrameworkElement;
        documentPaneDropTargetRight = GetTemplateChild("PART_DocumentPaneDropTargetRight") as FrameworkElement;
        documentPaneDropTargetInto = GetTemplateChild("PART_DocumentPaneDropTargetInto") as FrameworkElement;
        documentPaneDropTargetBottomAsAnchorablePane = GetTemplateChild("PART_DocumentPaneDropTargetBottomAsAnchorablePane") as FrameworkElement;
        documentPaneDropTargetTopAsAnchorablePane = GetTemplateChild("PART_DocumentPaneDropTargetTopAsAnchorablePane") as FrameworkElement;
        documentPaneDropTargetLeftAsAnchorablePane = GetTemplateChild("PART_DocumentPaneDropTargetLeftAsAnchorablePane") as FrameworkElement;
        documentPaneDropTargetRightAsAnchorablePane = GetTemplateChild("PART_DocumentPaneDropTargetRightAsAnchorablePane") as FrameworkElement;
        documentPaneFullDropTargetBottom = GetTemplateChild("PART_DocumentPaneFullDropTargetBottom") as FrameworkElement;
        documentPaneFullDropTargetTop = GetTemplateChild("PART_DocumentPaneFullDropTargetTop") as FrameworkElement;
        documentPaneFullDropTargetLeft = GetTemplateChild("PART_DocumentPaneFullDropTargetLeft") as FrameworkElement;
        documentPaneFullDropTargetRight = GetTemplateChild("PART_DocumentPaneFullDropTargetRight") as FrameworkElement;
        documentPaneFullDropTargetInto = GetTemplateChild("PART_DocumentPaneFullDropTargetInto") as FrameworkElement;
    }
    private IEnumerable<IDropTarget> GetOriginalTargets()
    {
        if (floatingWindow is null)
        {
            yield break;
        }

        foreach (IDropArea visibleArea in visibleAreas)
        {
            switch (visibleArea.Type)
            {
                case DropAreaType.DockingManager:
                    {
                        // Dragging over DockingManager -> Add DropTarget Area
                        if (visibleArea is not DropArea<DockingManager> dropAreaDockingManager)
                        {
                            break;
                        }

                        yield return new DockingManagerDropTarget(dropAreaDockingManager.AreaElement, GetNativeScreenArea(dockingManagerDropTargetLeft), DropTargetType.DockingManagerDockLeft);
                        yield return new DockingManagerDropTarget(dropAreaDockingManager.AreaElement, GetNativeScreenArea(dockingManagerDropTargetTop), DropTargetType.DockingManagerDockTop);
                        yield return new DockingManagerDropTarget(dropAreaDockingManager.AreaElement, GetNativeScreenArea(dockingManagerDropTargetBottom), DropTargetType.DockingManagerDockBottom);
                        yield return new DockingManagerDropTarget(dropAreaDockingManager.AreaElement, GetNativeScreenArea(dockingManagerDropTargetRight), DropTargetType.DockingManagerDockRight);
                    }

                    break;

                case DropAreaType.AnchorablePane:
                    {
                        // Dragging over AnchorablePane -> Add DropTarget Area
                        if (visibleArea is not DropArea<LayoutAnchorablePaneControl> dropAreaAnchorablePane)
                        {
                            break;
                        }

                        yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, GetNativeScreenArea(anchorablePaneDropTargetLeft), DropTargetType.AnchorablePaneDockLeft);
                        yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, GetNativeScreenArea(anchorablePaneDropTargetTop), DropTargetType.AnchorablePaneDockTop);
                        yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, GetNativeScreenArea(anchorablePaneDropTargetRight), DropTargetType.AnchorablePaneDockRight);
                        yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, GetNativeScreenArea(anchorablePaneDropTargetBottom), DropTargetType.AnchorablePaneDockBottom);
                        if (IsNativeTargetVisible(anchorablePaneDropTargetInto))
                        {
                            yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, GetNativeScreenArea(anchorablePaneDropTargetInto), DropTargetType.AnchorablePaneDockInside);
                        }

                        if (dropAreaAnchorablePane.AreaElement.Model is not LayoutAnchorablePane parentPaneModel)
                        {
                            break;
                        }

                        LayoutAnchorableTabItem? lastAreaTabItem = null;
                        foreach (LayoutAnchorableTabItem dropAreaTabItem in NativeHeaders<LayoutAnchorableTabItem>(dropAreaAnchorablePane.AreaElement))
                        {
                            if (dropAreaTabItem.Model is not LayoutAnchorable tabItemModel)
                            {
                                continue;
                            }

                            lastAreaTabItem = lastAreaTabItem == null || GetNativeScreenArea(lastAreaTabItem).Right < GetNativeScreenArea(dropAreaTabItem).Right ?
                                dropAreaTabItem : lastAreaTabItem;
                            int tabIndex = parentPaneModel.Children.IndexOf(tabItemModel);
                            yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, GetNativeScreenArea(dropAreaTabItem), DropTargetType.AnchorablePaneDockInside, tabIndex);
                        }

                        if (lastAreaTabItem != null)
                        {
                            Rect lastAreaTabItemScreenArea = GetNativeScreenArea(lastAreaTabItem);
                            Rect newAreaTabItemScreenArea = new(new Point(lastAreaTabItemScreenArea.Right, lastAreaTabItemScreenArea.Top), new Point(lastAreaTabItemScreenArea.Right + lastAreaTabItemScreenArea.Width, lastAreaTabItemScreenArea.Bottom));
                            if (newAreaTabItemScreenArea.Right < GetNativeScreenArea(dropAreaAnchorablePane.AreaElement).Right)
                            {
                                yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, newAreaTabItemScreenArea, DropTargetType.AnchorablePaneDockInside, parentPaneModel.Children.Count);
                            }
                        }

                        AnchorablePaneTitle? dropAreaTitle = dropAreaAnchorablePane.AreaElement.FindVisualChildren<AnchorablePaneTitle>().FirstOrDefault();
                        if (dropAreaTitle != null)
                        {
                            yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, GetNativeScreenArea(dropAreaTitle), DropTargetType.AnchorablePaneDockInside);
                        }
                    }

                    break;

                case DropAreaType.DocumentPane:
                    {
                        // Dragging over DocumentPane -> Add DropTarget Area
                        bool isDraggingAnchorables = floatingWindow.Model is LayoutAnchorableFloatingWindow;
                        if (isDraggingAnchorables && gridDocumentPaneFullDropTargets != null)
                        {
                            // Item dragged is a layout anchorable over the DockingManager's DocumentPane
                            // -> Yield a drop target structure with 9 buttons
                            if (visibleArea is not DropArea<LayoutDocumentPaneControl> dropAreaDocumentPane)
                            {
                                break;
                            }

                            if (IsNativeTargetVisible(documentPaneFullDropTargetLeft))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneFullDropTargetLeft), DropTargetType.DocumentPaneDockLeft);
                            }

                            if (IsNativeTargetVisible(documentPaneFullDropTargetTop))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneFullDropTargetTop), DropTargetType.DocumentPaneDockTop);
                            }

                            if (IsNativeTargetVisible(documentPaneFullDropTargetRight))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneFullDropTargetRight), DropTargetType.DocumentPaneDockRight);
                            }

                            if (IsNativeTargetVisible(documentPaneFullDropTargetBottom))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneFullDropTargetBottom), DropTargetType.DocumentPaneDockBottom);
                            }

                            if (IsNativeTargetVisible(documentPaneFullDropTargetInto))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneFullDropTargetInto), DropTargetType.DocumentPaneDockInside);
                            }

                            if (dropAreaDocumentPane.AreaElement.Model is not LayoutDocumentPane parentPaneModel)
                            {
                                break;
                            }

                            LayoutDocumentTabItem? lastAreaTabItem = null;
                            foreach (LayoutDocumentTabItem dropAreaTabItem in NativeHeaders<LayoutDocumentTabItem>(dropAreaDocumentPane.AreaElement))
                            {
                                if (dropAreaTabItem.Model is not { } tabItemModel)
                                {
                                    continue;
                                }

                                lastAreaTabItem = lastAreaTabItem == null || GetNativeScreenArea(lastAreaTabItem).Right < GetNativeScreenArea(dropAreaTabItem).Right ?
                                    dropAreaTabItem : lastAreaTabItem;
                                int tabIndex = parentPaneModel.Children.IndexOf(tabItemModel);
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(dropAreaTabItem), DropTargetType.DocumentPaneDockInside, tabIndex);
                            }

                            if (lastAreaTabItem != null)
                            {
                                Rect lastAreaTabItemScreenArea = GetNativeScreenArea(lastAreaTabItem);
                                Rect newAreaTabItemScreenArea = new(new Point(lastAreaTabItemScreenArea.Right, lastAreaTabItemScreenArea.Top), new Point(lastAreaTabItemScreenArea.Right + lastAreaTabItemScreenArea.Width, lastAreaTabItemScreenArea.Bottom));
                                if (newAreaTabItemScreenArea.Right < GetNativeScreenArea(dropAreaDocumentPane.AreaElement).Right)
                                {
                                    yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, newAreaTabItemScreenArea, DropTargetType.DocumentPaneDockInside, parentPaneModel.Children.Count);
                                }
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetLeftAsAnchorablePane))
                            {
                                yield return new DocumentPaneDropAsAnchorableTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetLeftAsAnchorablePane), DropTargetType.DocumentPaneDockAsAnchorableLeft);
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetTopAsAnchorablePane))
                            {
                                yield return new DocumentPaneDropAsAnchorableTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetTopAsAnchorablePane), DropTargetType.DocumentPaneDockAsAnchorableTop);
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetRightAsAnchorablePane))
                            {
                                yield return new DocumentPaneDropAsAnchorableTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetRightAsAnchorablePane), DropTargetType.DocumentPaneDockAsAnchorableRight);
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetBottomAsAnchorablePane))
                            {
                                yield return new DocumentPaneDropAsAnchorableTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetBottomAsAnchorablePane), DropTargetType.DocumentPaneDockAsAnchorableBottom);
                            }
                        }
                        else
                        {
                            // Item being dragged is a document over the DockingManager's DocumentPane
                            // -> Yield a drop target structure with 5 center buttons over the document
                            if (visibleArea is not DropArea<LayoutDocumentPaneControl> dropAreaDocumentPane)
                            {
                                break;
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetLeft))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetLeft), DropTargetType.DocumentPaneDockLeft);
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetTop))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetTop), DropTargetType.DocumentPaneDockTop);
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetRight))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetRight), DropTargetType.DocumentPaneDockRight);
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetBottom))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetBottom), DropTargetType.DocumentPaneDockBottom);
                            }

                            if (IsNativeTargetVisible(documentPaneDropTargetInto))
                            {
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetInto), DropTargetType.DocumentPaneDockInside);
                            }

                            if (dropAreaDocumentPane.AreaElement.Model is not LayoutDocumentPane parentPaneModel)
                            {
                                break;
                            }

                            LayoutDocumentTabItem? lastAreaTabItem = null;
                            foreach (LayoutDocumentTabItem dropAreaTabItem in NativeHeaders<LayoutDocumentTabItem>(dropAreaDocumentPane.AreaElement))
                            {
                                if (dropAreaTabItem.Model is not { } tabItemModel)
                                {
                                    continue;
                                }

                                lastAreaTabItem = lastAreaTabItem == null || GetNativeScreenArea(lastAreaTabItem).Right < GetNativeScreenArea(dropAreaTabItem).Right ?
                                    dropAreaTabItem : lastAreaTabItem;
                                int tabIndex = parentPaneModel.Children.IndexOf(tabItemModel);
                                yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(dropAreaTabItem), DropTargetType.DocumentPaneDockInside, tabIndex);
                            }

                            if (lastAreaTabItem != null)
                            {
                                Rect lastAreaTabItemScreenArea = GetNativeScreenArea(lastAreaTabItem);
                                Rect newAreaTabItemScreenArea = new(new Point(lastAreaTabItemScreenArea.Right, lastAreaTabItemScreenArea.Top), new Point(lastAreaTabItemScreenArea.Right + lastAreaTabItemScreenArea.Width, lastAreaTabItemScreenArea.Bottom));
                                if (newAreaTabItemScreenArea.Right < GetNativeScreenArea(dropAreaDocumentPane.AreaElement).Right)
                                {
                                    yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, newAreaTabItemScreenArea, DropTargetType.DocumentPaneDockInside, parentPaneModel.Children.Count);
                                }
                            }
                        }
                    }

                    break;

                case DropAreaType.DocumentPaneGroup:
                    {
                        // Dragging over DocumentPaneGroup -> Add DropTarget Area
                        if (visibleArea is not DropArea<LayoutDocumentPaneGroupControl> dropAreaDocumentPane)
                        {
                            break;
                        }

                        if (IsNativeTargetVisible(documentPaneDropTargetInto))
                        {
                            yield return new DocumentPaneGroupDropTarget(dropAreaDocumentPane.AreaElement, GetNativeScreenArea(documentPaneDropTargetInto), DropTargetType.DocumentPaneGroupDockInside);
                        }
                    }

                    break;
            }
        }

        yield break;
    }
    private void ApplyOriginalArea(IDropArea area)
    {
        if (floatingWindow?.Model.Root?.Manager is not { } floatingWindowManager)
        {
            return;
        }

        FrameworkElement? areaElement = null;
        switch (area.Type)
        {
            case DropAreaType.DockingManager:
                if (area is not DropArea<DockingManager> dropAreaDockingManager)
                {
                    return;
                }

                if (dropAreaDockingManager.AreaElement != floatingWindowManager)
                {
                    visibleAreas.Remove(area);
                    return;
                }

                areaElement = gridDockingManagerDropTargets;
                break;

            case DropAreaType.AnchorablePane:
                areaElement = gridAnchorablePaneDropTargets;

                if (area is not DropArea<LayoutAnchorablePaneControl> dropAreaAnchorablePaneGroup)
                {
                    return;
                }

                if (dropAreaAnchorablePaneGroup.AreaElement.Model is not LayoutAnchorablePane layoutAnchorablePane)
                {
                    return;
                }

                if (layoutAnchorablePane.Root?.Manager != floatingWindowManager)
                {
                    visibleAreas.Remove(area);
                    return;
                }

                SetDropTargetIntoVisibility(layoutAnchorablePane);
                break;

            case DropAreaType.DocumentPaneGroup:
                {
                    areaElement = gridDocumentPaneDropTargets;
                    if (area is not DropArea<LayoutDocumentPaneGroupControl> dropAreaDocumentPaneGroup)
                    {
                        return;
                    }

                    if (dropAreaDocumentPaneGroup.AreaElement.Model is not LayoutDocumentPaneGroup documentGroup)
                    {
                        return;
                    }

                    LayoutDocumentPane? layoutDocumentPane = documentGroup.Children.FirstOrDefault() as LayoutDocumentPane;
                    LayoutDocumentPaneGroup? parentDocumentPaneGroup = layoutDocumentPane?.Parent as LayoutDocumentPaneGroup;
                    if ((parentDocumentPaneGroup ?? documentGroup).Root?.Manager != floatingWindowManager)
                    {
                        visibleAreas.Remove(area);
                        return;
                    }

                    SetNativeTargetVisibility(documentPaneDropTargetLeft, Visibility.Collapsed);
                    SetNativeTargetVisibility(documentPaneDropTargetRight, Visibility.Collapsed);
                    SetNativeTargetVisibility(documentPaneDropTargetTop, Visibility.Collapsed);
                    SetNativeTargetVisibility(documentPaneDropTargetBottom, Visibility.Collapsed);
                }

                break;

            case DropAreaType.DocumentPane:
            default:
                {
                    bool isDraggingAnchorables = floatingWindow.Model is LayoutAnchorableFloatingWindow;
                    if (isDraggingAnchorables && gridDocumentPaneFullDropTargets != null)
                    {
                        areaElement = gridDocumentPaneFullDropTargets;
                        if (area is not DropArea<LayoutDocumentPaneControl> dropAreaDocumentPaneGroup)
                        {
                            return;
                        }

                        if (dropAreaDocumentPaneGroup.AreaElement.Model is not LayoutDocumentPane layoutDocumentPane)
                        {
                            return;
                        }

                        LayoutDocumentPaneGroup? parentDocumentPaneGroup = layoutDocumentPane.Parent as LayoutDocumentPaneGroup;
                        if (layoutDocumentPane.Root?.Manager != floatingWindowManager)
                        {
                            visibleAreas.Remove(area);
                            return;
                        }

                        SetDropTargetIntoVisibility(layoutDocumentPane);

                        if (parentDocumentPaneGroup != null &&
                            parentDocumentPaneGroup.Children.Where(c => c.IsVisible).Count() > 1)
                        {
                            DockingManager manager = floatingWindowManager;
                            if (!manager.AllowMixedOrientation)
                            {
                                SetNativeTargetVisibility(documentPaneFullDropTargetLeft, parentDocumentPaneGroup.Orientation == Orientation.Horizontal ? Visibility.Visible : Visibility.Collapsed);
                                SetNativeTargetVisibility(documentPaneFullDropTargetRight, parentDocumentPaneGroup.Orientation == Orientation.Horizontal ? Visibility.Visible : Visibility.Collapsed);
                                SetNativeTargetVisibility(documentPaneFullDropTargetTop, parentDocumentPaneGroup.Orientation == Orientation.Vertical ? Visibility.Visible : Visibility.Collapsed);
                                SetNativeTargetVisibility(documentPaneFullDropTargetBottom, parentDocumentPaneGroup.Orientation == Orientation.Vertical ? Visibility.Visible : Visibility.Collapsed);
                            }
                            else
                            {
                                SetNativeTargetVisibility(documentPaneFullDropTargetLeft, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneFullDropTargetRight, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneFullDropTargetTop, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneFullDropTargetBottom, Visibility.Visible);
                            }
                        }
                        else if (parentDocumentPaneGroup == null &&
                            layoutDocumentPane.ChildrenCount == 0)
                        {
                            SetNativeTargetVisibility(documentPaneFullDropTargetLeft, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneFullDropTargetRight, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneFullDropTargetTop, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneFullDropTargetBottom, Visibility.Collapsed);
                        }
                        else
                        {
                            SetNativeTargetVisibility(documentPaneFullDropTargetLeft, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneFullDropTargetRight, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneFullDropTargetTop, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneFullDropTargetBottom, Visibility.Visible);
                        }

                        if (layoutDocumentPane.IsHostedInFloatingWindow)
                        {
                            // Hide outer buttons if drop area is a document floating window host
                            // since these 4 drop area buttons are available over the DockingManager ONLY.
                            SetNativeTargetVisibility(documentPaneDropTargetBottomAsAnchorablePane, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneDropTargetLeftAsAnchorablePane, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneDropTargetRightAsAnchorablePane, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneDropTargetTopAsAnchorablePane, Visibility.Collapsed);
                        }
                        else if (parentDocumentPaneGroup != null &&
                            parentDocumentPaneGroup.Children.Where(c => c.IsVisible).Count() > 1)
                        {
                            int indexOfDocumentPane = parentDocumentPaneGroup.Children.Where(ch => ch.IsVisible).ToList().IndexOf(layoutDocumentPane);
                            bool isFirstChild = indexOfDocumentPane == 0;
                            bool isLastChild = indexOfDocumentPane == parentDocumentPaneGroup.ChildrenCount - 1;

                            DockingManager manager = floatingWindowManager;
                            if (!manager.AllowMixedOrientation)
                            {
                                SetNativeTargetVisibility(documentPaneDropTargetBottomAsAnchorablePane,
                                parentDocumentPaneGroup.Orientation == Orientation.Vertical ?
                                    (isLastChild ? Visibility.Visible : Visibility.Collapsed) :
                                    Visibility.Collapsed);
                                SetNativeTargetVisibility(documentPaneDropTargetTopAsAnchorablePane,
                                    parentDocumentPaneGroup.Orientation == Orientation.Vertical ?
                                        (isFirstChild ? Visibility.Visible : Visibility.Collapsed) :
                                        Visibility.Collapsed);

                                SetNativeTargetVisibility(documentPaneDropTargetLeftAsAnchorablePane,
                                    parentDocumentPaneGroup.Orientation == Orientation.Horizontal ?
                                        (isFirstChild ? Visibility.Visible : Visibility.Collapsed) :
                                        Visibility.Collapsed);

                                SetNativeTargetVisibility(documentPaneDropTargetRightAsAnchorablePane,
                                    parentDocumentPaneGroup.Orientation == Orientation.Horizontal ?
                                        (isLastChild ? Visibility.Visible : Visibility.Collapsed) :
                                        Visibility.Collapsed);
                            }
                            else
                            {
                                SetNativeTargetVisibility(documentPaneDropTargetBottomAsAnchorablePane, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneDropTargetLeftAsAnchorablePane, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneDropTargetRightAsAnchorablePane, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneDropTargetTopAsAnchorablePane, Visibility.Visible);
                            }
                        }
                        else
                        {
                            SetNativeTargetVisibility(documentPaneDropTargetBottomAsAnchorablePane, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneDropTargetLeftAsAnchorablePane, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneDropTargetRightAsAnchorablePane, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneDropTargetTopAsAnchorablePane, Visibility.Visible);
                        }
                    }
                    else
                    {
                        // Showing a drop target structure with 5 centered star like buttons.
                        areaElement = gridDocumentPaneDropTargets;
                        if (area is not DropArea<LayoutDocumentPaneControl> dropAreaDocumentPaneGroup)
                        {
                            return;
                        }

                        if (dropAreaDocumentPaneGroup.AreaElement.Model is not LayoutDocumentPane layoutDocumentPane)
                        {
                            return;
                        }

                        LayoutDocumentPaneGroup? parentDocumentPaneGroup = layoutDocumentPane.Parent as LayoutDocumentPaneGroup;
                        if (layoutDocumentPane.Root?.Manager != floatingWindowManager)
                        {
                            visibleAreas.Remove(area);
                            return;
                        }

                        SetDropTargetIntoVisibility(layoutDocumentPane);

                        if (parentDocumentPaneGroup != null &&
                            parentDocumentPaneGroup.Children.Where(c => c.IsVisible).Count() > 1)
                        {
                            DockingManager manager = floatingWindowManager;
                            if (!manager.AllowMixedOrientation)
                            {
                                SetNativeTargetVisibility(documentPaneDropTargetLeft, parentDocumentPaneGroup.Orientation == Orientation.Horizontal ? Visibility.Visible : Visibility.Collapsed);
                                SetNativeTargetVisibility(documentPaneDropTargetRight, parentDocumentPaneGroup.Orientation == Orientation.Horizontal ? Visibility.Visible : Visibility.Collapsed);
                                SetNativeTargetVisibility(documentPaneDropTargetTop, parentDocumentPaneGroup.Orientation == Orientation.Vertical ? Visibility.Visible : Visibility.Collapsed);
                                SetNativeTargetVisibility(documentPaneDropTargetBottom, parentDocumentPaneGroup.Orientation == Orientation.Vertical ? Visibility.Visible : Visibility.Collapsed);
                            }
                            else
                            {
                                SetNativeTargetVisibility(documentPaneDropTargetLeft, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneDropTargetRight, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneDropTargetTop, Visibility.Visible);
                                SetNativeTargetVisibility(documentPaneDropTargetBottom, Visibility.Visible);
                            }
                        }
                        else if (parentDocumentPaneGroup == null &&
                            layoutDocumentPane != null &&
                            layoutDocumentPane.ChildrenCount == 0)
                        {
                            SetNativeTargetVisibility(documentPaneDropTargetLeft, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneDropTargetRight, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneDropTargetTop, Visibility.Collapsed);
                            SetNativeTargetVisibility(documentPaneDropTargetBottom, Visibility.Collapsed);
                        }
                        else
                        {
                            SetNativeTargetVisibility(documentPaneDropTargetLeft, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneDropTargetRight, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneDropTargetTop, Visibility.Visible);
                            SetNativeTargetVisibility(documentPaneDropTargetBottom, Visibility.Visible);
                        }
                    }
                }

                break;
        }

        if (areaElement == null)
        {
            return;
        }

        Rect localBounds = GetNativeAreaBounds(area);
        if (Canvas.GetLeft(areaElement) != localBounds.Left)
        {
            Canvas.SetLeft(areaElement, localBounds.Left);
        }
        if (Canvas.GetTop(areaElement) != localBounds.Top)
        {
            Canvas.SetTop(areaElement, localBounds.Top);
        }
        if (areaElement.Width != localBounds.Width)
        {
            areaElement.Width = localBounds.Width;
        }
        if (areaElement.Height != localBounds.Height)
        {
            areaElement.Height = localBounds.Height;
        }
        SetNativeTargetVisibility(areaElement, Visibility.Visible);
        preparedGroups.Add(areaElement);
    }
    private void SetDropTargetIntoVisibility(ILayoutPositionableElement? positionableElement)
    {
        if (positionableElement is LayoutAnchorablePane)
        {
            SetNativeTargetVisibility(anchorablePaneDropTargetInto, Visibility.Visible);
        }
        else if (positionableElement is LayoutDocumentPane)
        {
            SetNativeTargetVisibility(documentPaneDropTargetInto, Visibility.Visible);
        }

        if (positionableElement == null || floatingWindow?.Model == null || positionableElement.AllowDuplicateContent)
        {
            return;
        }

        // Find all content layouts in the anchorable pane (object to drop on)
        duplicateTargetContents.Clear();
        GetAllLayoutContents(positionableElement, duplicateTargetContents);

        // Find all content layouts in the floating window (object to drop)
        duplicateSourceContents.Clear();
        GetAllLayoutContents(floatingWindow.Model, duplicateSourceContents);

        // If any of the content layouts is present in the drop area, then disable the DropTargetInto button.
        foreach (LayoutContent content in duplicateSourceContents)
        {
            bool duplicate = false;
            foreach (LayoutContent existing in duplicateTargetContents)
            {
                if (existing.Title == content.Title && existing.ContentId == content.ContentId)
                {
                    duplicate = true;
                    break;
                }
            }
            if (!duplicate)
            {
                continue;
            }

            if (positionableElement is LayoutAnchorablePane)
            {
                SetNativeTargetVisibility(anchorablePaneDropTargetInto, Visibility.Collapsed);
            }
            else if (positionableElement is LayoutDocumentPane)
            {
                SetNativeTargetVisibility(documentPaneDropTargetInto, Visibility.Collapsed);
            }

            break;
        }
    }
    private static void GetAllLayoutContents(object source, List<LayoutContent> result)
    {
        if (source is LayoutContent content)
        {
            result.Add(content);
        }
        else if (source is LayoutDocumentFloatingWindow or LayoutAnchorableFloatingWindow
            or LayoutDocumentPaneGroup or LayoutAnchorablePaneGroup or LayoutDocumentPane or LayoutAnchorablePane)
        {
            foreach (ILayoutElement child in ((ILayoutContainer)source).Children)
            {
                GetAllLayoutContents(child, result);
            }
        }
    }
}
