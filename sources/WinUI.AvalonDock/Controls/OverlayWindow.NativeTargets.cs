using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

public partial class OverlayWindow
{
    private static void SetNativeTargetVisibility(FrameworkElement? element, Visibility visibility)
    {
        if (element != null)
        {
            element.Visibility = visibility;
        }
    }

    private static bool IsNativeTargetVisible(FrameworkElement? element)
    {
        if (element == null || !element.IsLoaded)
        {
            return false;
        }

        for (DependencyObject? current = element; current is UIElement node; current = VisualTreeHelper.GetParent(current))
        {
            if (node.Visibility != Visibility.Visible || node.Opacity == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<T> NativeHeaders<T>(FrameworkElement pane) where T : FrameworkElement =>
        pane.FindVisualChildren<T>().Where(header => header.FindVisualAncestor<TabViewItem>() is { IsHitTestVisible: true, Opacity: > 0 } && IsNativeTargetVisible(header));

    private Rect GetNativeScreenArea(FrameworkElement? element)
    {
        if (element == null || !IsNativeTargetVisible(element))
        {
            return Rect.Empty;
        }

        if (element is LayoutDocumentTabItem or LayoutAnchorableTabItem)
        {
            element = element.FindVisualAncestor<TabViewItem>() ?? element;
        }

        if (ReferenceEquals(element.XamlRoot, view.XamlRoot) && canvas != null)
        {
            Rect rectangle = element.TransformToVisual(canvas).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            double scale = destination.XamlRoot.RasterizationScale;
            return new Rect(bounds.Left + rectangle.Left * scale, bounds.Top + rectangle.Top * scale, rectangle.Width * scale, rectangle.Height * scale);
        }
        return PlatformServices.Coordinates.TryGetScreenBounds(element, out Rect screen) ? screen : Rect.Empty;
    }

    private Rect GetNativeAreaBounds(IDropArea area)
    {
        // The area can resize during a drag as another pane leaves the layout.
        // DetectionRect also retains the initial bounds for a hidden empty pane.
        double scale = OverlayHost.Element(area)?.XamlRoot?.RasterizationScale ?? destination.XamlRoot.RasterizationScale;
        Rect rectangle = area.DetectionRect;
        return GetPreviewBounds(new Rect(rectangle.X * scale, rectangle.Y * scale, rectangle.Width * scale, rectangle.Height * scale));
    }

    private bool PrepareOriginalTargets()
    {
        if (closed || RenderFailure != null || !PlatformServices.Coordinates.TryGetScreenBounds(destination, out Rect newBounds))
        {
            return false;
        }

        bool changed = !bounds.Equals(newBounds) || !IsVisible || view.RequestedTheme != destination.ActualTheme;
        bounds = newBounds;
        view.RequestedTheme = destination.ActualTheme;
        double scale = destination.XamlRoot.RasterizationScale;
        view.Width = bounds.Width / scale;
        view.Height = bounds.Height / scale;
        ApplyThemeStyle();
        ApplyTemplate();
        if (canvas == null)
        {
            return false;
        }

        if (changed)
        {
            surface.Show(bounds);
        }

        foreach (Grid group in groups.Values)
        {
            group.Visibility = Visibility.Collapsed;
        }

        foreach (FrameworkElement part in TemplateParts())
        {
            part.Visibility = Visibility.Visible;
        }

        foreach (IDropArea area in visibleAreas.ToArray())
        {
            if (OverlayHost.Element(area) is { IsLoaded: true })
            {
                ApplyOriginalArea(area);
            }
        }

        view.Measure(new Size(view.Width, view.Height));
        view.Arrange(new Rect(0, 0, view.Width, view.Height));
        view.UpdateLayout();
        return true;
    }

    private DropTargetBase? InitializeOriginalTarget(IDropTarget target)
    {
        if (overlayHost is not { } host || floatingWindow?.Model is not LayoutFloatingWindow floatingModel)
        {
            return null;
        }

        (FrameworkElement? element, Rect rectangle) = target switch
        {
            DropTarget<DockingManager> item => ((FrameworkElement)item.TargetElement, item.DetectionRects[0]),
            DropTarget<LayoutDocumentPaneControl> item => ((FrameworkElement)item.TargetElement, item.DetectionRects[0]),
            DropTarget<LayoutAnchorablePaneControl> item => ((FrameworkElement)item.TargetElement, item.DetectionRects[0]),
            DropTarget<LayoutDocumentPaneGroupControl> item => ((FrameworkElement)item.TargetElement, item.DetectionRects[0]),
            _ => throw new InvalidOperationException("Original target has no native area element."),
        };
        ILayoutGroup? model = element is DockingManager manager ? manager.Layout.RootPanel : (element as ILayoutControl)?.Model as ILayoutGroup;
        if (model is null)
        {
            return null;
        }

        DropTargetBase original = (DropTargetBase)target;
        OverlayTarget native = new(model, element, original.Type, rectangle);
        return host.Manager.InitializeDropTarget(original, native, floatingModel, original.TabIndex);
    }
}
