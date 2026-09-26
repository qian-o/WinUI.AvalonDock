// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/ToggleLayoutEngine.cs
using System;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

/// <summary>
/// <see cref="ILayoutEngine"/> implementation for the <see cref="ToggleDockingManager"/>.
/// Extends the generic interface with zone-aware operations that use <see cref="DockZone"/>
/// for finer-grained control over pane placement (e.g. LeftTop vs LeftBottom).
/// <para>
/// All methods operate on the layout model only (no WPF visual tree), making them
/// independently testable.
/// </para>
/// </summary>
public class ToggleLayoutEngine : ILayoutEngine
{
    private static readonly DefaultLayoutEngine SharedLayout = new();

    /// <summary>
    /// Returns <c>true</c> if the zone is a bottom zone (BottomLeft or BottomRight).
    /// </summary>
    /// <param name="zone">The dock zone to test.</param>
    /// <returns><c>true</c> if the zone is a bottom zone; otherwise <c>false</c>.</returns>
    public static bool IsBottomZone(DockZone zone)
    {
        return zone == DockZone.BottomLeft || zone == DockZone.BottomRight;
    }

    /// <summary>
    /// Returns <c>true</c> if the zone is a left zone (LeftTop or LeftBottom).
    /// </summary>
    /// <param name="zone">The dock zone to test.</param>
    /// <returns><c>true</c> if the zone is a left zone; otherwise <c>false</c>.</returns>
    public static bool IsLeftZone(DockZone zone)
    {
        return zone == DockZone.LeftTop || zone == DockZone.LeftBottom;
    }

    /// <summary>
    /// Returns <c>true</c> if the zone is a right zone (RightTop or RightBottom).
    /// </summary>
    /// <param name="zone">The dock zone to test.</param>
    /// <returns><c>true</c> if the zone is a right zone; otherwise <c>false</c>.</returns>
    public static bool IsRightZone(DockZone zone)
    {
        return zone == DockZone.RightTop || zone == DockZone.RightBottom;
    }

    /// <summary>
    /// Maps a <see cref="DockZone"/> to the corresponding <see cref="AnchorSide"/>.
    /// </summary>
    /// <param name="zone">The dock zone.</param>
    /// <returns>The anchor side for the zone.</returns>
    public static AnchorSide ZoneToAnchorSide(DockZone zone)
    {
        if (IsLeftZone(zone))
        {
            return AnchorSide.Left;
        }

        if (IsRightZone(zone))
        {
            return AnchorSide.Right;
        }

        return AnchorSide.Bottom;
    }

    /// <summary>
    /// Returns the desired split orientation for panes docked in the given zone.
    /// Left/Right zones → Vertical (top-to-bottom stacking).
    /// Bottom zones → Horizontal (side-by-side stacking).
    /// </summary>
    /// <param name="zone">The dock zone.</param>
    /// <returns>The orientation for grouping panes in this zone.</returns>
    public static Orientation GetDesiredOrientationForZone(DockZone zone)
    {
        return IsBottomZone(zone) ? Orientation.Horizontal : Orientation.Vertical;
    }

    /// <inheritdoc/>
    public Orientation GetDesiredOrientation(AnchorSide side)
    {
        return side == AnchorSide.Bottom || side == AnchorSide.Top
            ? Orientation.Horizontal
            : Orientation.Vertical;
    }

    /// <inheritdoc/>
    public LayoutPanel FindOrCreateContentPanel(LayoutRoot root, Orientation orientation)
    {
        return SharedLayout.FindOrCreateContentPanel(root, orientation);
    }

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
                {
                    LayoutPanel hPanel = FindOrCreateContentPanel(root, Orientation.Horizontal);
                    hPanel.Children.Insert(0, pane);
                    break;
                }

            case AnchorSide.Right:
                {
                    LayoutPanel hPanel = FindOrCreateContentPanel(root, Orientation.Horizontal);
                    hPanel.Children.Add(pane);
                    break;
                }

            case AnchorSide.Bottom:
                {
                    LayoutPanel rootPanel = root.RootPanel;
                    if (rootPanel.Orientation == Orientation.Vertical)
                    {
                        rootPanel.Children.Add(pane);
                    }
                    else
                    {
                        LayoutPanel vPanel = new()
                        {
                            Orientation = Orientation.Vertical
                        };
                        root.RootPanel = vPanel;
                        vPanel.Children.Add(rootPanel);
                        vPanel.Children.Add(pane);
                    }

                    break;
                }

