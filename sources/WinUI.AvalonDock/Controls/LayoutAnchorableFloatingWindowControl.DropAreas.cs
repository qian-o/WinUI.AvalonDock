// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/LayoutAnchorableFloatingWindowControl.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using AvalonDock.Controls;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

public partial class LayoutAnchorableFloatingWindowControl
{
    private List<IDropArea>? dropAreas;
    internal void InvalidateDropAreas() => dropAreas = null;

    IEnumerable<IDropArea> IOverlayWindowHost.GetDropAreas(LayoutFloatingWindowControl draggingWindow)
    {
        List<IDropArea> currentAreas = new();
        if (draggingWindow.Model is LayoutDocumentFloatingWindow)
        {
            return dropAreas = currentAreas;
        }

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

        if (FloatingDropAreaRules.CanDockAsDocument(draggingWindow))
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
