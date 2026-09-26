// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/OverlayWindow.cs
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using AvalonDock.Themes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace AvalonDock.Controls;

/// <summary>Displays docking targets and a layout preview without taking input or keyboard focus.</summary>
[Microsoft.UI.Xaml.Data.Bindable]
public partial class OverlayWindow : Window
{
    private readonly FrameworkElement destination;
    private readonly TemplateView view;
    private readonly IOverlayWindowSurface surface;
    private readonly Style defaultStyle;
    private bool automaticStyle = true;
    private readonly Dictionary<string, Grid> groups = [];
    private readonly Dictionary<OverlayTarget, FrameworkElement> parts = [];
    private readonly Dictionary<Border, (Brush Background, Brush BorderBrush, Thickness BorderThickness)> targetedChrome = [];
    private Canvas? canvas;
    private Path? preview;
    private ResourceDictionary? themeResources;
    private Rect bounds;
    private IReadOnlyList<OverlayTarget> targets = [];
    private OverlayTarget? active;
    private bool closed;
    private bool queued;
    private bool rendering;
    private int revision;
    private int renderedRevision = -1;

    internal OverlayWindow(FrameworkElement destination, bool hostedInFloatingWindow)
    {
        this.destination = destination;
        IsHostedInFloatingWindow = hostedInFloatingWindow;
        view = new TemplateView(this) { IsHitTestVisible = false, IsTabStop = false };
        ResourceDictionary defaults = new()
        {
            Source = new Uri("ms-appx:///WinUI.AvalonDock/Themes/OverlayWindow.xaml")
        };
        view.Resources.MergedDictionaries.Add(defaults);
        defaultStyle = new Style(typeof(Control));
        defaultStyle.Setters.Add(new Setter(Control.TemplateProperty, defaults["AvalonDockOverlayWindowTemplate"]));
        view.Style = defaultStyle;
        Content = view;
        surface = PlatformServices.CreateOverlayWindowSurface(this, destination);
        view.Loaded += (_, _) => Invalidate();
        view.RegisterPropertyChangedCallback(Control.TemplateProperty, (_, _) => Invalidate());
        AppWindow.Closing += (_, args) =>
        {
            CancelEventArgs cancel = new();
            OnClosing(cancel);
            args.Cancel = cancel.Cancel;
        };
        Closed += (_, _) =>
        {
            closed = true;
            floatingWindow = null;
            revision++;
            surface.Dispose();
        };
    }

    [System.ComponentModel.Bindable(false)]
    [Description("Gets whether the window is hosted in a floating window.")]
    [Category("FloatingWindow")]
    public bool IsHostedInFloatingWindow
    {
        get;
    }

    // WinUI Window has no template/DependencyObject base. Keep the inherited calling entry
    // points through a private Control; ControlTemplate.TargetType must use WinUI Control.
    public static readonly DependencyProperty TemplateProperty = Control.TemplateProperty;
    public static readonly DependencyProperty StyleProperty = FrameworkElement.StyleProperty;
    public ControlTemplate? Template
    {
        get => view.Template; set
        {
            view.Template = value;
            ApplyTemplate();
            Invalidate();
        }
    }
    public Style? Style
    {
        get => view.Style; set
        {
            automaticStyle = false;
            view.Style = value;
            ApplyTemplate();
            Invalidate();
        }
    }
    public ResourceDictionary Resources => view.Resources;
    public object? GetValue(DependencyProperty dp) => view.GetValue(dp);
    public void SetValue(DependencyProperty dp, object? value)
    {
        if (dp == StyleProperty)
        {
            automaticStyle = false;
        }

        view.SetValue(dp, value);
    }
    public void ClearValue(DependencyProperty dp)
    {
        view.ClearValue(dp);
        if (dp == StyleProperty)
        {
            automaticStyle = true;
            ApplyThemeStyle();
        }
    }
    public object ReadLocalValue(DependencyProperty dp) => view.ReadLocalValue(dp);
    public void SetBinding(DependencyProperty dp, BindingBase binding) => BindingOperations.SetBinding(view, dp, binding);
    public bool ApplyTemplate() => view.ApplyTemplate();
    protected DependencyObject? GetTemplateChild(string childName) => view.Part(childName);

    public virtual void OnApplyTemplate()
    {
        ClearTargetedChrome();
        BindOriginalTargetParts();
        canvas = GetTemplateChild("PART_DropTargetsContainer") as Canvas;
        preview = GetTemplateChild("PART_PreviewBox") as Path;
        groups.Clear();
        foreach (string? name in new[] { "DockingManager", "AnchorablePane", "DocumentPane", "DocumentPaneFull" })
        {
            if (GetTemplateChild("PART_" + name + "DropTargets") is Grid group)
            {
                groups.Add(name, group);
                group.Visibility = Visibility.Collapsed;
            }
        }

        parts.Clear();
        active = null;
        if (preview != null)
        {
            preview.Visibility = Visibility.Collapsed;
        }

        Invalidate();
    }

