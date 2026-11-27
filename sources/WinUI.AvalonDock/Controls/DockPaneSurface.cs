// Ported from qian-o/AvalonDock.Themes.WPFUI e8a3da4e9761ff549e5a2afca2bdf891a681baf2,
// Controls/DockPaneSurface.cs. MIT, Copyright (c) 2024 qian-o.
// License: https://github.com/qian-o/AvalonDock.Themes.WPFUI/blob/e8a3da4e9761ff549e5a2afca2bdf891a681baf2/LICENSE
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace AvalonDock.Controls;

/// <summary>Draws the reference's single outline around the selected tab and pane content.</summary>
internal sealed class DockPaneSurface : Grid
{
    private readonly Path outline = new() { StrokeThickness = 1, Stretch = Stretch.None };
    private Rect contentBounds = Rect.Empty;
    private Rect tabBounds = Rect.Empty;
    private bool bottomTab;
    private TabControlEx? pane;
    private FrameworkElement? contentPanel;
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(DockPaneSurface), new PropertyMetadata(null, OnBrushChanged));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(DockPaneSurface), new PropertyMetadata(null, OnBrushChanged));
    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value);
    }
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value);
    }
    public DockPaneSurface()
    {
        CornerRadius = new CornerRadius(4);
        RegisterPropertyChangedCallback(CornerRadiusProperty, (_, _) => RenderOutline());
        IsHitTestVisible = false;
        Children.Add(outline);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    internal void Attach(TabControlEx owner, FrameworkElement? body)
    {
        pane = owner;
        contentPanel = body;
    }
    private static void OnBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        DockPaneSurface surface = (DockPaneSurface)sender;
        surface.outline.Fill = surface.Fill;
        surface.outline.Stroke = surface.Stroke;
    }
    private void OnLoaded(object? sender, RoutedEventArgs args)
    {
        LayoutUpdated -= OnLayoutUpdated;
        LayoutUpdated += OnLayoutUpdated;
        OnLayoutUpdated(this, EventArgs.Empty);
    }
    private void OnUnloaded(object? sender, RoutedEventArgs args)
    {
        LayoutUpdated -= OnLayoutUpdated;
        contentBounds = Rect.Empty;
        tabBounds = Rect.Empty;
        outline.Data = null;
    }
    private void OnLayoutUpdated(object? sender, object args)
    {
        Rect nextContent = Rect.Empty;
        Rect nextTab = Rect.Empty;
        bool nextBottom = false;
        if (pane is { IsLoaded: true } && pane.TabItems.Count > 0 && Visibility == Visibility.Visible
            && ActualWidth > 1 && ActualHeight > 1 && contentPanel is { IsLoaded: true, Visibility: Visibility.Visible })
        {
            nextContent = contentPanel.TransformToVisual(this).TransformBounds(new Rect(0, 0, contentPanel.ActualWidth, contentPanel.ActualHeight));
            nextContent.Intersect(new Rect(0, 0, ActualWidth, ActualHeight));
            if (pane.SelectedItem is TabViewItem { IsLoaded: true, Visibility: Visibility.Visible, Opacity: > 0 } tab && IsVisibleInPane(tab))
            {
                nextTab = tab.TransformToVisual(this).TransformBounds(new Rect(0, 0, tab.ActualWidth, tab.ActualHeight));
                nextBottom = nextTab.Top >= nextContent.Bottom - 1;
            }
        }
        if (nextContent != contentBounds || nextTab != tabBounds || nextBottom != bottomTab)
        {
            contentBounds = nextContent;
            tabBounds = nextTab;
            bottomTab = nextBottom;
            RenderOutline();
        }
    }
    private static bool IsVisibleInPane(FrameworkElement element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement { Visibility: Visibility.Collapsed })
            {
                return false;
            }
        }

        return true;
    }
    private void RenderOutline()
    {
        if (ActualWidth <= 1 || ActualHeight <= 1 || contentBounds.IsEmpty || contentBounds.Width <= 1 || contentBounds.Height <= 1)
        {
            outline.Data = null;
            return;
        }
        Rect body = contentBounds;
        Rect tab = tabBounds;
        if (bottomTab)
        {
            body.Y = ActualHeight - body.Bottom;
            if (!tab.IsEmpty)
            {
                tab.Y = ActualHeight - tab.Bottom;
            }
        }
        body = new Rect(body.X + .5, body.Y + .5, body.Width - 1, body.Height - 1);
        double radius = Math.Max(0, Math.Min(CornerRadius.TopLeft, Math.Min(body.Width, body.Height) / 2));
        Geometry geometry = CreateOutline(body, tab, radius);
        if (bottomTab)
        {
            geometry.Transform = new MatrixTransform { Matrix = new Matrix(1, 0, 0, -1, 0, ActualHeight) };
        }

        outline.Width = ActualWidth;
        outline.Height = ActualHeight;
        outline.Data = geometry;
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, ActualWidth, ActualHeight) };
    }
    private static Geometry CreateOutline(Rect body, Rect tab, double radius)
    {
        double tabLeft = tab.IsEmpty ? 0 : Math.Max(body.Left, tab.Left + 0.5);
        double tabRight = tab.IsEmpty ? 0 : Math.Min(body.Right - radius * 2, tab.Right - 0.5);
        double tabTop = tab.IsEmpty ? 0 : tab.Top + 0.5;
        bool hasTab = !tab.IsEmpty && tabRight - tabLeft >= radius * 2 && body.Top - tabTop >= radius * 2;
        bool touchesLeftEdge = hasTab && tabLeft - body.Left < radius * 2;
        PathFigure figure = new()
        {
            StartPoint = touchesLeftEdge ? new Point(tabLeft + radius, tabTop) : new Point(body.Left + radius, body.Top),
            IsFilled = true,
            IsClosed = true
        };
        void LineTo(double x, double y) => figure.Segments.Add(new LineSegment { Point = new Point(x, y) });
        void CurveTo(double cx, double cy, double x, double y) => figure.Segments.Add(new QuadraticBezierSegment
        {
            Point1 = new Point(cx, cy), Point2 = new Point(x, y)
        });

        if (hasTab)
        {
            if (!touchesLeftEdge)
            {
                LineTo(tabLeft - radius, body.Top);
                CurveTo(tabLeft, body.Top, tabLeft, body.Top - radius);
                LineTo(tabLeft, tabTop + radius);
                CurveTo(tabLeft, tabTop, tabLeft + radius, tabTop);
            }
            LineTo(tabRight - radius, tabTop);
            CurveTo(tabRight, tabTop, tabRight, tabTop + radius);
            LineTo(tabRight, body.Top - radius);
            CurveTo(tabRight, body.Top, tabRight + radius, body.Top);
        }
        LineTo(body.Right - radius, body.Top);
        CurveTo(body.Right, body.Top, body.Right, body.Top + radius);
        LineTo(body.Right, body.Bottom - radius);
        CurveTo(body.Right, body.Bottom, body.Right - radius, body.Bottom);
        LineTo(body.Left + radius, body.Bottom);
        CurveTo(body.Left, body.Bottom, body.Left, body.Bottom - radius);
        double leftRadius = touchesLeftEdge ? Math.Min(radius, tabLeft - body.Left) : radius;
        LineTo(body.Left, body.Top + leftRadius);
        CurveTo(body.Left, body.Top, body.Left + leftRadius, body.Top);
        if (touchesLeftEdge)
        {
            LineTo(tabLeft, body.Top);
            LineTo(tabLeft, tabTop + radius);
            CurveTo(tabLeft, tabTop, tabLeft + radius, tabTop);
        }
        return new PathGeometry { Figures = { figure } };
    }
}
