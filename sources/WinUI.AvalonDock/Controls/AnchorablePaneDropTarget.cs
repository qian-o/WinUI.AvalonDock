// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/AnchorablePaneDropTarget.cs
using System.Linq;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the anchorable pane drop target.
/// </summary>
internal class AnchorablePaneDropTarget : DropTarget<LayoutAnchorablePaneControl>
{
    private LayoutAnchorablePaneControl targetPane;
    private int tabIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnchorablePaneDropTarget"/> class.
    /// </summary>
    /// <param name="paneControl">The pane control.</param>
    /// <param name="detectionRect">The detection rectangle.</param>
    /// <param name="type">The drop target type.</param>
    internal AnchorablePaneDropTarget(
        LayoutAnchorablePaneControl paneControl,
        Rect detectionRect,
        DropTargetType type)
        : base(paneControl, detectionRect, type)
    {
        targetPane = paneControl;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnchorablePaneDropTarget"/> class.
    /// </summary>
    /// <param name="paneControl">The pane control.</param>
    /// <param name="detectionRect">The detection rectangle.</param>
    /// <param name="type">The drop target type.</param>
    /// <param name="tabIndex">The tab index.</param>
    internal AnchorablePaneDropTarget(
        LayoutAnchorablePaneControl paneControl,
        Rect detectionRect,
        DropTargetType type,
        int tabIndex)
        : base(paneControl, detectionRect, type)
    {
        targetPane = paneControl;
        this.tabIndex = tabIndex;
        TabIndex = tabIndex;
    }

    /// <inheritdoc/>
    protected override void Drop(LayoutAnchorableFloatingWindow floatingWindow)
    {
        if (targetPane.Model is not LayoutAnchorablePane targetModel || floatingWindow.RootPanel is not { } floatingPanel
            || targetModel.Parent is not ILayoutGroup parentModel || targetModel.Parent is not ILayoutOrientableGroup parentModelOrientable)
        {
            return;
        }

        LayoutAnchorable? anchorableActive = floatingPanel.Descendents().OfType<LayoutAnchorable>().FirstOrDefault();
        (Microsoft.UI.Xaml.Controls.Orientation Orientation, bool InsertAfter)? placement = Type switch
        {
            DropTargetType.AnchorablePaneDockLeft => (Microsoft.UI.Xaml.Controls.Orientation.Horizontal, false),
            DropTargetType.AnchorablePaneDockRight => (Microsoft.UI.Xaml.Controls.Orientation.Horizontal, true),
            DropTargetType.AnchorablePaneDockTop => (Microsoft.UI.Xaml.Controls.Orientation.Vertical, false),
            DropTargetType.AnchorablePaneDockBottom => (Microsoft.UI.Xaml.Controls.Orientation.Vertical, true),
            _ => null,
        };
        if (placement is { } edge)
        {
            DockAtEdge(targetModel, parentModel, parentModelOrientable, floatingPanel, edge.Orientation, edge.InsertAfter);
        }
        else if (Type == DropTargetType.AnchorablePaneDockInside)
        {
            int i = tabIndex == -1 ? 0 : tabIndex;
            foreach (LayoutAnchorable anchorableToImport in floatingPanel.Descendents().OfType<LayoutAnchorable>().ToArray())
            {
                targetModel.Children.Insert(i++, anchorableToImport);
            }
        }

        if (anchorableActive is not null)
        {
            anchorableActive.IsActive = true;
        }

        base.Drop(floatingWindow);
    }

    private static void DockAtEdge(LayoutAnchorablePane targetModel, ILayoutGroup parentModel,
        ILayoutOrientableGroup parentModelOrientable, LayoutAnchorablePaneGroup floatingPanel,
        Microsoft.UI.Xaml.Controls.Orientation orientation, bool insertAfter)
    {
        int insertToIndex = parentModel.IndexOfChild(targetModel);
        if (parentModelOrientable.Orientation != orientation && parentModel.ChildrenCount == 1)
        {
            parentModelOrientable.Orientation = orientation;
        }

        if (parentModelOrientable.Orientation == orientation)
        {
            int firstIndex = insertToIndex + (insertAfter ? 1 : 0);
            if (floatingPanel.Children.Count == 1 || floatingPanel.Orientation == orientation)
            {
                ILayoutAnchorablePane[] anchorablesToMove = floatingPanel.Children.ToArray();
                for (int i = 0; i < anchorablesToMove.Length; i++)
                {
                    parentModel.InsertChildAt(firstIndex + i, anchorablesToMove[i]);
                }
            }
            else
            {
                parentModel.InsertChildAt(firstIndex, floatingPanel);
            }
        }
        else
        {
            LayoutAnchorablePaneGroup newOrientedPanel = new()
            {
                Orientation = orientation,
                DockWidth = targetModel.DockWidth,
                DockHeight = targetModel.DockHeight,
            };

            parentModel.InsertChildAt(insertToIndex, newOrientedPanel);
            // Attach the target before transferring floating contents: the transfer
            // can synchronously collect empty layout groups.
            newOrientedPanel.Children.Add(targetModel);
            if (insertAfter)
            {
                newOrientedPanel.Children.Add(floatingPanel);
            }
            else
            {
                newOrientedPanel.Children.Insert(0, floatingPanel);
            }
        }
    }

    /// <inheritdoc/>
    public override Geometry? GetPreviewPath(
        OverlayWindow overlayWindow,
        LayoutFloatingWindow floatingWindowModel)
    {
        switch (Type)
        {
            case DropTargetType.AnchorablePaneDockBottom:
            case DropTargetType.AnchorablePaneDockTop:
            case DropTargetType.AnchorablePaneDockLeft:
            case DropTargetType.AnchorablePaneDockRight:
                {
                    Rect targetScreenRect = overlayWindow.GetPreviewBounds(TargetElement);

                    if (OverlayPreviewRules.TryComputePanePreviewRect(
                        Type,
                        targetScreenRect.Width,
                        targetScreenRect.Height,
                        out double left,
                        out double top,
                        out double width,
                        out double height))
                    {
                        targetScreenRect = new Rect(
                            targetScreenRect.Left + left,
                            targetScreenRect.Top + top,
                            width,
                            height);
                    }

                    return new RectangleGeometry { Rect = targetScreenRect };
                }

            case DropTargetType.AnchorablePaneDockInside:
                {
                    Rect targetScreenRect = overlayWindow.GetPreviewBounds(TargetElement);

                    if (tabIndex == -1)
                    {
                        return new RectangleGeometry { Rect = targetScreenRect };
                    }
                    else
                    {
                        Rect translatedDetectionRect = overlayWindow.GetPreviewBounds(DetectionRects[0]);

                        PathFigure pathFigure = new();
                        pathFigure.StartPoint = new Point(targetScreenRect.Left, targetScreenRect.Top);
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(targetScreenRect.Left, translatedDetectionRect.Top) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(translatedDetectionRect.Left, translatedDetectionRect.Top) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(translatedDetectionRect.Left, translatedDetectionRect.Bottom) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(translatedDetectionRect.Right, translatedDetectionRect.Bottom) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(translatedDetectionRect.Right, translatedDetectionRect.Top) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(targetScreenRect.Right, translatedDetectionRect.Top) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(targetScreenRect.Right, targetScreenRect.Top) });
                        pathFigure.IsClosed = true;
                        pathFigure.IsFilled = true;


                        return new PathGeometry { Figures = { pathFigure } };
                    }
                }
        }

        return null;
    }
}
