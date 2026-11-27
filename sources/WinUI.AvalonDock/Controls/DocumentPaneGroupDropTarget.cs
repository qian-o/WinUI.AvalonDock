// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DocumentPaneGroupDropTarget.cs
using System.Linq;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

internal class DocumentPaneGroupDropTarget : DropTarget<LayoutDocumentPaneGroupControl>
{
    private LayoutDocumentPaneGroupControl targetPane;

    internal DocumentPaneGroupDropTarget(
        LayoutDocumentPaneGroupControl paneControl,
        Rect detectionRect,
        DropTargetType type)
        : base(paneControl, detectionRect, type)
    {
        targetPane = paneControl;
    }

    /// <inheritdoc/>
    protected override void Drop(LayoutDocumentFloatingWindow floatingWindow)
    {
        if (targetPane.Model is not LayoutDocumentPaneGroup paneModel || floatingWindow.RootPanel is not { } sourceModel)
        {
            return;
        }

        switch (Type)
        {
            case DropTargetType.DocumentPaneGroupDockInside:
                {

                    paneModel.Children.Insert(0, sourceModel);
                }

                break;
        }

        base.Drop(floatingWindow);
    }

    /// <inheritdoc/>
    protected override void Drop(LayoutAnchorableFloatingWindow floatingWindow)
    {
        if (targetPane.Model is not LayoutDocumentPaneGroup paneGroupModel
            || paneGroupModel.Children.FirstOrDefault() is not LayoutDocumentPane paneModel
            || floatingWindow.RootPanel is not { } layoutAnchorablePaneGroup)
        {
            return;
        }

        switch (Type)
        {
            case DropTargetType.DocumentPaneGroupDockInside:
                {

                    int i = 0;
                    foreach (LayoutAnchorable? anchorableToImport in layoutAnchorablePaneGroup.Descendents().OfType<LayoutAnchorable>().ToArray())
                    {
                        paneModel.Children.Insert(i, anchorableToImport);
                        i++;
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
            case DropTargetType.DocumentPaneGroupDockInside:
                {
                    Rect targetScreenRect = overlayWindow.GetPreviewBounds(TargetElement);

                    return new RectangleGeometry { Rect = targetScreenRect };
                }
        }

        return null;
    }
}
