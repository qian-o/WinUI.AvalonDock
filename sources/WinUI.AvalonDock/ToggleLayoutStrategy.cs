// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/ToggleLayoutStrategy.cs
using System.Linq;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;

namespace AvalonDock;

/// <summary>
/// Layout strategy for the ToggleDockingManager that places anchorables
/// into left/right/bottom anchor sides based on <see cref="IToolbox.Zone"/>.
/// Also sets <see cref="ToggleDock.IconProperty"/> and <see cref="ToggleDock.ToolTipProperty"/> attached
/// properties from the ViewModel metadata.
/// </summary>
public class ToggleLayoutStrategy : ILayoutUpdateStrategy
{
    /// <inheritdoc/>
    public bool BeforeInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer? destinationContainer)
    {
        if (!(anchorableToShow.Content is IToolbox toolbox))
        {
            return false;
        }

        // Set metadata on the LayoutAnchorable
        anchorableToShow.Title = toolbox.Title;
        anchorableToShow.ContentId = toolbox.Id;

        if (toolbox.Icon != null)
        {
            ToggleDock.SetIcon(anchorableToShow, toolbox.Icon);
        }

        if (toolbox.ToolTipText != null)
        {
            ToggleDock.SetToolTip(anchorableToShow, toolbox.ToolTipText);
        }

        // Place into the correct anchor side based on zone
        LayoutAnchorSide side = GetAnchorSide(layout, toolbox.Zone);
        LayoutAnchorGroup? group = side.Children.OfType<LayoutAnchorGroup>().FirstOrDefault();
        if (group == null)
        {
            group = new LayoutAnchorGroup();
            side.Children.Add(group);
        }

        group.Children.Add(anchorableToShow);
        return true;
    }

    /// <inheritdoc/>
    public void AfterInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableShown)
    {
    }

    /// <inheritdoc/>
    public bool BeforeInsertDocument(LayoutRoot layout, LayoutDocument anchorableToShow, ILayoutContainer? destinationContainer)
    {
        return false;
    }

    /// <inheritdoc/>
    public void AfterInsertDocument(LayoutRoot layout, LayoutDocument anchorableShown)
    {
    }

    private static LayoutAnchorSide GetAnchorSide(LayoutRoot layout, DockZone zone)
    {
        switch (zone)
        {
            case DockZone.RightTop:
            case DockZone.RightBottom:
                if (layout.RightSide == null)
                {
                    layout.RightSide = new LayoutAnchorSide();
                }

                return layout.RightSide;

            case DockZone.BottomLeft:
            case DockZone.BottomRight:
                if (layout.BottomSide == null)
                {
                    layout.BottomSide = new LayoutAnchorSide();
                }

                return layout.BottomSide;

            default: // LeftTop, LeftBottom
                if (layout.LeftSide == null)
                {
                    layout.LeftSide = new LayoutAnchorSide();
                }

                return layout.LeftSide;
        }
    }
}
