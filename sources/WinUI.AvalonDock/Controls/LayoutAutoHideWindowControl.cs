// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/LayoutAutoHideWindowControl.cs.
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using AvalonDock.Compatibility;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using PropertyMetadata = Microsoft.UI.Xaml.PropertyMetadata;

namespace AvalonDock.Controls;

public class LayoutAutoHideWindowControl : ChildWindowHost, ILayoutControl
{
    private readonly ContentPresenter presenter = new();
    private LayoutAnchorable? model;
    private LayoutAnchorControl? anchor;
    private DockingManager? manager;
    private Grid? grid;
    private LayoutAnchorableControl? content;
    private LayoutGridResizerControl? resizer;
    private double startPosition;
    private double initialExtent;
    private double candidateExtent;
    private bool resizing;
    private OverlayWindow? resizePreviewWindow;
    private Rect? shownResizePreviewBounds;
    private Brush? shownResizePreviewFill;
    private double shownResizePreviewOpacity;
    private AnchorSide side;
    private bool focusOnLoad;
    private readonly List<(DependencyProperty Property, long Token)> managerTokens = [];
    private ResourceDictionary? themeResources;
    private Theme? currentTheme;
    private LayoutAnchorGroup? observedGroup;
    private LayoutAnchorSide? observedSide;

