// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DropArea.cs
using System.ComponentModel;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock.Controls;

/// <summary>Captures a docking region for a framework element.</summary>
/// <typeparam name="T">The framework element that presents the region.</typeparam>
public class DropArea<T> : IDropArea where T : FrameworkElement
{
    private readonly Rect capturedDetectionRect;

    internal DropArea(T areaElement, DropAreaType type)
    {
        AreaElement = areaElement;
        Type = type;
        // Retain the original fallback for an area not yet connected to a host.
        capturedDetectionRect = GetCurrentDetectionRect() ?? new Rect(0, 0, areaElement.ActualWidth, areaElement.ActualHeight);
    }

    /// <inheritdoc/>
    public Rect DetectionRect => GetCurrentDetectionRect() ?? capturedDetectionRect;

    /// <inheritdoc/>
    public DropAreaType Type
    {
        get;
    }

    /// <inheritdoc/>
    public Point TransformToDeviceDPI(Point dragPosition) => AreaElement.XamlRoot is null
        ? default
        : PlatformServices.Coordinates.ToHostLogical(AreaElement, dragPosition);

    /// <summary>Gets the framework element that implements this region.</summary>
    [Bindable(false)]
    [Description("Gets the FrameworkElement that implements a drop target for a drag & drop (dock) operation.")]
    [Category("Other")]
    public T AreaElement
    {
        get;
    }

    private Rect? GetCurrentDetectionRect()
    {
        if (!AreaElement.IsLoaded || !PlatformServices.Coordinates.TryGetScreenBounds(AreaElement, out Rect physicalBounds))
        {
            return null;
        }

        Point topLeft = TransformToDeviceDPI(new Point(physicalBounds.Left, physicalBounds.Top));
        Point bottomRight = TransformToDeviceDPI(new Point(physicalBounds.Right, physicalBounds.Bottom));
        return new Rect(topLeft, bottomRight);
    }
}

internal static class FloatingDropAreaRules
{
    internal static bool CanDockAsDocument(LayoutFloatingWindowControl draggingWindow) =>
        CanDockAsDocument(draggingWindow.Model);

    internal static bool CanDockAsDocument(ILayoutElement? draggingModel)
    {
        if (draggingModel is not LayoutAnchorableFloatingWindow tools)
        {
            return true;
        }

        LayoutAnchorable[] anchorables = tools.RootPanel?.Descendents().OfType<LayoutAnchorable>().ToArray() ?? [];
        return anchorables.Length > 0 && anchorables.All(item => item.CanDockAsTabbedDocument);
    }

    internal static bool IsDocumentDropTarget(DropTargetType type) => type is
        DropTargetType.DocumentPaneDockLeft or
        DropTargetType.DocumentPaneDockTop or
        DropTargetType.DocumentPaneDockRight or
        DropTargetType.DocumentPaneDockBottom or
        DropTargetType.DocumentPaneDockInside or
        DropTargetType.DocumentPaneGroupDockInside;
}
