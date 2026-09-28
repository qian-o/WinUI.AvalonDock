// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager
{
    private List<IDropArea>? areas;
    internal void InvalidateDropAreas() => areas = null;

    IEnumerable<IDropArea> IOverlayWindowHost.GetDropAreas(LayoutFloatingWindowControl draggingWindow)
    {
        List<IDropArea> currentAreas = new();
        bool isDraggingDocuments = draggingWindow.Model is LayoutDocumentFloatingWindow;
        if (!isDraggingDocuments && IsLoaded)
        {
            currentAreas.Add(DropAreaCache.Reuse(areas, this, DropAreaType.DockingManager));
            foreach (LayoutAnchorablePaneControl areaHost in this.FindVisualChildren<LayoutAnchorablePaneControl>())
            {
                if (DropAreaCache.IsConnected(areaHost, this) && areaHost.Model.Descendents().Any())
                {
                    currentAreas.Add(DropAreaCache.Reuse(areas, areaHost, DropAreaType.AnchorablePane));
                }
            }
        }

        // Dock only documents and tools in DocumentPane if configuration does allow that
        if (FloatingDropAreaRules.CanDockAsDocument(draggingWindow))
        {
            foreach (LayoutDocumentPaneControl areaHost in this.FindVisualChildren<LayoutDocumentPaneControl>())
            {
                if (DropAreaCache.IsConnected(areaHost, this))
                {
                    currentAreas.Add(DropAreaCache.Reuse(areas, areaHost, DropAreaType.DocumentPane));
                }
            }

            foreach (LayoutDocumentPaneGroupControl areaHost in this.FindVisualChildren<LayoutDocumentPaneGroupControl>())
            {
                LayoutDocumentPaneGroup documentGroupModel = (LayoutDocumentPaneGroup)areaHost.Model;
                if (DropAreaCache.IsConnected(areaHost, this) && !documentGroupModel.Children.Any(c => c.IsVisible))
                {
                    currentAreas.Add(DropAreaCache.Reuse(areas, areaHost, DropAreaType.DocumentPaneGroup));
                }
            }
        }

        areas = currentAreas;
        return areas;
    }
}

internal static class DropAreaCache
{
    // Layout tree changes can replace views while an overlay remains open. Keep
    // the identity of live regions, but never carry a removed view into the next frame.
    internal static DropArea<T> Reuse<T>(List<IDropArea>? previousAreas, T element, DropAreaType type) where T : FrameworkElement =>
        previousAreas?.OfType<DropArea<T>>().FirstOrDefault(area => ReferenceEquals(area.AreaElement, element) && area.Type == type)
        ?? new DropArea<T>(element, type);

    internal static bool IsConnected(FrameworkElement element, DockingManager? manager, ILayoutElement? floatingHost = null) =>
        manager != null && element.IsLoaded && element is ILayoutControl { Model: { } model }
        && ReferenceEquals(model.Root?.Manager, manager)
        && ReferenceEquals(model.FindParent<LayoutFloatingWindow>(), floatingHost)
        && PlatformServices.Coordinates.TryGetScreenBounds(element, out Rect bounds)
        && bounds.Width > 0 && bounds.Height > 0;
}