    internal LayoutAutoHideWindowControl()
    {
        DefaultStyleKey = typeof(LayoutAutoHideWindowControl);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/AutoHide.xaml");
        Visibility = Visibility.Collapsed;
        IsTabStop = true;
        Disconnected += (_, _) => EndResize();
        presenter.Loaded += (_, _) => { if (focusOnLoad) { FocusContent(); } };
        Unloaded += (_, _) => EndResize();
        DataContextChanged += (_, _) => UpdatePresentation();
    }
    public static readonly DependencyProperty AnchorableStyleProperty = DependencyProperty.Register(nameof(AnchorableStyle), typeof(Style), typeof(LayoutAutoHideWindowControl), new PropertyMetadata(null, (d, _) => ((LayoutAutoHideWindowControl)d).UpdateStyles()));
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the style to apply to the LayoutAnchorableControl hosted in this auto hide window.")]
    [Category("Style")]
    public Style? AnchorableStyle
    {
        get => (Style?)GetValue(AnchorableStyleProperty); set => SetValue(AnchorableStyleProperty, value);
    }
    public static readonly DependencyProperty AnchorableGridStyleProperty = DependencyProperty.Register(nameof(AnchorableGridStyle), typeof(Style), typeof(LayoutAutoHideWindowControl), new PropertyMetadata(null, (d, _) => ((LayoutAutoHideWindowControl)d).UpdateStyles()));
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the style to apply to the parent Grid of the LayoutAnchorableControl hosted in this auto hide window.")]
    [Category("Style")]
    public Style? AnchorableGridStyle
    {
        get => (Style?)GetValue(AnchorableGridStyleProperty); set => SetValue(AnchorableGridStyleProperty, value);
    }
    // WPF HwndHost has no Background; the native Control-based host already owns that DP.
    // Reuse it so compiled style setters and the original binding address the same value.
    public new static readonly DependencyProperty BackgroundProperty = Control.BackgroundProperty;
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the background brush of the autohide childwindow.")]
    [Category("Other")]
    public new Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value);
    }
    public ILayoutElement? Model => model;
    internal bool IsResizing => resizing;
    internal FrameworkElement? RootVisual => grid;
    internal bool IsPointerWithin => grid != null && PlatformServices.Coordinates.IsPointerOver(grid)
        || anchor != null && PlatformServices.Coordinates.IsPointerOver(anchor);

    protected override void OnHostConnected()
    {
        HostedRoot = presenter;
        AutomationProperties.SetName(presenter, "InternalWindowHost");
    }

    protected override void OnHostDisconnected() => HostedRoot = null;
    protected override bool HasFocusWithinCore() => false;
    protected override System.Collections.IEnumerator LogicalChildren => new UIElement[] { presenter }.GetEnumerator();
    protected override Size MeasureOverride(Size constraint)
    {
        presenter.Measure(constraint);
        return presenter.DesiredSize;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        presenter.Arrange(new Rect(default, finalSize));
        return base.ArrangeOverride(finalSize);
    }

    internal void Show(LayoutAnchorControl source)
    {
        if (model != null)
        {
            throw new InvalidOperationException("An auto-hide window must be hidden before another anchor is shown.");
        }

        if (source.Model is not LayoutAnchorable tool || tool.Parent?.Parent is not LayoutAnchorSide anchorSide || tool.Root?.Manager is not { } owner)
        {
            return;
        }

        anchor = source;
        model = tool;
        manager = owner;
        side = anchorSide.Side;
        observedGroup = (LayoutAnchorGroup)tool.Parent;
        observedSide = anchorSide;
        observedGroup.PropertyChanged += OnModelChanged;
        observedGroup.Children.CollectionChanged += OnAnchorMembershipChanged;
        observedSide.Children.CollectionChanged += OnAnchorMembershipChanged;
        model.PropertyChanged += OnModelChanged;
        manager.SizeChanged += OnManagerSizeChanged;
        manager.ActualThemeChanged += OnManagerThemeChanged;
        manager.DataContextChanged += OnManagerDataContextChanged;
        foreach (DependencyProperty? property in new[] { FlowDirectionProperty, DockingManager.ThemeProperty, DockingManager.GridSplitterWidthProperty,
            DockingManager.GridSplitterHeightProperty, DockingManager.GridSplitterVerticalStyleProperty, DockingManager.GridSplitterHorizontalStyleProperty })
        {
            managerTokens.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) => { UpdatePresentation(); UpdateExtent(); })));
        }

        Grid currentGrid = CreateInternalGrid(tool, owner);
        currentGrid.PreviewKeyDown += OnPreviewNavigatorKey;
        currentGrid.LayoutUpdated += OnGridLayoutUpdated;
        UpdateStyles();
        Visibility = Visibility.Visible;
        UpdateExtent();
        if (IsLoaded)
        {
            ConnectHost();
        }
    }
    private Grid CreateInternalGrid(LayoutAnchorable model, DockingManager manager)
    {
        Grid grid = this.grid = new Grid { FlowDirection = FlowDirection.LeftToRight, Style = AnchorableGridStyle };
        grid.SetBinding(Panel.BackgroundProperty, new Binding { Path = new PropertyPath(nameof(Grid.Background)), Source = this });
        grid.SizeChanged += OnInternalGridSizeChanged;

        LayoutAnchorableControl content = this.content = new LayoutAnchorableControl { Model = model, Style = AnchorableStyle };
        content.SetBinding(FlowDirectionProperty, new Binding { Path = new PropertyPath("Root.Manager.FlowDirection"), Source = model });

        grid.TabFocusNavigation = KeyboardNavigationMode.Cycle;
        LayoutGridResizerControl resizer = this.resizer = new LayoutGridResizerControl();
        // The reference outline extends across the splitter with a negative margin.
        // WinUI hit-tests that overflow; keep the native input surface above it.
        Canvas.SetZIndex(resizer, 1);

        resizer.DragStarted += OnResizerDragStarted;
        resizer.DragDelta += OnResizerDragDelta;
        resizer.DragCompleted += OnResizerDragCompleted;

        switch (side)
        {
            case AnchorSide.Right:
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(manager.GridSplitterWidth) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = model.AutoHideWidth == 0.0 ? new GridLength(model.AutoHideMinWidth) : new GridLength(model.AutoHideWidth, GridUnitType.Pixel) });
                Grid.SetColumn(resizer, 0);
                Grid.SetColumn(content, 1);
                resizer.SetResizeOrientation(Orientation.Horizontal);
                HorizontalAlignment = HorizontalAlignment.Right;
                VerticalAlignment = VerticalAlignment.Stretch;
                break;

            case AnchorSide.Left:
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = model.AutoHideWidth == 0.0 ? new GridLength(model.AutoHideMinWidth) : new GridLength(model.AutoHideWidth, GridUnitType.Pixel) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(manager.GridSplitterWidth) });
                Grid.SetColumn(content, 0);
                Grid.SetColumn(resizer, 1);
                resizer.SetResizeOrientation(Orientation.Horizontal);
                HorizontalAlignment = HorizontalAlignment.Left;
                VerticalAlignment = VerticalAlignment.Stretch;
                break;

            case AnchorSide.Top:
                grid.RowDefinitions.Add(new RowDefinition { Height = model.AutoHideHeight == 0.0 ? new GridLength(model.AutoHideMinHeight) : new GridLength(model.AutoHideHeight, GridUnitType.Pixel), });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(manager.GridSplitterHeight) });
                Grid.SetRow(content, 0);
                Grid.SetRow(resizer, 1);
                resizer.SetResizeOrientation(Orientation.Vertical);
                VerticalAlignment = VerticalAlignment.Top;
                HorizontalAlignment = HorizontalAlignment.Stretch;
                break;

            case AnchorSide.Bottom:
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(manager.GridSplitterHeight) });
                grid.RowDefinitions.Add(new RowDefinition { Height = model.AutoHideHeight == 0.0 ? new GridLength(model.AutoHideMinHeight) : new GridLength(model.AutoHideHeight, GridUnitType.Pixel), });
                Grid.SetRow(resizer, 0);
                Grid.SetRow(content, 1);
                resizer.SetResizeOrientation(Orientation.Vertical);
                VerticalAlignment = VerticalAlignment.Bottom;
                HorizontalAlignment = HorizontalAlignment.Stretch;
                break;
        }

        grid.Children.Add(resizer);
        grid.Children.Add(content);
        presenter.Content = grid;
        return grid;
    }

    private void RemoveInternalGrid()
    {
        if (resizer is { } splitter)
        {
            splitter.DragStarted -= OnResizerDragStarted;
            splitter.DragDelta -= OnResizerDragDelta;
            splitter.DragCompleted -= OnResizerDragCompleted;
            splitter.CancelDrag();
        }

        presenter.Content = null;
        if (grid is { } currentGrid)
        {
            currentGrid.SizeChanged -= OnInternalGridSizeChanged;
        }
    }

    private void OnInternalGridSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The native island arranges its stretch axis independently of this parent host.
        // Invalidating that axis alternates the child's provisional 1-DIP size and full
        // island size. Only the original side's extent changes the parent desired size.
        if (side is AnchorSide.Left or AnchorSide.Right ? e.NewSize.Width != e.PreviousSize.Width : e.NewSize.Height != e.PreviousSize.Height)
        {
            InvalidateMeasure();
        }
    }

    internal void Hide()
    {
        EndResize();
        focusOnLoad = false;
        DisconnectHost();
        if (model != null)
        {
            model.PropertyChanged -= OnModelChanged;
        }

        if (observedGroup != null)
        {
            observedGroup.PropertyChanged -= OnModelChanged;
            observedGroup.Children.CollectionChanged -= OnAnchorMembershipChanged;
        }
        if (observedSide != null)
        {
            observedSide.Children.CollectionChanged -= OnAnchorMembershipChanged;
        }

        observedGroup = null;
        observedSide = null;
        if (manager != null)
        {
            manager.SizeChanged -= OnManagerSizeChanged;
            manager.ActualThemeChanged -= OnManagerThemeChanged;
            manager.DataContextChanged -= OnManagerDataContextChanged;
            foreach ((DependencyProperty? property, long token) in managerTokens)
            {
                manager.UnregisterPropertyChangedCallback(property, token);
            }
        }
        managerTokens.Clear();
        if (grid != null)
        {
            grid.PreviewKeyDown -= OnPreviewNavigatorKey;
            grid.LayoutUpdated -= OnGridLayoutUpdated;
            if (themeResources != null)
            {
                grid.Resources.MergedDictionaries.Remove(themeResources);
            }
        }
        themeResources = null;
        currentTheme = null;
        if (grid != null)
        {
            RemoveInternalGrid();
        }

        if (content != null)
        {
            content.Model = null;
        }

        grid = null;
        content = null;
        resizer = null;
        anchor = null;
        model = null;
        manager = null;
        Visibility = Visibility.Collapsed;
    }
    internal void ValidateModel()
    {
        if (model != null && (!model.IsAutoHidden || model.Root?.Manager != manager || model.Parent is not LayoutAnchorGroup group
            || group.Parent is not LayoutAnchorSide anchorSide || !group.Children.Contains(model) || !anchorSide.Children.Contains(group)))
        {
            manager?.HideAutoHideWindow(anchor);
        }
    }
    internal void FocusContent()
    {
        focusOnLoad = true;
        if (grid?.XamlRoot == null)
        {
            return;
        }

        if (FocusManager.FindFirstFocusableElement(content!) is Control first && first.Focus(FocusState.Programmatic))
        {
            focusOnLoad = false;
        }
        else if (content?.Focus(FocusState.Programmatic) == true)
        {
            focusOnLoad = false;
        }
    }
    internal void UpdateExtent()
    {
        if (model == null || manager == null)
        {
            return;
        }

        FrameworkElement area = manager.GetAutoHideAreaElement();
        bool vertical = side is AnchorSide.Left or AnchorSide.Right;
        double desired = vertical ? model.AutoHideWidth > 0 ? model.AutoHideWidth : model.AutoHideMinWidth
            : model.AutoHideHeight > 0 ? model.AutoHideHeight : model.AutoHideMinHeight;
        double available = vertical ? area.ActualWidth : area.ActualHeight;
        double splitter = vertical ? manager.GridSplitterWidth : manager.GridSplitterHeight;
        double extent = Math.Min(desired + splitter, Math.Max(1, available));
        if (grid != null)
        {
            if (vertical)
            {
                grid.ColumnDefinitions[side == AnchorSide.Left ? 0 : 1].Width = new GridLength(desired);
            }
            else
            {
                grid.RowDefinitions[side == AnchorSide.Top ? 0 : 1].Height = new GridLength(desired);
            }
        }
        Width = vertical ? extent : double.NaN;
        Height = vertical ? double.NaN : extent;
        HorizontalAlignment = side == AnchorSide.Left ? HorizontalAlignment.Left : side == AnchorSide.Right ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        VerticalAlignment = side == AnchorSide.Top ? VerticalAlignment.Top : side == AnchorSide.Bottom ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
        InvalidateMeasure();
    }
    private void UpdateStyles()
    {
        if (grid == null || content == null)
        {
            return;
        }

        grid.Style = AnchorableGridStyle;
        if (AnchorableStyle != null)
        {
            content.Style = AnchorableStyle;
        }
        else
        {
            content.ClearValue(StyleProperty);
        }

        UpdatePresentation();
    }
    private void UpdatePresentation()
    {
        if (grid == null || manager == null)
        {
            return;
        }

        grid.RequestedTheme = manager.ActualTheme;
        grid.DataContext = ReadLocalValue(DataContextProperty) != DependencyProperty.UnsetValue ? DataContext : manager.DataContext;
        if (!ReferenceEquals(currentTheme, manager.Theme))
        {
            if (themeResources != null)
            {
                grid.Resources.MergedDictionaries.Remove(themeResources);
            }

            currentTheme = manager.Theme;
            themeResources = ThemeResourceFactory.Create(currentTheme);
            if (themeResources != null)
            {
                grid.Resources.MergedDictionaries.Add(themeResources);
            }
        }
        if (resizer == null)
        {
            return;
        }

        bool vertical = side is AnchorSide.Left or AnchorSide.Right;
        Style? style = vertical ? manager.GridSplitterVerticalStyle : manager.GridSplitterHorizontalStyle;
        if (style != null)
        {
            resizer.Style = style;
        }
        else
        {
            resizer.ClearValue(StyleProperty);
        }

        if (vertical)
        {
            grid.ColumnDefinitions[side == AnchorSide.Left ? 1 : 0].Width = new GridLength(manager.GridSplitterWidth);
        }
        else
        {
            grid.RowDefinitions[side == AnchorSide.Top ? 1 : 0].Height = new GridLength(manager.GridSplitterHeight);
        }
    }
    private void OnGridLayoutUpdated(object? sender, object args)
    {
        ValidateModel();
        UpdatePresentation();
    }
    private void OnManagerDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => UpdatePresentation();
    private void OnModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        ValidateModel();
        if (args.PropertyName is nameof(LayoutAnchorable.AutoHideWidth) or nameof(LayoutAnchorable.AutoHideHeight) or nameof(LayoutAnchorable.AutoHideMinWidth) or nameof(LayoutAnchorable.AutoHideMinHeight))
        {
            UpdateExtent();
        }
    }
    private void OnAnchorMembershipChanged(object? sender, NotifyCollectionChangedEventArgs args) => ValidateModel();
    private void OnManagerSizeChanged(object? sender, SizeChangedEventArgs args) => UpdateExtent();
    private void OnManagerThemeChanged(FrameworkElement sender, object args) => UpdatePresentation();
    private void OnPreviewNavigatorKey(object? sender, KeyRoutedEventArgs args) => manager?.HandleNavigatorKey(args);
    private void OnResizerDragStarted(object? sender, DragStartedEventArgs args)
    {
        if (resizer == null || model == null || manager == null)
        {
            resizer?.CancelDrag();
            return;
        }
        bool vertical = side is AnchorSide.Left or AnchorSide.Right;
        startPosition = vertical ? resizer.DragStartPosition.X : resizer.DragStartPosition.Y;
        initialExtent = candidateExtent = vertical ? ActualWidth - manager.GridSplitterWidth : ActualHeight - manager.GridSplitterHeight;
        resizing = true;
    }
    private void OnResizerDragDelta(object? sender, DragDeltaEventArgs args)
    {
        if (resizer is { } splitter && ReferenceEquals(sender, splitter))
        {
            UpdateResize(splitter.DragCurrentPosition, false);
        }
    }
    private void OnResizerDragCompleted(object? sender, DragCompletedEventArgs args)
    {
        if (resizer is not { } splitter || !ReferenceEquals(sender, splitter))
        {
            return;
        }

        if (args.Canceled)
        {
            EndResize();
        }
        else
        {
            UpdateResize(splitter.DragCurrentPosition, true);
        }
    }
    private void UpdateResize(DragInputPosition position, bool released)
    {
        if (model == null || manager == null || content == null)
        {
            EndResize();
            return;
        }
        bool vertical = side is AnchorSide.Left or AnchorSide.Right;
        double scale = grid?.XamlRoot?.RasterizationScale ?? XamlRoot?.RasterizationScale ?? 1;
        double delta = ((vertical ? position.X : position.Y) - startPosition) / scale
            * (vertical && content.FlowDirection == FlowDirection.RightToLeft ? -1 : 1)
            * (side is AnchorSide.Right or AnchorSide.Bottom ? -1 : 1);
        FrameworkElement area = manager.GetAutoHideAreaElement();
        // The source preview clamps the ghost's screen position. Convert that
        // interval to this side's content extent; the model clamps on release.
        double splitter = vertical ? resizer?.ActualWidth ?? manager.GridSplitterWidth : resizer?.ActualHeight ?? manager.GridSplitterHeight;
        double minimum = side switch
        {
            AnchorSide.Left or AnchorSide.Top => 25,
            AnchorSide.Right => -splitter,
            _ => model.AutoHideMinHeight - splitter
        };
        double maximum = side switch
        {
            AnchorSide.Left => area.ActualWidth,
            AnchorSide.Top => area.ActualHeight - model.AutoHideMinHeight,
            _ => (vertical ? area.ActualWidth : area.ActualHeight) - 25 - splitter
        };
        candidateExtent = Math.Clamp(initialExtent + delta, minimum, Math.Max(minimum, maximum));
        if (released)
        {
            // Original OnResizerDragCompleted commits model/grid dimensions before hiding
            // the preview and clearing IsResizing. Native preview close can reenter the
            // close timer; it must not discard the flyout before that commit completes.
            double deltaExtent = candidateExtent - initialExtent;
            try
            {
                if (vertical)
                {
                    if (model.AutoHideWidth == 0.0)
                    {
                        model.AutoHideWidth = content.ActualWidth + deltaExtent;
                    }
                    else
                    {
                        model.AutoHideWidth += deltaExtent;
                    }
                }
                else
                {
                    if (model.AutoHideHeight == 0.0)
                    {
                        model.AutoHideHeight = content.ActualHeight + deltaExtent;
                    }
                    else
                    {
                        model.AutoHideHeight += deltaExtent;
                    }
                }
                UpdateExtent();
            }
            finally { EndResize(); }
        }
        else if (grid != null && resizer != null)
        {
            if (!PlatformServices.Coordinates.TryGetScreenBounds(resizer, out Rect rectangle))
            {
                EndResize();
                return;
            }
            double displacement = (candidateExtent - initialExtent) * scale * (side is AnchorSide.Right or AnchorSide.Bottom ? -1 : 1);
            rectangle = new Rect(rectangle.X + (vertical ? displacement : 0), rectangle.Y + (vertical ? 0 : displacement), rectangle.Width, rectangle.Height);
            Brush fill = resizer.BackgroundWhileDragging;
            double opacity = resizer.OpacityWhileDragging;
            if (rectangle != shownResizePreviewBounds || !ReferenceEquals(fill, shownResizePreviewFill)
                || opacity != shownResizePreviewOpacity)
            {
                resizePreviewWindow ??= new OverlayWindow(area, false);
                resizePreviewWindow.ShowResizePreview(rectangle, fill, opacity);
                shownResizePreviewBounds = rectangle;
                shownResizePreviewFill = fill;
                shownResizePreviewOpacity = opacity;
            }
        }
    }
    private void EndResize()
    {
        resizer?.CancelDrag();
        OverlayWindow? preview = resizePreviewWindow;
        resizePreviewWindow = null;
        shownResizePreviewBounds = null;
        shownResizePreviewFill = null;
        try
        {
            preview?.CloseHost();
        }
        finally { resizing = false; }
    }
    protected override void Dispose(bool disposing)
    {
        Hide();
        base.Dispose(disposing);
    }
}
