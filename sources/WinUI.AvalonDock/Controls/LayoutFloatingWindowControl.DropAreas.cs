// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/LayoutAnchorableFloatingWindowControl.cs and LayoutDocumentFloatingWindowControl.cs
using AvalonDock.Layout;
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private List<IDropArea>? dropAreas;
    private readonly List<IDropArea> documentAreaBuffer = [];

    internal void InvalidateDropAreas()
    {
        ClearDropAreaCache();
        documentAreaBuffer.Clear();
    }

    internal void ClearDropAreaCache() => dropAreas = null;

    internal IEnumerable<IDropArea> GetFloatingDropAreas(LayoutFloatingWindowControl draggingWindow, bool acceptsDocumentWindows)
    {
        List<IDropArea> currentAreas = new();
        documentAreaBuffer.Clear();
        if (!acceptsDocumentWindows && draggingWindow.Model is LayoutDocumentFloatingWindow)
        {
            return dropAreas = currentAreas;
        }

        // Preserve each host's validation order: document hosts inspect the drag model
        // first, while tool hosts can return immediately after their content is released.
        bool canDockAsDocument = acceptsDocumentWindows && FloatingDropAreaRules.CanDockAsDocument(draggingWindow);
        UIElement? rootVisual = (Content as FloatingWindowContentHost)?.RootVisual;
        if (rootVisual == null)
        {
            return dropAreas = currentAreas;
        }

        if (!acceptsDocumentWindows)
        {
            canDockAsDocument = FloatingDropAreaRules.CanDockAsDocument(draggingWindow);
        }

        foreach (FrameworkElement element in rootVisual.FindVisualChildren<FrameworkElement>())
        {
            switch (element)
            {
                case LayoutAnchorablePaneControl toolPane when DropAreaCache.IsConnected(toolPane, Manager, Model):
                    currentAreas.Add(DropAreaCache.Reuse(dropAreas, toolPane, DropAreaType.AnchorablePane));
                    break;
                case LayoutDocumentPaneControl documentPane when canDockAsDocument
                    && DropAreaCache.IsConnected(documentPane, Manager, Model):
                    documentAreaBuffer.Add(DropAreaCache.Reuse(dropAreas, documentPane, DropAreaType.DocumentPane));
                    break;
            }
        }

        currentAreas.AddRange(documentAreaBuffer);
        return dropAreas = currentAreas;
    }
}
