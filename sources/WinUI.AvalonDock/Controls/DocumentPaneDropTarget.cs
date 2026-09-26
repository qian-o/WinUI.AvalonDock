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

        LayoutDocumentPaneGroup? paneGroup = targetModel.Parent as LayoutDocumentPaneGroup;
        Orientation requiredOrientation = Type == DropTargetType.DocumentPaneDockBottom || Type == DropTargetType.DocumentPaneDockTop ?
            Microsoft.UI.Xaml.Controls.Orientation.Vertical : Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
        bool allowMixedOrientation = manager.AllowMixedOrientation;

        if (paneGroup == null)
        {
            ILayoutPositionableElement targetModelAsPositionableElement = (ILayoutPositionableElement)targetModel;
            paneGroup = new LayoutDocumentPaneGroup
            {
                Orientation = requiredOrientation,
                DockWidth = targetModelAsPositionableElement.DockWidth,
                DockHeight = targetModelAsPositionableElement.DockHeight,
            };

            paneGroup.Children.Add(targetModel);
            layoutGroup.InsertChildAt(0, paneGroup);
        }
        else if (allowMixedOrientation && paneGroup.Orientation != requiredOrientation && Type != DropTargetType.DocumentPaneDockInside)
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

        switch (Type)
        {
            case DropTargetType.DocumentPaneDockBottom:
                {
                    if (!allowMixedOrientation && paneGroup.Orientation != Microsoft.UI.Xaml.Controls.Orientation.Vertical)
                    {
                        paneGroup.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                    }

                    int targetIndex = paneGroup.IndexOfChild(targetModel);
                    int insertToIndex = targetIndex < 0 ? paneGroup.Children.Count : targetIndex + 1;
                    if (insertToIndex > paneGroup.Children.Count)
                    {
                        insertToIndex = paneGroup.Children.Count;
                    }

                    ILayoutElement[] documentsToMove = floatingWindow.Children.ToArray();
                    for (int i = 0; i < documentsToMove.Length; i++)
                    {
                        ILayoutElement floatingChild = documentsToMove[i];
                        paneGroup.InsertChildAt(insertToIndex + i, floatingChild);
                    }
                }

                break;

            case DropTargetType.DocumentPaneDockTop:
                {
                    if (!allowMixedOrientation && paneGroup.Orientation != Microsoft.UI.Xaml.Controls.Orientation.Vertical)
                    {
                        paneGroup.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                    }

                    int insertToIndex = paneGroup.IndexOfChild(targetModel);
                    if (insertToIndex < 0)
                    {
                        insertToIndex = 0;
                    }

                    ILayoutElement[] documentsToMove = floatingWindow.Children.ToArray();
                    for (int i = 0; i < documentsToMove.Length; i++)
                    {
                        ILayoutElement floatingChild = documentsToMove[i];
                        paneGroup.InsertChildAt(insertToIndex + i, floatingChild);
                    }
                }

                break;

            case DropTargetType.DocumentPaneDockLeft:
                {
                    if (!allowMixedOrientation && paneGroup.Orientation != Microsoft.UI.Xaml.Controls.Orientation.Horizontal)
                    {
                        paneGroup.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                    }

                    int insertToIndex = paneGroup.IndexOfChild(targetModel);
                    if (insertToIndex < 0)
                    {
                        insertToIndex = 0;
                    }

                    ILayoutElement[] documentsToMove = floatingWindow.Children.ToArray();
                    for (int i = 0; i < documentsToMove.Length; i++)
                    {
                        ILayoutElement floatingChild = documentsToMove[i];
                        paneGroup.InsertChildAt(insertToIndex + i, floatingChild);
                    }
                }

                break;

            case DropTargetType.DocumentPaneDockRight:
                {
                    if (!allowMixedOrientation && paneGroup.Orientation != Microsoft.UI.Xaml.Controls.Orientation.Horizontal)
                    {
                        paneGroup.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                    }

                    int targetIndex = paneGroup.IndexOfChild(targetModel);
                    int insertToIndex = targetIndex < 0 ? paneGroup.Children.Count : targetIndex + 1;
                    if (insertToIndex > paneGroup.Children.Count)
                    {
                        insertToIndex = paneGroup.Children.Count;
                    }

                    ILayoutElement[] documentsToMove = floatingWindow.Children.ToArray();
                    for (int i = 0; i < documentsToMove.Length; i++)
                    {
                        ILayoutElement floatingChild = documentsToMove[i];
                        paneGroup.InsertChildAt(insertToIndex + i, floatingChild);
                    }
                }

                break;

            case DropTargetType.DocumentPaneDockInside:
                {
                    LayoutDocumentPane paneModel = targetModel;
                    LayoutDocumentPaneGroup layoutDocumentPaneGroup = floatingPanel;

                    // A LayoutFloatingDocumentWindow can contain multiple instances of both Anchorables or Documents
                    // and we should drop these back into the DocumentPane if they are available
                    Type[] allowedDropTypes = new[] { typeof(LayoutDocument), typeof(LayoutAnchorable) };

                    int i = tabIndex == -1 ? 0 : tabIndex;
                    foreach (LayoutContent? anchorableToImport in
                        layoutDocumentPaneGroup.Descendents().OfType<LayoutContent>()
                            .Where(item => allowedDropTypes.Any(dropType => dropType.IsInstanceOfType(item))).ToArray())
                    {
                        paneModel.Children.Insert(i, anchorableToImport);
                        i++;
                    }
                }

                break;
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

        switch (Type)
        {
            case DropTargetType.DocumentPaneDockBottom:
                {
                    LayoutDocumentPaneGroup? parentModel = targetModel.Parent as LayoutDocumentPaneGroup;
                    LayoutDocumentPane newLayoutDocumentPane = new();

                    if (parentModel == null)
                    {
                        LayoutDocumentPaneGroup newParentModel = new()
                        {
                            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical
                        };
                        parentContainer.ReplaceChild(targetModel, newParentModel);
                        newParentModel.Children.Add(targetModel);
                        newParentModel.Children.Add(newLayoutDocumentPane);
                    }
                    else
                    {
                        if (!manager.AllowMixedOrientation || parentModel.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical)
                        {
                            parentModel.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                            int targetPaneIndex = parentModel.IndexOfChild(targetModel);
                            parentModel.Children.Insert(targetPaneIndex + 1, newLayoutDocumentPane);
                        }
                        else
                        {
                            LayoutDocumentPaneGroup newChildGroup = new();
                            newChildGroup.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                            parentModel.ReplaceChild(targetModel, newChildGroup);
                            newChildGroup.Children.Add(targetModel);
                            newChildGroup.Children.Add(newLayoutDocumentPane);
                        }
                    }

                    foreach (LayoutAnchorable? cntToTransfer in floatingPanel.Descendents().OfType<LayoutAnchorable>().ToArray())
                    {
                        newLayoutDocumentPane.Children.Add(cntToTransfer);
                    }
                }

                break;

            case DropTargetType.DocumentPaneDockTop:
                {
                    LayoutDocumentPaneGroup? parentModel = targetModel.Parent as LayoutDocumentPaneGroup;
                    LayoutDocumentPane newLayoutDocumentPane = new();

                    if (parentModel == null)
                    {
                        LayoutDocumentPaneGroup newParentModel = new()
                        {
                            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical
                        };
                        parentContainer.ReplaceChild(targetModel, newParentModel);
                        newParentModel.Children.Add(newLayoutDocumentPane);
                        newParentModel.Children.Add(targetModel);
                    }
                    else
                    {
                        if (!manager.AllowMixedOrientation || parentModel.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical)
                        {
                            parentModel.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                            int targetPaneIndex = parentModel.IndexOfChild(targetModel);
                            parentModel.Children.Insert(targetPaneIndex, newLayoutDocumentPane);
                        }
                        else
                        {
                            LayoutDocumentPaneGroup newChildGroup = new();
                            newChildGroup.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                            parentModel.ReplaceChild(targetModel, newChildGroup);
                            newChildGroup.Children.Add(newLayoutDocumentPane);
                            newChildGroup.Children.Add(targetModel);
                        }
                    }

                    foreach (LayoutAnchorable? cntToTransfer in floatingPanel.Descendents().OfType<LayoutAnchorable>().ToArray())
                    {
                        newLayoutDocumentPane.Children.Add(cntToTransfer);
                    }
                }

                break;

            case DropTargetType.DocumentPaneDockLeft:
                {
                    LayoutDocumentPaneGroup? parentModel = targetModel.Parent as LayoutDocumentPaneGroup;
                    LayoutDocumentPane newLayoutDocumentPane = new();

                    if (parentModel == null)
                    {
                        LayoutDocumentPaneGroup newParentModel = new()
                        {
                            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal
                        };
                        parentContainer.ReplaceChild(targetModel, newParentModel);
                        newParentModel.Children.Add(newLayoutDocumentPane);
                        newParentModel.Children.Add(targetModel);
                    }
                    else
                    {
                        if (!manager.AllowMixedOrientation || parentModel.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Horizontal)
                        {
                            parentModel.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                            int targetPaneIndex = parentModel.IndexOfChild(targetModel);
                            parentModel.Children.Insert(targetPaneIndex, newLayoutDocumentPane);
                        }
                        else
                        {
                            LayoutDocumentPaneGroup newChildGroup = new();
                            newChildGroup.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                            parentModel.ReplaceChild(targetModel, newChildGroup);
                            newChildGroup.Children.Add(newLayoutDocumentPane);
                            newChildGroup.Children.Add(targetModel);
                        }
                    }

                    foreach (LayoutAnchorable? cntToTransfer in floatingPanel.Descendents().OfType<LayoutAnchorable>().ToArray())
                    {
                        newLayoutDocumentPane.Children.Add(cntToTransfer);
                    }
                }

                break;

            case DropTargetType.DocumentPaneDockRight:
                {
                    LayoutDocumentPaneGroup? parentModel = targetModel.Parent as LayoutDocumentPaneGroup;
                    LayoutDocumentPane newLayoutDocumentPane = new();

                    if (parentModel == null)
                    {
                        LayoutDocumentPaneGroup newParentModel = new()
                        {
                            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal
                        };
                        parentContainer.ReplaceChild(targetModel, newParentModel);
                        newParentModel.Children.Add(targetModel);
                        newParentModel.Children.Add(newLayoutDocumentPane);
                    }
                    else
                    {
                        if (!manager.AllowMixedOrientation || parentModel.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Horizontal)
                        {
                            parentModel.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                            int targetPaneIndex = parentModel.IndexOfChild(targetModel);
                            parentModel.Children.Insert(targetPaneIndex + 1, newLayoutDocumentPane);
                        }
                        else
                        {
                            LayoutDocumentPaneGroup newChildGroup = new();
                            newChildGroup.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                            parentModel.ReplaceChild(targetModel, newChildGroup);
                            newChildGroup.Children.Add(targetModel);
                            newChildGroup.Children.Add(newLayoutDocumentPane);
                        }
                    }

                    foreach (LayoutAnchorable? cntToTransfer in floatingPanel.Descendents().OfType<LayoutAnchorable>().ToArray())
                    {
                        newLayoutDocumentPane.Children.Add(cntToTransfer);
                    }
                }

                break;

            case DropTargetType.DocumentPaneDockInside:
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

                        // BD: 17.08.2020 Remove that bodge and handle CanClose=false && CanHide=true in XAML
                        // anchorableToImport.SetCanCloseInternal(true);
                        paneModel.Children.Insert(i, anchorableToImport);
                        i++;
                        anchorableToActivate = anchorableToImport;
                    }

                    if (anchorableToActivate is not null)
                    {
                        anchorableToActivate.IsActive = true;
                    }
                }

                break;
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
