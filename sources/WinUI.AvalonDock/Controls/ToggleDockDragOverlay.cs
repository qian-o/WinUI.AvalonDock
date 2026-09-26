// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/ToggleDockButtonBar.cs.
using System.Runtime.InteropServices.WindowsRuntime;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;

namespace AvalonDock.Controls;

internal sealed class ToggleDockDragOverlay : IDisposable
{
    private readonly ToggleDockingManager manager;
    private readonly Window window = new();
    private readonly Canvas canvas = new();
    private readonly Grid renderRoot = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly IOverlayWindowSurface surface;
    private readonly List<Zone> zones = [];
    private readonly Image ghost = new() { Width = 24, Height = 24, Opacity = 0.6, IsHitTestVisible = false };
    private Rect bounds;
    private double scale;
    private bool disposed;
    private bool rendering;
    private bool queued;
    private long revision;
    private Point lastPointer;
    private bool updated;
    private bool failed;
    internal Exception? Failure
    {
        get; private set;
    }
    internal int PresentedFrames
    {
        get; private set;
    }
    internal ToggleDockDragOverlay(ToggleDockingManager manager, FrameworkElement origin)
    {
        this.manager = manager;
        renderRoot.Children.Add(canvas);
        window.Content = renderRoot;
        try
        {
            surface = PlatformServices.CreateOverlayWindowSurface(window, manager);
        }
        catch { window.Close(); throw; }
        _ = CaptureGhost(origin);
    }
    private async Task CaptureGhost(FrameworkElement origin)
    {
        try
        {
            RenderTargetBitmap image = new();
            await image.RenderAsync(origin);
            if (disposed)
            {
                return;
            }

            ghost.Source = image;
            ghost.Width = origin is ToggleDockButton ? origin.ActualWidth : 24;
            ghost.Height = origin is ToggleDockButton ? origin.ActualHeight : 24;
            Invalidate();
        }
        catch (Exception exception)
        {
            if (!disposed)
            {
                Failure = exception;
            }
        }
    }
    internal void Update(Point pointer)
    {
        if (disposed || failed || !PlatformServices.Coordinates.TryGetScreenBounds(manager, out Rect nextBounds))
        {
            return;
        }

        Zone[] previous = zones.ToArray();
        bool samePosition = updated && pointer == lastPointer && bounds == nextBounds;
        bounds = nextBounds;
        lastPointer = pointer;
        updated = true;
        scale = manager.XamlRoot.RasterizationScale;
        BuildZones();
        if (samePosition && previous.SequenceEqual(zones))
        {
            return;
        }

        canvas.Width = bounds.Width / scale;
        canvas.Height = bounds.Height / scale;
        renderRoot.Width = canvas.Width;
        renderRoot.Height = canvas.Height;
        surface.Show(bounds);
        Zone? selected = HitZone(pointer);
        Color accent = manager.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? resource) && resource is SolidColorBrush local ? local.Color
            : ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;
        canvas.Children.Clear();
        foreach (Zone zone in zones)
        {
            if (zone.Label == null && !ReferenceEquals(zone, selected))
            {
                continue;
            }

            Rect rectangle = zone.Bounds;
            Border border = new()
            {
                Width = rectangle.Width / scale,
                Height = rectangle.Height / scale,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(ReferenceEquals(zone, selected) ? (byte)96 : (byte)48, accent.R, accent.G, accent.B)),
                BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(128, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1.5)
            };
            Canvas.SetLeft(border, (rectangle.X - bounds.X) / scale);
            Canvas.SetTop(border, (rectangle.Y - bounds.Y) / scale);
            if (zone.Label != null)
            {
                border.Child = new TextBlock { Text = zone.Label, FontSize = 14, Foreground = new SolidColorBrush(accent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }

            canvas.Children.Add(border);
            if (zone.Line is { } y)
            {
                Border line = new()
                {
                    Height = 3,
                    Width = Math.Max(0, rectangle.Width / scale - 4),
                    Background = new SolidColorBrush(accent),
                    CornerRadius = new CornerRadius(2)
                };
                Canvas.SetLeft(line, (rectangle.X - bounds.X) / scale + 2);
                Canvas.SetTop(line, (y - bounds.Y) / scale);
                canvas.Children.Add(line);
            }
        }
        canvas.Children.Add(ghost);
        Canvas.SetLeft(ghost, (pointer.X - bounds.X) / scale + 12);
        Canvas.SetTop(ghost, (pointer.Y - bounds.Y) / scale - ghost.Height / 2);
        Invalidate();
    }
    internal DockZone? Hit(Point point) => failed || disposed ? null : HitZone(point)?.Target;
    private Zone? HitZone(Point point) => zones.LastOrDefault(zone => zone.Bounds.Contains(point));
    private void BuildZones()
    {
        zones.Clear();
        double Width(FrameworkElement? element) => element != null && IsEffectivelyVisible(element)
            && PlatformServices.Coordinates.TryGetScreenBounds(element, out Rect rectangle) ? rectangle.Width : 0;
        double leftBar = Width(manager.injectedLeftDockPanel);
        double rightBar = Width(manager.rightTopBar);
        double x = bounds.X + leftBar;
        double width = bounds.Width - leftBar - rightBar;
        double height = bounds.Height;
        if (width < 50 * scale || height < 50 * scale)
        {
            return;
        }

        double Extent(AnchorSide side, double fallback, bool horizontal)
        {
            foreach (LayoutAnchorablePaneControl pane in ToggleDockingManager.Visuals<LayoutAnchorablePaneControl>(manager))
            {
                if (pane.Model is LayoutAnchorablePane model && model.GetSide() == side && model.Children.Any(tool => !tool.IsAutoHidden)
                    && PlatformServices.Coordinates.TryGetScreenBounds(pane, out Rect measured) && (horizontal ? measured.Width : measured.Height) > 10 * scale)
                {
                    return horizontal ? measured.Width : measured.Height;
                }
            }

            return fallback;
        }
        double left = Extent(AnchorSide.Left, width * .25, true);
        double right = Extent(AnchorSide.Right, width * .25, true);
        double bottom = Extent(AnchorSide.Bottom, height * .25, false);
        double half = (height - bottom) / 2;
        zones.Add(new(new Rect(x, bounds.Y, left, half), DockZone.LeftTop, "Left Top"));
        zones.Add(new(new Rect(x, bounds.Y + half, left, half), DockZone.LeftBottom, "Left Bottom"));
        zones.Add(new(new Rect(x + width - right, bounds.Y, right, half), DockZone.RightTop, "Right Top"));
        zones.Add(new(new Rect(x + width - right, bounds.Y + half, right, half), DockZone.RightBottom, "Right Bottom"));
        zones.Add(new(new Rect(x, bounds.Y + 2 * half, width / 2, bottom), DockZone.BottomLeft, "Bottom Left"));
        zones.Add(new(new Rect(x + width / 2, bounds.Y + 2 * half, width / 2, bottom), DockZone.BottomRight, "Bottom Right"));
        AddSide(manager.injectedLeftDockPanel, manager.leftSeparator, manager.bottomLeftBar, DockZone.LeftTop, DockZone.LeftBottom);
        AddSide(manager.injectedRightDockPanel, manager.rightSeparator, manager.bottomRightBar, DockZone.RightTop, DockZone.RightBottom);
        foreach (ToggleDockButtonBar? bar in new[] { manager.bottomLeftBar, manager.bottomRightBar })
        {
            if (bar != null && IsEffectivelyVisible(bar) && PlatformServices.Coordinates.TryGetScreenBounds(bar, out Rect barBounds))
            {
                zones.Add(new(new Rect(barBounds.X, barBounds.Y, Math.Max(barBounds.Width, 20 * scale), Math.Max(barBounds.Height, 20 * scale)), bar.Zone, null));
            }
        }
    }
    private void AddSide(FrameworkElement? panel, FrameworkElement? separator, ToggleDockButtonBar? bottom, DockZone topZone, DockZone lowerZone)
    {
        if (panel == null || !IsEffectivelyVisible(panel) || !PlatformServices.Coordinates.TryGetScreenBounds(panel, out Rect area))
        {
            return;
        }

        area = new Rect(area.X, area.Y, Math.Max(area.Width, 20 * scale), Math.Max(area.Height, 20 * scale));
        double bottomBarHeight = bottom != null && IsEffectivelyVisible(bottom)
            && PlatformServices.Coordinates.TryGetScreenBounds(bottom, out Rect bottomBounds) ? bottomBounds.Height : 0;
        double usableHeight = area.Height - bottomBarHeight;
        if (usableHeight < 10 * scale)
        {
            return;
        }

        double y = separator != null && IsEffectivelyVisible(separator)
            && PlatformServices.Coordinates.TryGetScreenBounds(separator, out Rect split)
            ? split.Y + split.Height / 2 : area.Y + usableHeight / 2;
        zones.Add(new(new Rect(area.X, area.Y, area.Width, y - area.Y), topZone, null, y));
        zones.Add(new(new Rect(area.X, y, area.Width, area.Y + usableHeight - y), lowerZone, null, y));
    }
    private static bool IsEffectivelyVisible(FrameworkElement element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement visual && visual.Visibility != Visibility.Visible)
            {
                return false;
            }
        }

        return true;
    }
    private void Invalidate()
    {
        revision++;
        if (!disposed && !rendering && !queued)
        {
            queued = canvas.DispatcherQueue.TryEnqueue(Render);
        }
    }
    private async void Render()
    {
        queued = false;
        if (disposed || rendering || !surface.IsVisible)
        {
            return;
        }

        rendering = true;
        long current = revision;
        try
        {
            renderRoot.Measure(new Size(canvas.Width, canvas.Height));
            renderRoot.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
            renderRoot.UpdateLayout();
            // The native root applies its rasterization scale. Explicit physical dimensions
            // here would apply that scale twice and move the visible hints away from hit bounds.
            RenderTargetBitmap bitmap = new();
            await bitmap.RenderAsync(renderRoot);
            IBuffer pixels = await bitmap.GetPixelsAsync();
            if (!disposed && current == revision)
            {
                surface.Present(pixels.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight);
                PresentedFrames++;
            }
        }
        catch (Exception exception) { if (!disposed) { Failure = exception; failed = true; zones.Clear(); surface.Hide(); } }
        finally
        {
            rendering = false;
            if (!disposed && current != revision)
            {
                Invalidate();
            }
        }
    }
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        surface.Dispose();
        window.Content = null;
        window.Close();
    }
    private sealed record Zone(Rect Bounds, DockZone Target, string? Label, double? Line = null);
}