            case AnchorSide.Top:
                {
                    LayoutPanel rootPanel = root.RootPanel;
                    if (rootPanel.Orientation == Orientation.Vertical)
                    {
                        rootPanel.Children.Insert(0, pane);
                    }
                    else
                    {
                        LayoutPanel vPanel = new()
                        {
                            Orientation = Orientation.Vertical
                        };
                        root.RootPanel = vPanel;
                        vPanel.Children.Add(pane);
                        vPanel.Children.Add(rootPanel);
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// Inserts an anchorable pane into the correct position in the layout tree
    /// for the given dock zone. Provides finer-grained control than
    /// <see cref="InsertPane(LayoutRoot, LayoutAnchorablePane, AnchorSide)"/>
    /// by distinguishing between top/bottom zones on each side.
    /// </summary>
    /// <param name="root">The layout root.</param>
    /// <param name="pane">The pane to insert.</param>
    /// <param name="zone">The target dock zone.</param>
    public void InsertPaneForZone(LayoutRoot root, LayoutAnchorablePane pane, DockZone zone)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (pane == null)
        {
            throw new ArgumentNullException(nameof(pane));
        }

        AnchorSide side = ZoneToAnchorSide(zone);

        switch (side)
        {
            case AnchorSide.Right:
                {
                    LayoutPanel hPanel = FindOrCreateContentPanel(root, Orientation.Horizontal);
                    if (zone == DockZone.RightTop)
                    {
                        // Insert before existing right-side panes/groups
                        int insertIdx = hPanel.Children.Count;
                        while (insertIdx > 0 &&
                               (hPanel.Children[insertIdx - 1] is LayoutAnchorablePane ||
                                hPanel.Children[insertIdx - 1] is LayoutAnchorablePaneGroup))
                        {
                            insertIdx--;
                        }

                        hPanel.Children.Insert(insertIdx, pane);
                    }
                    else
                    {
                        hPanel.Children.Add(pane);
                    }

                    break;
                }

            case AnchorSide.Left:
                {
                    LayoutPanel hPanel = FindOrCreateContentPanel(root, Orientation.Horizontal);
                    if (zone == DockZone.LeftBottom)
                    {
                        // Insert after existing left-side panes/groups
                        int insertIdx = 0;
                        while (insertIdx < hPanel.Children.Count &&
                               (hPanel.Children[insertIdx] is LayoutAnchorablePane ||
                                hPanel.Children[insertIdx] is LayoutAnchorablePaneGroup))
                        {
                            insertIdx++;
                        }

                        hPanel.Children.Insert(insertIdx, pane);
                    }
                    else
                    {
                        hPanel.Children.Insert(0, pane);
                    }

                    break;
                }

            case AnchorSide.Bottom:
                {
                    LayoutPanel rootPanel = root.RootPanel;
                    if (rootPanel.Orientation == Orientation.Vertical)
                    {
                        if (zone == DockZone.BottomLeft)
                        {
                            // BottomLeft inserts before existing bottom panes/groups
                            int insertIdx = rootPanel.Children.Count;
                            while (insertIdx > 0 &&
                                   (rootPanel.Children[insertIdx - 1] is LayoutAnchorablePane ||
                                    rootPanel.Children[insertIdx - 1] is LayoutAnchorablePaneGroup))
                            {
                                insertIdx--;
                            }

                            rootPanel.Children.Insert(insertIdx, pane);
                        }
                        else
                        {
                            // BottomRight appends at the end
                            rootPanel.Children.Add(pane);
                        }
                    }
                    else
                    {
                        LayoutPanel vPanel = new()
                        {
                            Orientation = Orientation.Vertical
                        };
                        root.RootPanel = vPanel;
                        vPanel.Children.Add(rootPanel);
                        vPanel.Children.Add(pane);
                    }

                    break;
                }
        }
    }

    /// <inheritdoc/>
    public void FixSplitOrientation(LayoutAnchorable anchorable, AnchorSide side)
    {
        DefaultLayoutEngine.FixSplitOrientationCore(anchorable, GetDesiredOrientation(side), side == AnchorSide.Left || side == AnchorSide.Top);
    }

    /// <summary>
    /// After an anchorable is docked, fixes the split orientation of contiguous
    /// anchorable panes. Left/Right panes are grouped vertically (top-to-bottom),
    /// Bottom panes are grouped horizontally (side-by-side).
    /// </summary>
    /// <param name="anchorable">The anchorable that was just docked.</param>
    /// <param name="zone">The dock zone it was docked into.</param>
    public void FixSplitOrientationForZone(LayoutAnchorable anchorable, DockZone zone)
    {
        // "Start variant" means this zone should be first in the group:
        // LeftTop/RightTop → top of vertical group, BottomLeft → left of horizontal group.
        bool insertAtStart = zone == DockZone.LeftTop || zone == DockZone.RightTop || zone == DockZone.BottomLeft;
        DefaultLayoutEngine.FixSplitOrientationCore(anchorable, GetDesiredOrientationForZone(zone), insertAtStart);
    }

    /// <inheritdoc/>
    public void EnsureBottomFullWidth(LayoutRoot root)
    {
        SharedLayout.EnsureBottomFullWidth(root);
    }

    /// <inheritdoc/>
    public void EnsureSidesFullHeight(LayoutRoot root)
    {
        SharedLayout.EnsureSidesFullHeight(root);
    }


}
