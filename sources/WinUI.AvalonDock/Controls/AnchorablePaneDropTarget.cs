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

        switch (Type)
        {
            case DropTargetType.AnchorablePaneDockBottom:
                {
                    int insertToIndex = parentModel.IndexOfChild(targetModel);

                    if (parentModelOrientable.Orientation != Microsoft.UI.Xaml.Controls.Orientation.Vertical &&
                        parentModel.ChildrenCount == 1)
                    {
                        parentModelOrientable.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                    }

                    if (parentModelOrientable.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical)
                    {
                        LayoutAnchorablePaneGroup layoutAnchorablePaneGroup = floatingPanel;
                        if (layoutAnchorablePaneGroup != null &&
                            (layoutAnchorablePaneGroup.Children.Count == 1 ||
                                layoutAnchorablePaneGroup.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical))
                        {
                            ILayoutAnchorablePane[] anchorablesToMove = layoutAnchorablePaneGroup.Children.ToArray();
                            for (int i = 0; i < anchorablesToMove.Length; i++)
                            {
                                parentModel.InsertChildAt(insertToIndex + 1 + i, anchorablesToMove[i]);
                            }
                        }
                        else
                        {
                            parentModel.InsertChildAt(insertToIndex + 1, floatingPanel);
                        }
                    }
                    else
                    {
                        LayoutAnchorablePane targetModelAsPositionableElement = targetModel;
                        LayoutAnchorablePaneGroup newOrientedPanel = new()
                        {
                            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical,
                            DockWidth = targetModelAsPositionableElement.DockWidth,
                            DockHeight = targetModelAsPositionableElement.DockHeight,
                        };

                        parentModel.InsertChildAt(insertToIndex, newOrientedPanel);
                        newOrientedPanel.Children.Add(targetModel);
                        newOrientedPanel.Children.Add(floatingPanel);
                    }
                }

                break;

            case DropTargetType.AnchorablePaneDockTop:
                {
                    int insertToIndex = parentModel.IndexOfChild(targetModel);

                    if (parentModelOrientable.Orientation != Microsoft.UI.Xaml.Controls.Orientation.Vertical &&
                        parentModel.ChildrenCount == 1)
                    {
                        parentModelOrientable.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                    }

                    if (parentModelOrientable.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical)
                    {
                        LayoutAnchorablePaneGroup layoutAnchorablePaneGroup = floatingPanel;
                        if (layoutAnchorablePaneGroup != null &&
                            (layoutAnchorablePaneGroup.Children.Count == 1 ||
                                layoutAnchorablePaneGroup.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical))
                        {
                            ILayoutAnchorablePane[] anchorablesToMove = layoutAnchorablePaneGroup.Children.ToArray();
                            for (int i = 0; i < anchorablesToMove.Length; i++)
                            {
                                parentModel.InsertChildAt(insertToIndex + i, anchorablesToMove[i]);
                            }
                        }
                        else
                        {
                            parentModel.InsertChildAt(insertToIndex, floatingPanel);
                        }
                    }
                    else
                    {
                        LayoutAnchorablePane targetModelAsPositionableElement = targetModel;
                        LayoutAnchorablePaneGroup newOrientedPanel = new()
                        {
                            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical,
                            DockWidth = targetModelAsPositionableElement.DockWidth,
                            DockHeight = targetModelAsPositionableElement.DockHeight,
                        };

                        parentModel.InsertChildAt(insertToIndex, newOrientedPanel);
                        // the floating window must be added after the target modal as it could be raise a CollectGarbage call
                        newOrientedPanel.Children.Add(targetModel);
                        newOrientedPanel.Children.Insert(0, floatingPanel);
                    }
                }

                break;

            case DropTargetType.AnchorablePaneDockLeft:
                {
                    int insertToIndex = parentModel.IndexOfChild(targetModel);

                    if (parentModelOrientable.Orientation != Microsoft.UI.Xaml.Controls.Orientation.Horizontal &&
                        parentModel.ChildrenCount == 1)
                    {
                        parentModelOrientable.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                    }

                    if (parentModelOrientable.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Horizontal)
                    {
                        LayoutAnchorablePaneGroup layoutAnchorablePaneGroup = floatingPanel;
                        if (layoutAnchorablePaneGroup != null &&
                            (layoutAnchorablePaneGroup.Children.Count == 1 ||
                                layoutAnchorablePaneGroup.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Horizontal))
                        {
                            ILayoutAnchorablePane[] anchorablesToMove = layoutAnchorablePaneGroup.Children.ToArray();
                            for (int i = 0; i < anchorablesToMove.Length; i++)
                            {
                                parentModel.InsertChildAt(insertToIndex + i, anchorablesToMove[i]);
                            }
                        }
                        else
                        {
                            parentModel.InsertChildAt(insertToIndex, floatingPanel);
                        }
                    }
                    else
                    {
                        LayoutAnchorablePane targetModelAsPositionableElement = targetModel;
                        LayoutAnchorablePaneGroup newOrientedPanel = new()
                        {
                            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
                            DockWidth = targetModelAsPositionableElement.DockWidth,
                            DockHeight = targetModelAsPositionableElement.DockHeight,
                        };

                        parentModel.InsertChildAt(insertToIndex, newOrientedPanel);
                        // the floating window must be added after the target modal as it could be raise a CollectGarbage call
                        newOrientedPanel.Children.Add(targetModel);
                        newOrientedPanel.Children.Insert(0, floatingPanel);
                    }
                }

                break;

            case DropTargetType.AnchorablePaneDockRight:
                {
                    int insertToIndex = parentModel.IndexOfChild(targetModel);

                    if (parentModelOrientable.Orientation != Microsoft.UI.Xaml.Controls.Orientation.Horizontal &&
                        parentModel.ChildrenCount == 1)
                    {
                        parentModelOrientable.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                    }

                    if (parentModelOrientable.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Horizontal)
                    {
                        LayoutAnchorablePaneGroup layoutAnchorablePaneGroup = floatingPanel;
                        if (layoutAnchorablePaneGroup != null &&
                            (layoutAnchorablePaneGroup.Children.Count == 1 ||
                                layoutAnchorablePaneGroup.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Horizontal))
                        {
                            ILayoutAnchorablePane[] anchorablesToMove = layoutAnchorablePaneGroup.Children.ToArray();
                            for (int i = 0; i < anchorablesToMove.Length; i++)
                            {
                                parentModel.InsertChildAt(insertToIndex + 1 + i, anchorablesToMove[i]);
                            }
                        }
                        else
                        {
                            parentModel.InsertChildAt(insertToIndex + 1, floatingPanel);
                        }
                    }
                    else
                    {
                        LayoutAnchorablePane targetModelAsPositionableElement = targetModel;
                        LayoutAnchorablePaneGroup newOrientedPanel = new()
                        {
                            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
                            DockWidth = targetModelAsPositionableElement.DockWidth,
                            DockHeight = targetModelAsPositionableElement.DockHeight,
                        };

                        parentModel.InsertChildAt(insertToIndex, newOrientedPanel);
                        newOrientedPanel.Children.Add(targetModel);
                        newOrientedPanel.Children.Add(floatingPanel);
                    }
                }

                break;

            case DropTargetType.AnchorablePaneDockInside:
                {
                    LayoutAnchorablePane paneModel = targetModel;
                    LayoutAnchorablePaneGroup layoutAnchorablePaneGroup = floatingPanel;

                    int i = tabIndex == -1 ? 0 : tabIndex;
                    foreach (LayoutAnchorable? anchorableToImport in
                        layoutAnchorablePaneGroup.Descendents().OfType<LayoutAnchorable>().ToArray())
                    {
                        paneModel.Children.Insert(i, anchorableToImport);
                        i++;
                    }
                }

                break;
        }

        if (anchorableActive is not null)
        {
            anchorableActive.IsActive = true;
        }

        base.Drop(floatingWindow);
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
