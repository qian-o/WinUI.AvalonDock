// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/DefaultLayoutEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

/// <summary>
/// Default implementation of <see cref="ILayoutEngine"/> that matches the conventional
/// <see cref="DockingManager"/> behavior. Inserts panes with simple orientation-based logic
/// (if the root panel orientation matches the target side, append/insert directly;
/// otherwise wrap the root in a new panel).
/// </summary>
public class DefaultLayoutEngine : ILayoutEngine
{
    /// <inheritdoc/>
    public void InsertPane(LayoutRoot root, LayoutAnchorablePane pane, AnchorSide side)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (pane == null)
        {
            throw new ArgumentNullException(nameof(pane));
        }

        switch (side)
        {
            case AnchorSide.Left:
                InsertPaneAtRoot(root, pane, Orientation.Horizontal, insertAtStart: true);
                break;
            case AnchorSide.Right:
                InsertPaneAtRoot(root, pane, Orientation.Horizontal, insertAtStart: false);
                break;
            case AnchorSide.Top:
                InsertPaneAtRoot(root, pane, Orientation.Vertical, insertAtStart: true);
                break;
            case AnchorSide.Bottom:
                InsertPaneAtRoot(root, pane, Orientation.Vertical, insertAtStart: false);
                break;
        }
    }

    private static void InsertPaneAtRoot(LayoutRoot root, LayoutAnchorablePane pane, Orientation orientation, bool insertAtStart)
    {
        LayoutPanel rootPanel = root.RootPanel;
        if (rootPanel.Orientation == orientation)
        {
            if (insertAtStart)
            {
                rootPanel.Children.Insert(0, pane);
            }
            else
            {
                rootPanel.Children.Add(pane);
            }
            return;
        }

        LayoutPanel panel = new()
        {
            Orientation = orientation
        };
        // Publish the wrapper before moving children, retaining the layout notification order.
        root.RootPanel = panel;
        panel.Children.Add(insertAtStart ? pane : rootPanel);
        panel.Children.Add(insertAtStart ? rootPanel : pane);
    }

    /// <inheritdoc/>
    public LayoutPanel FindOrCreateContentPanel(LayoutRoot root, Orientation orientation)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        LayoutPanel rootPanel = root.RootPanel;
        if (rootPanel.Orientation == orientation)
        {
            return rootPanel;
        }

        // Look for an existing child panel with the desired orientation
        foreach (ILayoutPanelElement child in rootPanel.Children)
        {
            if (child is LayoutPanel panel && panel.Orientation == orientation)
            {
                return panel;
            }
        }

        // Wrap the first non-anchorable child in a new panel
        LayoutPanel newPanel = new()
        {
            Orientation = orientation
        };

        ILayoutPanelElement? contentChild = null;
        for (int i = 0; i < rootPanel.Children.Count; i++)
        {
            ILayoutPanelElement child = rootPanel.Children[i];
            if (!(child is LayoutAnchorablePane))
            {
                contentChild = child;
                break;
            }
        }

        if (contentChild != null)
        {
            int idx = rootPanel.Children.IndexOf(contentChild);
            rootPanel.Children.Remove(contentChild);
            newPanel.Children.Add(contentChild);
            rootPanel.Children.Insert(idx, newPanel);
        }
        else
        {
            root.RootPanel = newPanel;
            newPanel.Children.Add(rootPanel);
        }

        return newPanel;
    }

    /// <inheritdoc/>
    public void FixSplitOrientation(LayoutAnchorable anchorable, AnchorSide side)
    {
        FixSplitOrientationCore(anchorable, GetDesiredOrientation(side), side == AnchorSide.Left || side == AnchorSide.Top);
    }

    internal static void FixSplitOrientationCore(LayoutAnchorable anchorable, Orientation desiredOrientation, bool insertAtStart)
    {
        if (anchorable == null)
        {
            return;
        }

        LayoutAnchorablePane? pane = anchorable.Parent as LayoutAnchorablePane;
        if (pane == null)
        {
            return;
        }

        LayoutPanel? parentPanel = pane.Parent as LayoutPanel;
        if (parentPanel == null)
        {
            return;
        }

        // Case 1: Adjacent LayoutAnchorablePaneGroup with matching orientation
        int paneIdx = parentPanel.Children.IndexOf(pane);
        LayoutAnchorablePaneGroup? existingGroup = null;
        if (paneIdx > 0 && parentPanel.Children[paneIdx - 1] is LayoutAnchorablePaneGroup leftNeighbor
            && leftNeighbor.Orientation == desiredOrientation)
        {
            existingGroup = leftNeighbor;
        }
        else if (paneIdx < parentPanel.Children.Count - 1 && parentPanel.Children[paneIdx + 1] is LayoutAnchorablePaneGroup rightNeighbor
            && rightNeighbor.Orientation == desiredOrientation)
        {
            existingGroup = rightNeighbor;
        }

        if (existingGroup != null)
        {
            parentPanel.Children.Remove(pane);
            if (insertAtStart)
            {
                existingGroup.Children.Insert(0, pane);
            }
            else
            {
                existingGroup.Children.Add(pane);
            }

            // Update the group's cross-axis dimension to the max of all children
            UpdateGroupDimensions(existingGroup);
            return;
        }

        // Case 2: Multiple contiguous LayoutAnchorablePane siblings — wrap in a group
        paneIdx = parentPanel.Children.IndexOf(pane);
        List<LayoutAnchorablePane> contiguousPanes = new()
        { pane };

        for (int i = paneIdx - 1; i >= 0; i--)
        {
            if (parentPanel.Children[i] is LayoutAnchorablePane adjPane)
            {
                contiguousPanes.Insert(0, adjPane);
            }
            else
            {
                break;
            }
        }

        for (int i = paneIdx + 1; i < parentPanel.Children.Count; i++)
        {
            if (parentPanel.Children[i] is LayoutAnchorablePane adjPane)
            {
                contiguousPanes.Add(adjPane);
            }
            else
            {
                break;
            }
        }

        if (contiguousPanes.Count < 2)
        {
            return;
        }

        if (parentPanel.Orientation == desiredOrientation)
        {
            return;
        }

        LayoutAnchorablePaneGroup group = new()
        {
            Orientation = desiredOrientation
        };
        int firstIdx = parentPanel.Children.IndexOf(contiguousPanes[0]);

        // Set the group's cross-axis dimension to the max of all panes.
        // For vertical groups (left/right): width = max width.
        // For horizontal groups (bottom): height = max height.
        if (desiredOrientation == Orientation.Vertical)
        {
            double maxWidth = contiguousPanes.Max(p => p.DockWidth.IsAbsolute ? p.DockWidth.Value : 0);
            double maxMinWidth = contiguousPanes.Max(p => p.DockMinWidth);
            if (maxWidth > 0)
            {
                group.DockWidth = new GridLength(maxWidth);
            }

            if (maxMinWidth > 0)
            {
                group.DockMinWidth = maxMinWidth;
            }
        }
        else
        {
            double maxHeight = contiguousPanes.Max(p => p.DockHeight.IsAbsolute ? p.DockHeight.Value : 0);
            double maxMinHeight = contiguousPanes.Max(p => p.DockMinHeight);
            if (maxHeight > 0)
            {
                group.DockHeight = new GridLength(maxHeight);
            }

            if (maxMinHeight > 0)
            {
                group.DockMinHeight = maxMinHeight;
            }
        }

        for (int i = contiguousPanes.Count - 1; i >= 0; i--)
        {
            parentPanel.Children.Remove(contiguousPanes[i]);
        }

        foreach (LayoutAnchorablePane sp in contiguousPanes)
        {
            group.Children.Add(sp);
        }

        parentPanel.Children.Insert(Math.Min(firstIdx, parentPanel.Children.Count), group);
    }

    /// <inheritdoc/>
    public void EnsureBottomFullWidth(LayoutRoot root)
    {
        if (root == null)
        {
            return;
        }

        LayoutPanel rootPanel = root.RootPanel;
        if (rootPanel == null)
        {
            return;
        }

        if (rootPanel.Orientation == Orientation.Horizontal)
        {
            List<LayoutAnchorablePane> bottomPanes = new();
            foreach (LayoutPanel? child in rootPanel.Children.OfType<LayoutPanel>().ToList())
            {
                if (child.Orientation == Orientation.Vertical)
                {
                    foreach (LayoutAnchorablePane? pane in child.Children.OfType<LayoutAnchorablePane>().ToList())
                    {
                        if (child.Children.IndexOf(pane) == child.Children.Count - 1 && child.Children.Count > 1)
                        {
                            bottomPanes.Add(pane);
                        }
                    }
                }
            }

            if (bottomPanes.Count > 0)
            {
                LayoutPanel vPanel = new()
                {
                    Orientation = Orientation.Vertical
                };
                root.RootPanel = vPanel;

                foreach (LayoutAnchorablePane bp in bottomPanes)
                {
                    bp.Parent?.RemoveChild(bp);
                }

                vPanel.Children.Add(rootPanel);
                foreach (LayoutAnchorablePane bp in bottomPanes)
                {
                    vPanel.Children.Add(bp);
                }
            }
        }
        else if (rootPanel.Orientation == Orientation.Vertical)
        {
            List<LayoutAnchorablePane> bottomPanesToLift = new();
            foreach (LayoutPanel? child in rootPanel.Children.OfType<LayoutPanel>().ToList())
            {
                if (child.Orientation == Orientation.Horizontal)
                {
                    foreach (LayoutPanel? subChild in child.Children.OfType<LayoutPanel>().ToList())
                    {
                        if (subChild.Orientation == Orientation.Vertical)
                        {
                            foreach (LayoutAnchorablePane? pane in subChild.Children.OfType<LayoutAnchorablePane>().ToList())
                            {
                                if (subChild.Children.IndexOf(pane) == subChild.Children.Count - 1 && subChild.Children.Count > 1)
                                {
                                    bottomPanesToLift.Add(pane);
                                }
                            }
                        }
                    }
                }
            }

            foreach (LayoutAnchorablePane bp in bottomPanesToLift)
            {
                bp.Parent?.RemoveChild(bp);
                rootPanel.Children.Add(bp);
            }
        }
    }

    /// <inheritdoc/>
    public void EnsureSidesFullHeight(LayoutRoot root)
    {
        if (root == null)
        {
            return;
        }

        LayoutPanel rootPanel = root.RootPanel;
        if (rootPanel == null)
        {
            return;
        }

        if (rootPanel.Orientation == Orientation.Vertical)
        {
            List<LayoutAnchorablePane> leftPanes = new();
            List<LayoutAnchorablePane> rightPanes = new();

            foreach (LayoutPanel? child in rootPanel.Children.OfType<LayoutPanel>().ToList())
            {
                if (child.Orientation == Orientation.Horizontal)
                {
                    foreach (LayoutAnchorablePane? pane in child.Children.OfType<LayoutAnchorablePane>().ToList())
                    {
                        int idx = child.Children.IndexOf(pane);
                        if (idx == 0 && child.Children.Count > 1)
                        {
                            leftPanes.Add(pane);
                        }
                        else if (idx == child.Children.Count - 1 && child.Children.Count > 1)
                        {
                            rightPanes.Add(pane);
                        }
                    }
                }
            }

            if (leftPanes.Count > 0 || rightPanes.Count > 0)
            {
                LayoutPanel hPanel = new()
                {
                    Orientation = Orientation.Horizontal
                };
                root.RootPanel = hPanel;

                foreach (LayoutAnchorablePane lp in leftPanes)
                {
                    lp.Parent?.RemoveChild(lp);
                }

                foreach (LayoutAnchorablePane rp in rightPanes)
                {
                    rp.Parent?.RemoveChild(rp);
                }

                foreach (LayoutAnchorablePane lp in leftPanes)
                {
                    hPanel.Children.Add(lp);
                }

                hPanel.Children.Add(rootPanel);
                foreach (LayoutAnchorablePane rp in rightPanes)
                {
                    hPanel.Children.Add(rp);
                }
            }
        }
        else if (rootPanel.Orientation == Orientation.Horizontal)
        {
            List<LayoutAnchorablePane> leftPanesToLift = new();
            List<LayoutAnchorablePane> rightPanesToLift = new();

            foreach (LayoutPanel? child in rootPanel.Children.OfType<LayoutPanel>().ToList())
            {
                if (child.Orientation == Orientation.Vertical)
                {
                    foreach (LayoutPanel? subChild in child.Children.OfType<LayoutPanel>().ToList())
                    {
                        if (subChild.Orientation == Orientation.Horizontal)
                        {
                            foreach (LayoutAnchorablePane? pane in subChild.Children.OfType<LayoutAnchorablePane>().ToList())
                            {
                                int idx = subChild.Children.IndexOf(pane);
                                if (idx == 0 && subChild.Children.Count > 1)
                                {
                                    leftPanesToLift.Add(pane);
                                }
                                else if (idx == subChild.Children.Count - 1 && subChild.Children.Count > 1)
                                {
                                    rightPanesToLift.Add(pane);
                                }
                            }
                        }
                    }
                }
            }

            foreach (LayoutAnchorablePane lp in leftPanesToLift)
            {
                lp.Parent?.RemoveChild(lp);
                rootPanel.Children.Insert(0, lp);
            }

            foreach (LayoutAnchorablePane rp in rightPanesToLift)
            {
                rp.Parent?.RemoveChild(rp);
                rootPanel.Children.Add(rp);
            }
        }
    }

    /// <inheritdoc/>
    public Orientation GetDesiredOrientation(AnchorSide side)
    {
        return side == AnchorSide.Bottom || side == AnchorSide.Top
            ? Orientation.Horizontal
            : Orientation.Vertical;
    }

    /// <summary>
    /// Updates the group's cross-axis dimension (DockWidth for Vertical groups,
    /// DockHeight for Horizontal groups) to the maximum of its children.
    /// </summary>
    /// <param name="group">The pane group to update.</param>
    private static void UpdateGroupDimensions(LayoutAnchorablePaneGroup group)
    {
        List<LayoutAnchorablePane> panes = group.Children.OfType<LayoutAnchorablePane>().ToList();
        if (panes.Count == 0)
        {
            return;
        }

        if (group.Orientation == Orientation.Vertical)
        {
            double maxWidth = panes.Max(p => p.DockWidth.IsAbsolute ? p.DockWidth.Value : 0);
            double maxMinWidth = panes.Max(p => p.DockMinWidth);
            if (maxWidth > 0)
            {
                group.DockWidth = new GridLength(maxWidth);
            }

            if (maxMinWidth > 0)
            {
                group.DockMinWidth = maxMinWidth;
            }
        }
        else
        {
            double maxHeight = panes.Max(p => p.DockHeight.IsAbsolute ? p.DockHeight.Value : 0);
            double maxMinHeight = panes.Max(p => p.DockMinHeight);
            if (maxHeight > 0)
            {
                group.DockHeight = new GridLength(maxHeight);
            }

            if (maxMinHeight > 0)
            {
                group.DockMinHeight = maxMinHeight;
            }
        }
    }
}
