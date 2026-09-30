// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DocumentPaneDropTarget.cs
using System.Linq;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the document pane drop target.
/// </summary>
internal class DocumentPaneDropTarget : DropTarget<LayoutDocumentPaneControl>
{
    private LayoutDocumentPaneControl targetPane;
    private int tabIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentPaneDropTarget"/> class.
    /// </summary>
    /// <param name="paneControl">The pane control.</param>
    /// <param name="detectionRect">The detection rectangle.</param>
    /// <param name="type">The drop target type.</param>
    internal DocumentPaneDropTarget(
        LayoutDocumentPaneControl paneControl,
        Rect detectionRect,
        DropTargetType type)
        : base(paneControl, detectionRect, type)
    {
        targetPane = paneControl;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentPaneDropTarget"/> class.
    /// </summary>
    /// <param name="paneControl">The pane control.</param>
    /// <param name="detectionRect">The detection rectangle.</param>
    /// <param name="type">The drop target type.</param>
    /// <param name="tabIndex">The tab index.</param>
    internal DocumentPaneDropTarget(
        LayoutDocumentPaneControl paneControl,
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
    protected override void Drop(LayoutDocumentFloatingWindow floatingWindow)
    {
        if (targetPane.Model is not LayoutDocumentPane targetModel || targetModel.Root?.Manager is not { } manager
            || targetModel.Parent is not ILayoutGroup layoutGroup || floatingWindow.RootPanel is not { } floatingPanel)
        {
            return;
        }

        LayoutDocument? documentActive = floatingWindow.Descendents().OfType<LayoutDocument>().FirstOrDefault();

        Orientation requiredOrientation = Type == DropTargetType.DocumentPaneDockBottom || Type == DropTargetType.DocumentPaneDockTop ?
            Microsoft.UI.Xaml.Controls.Orientation.Vertical : Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
        bool allowMixedOrientation = manager.AllowMixedOrientation;
        bool isInsideDrop = Type == DropTargetType.DocumentPaneDockInside;
        if (isInsideDrop)
        {
            LayoutDocumentPaneGroup layoutDocumentPaneGroup = floatingPanel;
            int i = tabIndex == -1 ? 0 : tabIndex;
            foreach (LayoutContent? contentToImport in layoutDocumentPaneGroup.Descendents().OfType<LayoutContent>().ToArray())
            {
                if (contentToImport is LayoutDocument or LayoutAnchorable)
                {
                    targetModel.Children.Insert(i++, contentToImport);
                }
            }

            if (documentActive != null)
            {
                documentActive.IsActive = true;
            }
            base.Drop(floatingWindow);
            return;
        }

        LayoutDocumentPaneGroup paneGroup;
        if (targetModel.Parent is LayoutDocumentPaneGroup existingPaneGroup)
        {
            paneGroup = existingPaneGroup;
        }
        else
        {
            ILayoutPositionableElement targetModelAsPositionableElement = (ILayoutPositionableElement)targetModel;
            int targetIndex = layoutGroup.IndexOfChild(targetModel);
            if (targetIndex < 0)
            {
                return;
            }

            paneGroup = new LayoutDocumentPaneGroup
            {
                Orientation = requiredOrientation,
                DockWidth = targetModelAsPositionableElement.DockWidth,
                DockHeight = targetModelAsPositionableElement.DockHeight,
            };

            layoutGroup.ReplaceChild(targetModel, paneGroup);
            paneGroup.Children.Add(targetModel);
        }

        if (allowMixedOrientation && paneGroup.Orientation != requiredOrientation)
        {
            ILayoutPositionableElement targetModelAsPositionableElement = (ILayoutPositionableElement)targetModel;
            LayoutDocumentPaneGroup newGroup = new()
            {
                Orientation = requiredOrientation,
                DockWidth = targetModelAsPositionableElement.DockWidth,
                DockHeight = targetModelAsPositionableElement.DockHeight,
            };

            paneGroup.ReplaceChild(targetModel, newGroup);
            newGroup.Children.Add(targetModel);
            paneGroup = newGroup;
        }

        if (Type is DropTargetType.DocumentPaneDockBottom or DropTargetType.DocumentPaneDockTop
            or DropTargetType.DocumentPaneDockLeft or DropTargetType.DocumentPaneDockRight)
        {
            if (!allowMixedOrientation && paneGroup.Orientation != requiredOrientation)
            {
                paneGroup.Orientation = requiredOrientation;
            }

            bool insertAfter = Type is DropTargetType.DocumentPaneDockBottom or DropTargetType.DocumentPaneDockRight;
            int targetIndex = paneGroup.IndexOfChild(targetModel);
            int insertToIndex = insertAfter
                ? targetIndex < 0 ? paneGroup.Children.Count : Math.Min(targetIndex + 1, paneGroup.Children.Count)
                : targetIndex < 0 ? 0 : targetIndex;

            ILayoutElement[] documentsToMove = floatingWindow.Children.ToArray();
            for (int i = 0; i < documentsToMove.Length; i++)
            {
                paneGroup.InsertChildAt(insertToIndex + i, documentsToMove[i]);
            }
        }

        if (documentActive != null)
        {
            documentActive.IsActive = true;
        }

        base.Drop(floatingWindow);
    }

    /// <inheritdoc/>
    protected override void Drop(LayoutAnchorableFloatingWindow floatingWindow)
    {
        if (targetPane.Model is not LayoutDocumentPane targetModel || targetModel.Root?.Manager is not { } manager
            || targetModel.Parent is not { } parentContainer || floatingWindow.RootPanel is not { } floatingPanel)
        {
            return;
        }

        if (Type is DropTargetType.DocumentPaneDockBottom or DropTargetType.DocumentPaneDockTop
            or DropTargetType.DocumentPaneDockLeft or DropTargetType.DocumentPaneDockRight)
        {
            Orientation orientation = Type is DropTargetType.DocumentPaneDockBottom or DropTargetType.DocumentPaneDockTop
                ? Orientation.Vertical : Orientation.Horizontal;
            bool insertAfter = Type is DropTargetType.DocumentPaneDockBottom or DropTargetType.DocumentPaneDockRight;
            LayoutDocumentPane newLayoutDocumentPane = AddAdjacentDocumentPane(targetModel, parentContainer, manager,
                orientation, insertAfter);
            foreach (LayoutAnchorable content in floatingPanel.Descendents().OfType<LayoutAnchorable>().ToArray())
            {
                newLayoutDocumentPane.Children.Add(content);
            }
        }
        else if (Type == DropTargetType.DocumentPaneDockInside)
        {
            LayoutDocumentPane paneModel = targetModel;
            LayoutAnchorablePaneGroup layoutAnchorablePaneGroup = floatingPanel;

            bool checkPreviousContainer = true;
            int i = 0;
            if (tabIndex != -1)
            {
                i = tabIndex;
                checkPreviousContainer = false;
            }

            LayoutAnchorable? anchorableToActivate = null;

            foreach (LayoutAnchorable? anchorableToImport in layoutAnchorablePaneGroup.Descendents().OfType<LayoutAnchorable>().ToArray())
            {
                if (checkPreviousContainer)
                {
                    ILayoutContainer? previousContainer = ((ILayoutPreviousContainer)anchorableToImport).PreviousContainer;
                    if (object.ReferenceEquals(previousContainer, targetModel) && (anchorableToImport.PreviousContainerIndex != -1))
                    {
                        i = anchorableToImport.PreviousContainerIndex;
                    }

                    checkPreviousContainer = false;
                }

                paneModel.Children.Insert(i, anchorableToImport);
                i++;
                anchorableToActivate = anchorableToImport;
            }

            if (anchorableToActivate is not null)
            {
                anchorableToActivate.IsActive = true;
            }
        }

        base.Drop(floatingWindow);
    }

    private static LayoutDocumentPane AddAdjacentDocumentPane(LayoutDocumentPane targetModel,
        ILayoutContainer parentContainer, DockingManager manager, Orientation orientation, bool insertAfter)
    {
        LayoutDocumentPaneGroup? parentModel = targetModel.Parent as LayoutDocumentPaneGroup;
        LayoutDocumentPane newPane = new();
        if (parentModel == null)
        {
            LayoutDocumentPaneGroup newParentModel = new() { Orientation = orientation };
            parentContainer.ReplaceChild(targetModel, newParentModel);
            newParentModel.Children.Add(insertAfter ? targetModel : newPane);
            newParentModel.Children.Add(insertAfter ? newPane : targetModel);
        }
        else if (!manager.AllowMixedOrientation || parentModel.Orientation == orientation)
        {
            parentModel.Orientation = orientation;
            int targetPaneIndex = parentModel.IndexOfChild(targetModel);
            parentModel.Children.Insert(targetPaneIndex + (insertAfter ? 1 : 0), newPane);
        }
        else
        {
            LayoutDocumentPaneGroup newChildGroup = new() { Orientation = orientation };
            parentModel.ReplaceChild(targetModel, newChildGroup);
            newChildGroup.Children.Add(insertAfter ? targetModel : newPane);
            newChildGroup.Children.Add(insertAfter ? newPane : targetModel);
        }

        return newPane;
    }

    /// <inheritdoc/>
    public override Geometry? GetPreviewPath(
        OverlayWindow overlayWindow,
        LayoutFloatingWindow floatingWindowModel)
    {
        switch (Type)
        {
            case DropTargetType.DocumentPaneDockInside:
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
                        pathFigure.StartPoint = new Point(targetScreenRect.Right, targetScreenRect.Bottom);
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(targetScreenRect.Right, translatedDetectionRect.Bottom) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(translatedDetectionRect.Right, translatedDetectionRect.Bottom) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(translatedDetectionRect.Right, translatedDetectionRect.Top) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(translatedDetectionRect.Left, translatedDetectionRect.Top) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(translatedDetectionRect.Left, translatedDetectionRect.Bottom) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(targetScreenRect.Left, translatedDetectionRect.Bottom) });
                        pathFigure.Segments.Add(new LineSegment() { Point = new Point(targetScreenRect.Left, targetScreenRect.Bottom) });
                        pathFigure.IsClosed = true;
                        pathFigure.IsFilled = true;

                        return new PathGeometry { Figures = { pathFigure } };
                    }
                }

            case DropTargetType.DocumentPaneDockBottom:
            case DropTargetType.DocumentPaneDockTop:
            case DropTargetType.DocumentPaneDockLeft:
            case DropTargetType.DocumentPaneDockRight:
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
        }

        return null;
    }
}