    protected virtual void OnClosing(CancelEventArgs e)
    {
    }

    public void Show()
    {
        if (!PlatformServices.Coordinates.TryGetScreenBounds(destination, out Rect rectangle))
        {
            return;
        }

        bounds = rectangle;
        double scale = destination.XamlRoot.RasterizationScale;
        view.Width = bounds.Width / scale;
        view.Height = bounds.Height / scale;
        ApplyTemplate();
        surface.Show(bounds);
        Invalidate();
    }
    public void Hide()
    {
        ClearTargetedChrome();
        if (overlayHost != null)
        {
            OverlayHost.InvalidateAreas(overlayHost);
        }

        ClearDragAreas();
        revision++;
        targets = [];
        active = null;
        parts.Clear();
        foreach (Grid group in groups.Values)
        {
            group.Visibility = Visibility.Collapsed;
        }

        if (preview != null)
        {
            preview.Visibility = Visibility.Collapsed;
        }

        surface.Hide();
    }
    public new void Close()
    {
        if (closed)
        {
            return;
        }

        CancelEventArgs args = new();
        OnClosing(args);
        if (!args.Cancel)
        {
            CloseHost();
        }
    }

    internal bool IsClosed => closed;
    internal bool IsVisible => !closed && surface.IsVisible;
    internal Exception? RenderFailure
    {
        get; private set;
    }
    internal int PresentedFrames
    {
        get; private set;
    }
    internal Canvas? TargetCanvas => canvas;
    internal IReadOnlyList<OverlayTarget> CurrentTargets => targets;
    internal void CloseHost()
    {
        if (closed)
        {
            return;
        }

        if (surface.IsClosed)
        {
            closed = true;
            surface.Dispose();
            return;
        }
        Hide();
        base.Close();
    }

    internal void UpdateThemeResources(Theme? theme)
    {
        if (themeResources != null)
        {
            Resources.MergedDictionaries.Remove(themeResources);
        }

        themeResources = ThemeResourceFactory.Create(theme);
        if (themeResources != null)
        {
            Resources.MergedDictionaries.Add(themeResources);
        }

        ApplyThemeStyle();
        Invalidate();
    }

    private void ApplyThemeStyle()
    {
        if (!automaticStyle)
        {
            return;
        }

        Style candidate = Resources.TryGetValue(typeof(OverlayWindow), out object? local) && local is Style own ? own
            : Application.Current.Resources.TryGetValue(typeof(OverlayWindow), out object? global) && global is Style application ? application : defaultStyle;
        if (!ReferenceEquals(view.Style, candidate))
        {
            view.Style = candidate;
        }
    }



    private IEnumerable<FrameworkElement> TemplateParts()
    {
        foreach (string name in groups.Keys)
        {
            foreach (string? direction in new[] { "Left", "Top", "Right", "Bottom", "Into" })
            {
                if (GetTemplateChild("PART_" + name + "DropTarget" + direction) is FrameworkElement part)
                {
                    yield return part;
                }
            }
        }

        foreach (string? direction in new[] { "Left", "Top", "Right", "Bottom" })
        {
            if (GetTemplateChild("PART_DocumentPaneDropTarget" + direction + "AsAnchorablePane") is FrameworkElement part)
            {
                yield return part;
            }
        }
    }

    internal void SetActive(OverlayTarget? target)
    {
        if (Equals(active, target))
        {
            return;
        }

        ClearTargetedChrome();
        active = target;
        if (preview != null)
        {
            preview.Visibility = Visibility.Collapsed;
        }

        foreach (KeyValuePair<OverlayTarget, FrameworkElement> pair in parts)
        {
            bool targeted = target != null && ReferenceEquals(pair.Key.Model, target.Model) && pair.Key.Type == target.Type;
            if (pair.Value is Border { Child: ContentControl { Tag: "AvalonDock.WPFUI.Glyph" } glyph })
            {
                // WPFUI's IsTargeted trigger is a native visual state; the shared session
                // already knows the hit target, so no cursor polling belongs in the template.
                glyph.ApplyTemplate();
                VisualStateManager.GoToState(glyph, targeted ? "Targeted" : "Normal", false);
                Border border = (Border)pair.Value;
                if (targeted && CanApplyDefaultTargetChrome(border))
                {
                    (Brush, Brush, Thickness) applied = ((Brush)view.Resources["AvalonDockTargetedSurfaceBrush"],
                        (Brush)view.Resources["AvalonDockTargetedBorderBrush"], new Thickness(1));
                    border.Background = applied.Item1;
                    border.BorderBrush = applied.Item2;
                    border.BorderThickness = applied.Item3;
                    targetedChrome.Add(border, applied);
                }
                border.Opacity = targeted ? 1 : .82;
            }
            else
            {
                pair.Value.Opacity = targeted ? 1 : .92;
            }
        }
        Invalidate();
    }

