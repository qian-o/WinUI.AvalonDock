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
        if (dropAreas != null)
        {
            return dropAreas;
        }

        dropAreas = new List<IDropArea>();
        if (draggingWindow.Model is LayoutDocumentFloatingWindow)
        {
            return dropAreas;
        }

        // A window whose content has already been released offers nothing to drop onto (issue #587).
        UIElement? rootVisual = (Content as FloatingWindowContentHost)?.RootVisual;
        if (rootVisual == null)
        {
            return dropAreas;
        }

        foreach (LayoutAnchorablePaneControl areaHost in rootVisual.FindVisualChildren<LayoutAnchorablePaneControl>())
        {
            dropAreas.Add(new DropArea<LayoutAnchorablePaneControl>(areaHost, DropAreaType.AnchorablePane));
        }

        foreach (LayoutDocumentPaneControl areaHost in rootVisual.FindVisualChildren<LayoutDocumentPaneControl>())
        {
            dropAreas.Add(new DropArea<LayoutDocumentPaneControl>(areaHost, DropAreaType.DocumentPane));
        }

        return dropAreas;
    }
}
