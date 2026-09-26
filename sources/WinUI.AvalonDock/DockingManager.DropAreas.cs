// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace AvalonDock;

public partial class DockingManager
{
    private List<IDropArea>? areas;
    internal void InvalidateDropAreas() => areas = null;

    IEnumerable<IDropArea> IOverlayWindowHost.GetDropAreas(LayoutFloatingWindowControl draggingWindow)
    {
        if (areas != null)
        {
            return areas;
        }

        areas = new List<IDropArea>();
        bool isDraggingDocuments = draggingWindow.Model is LayoutDocumentFloatingWindow;
        if (!isDraggingDocuments)
        {
            areas.Add(new DropArea<DockingManager>(this, DropAreaType.DockingManager));
            foreach (LayoutAnchorablePaneControl areaHost in this.FindVisualChildren<LayoutAnchorablePaneControl>())
            {
                if (areaHost.Model.Descendents().Any())
                {
                    areas.Add(new DropArea<LayoutAnchorablePaneControl>(areaHost, DropAreaType.AnchorablePane));
                }
            }
        }

        // Dock only documents and tools in DocumentPane if configuration does allow that
        if (FloatingDropAreaRules.CanDockAsDocument(draggingWindow))
        {
            foreach (LayoutDocumentPaneControl areaHost in this.FindVisualChildren<LayoutDocumentPaneControl>())
            {
                areas.Add(new DropArea<LayoutDocumentPaneControl>(areaHost, DropAreaType.DocumentPane));
            }

            foreach (LayoutDocumentPaneGroupControl areaHost in this.FindVisualChildren<LayoutDocumentPaneGroupControl>())
            {
                LayoutDocumentPaneGroup documentGroupModel = (LayoutDocumentPaneGroup)areaHost.Model;
                if (!documentGroupModel.Children.Any(c => c.IsVisible))
                {
                    areas.Add(new DropArea<LayoutDocumentPaneGroupControl>(areaHost, DropAreaType.DocumentPaneGroup));
                }
            }
        }

        return areas;
    }
}