    internal bool IsPresenting(OverlayTarget? target) => Equals(active, target);

    private bool CanApplyDefaultTargetChrome(Border border) => !targetedChrome.ContainsKey(border)
        && ReferenceEquals(border.Style, view.Resources["AvalonDockOverlayTargetStyle"])
        && border.ReadLocalValue(Border.BackgroundProperty) == DependencyProperty.UnsetValue
        && border.ReadLocalValue(Border.BorderBrushProperty) == DependencyProperty.UnsetValue
        && border.ReadLocalValue(Border.BorderThicknessProperty) == DependencyProperty.UnsetValue;

    private void ClearTargetedChrome()
    {
        foreach ((Border? border, (Brush Background, Brush BorderBrush, Thickness BorderThickness) applied) in targetedChrome)
        {
            ClearTargetChrome(border, applied);
        }

        targetedChrome.Clear();
    }

    private static void ClearTargetChrome(Border border, (Brush Background, Brush BorderBrush, Thickness BorderThickness) applied)
    {
        if (ReferenceEquals(border.ReadLocalValue(Border.BackgroundProperty), applied.Background))
        {
            border.ClearValue(Border.BackgroundProperty);
        }

        if (ReferenceEquals(border.ReadLocalValue(Border.BorderBrushProperty), applied.BorderBrush))
        {
            border.ClearValue(Border.BorderBrushProperty);
        }

        if (Equals(border.ReadLocalValue(Border.BorderThicknessProperty), applied.BorderThickness))
        {
            border.ClearValue(Border.BorderThicknessProperty);
        }
    }

    internal void ShowResizePreview(Rect screenRectangle, Brush fill, double opacity)
    {
        Show();
        if (preview == null)
        {
            return;
        }

        double scale = destination.XamlRoot.RasterizationScale;
        preview.Data = new RectangleGeometry
        {
            Rect = new Rect((screenRectangle.X - bounds.X) / scale,
            (screenRectangle.Y - bounds.Y) / scale, screenRectangle.Width / scale, screenRectangle.Height / scale)
        };
        preview.Width = view.Width;
        preview.Height = view.Height;
        preview.Stretch = Stretch.None;
        preview.Fill = fill;
        preview.StrokeThickness = 0;
        preview.Opacity = opacity;
        preview.Visibility = Visibility.Visible;
        Invalidate();
    }

    internal Rect GetPreviewBounds(FrameworkElement element)
    {
        if (!PlatformServices.Coordinates.TryGetScreenBounds(element, out Rect rectangle))
        {
            return Rect.Empty;
        }

        return GetPreviewBounds(rectangle);
    }
    internal Rect GetPreviewBounds(Rect screenRectangle)
    {
        double scale = destination.XamlRoot.RasterizationScale;
        return new Rect((screenRectangle.X - bounds.X) / scale, (screenRectangle.Y - bounds.Y) / scale,
            screenRectangle.Width / scale, screenRectangle.Height / scale);
    }
    internal IEnumerable<ILayoutControl> GetLayoutControls() => destination.FindVisualChildren<FrameworkElement>().OfType<ILayoutControl>();

    private void Invalidate()
    {
        revision++;
        if (closed || queued || rendering)
        {
            return;
        }

        queued = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, Render);
    }
    private async void Render()
    {
        queued = false;
        if (closed || !surface.IsVisible || !view.IsLoaded || canvas == null)
        {
            return;
        }

        rendering = true;
        try
        {
            RenderTargetBitmap bitmap = new();
            while (!closed && surface.IsVisible && renderedRevision != revision)
            {
                int current = revision;
                // The native composition child is hidden; it does not schedule the normal
                // layout/render pass after Path.Data or template properties change.
                view.Measure(new Size(view.Width, view.Height));
                view.Arrange(new Rect(0, 0, view.Width, view.Height));
                view.UpdateLayout();
                await bitmap.RenderAsync(view);
                byte[] pixels = (await bitmap.GetPixelsAsync()).ToArray();
                if (closed || !surface.IsVisible)
                {
                    break;
                }

                if (current != revision)
                {
                    continue;
                }

                surface.Present(pixels, bitmap.PixelWidth, bitmap.PixelHeight);
                renderedRevision = current;
                PresentedFrames++;
                RenderFailure = null;
            }
        }
        catch (Exception exception)
        {
            // Do not let an asynchronous render resurrect a cancelled session or leave an opaque window.
            RenderFailure = exception;
            Hide();
            System.Diagnostics.Trace.TraceError("AvalonDock overlay rendering failed: {0}", exception);
        }
        finally { rendering = false; }
    }

    private sealed class TemplateView(OverlayWindow owner) : Control
    {
        internal DependencyObject? Part(string name) => GetTemplateChild(name);
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            owner.OnApplyTemplate();
        }
    }
}
