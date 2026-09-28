// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/LayoutDocumentFloatingWindowControl.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

public partial class LayoutDocumentFloatingWindowControl
{
    private List<IDropArea>? dropAreas;
    internal void InvalidateDropAreas() => dropAreas = null;

    public IEnumerable<IDropArea> GetDropAreas(LayoutFloatingWindowControl draggingWindow)
    {
        List<IDropArea> currentAreas = new();
        bool dockAsDocument = FloatingDropAreaRules.CanDockAsDocument(draggingWindow);

        // A window whose content has already been released offers nothing to drop onto (issue #587).
        UIElement? rootVisual = (Content as FloatingWindowContentHost)?.RootVisual;
        if (rootVisual == null)
        {
            return dropAreas = currentAreas;
        }

        foreach (LayoutAnchorablePaneControl areaHost in rootVisual.FindVisualChildren<LayoutAnchorablePaneControl>())
        {
            if (DropAreaCache.IsConnected(areaHost, Manager, Model))
            {
                currentAreas.Add(DropAreaCache.Reuse(dropAreas, areaHost, DropAreaType.AnchorablePane));
            }
        }

        if (dockAsDocument)
        {
            foreach (LayoutDocumentPaneControl areaHost in rootVisual.FindVisualChildren<LayoutDocumentPaneControl>())
            {
                if (DropAreaCache.IsConnected(areaHost, Manager, Model))
                {
                    currentAreas.Add(DropAreaCache.Reuse(dropAreas, areaHost, DropAreaType.DocumentPane));
                }
            }
        }

        dropAreas = currentAreas;
        return dropAreas;
    }
}
