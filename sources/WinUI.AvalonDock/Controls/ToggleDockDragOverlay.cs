// Drag lifecycle adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/ToggleDockButtonBar.cs.
// Preview geometry and presentation ported from qian-o/AvalonDock.Themes.WPFUI ffff79a (MIT).
// License: https://github.com/qian-o/AvalonDock.Themes.WPFUI/blob/ffff79aefd2139c34a3a87a0f1059bb036f1a0dc/LICENSE
using System.Runtime.InteropServices.WindowsRuntime;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;

namespace AvalonDock.Controls;

internal sealed class ToggleDockDragOverlay : IDisposable
{
    private readonly ToggleDockingManager manager;
    private readonly Window window = new();
    private readonly Window dragLabelWindow = new();
    private readonly Canvas canvas = new();
    private readonly Grid renderRoot = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly Grid dragLabelRoot = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly IOverlayWindowSurface surface;
    private readonly IOverlayWindowSurface dragLabelSurface;
    private readonly List<Zone> zones = new(16);
    private readonly List<Zone> pendingZones = new(16);
    private readonly Dictionary<DockZone, Rect> visiblePaneBounds = new(6);
    private readonly Border dragLabel;
    private readonly FrameworkElement? leftNavigationFrame;
    private readonly FrameworkElement? rightNavigationFrame;
    private Rect bounds;
    private double scale;
    private bool disposed;
    private bool rendering;
    private bool queued;
    private bool dragLabelRendering;
    private bool dragLabelRendered;
    private long revision;
    private long dragLabelRevision;
    private bool updated;
    private bool failed;
    private Zone? selectedZone;
    private Rect dragLabelBounds;
    internal ToggleDockDragOverlay(ToggleDockingManager manager, LayoutAnchorable tool)
    {
        this.manager = manager;
        leftNavigationFrame = ToggleDockingManager.Visuals<FrameworkElement>(manager)
            .FirstOrDefault(element => element.Name == "PART_LeftNavigationFrame");
        rightNavigationFrame = ToggleDockingManager.Visuals<FrameworkElement>(manager)
            .FirstOrDefault(element => element.Name == "PART_RightNavigationFrame");
        TextBlock label = new()
        {
            Text = tool.Title ?? string.Empty,
            MaxWidth = 160,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = Resource("DockFontFamily", new FontFamily("Segoe UI Variable Text, Segoe UI")),
            FontSize = Resource("DockFontSize", 12d),
            Foreground = Brush("DockTextBrush", "TextFillColorPrimaryBrush")
        };
        dragLabel = new Border
        {
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Background = Brush("DockSurfaceBrush", "SolidBackgroundFillColorTertiaryBrush"),
            BorderBrush = Brush("DockBorderBrush", "CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = label,
            IsHitTestVisible = false
        };
        renderRoot.Children.Add(canvas);
        dragLabelRoot.Children.Add(dragLabel);
        window.Content = renderRoot;
        dragLabelWindow.Content = dragLabelRoot;
        try
        {
            surface = PlatformServices.CreateOverlayWindowSurface(window, manager);
        }
        catch { window.Close(); dragLabelWindow.Close(); throw; }
        try
        {
            dragLabelSurface = PlatformServices.CreateOverlayWindowSurface(dragLabelWindow, manager);
        }
        catch { surface.Dispose(); window.Close(); dragLabelWindow.Close(); throw; }
    }
    internal void Update(Point pointer, bool refreshGeometry = false)
    {
        if (disposed || failed)
        {
            return;
        }

        bool geometryChanged = false;
        bool boundsChanged = false;
        bool mainShown = false;
        if (!updated || refreshGeometry)
        {
            if (!PlatformServices.Coordinates.TryGetScreenBounds(manager, out Rect nextBounds))
            {
                zones.Clear();
                selectedZone = null;
                updated = false;
                surface.Hide();
                dragLabelSurface.Hide();
                return;
            }

            double nextScale = manager.XamlRoot.RasterizationScale;
            if (scale != nextScale)
            {
                dragLabelRendered = false;
                dragLabelRevision++;
            }
            boundsChanged = bounds != nextBounds || scale != nextScale;
            bounds = nextBounds;
            scale = nextScale;
            BuildZones();
            geometryChanged = !updated || boundsChanged || !pendingZones.SequenceEqual(zones);
            if (geometryChanged)
            {
                zones.Clear();
                zones.AddRange(pendingZones);
            }
            updated = true;
        }

        Zone? selected = HitZone(pointer);
        bool selectionChanged = selectedZone != selected;
        selectedZone = selected;
        if (geometryChanged || selectionChanged || !surface.IsVisible)
        {
            canvas.Width = bounds.Width / scale;
            canvas.Height = bounds.Height / scale;
            renderRoot.Width = canvas.Width;
            renderRoot.Height = canvas.Height;
            if (boundsChanged || !surface.IsVisible)
            {
                surface.Show(bounds);
                mainShown = true;
            }

            DrawZones(selected);
            Invalidate();
        }

        MoveDragLabel(pointer, mainShown);
    }
    internal DockZone? Hit(Point point) => failed || disposed ? null : HitZone(point)?.Target;
    private Zone? HitZone(Point point)
    {
        for (int index = zones.Count - 1; index >= 0; index--)
        {
            if (zones[index].Bounds.Contains(point))
            {
                return zones[index];
            }
        }

        return null;
    }
    private void DrawZones(Zone? selected)
    {
        canvas.Children.Clear();
        foreach (Zone zone in zones)
        {
            bool targeted = selected.HasValue && zone == selected.Value;
            if (zone.Label == null && !targeted)
            {
                continue;
            }

            Rect rectangle = zone.Bounds;
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                continue;
            }

            Rectangle border = new()
            {
                Width = rectangle.Width / scale,
                Height = rectangle.Height / scale,
                RadiusX = 4,
                RadiusY = 4,
                Fill = targeted ? Brush("DockPreviewBrush", "SubtleFillColorTransparentBrush") : Brush("DockChromeBrush", "SolidBackgroundFillColorSecondaryBrush"),
                Stroke = targeted ? Brush("DockPreviewBorderBrush", "AccentFillColorDefaultBrush") : Brush("DockBorderBrush", "CardStrokeColorDefaultBrush"),
                StrokeThickness = targeted ? 1.5 : 1,
                Opacity = targeted ? 1 : 0.65
            };
            if (!targeted)
            {
                border.StrokeDashArray = new DoubleCollection { 4, 4 };
            }
            Canvas.SetLeft(border, (rectangle.X - bounds.X) / scale);
            Canvas.SetTop(border, (rectangle.Y - bounds.Y) / scale);
            canvas.Children.Add(border);
            if (zone.Label != null)
            {
                TextBlock text = new()
                {
                    Text = zone.Label,
                    FontFamily = Resource("DockFontFamily", new FontFamily("Segoe UI Variable Text, Segoe UI")),
                    FontSize = Resource("DockFontSize", 12d),
                    Foreground = targeted ? Brush("DockTextBrush", "TextFillColorPrimaryBrush") : Brush("DockSecondaryTextBrush", "TextFillColorSecondaryBrush"),
                    FontWeight = targeted ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(text, (rectangle.X - bounds.X) / scale);
                Canvas.SetTop(text, (rectangle.Y - bounds.Y) / scale + Math.Max(0, rectangle.Height / scale / 2 - 10));
                text.Width = rectangle.Width / scale;
                canvas.Children.Add(text);
            }
            if (targeted && zone.Line is { } y)
            {
                double lineInset = Math.Min(6, rectangle.Width / scale / 2);
                Border line = new()
                {
                    Height = 2,
                    Width = Math.Max(0, rectangle.Width / scale - 2 * lineInset),
                    Background = Brush("AccentFillColorDefaultBrush", "AccentFillColorDefaultBrush"),
                    CornerRadius = new CornerRadius(1)
                };
                Canvas.SetLeft(line, (rectangle.X - bounds.X) / scale + lineInset);
                Canvas.SetTop(line, (y - bounds.Y) / scale - line.Height / 2);
                canvas.Children.Add(line);
            }
        }
    }
    private void MoveDragLabel(Point pointer, bool bringToFront)
    {
        if (!dragLabelRendered && !dragLabelRendering)
        {
            dragLabel.Measure(new Size(double.PositiveInfinity, 28));
            dragLabelRoot.Width = dragLabel.DesiredSize.Width;
            dragLabelRoot.Height = dragLabel.Height;
        }

        double width = Math.Ceiling(dragLabelRoot.Width * scale);
        double height = Math.Ceiling(dragLabelRoot.Height * scale);
        Rect nextBounds = new(
            Math.Clamp(pointer.X + 16 * scale, bounds.Left + 4 * scale, Math.Max(bounds.Left + 4 * scale, bounds.Right - width - 4 * scale)),
            Math.Clamp(pointer.Y - 14 * scale, bounds.Top + 4 * scale, Math.Max(bounds.Top + 4 * scale, bounds.Bottom - height - 4 * scale)),
            width, height);
        if (bringToFront || !dragLabelSurface.IsVisible || nextBounds != dragLabelBounds)
        {
            dragLabelBounds = nextBounds;
            dragLabelSurface.Show(nextBounds);
        }

        if (!dragLabelRendered && !dragLabelRendering)
        {
            RenderDragLabel();
        }
    }
    private async void RenderDragLabel()
    {
        dragLabelRendering = true;
        long current = dragLabelRevision;
        try
        {
            dragLabelRoot.Measure(new Size(dragLabelRoot.Width, dragLabelRoot.Height));
            dragLabelRoot.Arrange(new Rect(0, 0, dragLabelRoot.Width, dragLabelRoot.Height));
            dragLabelRoot.UpdateLayout();
            RenderTargetBitmap bitmap = new();
            await bitmap.RenderAsync(dragLabelRoot);
            IBuffer pixels = await bitmap.GetPixelsAsync();
            if (!disposed && current == dragLabelRevision)
            {
                dragLabelSurface.Present(pixels.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight);
                dragLabelRendered = true;
            }
        }
        catch (Exception)
        {
            if (!disposed)
            {
                failed = true;
                zones.Clear();
                surface.Hide();
                dragLabelSurface.Hide();
            }
        }
        finally
        {
            dragLabelRendering = false;
            if (!disposed && !failed && !dragLabelRendered && dragLabelSurface.IsVisible)
            {
                RenderDragLabel();
            }
        }
    }
    private void BuildZones()
    {
        pendingZones.Clear();
        if (manager.LayoutRootPanel is not { } root || !IsEffectivelyVisible(root)
            || !PlatformServices.Coordinates.TryGetScreenBounds(root, out Rect content)
            || content.Width < 50 * scale || content.Height < 50 * scale)
        {
            return;
        }

        double leftWidth = content.Width * .25;
        double rightWidth = content.Width * .25;
        double bottomHeight = content.Height * .25;
        double sideBottom = double.NaN;
        visiblePaneBounds.Clear();
        foreach (LayoutAnchorablePaneControl pane in ToggleDockingManager.Visuals<LayoutAnchorablePaneControl>(manager))
        {
            if (pane.Model is not LayoutAnchorablePane model || !model.Children.Any(tool => !tool.IsAutoHidden)
                || !IsEffectivelyVisible(pane) || !PlatformServices.Coordinates.TryGetScreenBounds(pane, out Rect measured))
            {
                continue;
            }

            measured.Intersect(content);
            if (measured.IsEmpty || measured.Width <= 0 || measured.Height <= 0)
            {
                continue;
            }

            // 已展开区域使用窗格实测边界，避开分隔条和非均分布局造成的预览偏差。
            foreach (ToggleDockButtonBar bar in manager.Bars)
            {
                if (model.Children.Any(bar.ContainsAnchorable))
                {
                    visiblePaneBounds[bar.Zone] = measured;
                }
            }

            switch (model.GetSide())
            {
                case AnchorSide.Left when measured.Width > 10 * scale:
                    leftWidth = measured.Width;
                    break;
                case AnchorSide.Right when measured.Width > 10 * scale:
                    rightWidth = measured.Width;
                    break;
                case AnchorSide.Bottom when measured.Height > 10 * scale:
                    bottomHeight = content.Bottom - measured.Top;
                    sideBottom = measured.Top - manager.GridSplitterHeight * scale;
                    break;
            }
        }

        leftWidth = Math.Clamp(leftWidth, 0, content.Width);
        rightWidth = Math.Clamp(rightWidth, 0, content.Width);
        bottomHeight = Math.Clamp(bottomHeight, 0, content.Height);
        double bottomTop = content.Bottom - bottomHeight;
        double sideEnd = double.IsNaN(sideBottom) ? bottomTop : Math.Clamp(sideBottom, content.Top, content.Bottom);
        double sideHeight = Math.Max(0, sideEnd - content.Top);
        AddContentPair(visiblePaneBounds, new Rect(content.Left, content.Top, leftWidth, sideHeight),
            DockZone.LeftTop, ToggleDockingManager.GetLocalizedZoneName(DockZone.LeftTop),
            DockZone.LeftBottom, ToggleDockingManager.GetLocalizedZoneName(DockZone.LeftBottom), vertical: true);
        AddContentPair(visiblePaneBounds, new Rect(content.Right - rightWidth, content.Top, rightWidth, sideHeight),
            DockZone.RightTop, ToggleDockingManager.GetLocalizedZoneName(DockZone.RightTop),
            DockZone.RightBottom, ToggleDockingManager.GetLocalizedZoneName(DockZone.RightBottom), vertical: true);
        AddContentPair(visiblePaneBounds, new Rect(content.Left, bottomTop, content.Width, bottomHeight),
            DockZone.BottomLeft, ToggleDockingManager.GetLocalizedZoneName(DockZone.BottomLeft),
            DockZone.BottomRight, ToggleDockingManager.GetLocalizedZoneName(DockZone.BottomRight), vertical: false);

        FrameworkElement? leftFrame = leftNavigationFrame ?? manager.injectedLeftDockPanel;
        FrameworkElement? rightFrame = rightNavigationFrame ?? manager.injectedRightDockPanel;
        AddSide(leftFrame, manager.leftSeparator, manager.bottomLeftBar, DockZone.LeftTop, DockZone.LeftBottom);
        AddSide(rightFrame, manager.rightSeparator, manager.bottomRightBar, DockZone.RightTop, DockZone.RightBottom);
        AddBar(leftFrame, manager.bottomLeftBar);
        AddBar(rightFrame, manager.bottomRightBar);
    }
    private void AddContentPair(Dictionary<DockZone, Rect> visiblePaneBounds, Rect fallback,
        DockZone firstZone, string firstLabel, DockZone secondZone, string secondLabel, bool vertical)
    {
        bool hasFirst = visiblePaneBounds.TryGetValue(firstZone, out Rect first);
        bool hasSecond = visiblePaneBounds.TryGetValue(secondZone, out Rect second);
        if (hasFirst && hasSecond && first != second)
        {
            pendingZones.Add(new(first, firstZone, firstLabel));
            pendingZones.Add(new(second, secondZone, secondLabel));
            return;
        }

        // 单个展开窗格虽占满分组，拖动时仍需把实测分组对半划成两个停靠区。
        Rect area = hasFirst ? first : hasSecond ? second : fallback;
        if (vertical)
        {
            // Synthesized zones need the same splitter-sized gap that separate
            // panes already expose through their measured bounds.
            double splitter = Math.Clamp(manager.GridSplitterHeight * scale, 0, area.Height);
            double half = Math.Max(0, (area.Height - splitter) / 2);
            pendingZones.Add(new(new Rect(area.Left, area.Top, area.Width, half), firstZone, firstLabel));
            pendingZones.Add(new(new Rect(area.Left, area.Top + half + splitter, area.Width, half), secondZone, secondLabel));
        }
        else
        {
            double splitter = Math.Clamp(manager.GridSplitterWidth * scale, 0, area.Width);
            double half = Math.Max(0, (area.Width - splitter) / 2);
            pendingZones.Add(new(new Rect(area.Left, area.Top, half, area.Height), firstZone, firstLabel));
            pendingZones.Add(new(new Rect(area.Left + half + splitter, area.Top, half, area.Height), secondZone, secondLabel));
        }
    }
    private void AddBar(FrameworkElement? frame, ToggleDockButtonBar? bar)
    {
        if (frame == null || bar == null || !IsEffectivelyVisible(frame) || !IsEffectivelyVisible(bar)
            || !PlatformServices.Coordinates.TryGetScreenBounds(frame, out Rect frameBounds)
            || !PlatformServices.Coordinates.TryGetScreenBounds(bar, out Rect barBounds))
        {
            return;
        }

        Rect aligned = new(frameBounds.X, barBounds.Y, frameBounds.Width, Math.Max(barBounds.Height, 20 * scale));
        aligned.Intersect(frameBounds);
        if (!aligned.IsEmpty && aligned.Width > 0 && aligned.Height > 0)
        {
            pendingZones.Add(new(aligned, bar.Zone, null));
        }
    }
    private void AddSide(FrameworkElement? frame, FrameworkElement? separator, ToggleDockButtonBar? bottom, DockZone topZone, DockZone lowerZone)
    {
        if (frame == null || !IsEffectivelyVisible(frame) || !PlatformServices.Coordinates.TryGetScreenBounds(frame, out Rect area))
        {
            return;
        }

        area = new Rect(area.X, area.Y, Math.Max(area.Width, 20 * scale), Math.Max(area.Height, 20 * scale));
        double usableBottom = bottom != null && IsEffectivelyVisible(bottom)
            && PlatformServices.Coordinates.TryGetScreenBounds(bottom, out Rect bottomBounds)
            ? Math.Clamp(bottomBounds.Top, area.Top, area.Bottom) : area.Bottom;
        double usableHeight = usableBottom - area.Top;
        if (usableHeight < 10 * scale)
        {
            return;
        }

        double y = separator != null && IsEffectivelyVisible(separator)
            && PlatformServices.Coordinates.TryGetScreenBounds(separator, out Rect split)
            ? Math.Clamp(split.Y + split.Height / 2, area.Top, usableBottom) : area.Y + usableHeight / 2;
        pendingZones.Add(new(new Rect(area.X, area.Y, area.Width, y - area.Y), topZone, null, y));
        pendingZones.Add(new(new Rect(area.X, y, area.Width, usableBottom - y), lowerZone, null, y));
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
            }
        }
        catch (Exception)
        {
            if (!disposed)
            {
                failed = true;
                zones.Clear();
                surface.Hide();
                dragLabelSurface.Hide();
            }
        }
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
        dragLabelSurface.Dispose();
        surface.Dispose();
        dragLabelWindow.Content = null;
        window.Content = null;
        dragLabelWindow.Close();
        window.Close();
    }
    private T Resource<T>(string key, T fallback)
    {
        if (manager.Resources.TryGetValue(key, out object? local) && local is T value)
        {
            return value;
        }

        if (Application.Current.Resources.TryGetValue(key, out object? application) && application is T appValue)
        {
            return appValue;
        }

        return fallback;
    }
    private Brush Brush(string key, string fallbackKey) => Resource(key,
        Application.Current.Resources.TryGetValue(fallbackKey, out object? fallback) && fallback is Brush brush
            ? brush : new SolidColorBrush(Microsoft.UI.Colors.Transparent));
    private readonly record struct Zone(Rect Bounds, DockZone Target, string? Label, double? Line = null);
}
