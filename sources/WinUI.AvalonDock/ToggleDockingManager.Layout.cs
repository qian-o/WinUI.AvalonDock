// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), ToggleDockingManager.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792; retain layout and toolbox-state rules.
using System.ComponentModel;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace AvalonDock;

public partial class ToggleDockingManager
{
    private void FixSplitOrientation(LayoutAnchorable anchorable, DockZone zone)
    {
        layoutEngine.FixSplitOrientationForZone(anchorable, zone);
    }
    private void EnsureBottomZoneOrder()
    {
        LayoutRoot root = Layout as LayoutRoot;
        if (root?.RootPanel == null)
        {
            return;
        }

        // Find the horizontal group of bottom anchorable panes
        LayoutAnchorablePaneGroup? group = null;
        foreach (ILayoutPanelElement child in root.RootPanel.Children)
        {
            if (child is LayoutAnchorablePaneGroup g && g.Orientation == Orientation.Horizontal)
            {
                group = g;
                break;
            }
        }

        if (group == null || group.Children.Count < 2)
        {
            return;
        }

        // Check if any BottomRight pane appears before a BottomLeft pane
        bool needsReorder = false;
        bool seenRight = false;
        foreach (LayoutAnchorablePane child in group.Children.OfType<LayoutAnchorablePane>())
        {
            LayoutAnchorable? anc = child.Children.OfType<LayoutAnchorable>().FirstOrDefault();
            if (anc == null)
            {
                continue;
            }

            DockZone zone = GetAnchorableZone(anc);
            if (zone == DockZone.BottomRight)
            {
                seenRight = true;
            }
            else if (zone == DockZone.BottomLeft && seenRight)
            {
                needsReorder = true;
                break;
            }
        }

        if (!needsReorder)
        {
            return;
        }

        // Partition into left and right panes, preserving relative order within each group
        List<ILayoutAnchorablePane> leftPanes = new();
        List<ILayoutAnchorablePane> rightPanes = new();
        foreach (ILayoutAnchorablePane? child in group.Children.ToList())
        {
            if (child is LayoutAnchorablePane pane)
            {
                LayoutAnchorable? anc = pane.Children.OfType<LayoutAnchorable>().FirstOrDefault();
                if (anc != null && GetAnchorableZone(anc) == DockZone.BottomRight)
                {
                    rightPanes.Add(pane);
                }
                else
                {
                    leftPanes.Add(pane);
                }
            }
            else
            {
                leftPanes.Add(child);
            }
        }

        // Rebuild group: all left panes first, then right panes
        while (group.Children.Count > 0)
        {
            group.Children.RemoveAt(group.Children.Count - 1);
        }

        foreach (ILayoutAnchorablePane p in leftPanes)
        {
            group.Children.Add(p);
        }

        foreach (ILayoutAnchorablePane p in rightPanes)
        {
            group.Children.Add(p);
        }
    }
    private void EnsureBottomFullWidth()
    {
        layoutEngine.EnsureBottomFullWidth(Layout as LayoutRoot);
    }
    private void EnsureSidesFullHeight()
    {
        layoutEngine.EnsureSidesFullHeight(Layout as LayoutRoot);
    }
    private void DockFromAutoHide(LayoutAnchorable anchorable, DockZone zone)
    {
        LayoutAnchorGroup? parentGroup = anchorable.Parent as LayoutAnchorGroup;
        if (parentGroup == null)
        {
            return;
        }

        LayoutAnchorSide? parentSide = parentGroup.Parent as LayoutAnchorSide;
        if (parentSide == null)
        {
            return;
        }

        LayoutAnchorablePane? previousContainer = ((ILayoutPreviousContainer)parentGroup).PreviousContainer as LayoutAnchorablePane;
        if (previousContainer != null && previousContainer.Root == null)
        {
            previousContainer = null;
        }

        if (parentGroup.Root is not LayoutRoot root)
        {
            return;
        }

        if (previousContainer == null)
        {
            AnchorSide side = ToggleLayoutEngine.ZoneToAnchorSide(zone);
            previousContainer = new LayoutAnchorablePane
            {
                DockMinWidth = anchorable.AutoHideMinWidth,
                DockMinHeight = anchorable.AutoHideMinHeight
            };

            // Apply default dock dimensions from the manager
            if (side == AnchorSide.Left || side == AnchorSide.Right)
            {
                previousContainer.DockWidth = new GridLength(DefaultDockWidth);
            }
            else
            {
                previousContainer.DockHeight = new GridLength(DefaultDockHeight);
            }

            layoutEngine.InsertPaneForZone(root, previousContainer, zone);
        }
        else
        {
            // Re-insert at the correct zone position to maintain zone ordering
            previousContainer.Parent?.RemoveChild(previousContainer);
            layoutEngine.InsertPaneForZone(root, previousContainer, zone);
        }

        parentGroup.Children.Remove(anchorable);
        previousContainer.Children.Add(anchorable);

        if (parentGroup.Children.Count == 0)
        {
            parentSide.Children.Remove(parentGroup);
        }
    }
    private void AutoHideFromDock(LayoutAnchorable anchorable, DockZone zone)
    {
        LayoutAnchorablePane? parentPane = anchorable.Parent as LayoutAnchorablePane;
        if (parentPane == null)
        {
            return;
        }

        ILayoutRoot? root = anchorable.Root;
        if (root == null)
        {
            return;
        }

        AnchorSide side = ToggleLayoutEngine.ZoneToAnchorSide(zone);
        LayoutAnchorGroup newAnchorGroup = new();
        ((ILayoutPreviousContainer)newAnchorGroup).PreviousContainer = parentPane;

        parentPane.Children.Remove(anchorable);
        newAnchorGroup.Children.Add(anchorable);

        switch (side)
        {
            case AnchorSide.Right:
                root.RightSide?.Children.Add(newAnchorGroup);
                break;
            case AnchorSide.Left:
                root.LeftSide?.Children.Add(newAnchorGroup);
                break;
            case AnchorSide.Bottom:
                root.BottomSide?.Children.Add(newAnchorGroup);
                break;
        }
    }
    private static List<LayoutAnchorable> CollectAnchorables(LayoutAnchorSide? side)
    {
        List<LayoutAnchorable> result = new();
        if (side == null)
        {
            return result;
        }

        foreach (LayoutAnchorGroup group in side.Children)
        {
            foreach (LayoutAnchorable anchorable in group.Children)
            {
                result.Add(anchorable);
            }
        }

        return result;
    }
    private static List<LayoutAnchorable> CollectDockedAnchorables(LayoutRoot layout)
    {
        if (layout == null)
        {
            return new List<LayoutAnchorable>();
        }

        return layout.Descendents()
            .OfType<LayoutAnchorable>()
            .Where(a => a.Parent is LayoutAnchorablePane && !a.IsAutoHidden && !a.IsFloating && !a.IsHidden)
            .ToList();
    }
    private void ApplyInitialToolboxState()
    {
        if (Layout == null)
        {
            return;
        }

        foreach (LayoutAnchorable? anchorable in Layout.Descendents().OfType<LayoutAnchorable>().ToList())
        {
            if (!(anchorable.Content is IToolbox toolbox))
            {
                continue;
            }

            if (!toolbox.IsOpen && !toolbox.IsOpenByDefault)
            {
                continue;
            }

            if (anchorable.IsAutoHidden && !IsDetached(anchorable))
            {
                ToggleAnchorable(anchorable, toolbox.Zone);
            }
            else
            {
                // Already on screen - only the toolbox still has to be told, which is what turns
                // IsOpenByDefault into an IsOpen the application can read back.
                SetToolboxIsOpen(anchorable);
            }
        }
    }
    private void SyncToolboxStateToLayout()
    {
        foreach (LayoutAnchorable? anchorable in toolboxToAnchorable.Values.ToList())
        {
            SetToolboxIsOpen(anchorable);
        }
    }
    private void RegisterToolboxesFromBars()
    {
        ToggleDockButtonBar?[] allBars = { leftTopBar, leftBottomBar, rightTopBar, rightBottomBar, bottomLeftBar, bottomRightBar };
        foreach (ToggleDockButtonBar? bar in allBars)
        {
            if (bar == null)
            {
                continue;
            }

            foreach (object? item in bar.Items)
            {
                if (item is ToggleDockButton btn && btn.Anchorable?.Content is IToolbox toolbox)
                {
                    RegisterToolbox(toolbox, btn.Anchorable);
                }
            }
        }
    }
    internal void RegisterToolbox(IToolbox toolbox, LayoutAnchorable anchorable)
    {
        if (toolboxToAnchorable.ContainsKey(toolbox))
        {
            toolboxToAnchorable[toolbox] = anchorable;
            return;
        }

        toolboxToAnchorable[toolbox] = anchorable;

        if (toolbox is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnToolboxPropertyChanged;
        }

        RefreshShortcuts();
    }
    internal void UnregisterToolbox(IToolbox toolbox)
    {
        toolboxToAnchorable.Remove(toolbox);

        if (toolbox is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged -= OnToolboxPropertyChanged;
        }

        RefreshShortcuts();
    }
    private void OnToolboxPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IToolbox.IsOpen) || syncDepth > 0)
        {
            return;
        }

        if (!(sender is IToolbox toolbox) || !toolboxToAnchorable.TryGetValue(toolbox, out LayoutAnchorable? anchorable))
        {
            return;
        }

        // A detached anchorable sits collapsed on its stripe while its content is on screen in a
        // standalone window, so IsAutoHidden on its own does not say whether the toolbox is showing.
        bool isOpen = !anchorable.IsAutoHidden || IsDetached(anchorable);

        if (toolbox.IsOpen == isOpen)
        {
            return;
        }

        // ToggleAnchorable is the one implementation of this transition: it carries the zone
        // bookkeeping, the layout priority handling and the detached window case, and it writes the
        // resulting state back onto the toolbox. Duplicating it here is what let the two paths drift
        // apart. The syncDepth guard keeps that write-back from re-entering this handler.
        syncDepth++;
        try
        {
            ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
        }
        finally
        {
            syncDepth--;
        }
    }
    private void SetToolboxIsOpen(LayoutAnchorable anchorable)
    {
        if (!(anchorable.Content is IToolbox toolbox))
        {
            return;
        }

        syncDepth++;
        try
        {
            // A detached anchorable is collapsed onto its stripe but its content is on screen in a
            // standalone window, so it counts as open.
            toolbox.IsOpen = !anchorable.IsAutoHidden || IsDetached(anchorable);
        }
        finally
        {
            syncDepth--;
        }
    }
    private DockZone GetAnchorableZone(LayoutAnchorable anchorable)
    {
        if (leftTopBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.LeftTop;
        }

        if (leftBottomBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.LeftBottom;
        }

        if (rightTopBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.RightTop;
        }

        if (rightBottomBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.RightBottom;
        }

        if (bottomLeftBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.BottomLeft;
        }

        if (bottomRightBar?.ContainsAnchorable(anchorable) == true)
        {
            return DockZone.BottomRight;
        }

        // Fallback
        if (anchorable.Parent is LayoutAnchorGroup group && group.Parent is LayoutAnchorSide side)
        {
            switch (side.Side)
            {
                case AnchorSide.Left:
                    return DockZone.LeftTop;
                case AnchorSide.Right:
                    return DockZone.RightTop;
                case AnchorSide.Bottom:
                    return DockZone.BottomLeft;
            }
        }

        return DockZone.LeftTop;
    }
    private LayoutAnchorSide GetLayoutSideForZone(DockZone zone)
    {
        switch (zone)
        {
            case DockZone.LeftTop:
            case DockZone.LeftBottom:
                return Layout.LeftSide ??= new LayoutAnchorSide();
            case DockZone.RightTop:
            case DockZone.RightBottom:
                return Layout.RightSide ??= new LayoutAnchorSide();
            case DockZone.BottomLeft:
            case DockZone.BottomRight:
                return Layout.BottomSide ??= new LayoutAnchorSide();
            default:
                return Layout.LeftSide ??= new LayoutAnchorSide();
        }
    }
    internal ToggleDockButtonBar? GetBarForZone(DockZone zone)
    {
        switch (zone)
        {
            case DockZone.LeftTop:
                return leftTopBar;
            case DockZone.LeftBottom:
                return leftBottomBar;
            case DockZone.RightTop:
                return rightTopBar;
            case DockZone.RightBottom:
                return rightBottomBar;
            case DockZone.BottomLeft:
                return bottomLeftBar;
            case DockZone.BottomRight:
                return bottomRightBar;
            default:
                return leftTopBar;
        }
    }
    private static void RefreshBarStates(ToggleDockButtonBar? bar, object? activeContent)
    {
        if (bar == null)
        {
            return;
        }

        foreach (object? item in bar.Items)
        {
            if (item is ToggleDockButton btn && btn.Anchorable != null)
            {
                btn.IsChecked = !btn.Anchorable.IsAutoHidden;
                btn.IsAnchorableFocused = !btn.Anchorable.IsAutoHidden
                                          && activeContent != null
                                          && activeContent == btn.Anchorable.Content;
            }
        }
    }
    private void RefreshButtonStates()
    {
        object? activeContent = ActiveContent;
        RefreshBarStates(leftTopBar, activeContent);
        RefreshBarStates(leftBottomBar, activeContent);
        RefreshBarStates(rightTopBar, activeContent);
        RefreshBarStates(rightBottomBar, activeContent);
        RefreshBarStates(bottomLeftBar, activeContent);
        RefreshBarStates(bottomRightBar, activeContent);
    }
    private void HideDockedInBar(ToggleDockButtonBar? bar)
    {
        if (bar == null)
        {
            return;
        }

        foreach (object? item in bar.Items)
        {
            if (item is ToggleDockButton btn && btn.Anchorable != null && !btn.Anchorable.IsAutoHidden)
            {
                AutoHideFromDock(btn.Anchorable, bar.Zone);

                // This collapse is a side effect of opening a sibling, so nothing else writes it back.
                // Leaving it out is what used to strand a toolbox at IsOpen == true while it sat on
                // its stripe, after which setting IsOpen = true again raised no change and the
                // toolbox could not be reopened from the view model at all.
                SetToolboxIsOpen(btn.Anchorable);
            }
        }
    }
}
