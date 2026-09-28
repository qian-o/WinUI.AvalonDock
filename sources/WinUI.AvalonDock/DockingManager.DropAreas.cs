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
    private readonly List<IDropArea> documentAreaBuffer = [];
    private readonly List<IDropArea> emptyGroupAreaBuffer = [];
    internal void InvalidateDropAreas()
    {
        areas = null;
        documentAreaBuffer.Clear();
        emptyGroupAreaBuffer.Clear();
    }

    IEnumerable<IDropArea> IOverlayWindowHost.GetDropAreas(LayoutFloatingWindowControl draggingWindow)
    {
        List<IDropArea> currentAreas = new();
        documentAreaBuffer.Clear();
        emptyGroupAreaBuffer.Clear();
        bool isDraggingDocuments = draggingWindow.Model is LayoutDocumentFloatingWindow;
        bool canDockAsDocument = FloatingDropAreaRules.CanDockAsDocument(draggingWindow);
        if (!isDraggingDocuments && IsLoaded)
        {
            currentAreas.Add(DropAreaCache.Reuse(areas, this, DropAreaType.DockingManager));
        }

        // 拖动中窗格视图可能被替换；一次遍历当前可视树，保留工具、文档和组的原有顺序。
        foreach (FrameworkElement element in this.FindVisualChildren<FrameworkElement>())
        {
            switch (element)
            {
                case LayoutAnchorablePaneControl toolPane when !isDraggingDocuments
                    && DropAreaCache.IsConnected(toolPane, this) && toolPane.Model.Descendents().Any():
                    currentAreas.Add(DropAreaCache.Reuse(areas, toolPane, DropAreaType.AnchorablePane));
                    break;
                case LayoutDocumentPaneControl documentPane when canDockAsDocument
                    && DropAreaCache.IsConnected(documentPane, this):
                    documentAreaBuffer.Add(DropAreaCache.Reuse(areas, documentPane, DropAreaType.DocumentPane));
                    break;
                case LayoutDocumentPaneGroupControl documentGroup when canDockAsDocument
                    && DropAreaCache.IsConnected(documentGroup, this)
                    && documentGroup.Model is LayoutDocumentPaneGroup model && !model.Children.Any(c => c.IsVisible):
                    emptyGroupAreaBuffer.Add(DropAreaCache.Reuse(areas, documentGroup, DropAreaType.DocumentPaneGroup));
                    break;
            }
        }

        currentAreas.AddRange(documentAreaBuffer);
        currentAreas.AddRange(emptyGroupAreaBuffer);
        areas = currentAreas;
        return areas;
    }
}

internal static class DropAreaCache
{
    // Layout tree changes can replace views while an overlay remains open. Keep
    // the identity of live regions, but never carry a removed view into the next frame.
    internal static DropArea<T> Reuse<T>(List<IDropArea>? previousAreas, T element, DropAreaType type) where T : FrameworkElement
    {
        if (previousAreas != null)
        {
            foreach (IDropArea previous in previousAreas)
            {
                if (previous is DropArea<T> area && ReferenceEquals(area.AreaElement, element) && area.Type == type)
                {
                    return area;
                }
            }
        }

        return new DropArea<T>(element, type);
    }

    internal static bool IsConnected(FrameworkElement element, DockingManager? manager, ILayoutElement? floatingHost = null) =>
        manager != null && element.IsLoaded && element is ILayoutControl { Model: { } model }
        && ReferenceEquals(model.Root?.Manager, manager)
        && ReferenceEquals(model.FindParent<LayoutFloatingWindow>(), floatingHost)
        && PlatformServices.Coordinates.TryGetScreenBounds(element, out Rect bounds)
        && bounds.Width > 0 && bounds.Height > 0;
}
