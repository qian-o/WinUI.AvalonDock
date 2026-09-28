// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/LayoutDocumentFloatingWindowControl.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

public partial class LayoutDocumentFloatingWindowControl
{
    private List<IDropArea>? dropAreas;
    private readonly List<IDropArea> documentAreaBuffer = [];
    internal void InvalidateDropAreas()
    {
        dropAreas = null;
        documentAreaBuffer.Clear();
    }

    public IEnumerable<IDropArea> GetDropAreas(LayoutFloatingWindowControl draggingWindow)
    {
        List<IDropArea> currentAreas = new();
        documentAreaBuffer.Clear();
        bool dockAsDocument = FloatingDropAreaRules.CanDockAsDocument(draggingWindow);

        // A window whose content has already been released offers nothing to drop onto (issue #587).
        UIElement? rootVisual = (Content as FloatingWindowContentHost)?.RootVisual;
        if (rootVisual == null)
        {
            return dropAreas = currentAreas;
        }

        foreach (FrameworkElement element in rootVisual.FindVisualChildren<FrameworkElement>())
        {
            switch (element)
            {
                case LayoutAnchorablePaneControl toolPane when DropAreaCache.IsConnected(toolPane, Manager, Model):
                    currentAreas.Add(DropAreaCache.Reuse(dropAreas, toolPane, DropAreaType.AnchorablePane));
                    break;
                case LayoutDocumentPaneControl documentPane when dockAsDocument
                    && DropAreaCache.IsConnected(documentPane, Manager, Model):
                    documentAreaBuffer.Add(DropAreaCache.Reuse(dropAreas, documentPane, DropAreaType.DocumentPane));
                    break;
            }
        }

        currentAreas.AddRange(documentAreaBuffer);

        dropAreas = currentAreas;
        return dropAreas;
    }
}
