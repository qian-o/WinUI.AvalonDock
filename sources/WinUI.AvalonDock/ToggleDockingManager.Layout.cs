// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), ToggleDockingManager.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792; retain layout-state rules.
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

public partial class ToggleDockingManager
{
    private readonly Dictionary<LayoutAnchorable, DockZone> detachedZones = [];
    private bool refreshQueued;
    private LayoutRoot? observedLayout;

    public void ToggleAnchorable(LayoutAnchorable anchorable, DockZone zone)
    {
        if (IsDisposed)
        {
            return;
        }

        if (IsDetached(anchorable))
        {
            ActivateDetachedWindow(anchorable);
            SetToolboxIsOpen(anchorable);
            return;
        }
        if (anchorable.IsAutoHidden)
        {
            HideDockedInBar(GetBarForZone(zone));
            DockFromAutoHide(anchorable, zone);
            FixSplitOrientation(anchorable, zone);
            if (ToggleLayoutEngine.IsBottomZone(zone))
            {
                EnsureBottomZoneOrder();
            }

            if (LayoutPriority == DockLayoutPriority.BottomFullWidth)
            {
                EnsureBottomFullWidth();
            }
            else if (LayoutPriority == DockLayoutPriority.SidesFullHeight)
            {
                EnsureSidesFullHeight();
            }

            ActiveContent = anchorable.Content;
        }
        else
        {
            AutoHideFromDock(anchorable, zone);
        }

        RefreshButtonStates();
        QueuePinButtonUpdate();
        SetToolboxIsOpen(anchorable);
    }
    public void MoveAnchorableToZone(LayoutAnchorable anchorable, DockZone targetZone)
    {
        if (IsDisposed || anchorable == null)
        {
            return;
        }

        if (IsDetached(anchorable))
        {
            detachedZones[anchorable] = targetZone;
            return;
        }
        DockZone oldZone = GetAnchorableZone(anchorable);
        if (oldZone == targetZone)
        {
            if (anchorable.IsAutoHidden)
            {
                ToggleAnchorable(anchorable, targetZone);
            }
            return;
        }
        if (!anchorable.IsAutoHidden)
        {
            AutoHideFromDock(anchorable, oldZone);
        }

        if (anchorable.Parent is LayoutAnchorGroup oldGroup)
        {
            oldGroup.RemoveChild(anchorable);
        }

        LayoutAnchorGroup group = new();
        GetLayoutSideForZone(targetZone).Children.Add(group);
        group.Children.Add(anchorable);
        RemoveFromAllBars(anchorable);
        AddButton(anchorable, targetZone);
        ToggleAnchorable(anchorable, targetZone);
    }
    protected override object DetachFromLayout(LayoutAnchorable anchorable)
    {
        DockZone zone = GetAnchorableZone(anchorable);
        detachedZones[anchorable] = zone;
        if (!anchorable.IsAutoHidden)
        {
            AutoHideFromDock(anchorable, zone);
        }

        return zone;
    }
    protected override void ReturnToLayout(LayoutAnchorable anchorable, object? restoreState)
    {
        DockZone zone = detachedZones.TryGetValue(anchorable, out DockZone saved) ? saved : restoreState as DockZone? ?? GetAnchorableZone(anchorable);
        detachedZones.Remove(anchorable);
        if (anchorable.IsAutoHidden)
        {
            ToggleAnchorable(anchorable, zone);
        }
    }
    protected override FrameworkElement CreateDetachedWindowHeader(LayoutAnchorable anchorable) => new ToggleAnchorablePaneTitle { Model = anchorable };
    protected override void OnDetachedAnchorablesChanged(LayoutAnchorable anchorable)
    {
        SetToolboxIsOpen(anchorable);
        RefreshButtonStates();
    }
    internal override void ExecuteAutoHideCommand(LayoutAnchorable anchorable)
    {
        if (anchorable != null)
        {
            ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
        }
    }
    internal override void StartDraggingFloatingWindowForPane(LayoutAnchorablePane paneModel)
    {
        if (paneModel.Children.FirstOrDefault() is { } tool)
        {
            BeginZoneDrag(tool, this);
        }
        else
        {
            base.StartDraggingFloatingWindowForPane(paneModel);
        }
    }
    internal override void StartDraggingFloatingWindowForContent(LayoutContent contentModel, bool startDrag = true)
    {
        if (startDrag && contentModel is LayoutAnchorable tool)
        {
            BeginZoneDrag(tool, this);
        }
        else
        {
            base.StartDraggingFloatingWindowForContent(contentModel, startDrag);
        }
    }
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

    private void ObserveLayout() => ObserveLayout(Layout);
    private void ObserveLayout(LayoutRoot? layout)
    {
        if (observedLayout != null)
        {
            observedLayout.Updated -= OnToggleLayoutUpdated;
            observedLayout.ElementAdded -= OnToggleElementChanged;
            observedLayout.ElementRemoved -= OnToggleElementChanged;
        }
        observedLayout = IsDisposed ? null : layout;
        if (observedLayout != null)
        {
            observedLayout.Updated += OnToggleLayoutUpdated;
            observedLayout.ElementAdded += OnToggleElementChanged;
            observedLayout.ElementRemoved += OnToggleElementChanged;
        }
    }
    private void OnToggleElementChanged(object? sender, LayoutElementEventArgs args) => OnToggleLayoutUpdated(sender, args);
    private void OnToggleLayoutUpdated(object? sender, EventArgs e)
    {
        if (IsDisposed || settingUp || refreshQueued || !IsLoaded)
        {
            return;
        }

        refreshQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            refreshQueued = false;
            if (IsDisposed || !IsLoaded || leftTopBar == null)
            {
                return;
            }

            HideOrdinarySides();
            HashSet<LayoutAnchorable> currentTools = Layout.Descendents().OfType<LayoutAnchorable>().ToHashSet();
            foreach (ToggleDockButtonBar bar in Bars)
            {
                foreach (ToggleDockButton? button in bar.Items.OfType<ToggleDockButton>().Where(b => b.Anchorable is not { } tool || !currentTools.Contains(tool) || tool.IsHidden).ToArray())
                {
                    if (button.Anchorable?.Content is IToolbox toolbox)
                    {
                        UnregisterToolbox(toolbox);
                    }

                    button.Release();
                    bar.Items.Remove(button);
                }
            }

            foreach (LayoutAnchorSide side in new[] { Layout.LeftSide, Layout.RightSide, Layout.BottomSide }.OfType<LayoutAnchorSide>())
            {
                foreach (LayoutAnchorable tool in CollectAnchorables(side))
                {
                    if (!Bars.Any(bar => bar.ContainsAnchorable(tool)))
                    {
                        AddButton(tool, InitialZone(tool, side.Side));
                    }
                }
            }

            UpdateNavigationPanelVisibility();
            RefreshButtonStates();
        });
    }
}
