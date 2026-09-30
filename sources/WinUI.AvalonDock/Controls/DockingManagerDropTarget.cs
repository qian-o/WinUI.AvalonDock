// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DockingManagerDropTarget.cs
using System;
using System.Linq;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the docking manager drop target.
/// </summary>
internal class DockingManagerDropTarget : DropTarget<DockingManager>
{
    private DockingManager manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockingManagerDropTarget"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="detectionRect">The detection rectangle.</param>
    /// <param name="type">The drop target type.</param>
    internal DockingManagerDropTarget(
        DockingManager manager,
        Rect detectionRect,
        DropTargetType type)
        : base(manager, detectionRect, type)
    {
        this.manager = manager;
    }

    /// <inheritdoc/>
    protected override void Drop(LayoutAnchorableFloatingWindow floatingWindow)
    {
        if (floatingWindow.RootPanel is not { } floatingPanel)
        {
            return;
        }

        (Orientation Orientation, bool InsertAfter)? placement = Type switch
        {
            DropTargetType.DockingManagerDockLeft => (Orientation.Horizontal, false),
            DropTargetType.DockingManagerDockRight => (Orientation.Horizontal, true),
            DropTargetType.DockingManagerDockTop => (Orientation.Vertical, false),
            DropTargetType.DockingManagerDockBottom => (Orientation.Vertical, true),
            _ => null,
        };
        if (placement is { } edge)
        {
            DockAtEdge(floatingPanel, edge.Orientation, edge.InsertAfter);
        }

        base.Drop(floatingWindow);
    }

    private void DockAtEdge(LayoutAnchorablePaneGroup floatingPanel, Orientation orientation, bool insertAfter)
    {
        if (manager.Layout.RootPanel.Orientation != orientation && manager.Layout.RootPanel.Children.Count == 1)
        {
            manager.Layout.RootPanel.Orientation = orientation;
        }

        if (manager.Layout.RootPanel.Orientation == orientation)
        {
            if (floatingPanel.Orientation == orientation)
            {
                ILayoutAnchorablePane[] childrenToTransfer = floatingPanel.Children.ToArray();
                for (int i = 0; i < childrenToTransfer.Length; i++)
                {
                    if (insertAfter)
                    {
                        manager.Layout.RootPanel.Children.Add(childrenToTransfer[i]);
                    }
                    else
                    {
                        manager.Layout.RootPanel.Children.Insert(i, childrenToTransfer[i]);
                    }
                }
            }
            else if (insertAfter)
            {
                manager.Layout.RootPanel.Children.Add(floatingPanel);
            }
            else
            {
                manager.Layout.RootPanel.Children.Insert(0, floatingPanel);
            }
        }
        else
        {
            LayoutPanel newOrientedPanel = new() { Orientation = orientation };
            // Retain the original mutation order: the floating group is attached first.
            newOrientedPanel.Children.Add(floatingPanel);
            if (insertAfter)
            {
                newOrientedPanel.Children.Insert(0, manager.Layout.RootPanel);
            }
            else
            {
                newOrientedPanel.Children.Add(manager.Layout.RootPanel);
            }

            manager.Layout.RootPanel = newOrientedPanel;
        }
    }

    /// <inheritdoc/>
    public override Geometry? GetPreviewPath(
        OverlayWindow overlayWindow,
        LayoutFloatingWindow floatingWindowModel)
    {
        if (floatingWindowModel is not LayoutAnchorableFloatingWindow { RootPanel: { } layoutAnchorablePane })
        {
            return null;
        }

        ILayoutPositionableElementWithActualSize layoutAnchorablePaneWithActualSize = (ILayoutPositionableElementWithActualSize)layoutAnchorablePane;

        Rect targetScreenRect = overlayWindow.GetPreviewBounds(TargetElement);

        // Preferred dock size used by the outer-edge rules: width for Left/Right, height for Top/Bottom.
        double preferredSize = Type == DropTargetType.DockingManagerDockTop || Type == DropTargetType.DockingManagerDockBottom
            ? (layoutAnchorablePane.DockHeight.IsAbsolute ? layoutAnchorablePane.DockHeight.Value : layoutAnchorablePaneWithActualSize.ActualHeight)
            : (layoutAnchorablePane.DockWidth.IsAbsolute ? layoutAnchorablePane.DockWidth.Value : layoutAnchorablePaneWithActualSize.ActualWidth);

        if (OverlayPreviewRules.TryComputeManagerPreviewRect(
            Type,
            targetScreenRect.Width,
            targetScreenRect.Height,
            preferredSize,
            out double left,
            out double top,
            out double width,
            out double height))
        {
            Rect previewBoxRect = new(
                targetScreenRect.Left + left,
                targetScreenRect.Top + top,
                width,
                height);

            return new RectangleGeometry { Rect = previewBoxRect };
        }

        throw new InvalidOperationException();
    }
}
