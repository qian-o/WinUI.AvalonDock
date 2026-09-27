// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), ToggleDockingManager.cs.
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace AvalonDock;

public enum DockLayoutPriority
{
    BottomFullWidth, SidesFullHeight, Default
}

public partial class ToggleDockingManager : DockingManager
{
    private readonly ToggleLayoutEngine layoutEngine = new();
    private readonly Dictionary<IToolbox, LayoutAnchorable> toolboxToAnchorable = [];
    private readonly Dictionary<LayoutAnchorable, DockZone> detachedZones = [];
    private int syncDepth;
    private bool settingUp;
    private bool refreshQueued;
    private LayoutRoot? observedLayout;
    internal ToggleDockButtonBar? leftTopBar, leftBottomBar, rightTopBar, rightBottomBar, bottomLeftBar, bottomRightBar;
    internal Grid? injectedLeftDockPanel, injectedRightDockPanel;
    private Grid? injectedRoot;
    internal FrameworkElement? leftSeparator, rightSeparator;
    private Button? hiddenButton;
    private readonly Style defaultPaneStyle;
    internal IEnumerable<ToggleDockButtonBar> Bars => new[] { leftTopBar, leftBottomBar, rightTopBar, rightBottomBar, bottomLeftBar, bottomRightBar }.OfType<ToggleDockButtonBar>();
    public override ILayoutEngine LayoutEngine => layoutEngine;

    public static readonly DependencyProperty LayoutPriorityProperty = DependencyProperty.Register(nameof(LayoutPriority), typeof(DockLayoutPriority), typeof(ToggleDockingManager), new PropertyMetadata(DockLayoutPriority.BottomFullWidth));
    public DockLayoutPriority LayoutPriority
    {
        get => (DockLayoutPriority)GetValue(LayoutPriorityProperty); set => SetValue(LayoutPriorityProperty, value);
    }
    public static readonly DependencyProperty ButtonSizeProperty = DependencyProperty.Register(nameof(ButtonSize), typeof(double), typeof(ToggleDockingManager), new PropertyMetadata(25d));
    public double ButtonSize
    {
        get => (double)GetValue(ButtonSizeProperty); set => SetValue(ButtonSizeProperty, value);
    }
    public static readonly DependencyProperty DefaultDockWidthProperty = DependencyProperty.Register(nameof(DefaultDockWidth), typeof(double), typeof(ToggleDockingManager), new PropertyMetadata(250d));
    public double DefaultDockWidth
    {
        get => (double)GetValue(DefaultDockWidthProperty); set => SetValue(DefaultDockWidthProperty, value);
    }
    public static readonly DependencyProperty DefaultDockHeightProperty = DependencyProperty.Register(nameof(DefaultDockHeight), typeof(double), typeof(ToggleDockingManager), new PropertyMetadata(200d));
    public double DefaultDockHeight
    {
        get => (double)GetValue(DefaultDockHeightProperty); set => SetValue(DefaultDockHeightProperty, value);
    }
    public static readonly DependencyProperty ShowHeaderMinimizeButtonProperty = DependencyProperty.Register(nameof(ShowHeaderMinimizeButton), typeof(bool), typeof(ToggleDockingManager), new PropertyMetadata(true));
    public bool ShowHeaderMinimizeButton
    {
        get => (bool)GetValue(ShowHeaderMinimizeButtonProperty); set => SetValue(ShowHeaderMinimizeButtonProperty, value);
    }
    public static readonly DependencyProperty ShowHeaderOptionsButtonProperty = DependencyProperty.Register(nameof(ShowHeaderOptionsButton), typeof(bool), typeof(ToggleDockingManager), new PropertyMetadata(true));
    public bool ShowHeaderOptionsButton
    {
        get => (bool)GetValue(ShowHeaderOptionsButtonProperty); set => SetValue(ShowHeaderOptionsButtonProperty, value);
    }

    public ToggleDockingManager()
    {
        DefaultStyleKey = typeof(ToggleDockingManager);
        BorderThickness = new Thickness(0);
        SupportsAutoHideFlyout = false;
        LayoutUpdateStrategy = new ToggleLayoutStrategy();
        ResourceDictionary defaults = new()
        {
            Source = new Uri("ms-appx:///WinUI.AvalonDock/Themes/Generic.xaml")
        };
        AnchorablePaneControlStyle = defaultPaneStyle = (Style)defaults["ToggleAnchorablePaneControlStyle"];
        Loaded += OnToggleLoaded;
        Unloaded += OnToggleUnloaded;
        ActiveContentChanged += OnToggleActiveContentChanged;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                Loaded -= OnToggleLoaded;
                Unloaded -= OnToggleUnloaded;
                ActiveContentChanged -= OnToggleActiveContentChanged;
                StopZoneDrag();
                ObserveLayout(null);
                RemoveToggleDockButtonBars();
                detachedZones.Clear();
                refreshQueued = false;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private void OnToggleLoaded(object sender, RoutedEventArgs args)
    {
        if (!IsDisposed)
        {
            ObserveLayout();
            ReapplyThemeStyles();
        }
    }

    private void OnToggleUnloaded(object sender, RoutedEventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        StopZoneDrag();
        RemoveToggleDockButtonBars();
        ObserveLayout(null);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsDisposed && IsLoaded && leftTopBar == null)
            {
                ObserveLayout();
                ReapplyThemeStyles();
            }
        });
    }

    private void OnToggleActiveContentChanged(object? sender, EventArgs args) => RefreshButtonStates();

    protected override void OnDockLayoutChanged(IRootDock? oldValue, IRootDock? newValue)
    {
        if (IsDisposed)
        {
            return;
        }

        base.OnDockLayoutChanged(oldValue, newValue);
        if (IsLoaded)
        {
            SetupToggleDockButtonBars();
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (IsDisposed || !IsLoaded)
                {
                    return;
                }

                ApplyInitialToolboxState();
                RefreshButtonStates();
                UpdatePinButtonsToMinimize();
            });
        }
    }
    protected override void OnLayoutChanged(LayoutRoot? oldLayout, LayoutRoot newLayout)
    {
        if (IsDisposed)
        {
            return;
        }

        List<LayoutAnchorable> restoreDocked = CollectDockedAnchorables(newLayout);
        base.OnLayoutChanged(oldLayout, newLayout);
        if (oldLayout == null || detachedZones == null)
        {
            return;
        }

        StopZoneDrag();
        detachedZones.Clear();
        ObserveLayout(IsLoaded ? newLayout : null);
        if (!IsLoaded)
        {
            RemoveToggleDockButtonBars();
            return;
        }
        SetupToggleDockButtonBars();
        foreach (LayoutAnchorable tool in restoreDocked)
        {
            if (ReferenceEquals(tool.Root, newLayout) && tool.IsAutoHidden)
            {
                ToggleAnchorable(tool, GetAnchorableZone(tool));
            }
        }

        SyncToolboxStateToLayout();
        RefreshButtonStates();
        QueuePinButtonUpdate();
    }
    protected override void OnThemeChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnThemeChanged(e);
        if (!IsDisposed && IsLoaded)
        {
            DispatcherQueue.TryEnqueue(ReapplyThemeStyles);
        }
    }
    protected override void OnApplyTemplate()
    {
        RemoveToggleDockButtonBars();
        base.OnApplyTemplate();
        if (!IsDisposed && IsLoaded)
        {
            ReapplyThemeStyles();
        }
    }
    private void ReapplyThemeStyles()
    {
        if (IsDisposed || !IsLoaded || settingUp)
        {
            return;
        }

        AnchorablePaneControlStyle = Resources.TryGetValue("ToggleAnchorablePaneControlStyle", out object? style) && style is Style paneStyle ? paneStyle
            : Application.Current.Resources.TryGetValue("ToggleAnchorablePaneControlStyle", out style) && style is Style applicationStyle ? applicationStyle : defaultPaneStyle;
        SetupToggleDockButtonBars();
        ApplyInitialToolboxState();
        RefreshButtonStates();
        QueuePinButtonUpdate();
    }
    public void ToggleAnchorable(LayoutAnchorable anchorable, DockZone zone)
    {
        if (IsDisposed)
        {
            return;
        }

        if (IsDetached(anchorable))
        {
            ActivateDetachedWindow(anchorable);
            SetToolboxIsOpen(anchorable);
            return;
        }
        if (anchorable.IsAutoHidden)
        {
            HideDockedInBar(GetBarForZone(zone));
            DockFromAutoHide(anchorable, zone);
            FixSplitOrientation(anchorable, zone);
            if (ToggleLayoutEngine.IsBottomZone(zone))
            {
                EnsureBottomZoneOrder();
            }

            if (LayoutPriority == DockLayoutPriority.BottomFullWidth)
            {
                EnsureBottomFullWidth();
            }
            else if (LayoutPriority == DockLayoutPriority.SidesFullHeight)
            {
                EnsureSidesFullHeight();
            }

            ActiveContent = anchorable.Content;
        }
        else
        {
            AutoHideFromDock(anchorable, zone);
        }

        RefreshButtonStates();
        QueuePinButtonUpdate();
        SetToolboxIsOpen(anchorable);
    }
    public void MoveAnchorableToZone(LayoutAnchorable anchorable, DockZone targetZone)
    {
        if (IsDisposed || anchorable == null)
        {
            return;
        }

        if (IsDetached(anchorable))
        {
            detachedZones[anchorable] = targetZone;
            return;
        }
        DockZone oldZone = GetAnchorableZone(anchorable);
        if (oldZone == targetZone)
        {
            if (anchorable.IsAutoHidden)
            {
                ToggleAnchorable(anchorable, targetZone);
            }
            return;
        }
        if (!anchorable.IsAutoHidden)
        {
            AutoHideFromDock(anchorable, oldZone);
        }

        if (anchorable.Parent is LayoutAnchorGroup oldGroup)
        {
            oldGroup.RemoveChild(anchorable);
        }

        LayoutAnchorGroup group = new();
        GetLayoutSideForZone(targetZone).Children.Add(group);
        group.Children.Add(anchorable);
        RemoveFromAllBars(anchorable);
        AddButton(anchorable, targetZone);
        ToggleAnchorable(anchorable, targetZone);
    }
    protected override object DetachFromLayout(LayoutAnchorable anchorable)
    {
        DockZone zone = GetAnchorableZone(anchorable);
        detachedZones[anchorable] = zone;
        if (!anchorable.IsAutoHidden)
        {
            AutoHideFromDock(anchorable, zone);
        }

        return zone;
    }
    protected override void ReturnToLayout(LayoutAnchorable anchorable, object? restoreState)
    {
        DockZone zone = detachedZones.TryGetValue(anchorable, out DockZone saved) ? saved : restoreState as DockZone? ?? GetAnchorableZone(anchorable);
        detachedZones.Remove(anchorable);
        if (anchorable.IsAutoHidden)
        {
            ToggleAnchorable(anchorable, zone);
        }
    }
    protected override FrameworkElement CreateDetachedWindowHeader(LayoutAnchorable anchorable) => new ToggleAnchorablePaneTitle { Model = anchorable };
    protected override void OnDetachedAnchorablesChanged(LayoutAnchorable anchorable)
    {
        SetToolboxIsOpen(anchorable);
        RefreshButtonStates();
    }
    internal override void ExecuteAutoHideCommand(LayoutAnchorable anchorable)
    {
        if (anchorable != null)
        {
            ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
        }
    }
    internal override void StartDraggingFloatingWindowForPane(LayoutAnchorablePane paneModel)
    {
        if (paneModel.Children.FirstOrDefault() is { } tool)
        {
            BeginZoneDrag(tool, this);
        }
        else
        {
            base.StartDraggingFloatingWindowForPane(paneModel);
        }
    }
    internal override void StartDraggingFloatingWindowForContent(LayoutContent contentModel, bool startDrag = true)
    {
        if (startDrag && contentModel is LayoutAnchorable tool)
        {
            BeginZoneDrag(tool, this);
        }
        else
        {
            base.StartDraggingFloatingWindowForContent(contentModel, startDrag);
        }
    }
    private void SetupToggleDockButtonBars()
    {
        if (IsDisposed || settingUp)
        {
            return;
        }

        settingUp = true;
        try
        {
            RemoveToggleDockButtonBars();
            HideOrdinarySides();
            foreach (LayoutAnchorable? tool in Layout.Descendents().OfType<LayoutAnchorable>().Where(a => a.Parent is LayoutAnchorablePane && !a.IsFloating).ToList())
            {
                tool.ToggleSingleAutoHide();
            }

            leftTopBar = Bar(DockZone.LeftTop);
            leftBottomBar = Bar(DockZone.LeftBottom);
            rightTopBar = Bar(DockZone.RightTop);
            rightBottomBar = Bar(DockZone.RightBottom);
            bottomLeftBar = Bar(DockZone.BottomLeft);
            bottomRightBar = Bar(DockZone.BottomRight);
            foreach (LayoutAnchorSide side in new[] { Layout.LeftSide, Layout.RightSide, Layout.BottomSide }.OfType<LayoutAnchorSide>())
            {
                foreach (LayoutAnchorable tool in CollectAnchorables(side))
                {
                    DockZone zone = InitialZone(tool, side.Side);
                    GetBarForZone(zone)?.Items.Add(new ToggleDockButton { Anchorable = tool, Zone = zone });
                }
            }
            // The original SetAnchorables populates all six bars before registering
            // toolboxes. Registration order is bar order, including duplicate shortcuts.
            RegisterToolboxesFromBars();
            Grid? root = GetTemplateChild("PART_ToggleNavigationGrid") as Grid
                ?? Visuals<Grid>(this).FirstOrDefault();
            if (root == null)
            {
                return;
            }

            injectedRoot = root;
            injectedLeftDockPanel = SidePanel(leftTopBar, leftBottomBar, bottomLeftBar, true);
            injectedRightDockPanel = SidePanel(rightTopBar, rightBottomBar, bottomRightBar, false);
            Grid.SetRow(injectedLeftDockPanel, 0);
            Grid.SetRowSpan(injectedLeftDockPanel, 3);
            Grid.SetColumn(injectedLeftDockPanel, 0);
            Grid.SetRow(injectedRightDockPanel, 0);
            Grid.SetRowSpan(injectedRightDockPanel, 3);
            Grid.SetColumn(injectedRightDockPanel, 2);
            root.Children.Add(injectedLeftDockPanel);
            root.Children.Add(injectedRightDockPanel);
            UpdateNavigationPanelVisibility();
            RefreshShortcuts();
        }
        finally { settingUp = false; }
    }
    private ToggleDockButtonBar Bar(DockZone zone)
    {
        ToggleDockButtonBar bar = new()
        {
            Zone = zone,
            Orientation = Orientation.Vertical
        };
        AutomationProperties.SetAutomationId(bar, "ToggleDockBar_" + zone);
        return bar;
    }
    private Grid SidePanel(ToggleDockButtonBar top, ToggleDockButtonBar middle, ToggleDockButtonBar bottom, bool left)
    {
        Grid grid = new()
        {
            Margin = new Thickness(4),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        foreach (GridLength height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = height });
        }

        Border separator = new()
        {
            Height = 1,
            Margin = new Thickness(4, 6, 4, 6),
            Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
        };
        if (left)
        {
            leftSeparator = separator;
        }
        else
        {
            rightSeparator = separator;
        }

        grid.Children.Add(top);
        grid.Children.Add(separator);
        Grid.SetRow(separator, 1);
        grid.Children.Add(middle);
        Grid.SetRow(middle, 2);
        grid.Children.Add(bottom);
        Grid.SetRow(bottom, 5);
        if (left)
        {
            hiddenButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE712", FontSize = 14 },
                Width = ButtonSize,
                Height = ButtonSize,
                MinWidth = 0,
                MinHeight = 0,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                IsTabStop = false
            };
            if (Resources.TryGetValue("AvalonDockChromeButtonStyle", out object? chromeStyle)
                || Application.Current.Resources.TryGetValue("AvalonDockChromeButtonStyle", out chromeStyle))
            {
                hiddenButton.Style = chromeStyle as Style;
            }
            hiddenButton.SetBinding(WidthProperty, new Binding { Source = this, Path = new PropertyPath(nameof(ButtonSize)) });
            hiddenButton.SetBinding(HeightProperty, new Binding { Source = this, Path = new PropertyPath(nameof(ButtonSize)) });
            ToolTipService.SetToolTip(hiddenButton, "Show Hidden Tool Windows");
            hiddenButton.Click += (_, _) => ShowHiddenMenu(hiddenButton);
            grid.Children.Add(hiddenButton);
            Grid.SetRow(hiddenButton, 3);
        }
        return grid;
    }
    private static DockZone InitialZone(LayoutAnchorable tool, AnchorSide side) => side switch
    {
        AnchorSide.Left => tool.Content is IToolbox { Zone: DockZone.LeftBottom } ? DockZone.LeftBottom : DockZone.LeftTop,
        AnchorSide.Right => tool.Content is IToolbox { Zone: DockZone.RightBottom } ? DockZone.RightBottom : DockZone.RightTop,
        _ => tool.Content is IToolbox { Zone: DockZone.BottomRight } ? DockZone.BottomRight : DockZone.BottomLeft
    };
    private void HideOrdinarySides()
    {
        foreach (LayoutAnchorSideControl? side in new[] { LeftSidePanel, RightSidePanel, TopSidePanel, BottomSidePanel })
        {
            if (side != null)
            {
                side.Visibility = Visibility.Collapsed;
            }
        }
    }
    private void RemoveToggleDockButtonBars()
    {
        if (toolboxToAnchorable == null)
        {
            return;
        }

        foreach (IToolbox? toolbox in toolboxToAnchorable.Keys.ToArray())
        {
            UnregisterToolbox(toolbox);
        }

        RemoveShortcuts();
        foreach (ToggleDockButtonBar bar in Bars)
        {
            foreach (ToggleDockButton button in bar.Items.OfType<ToggleDockButton>())
            {
                button.Release();
            }
            bar.Items.Clear();
        }
        if (injectedLeftDockPanel != null)
        {
            injectedRoot?.Children.Remove(injectedLeftDockPanel);
        }

        if (injectedRightDockPanel != null)
        {
            injectedRoot?.Children.Remove(injectedRightDockPanel);
        }

        leftTopBar = leftBottomBar = rightTopBar = rightBottomBar = bottomLeftBar = bottomRightBar = null;
        injectedLeftDockPanel = injectedRightDockPanel = null;
        leftSeparator = rightSeparator = null;
        injectedRoot = null;
        hiddenButton = null;
    }
    private void AddButton(LayoutAnchorable tool, DockZone zone)
    {
        ToggleDockButtonBar? bar = GetBarForZone(zone);
        if (bar == null || bar.ContainsAnchorable(tool))
        {
            return;
        }

        bar.Items.Add(new ToggleDockButton { Anchorable = tool, Zone = zone });
        if (tool.Content is IToolbox toolbox)
        {
            RegisterToolbox(toolbox, tool);
        }
        UpdateNavigationPanelVisibility();
    }
    internal void RemoveButtonFromAllBars(LayoutAnchorable anchorable) => RemoveFromAllBars(anchorable);
    private void RemoveFromAllBars(LayoutAnchorable anchorable)
    {
        foreach (ToggleDockButtonBar bar in Bars)
        {
            foreach (ToggleDockButton? button in bar.Items.OfType<ToggleDockButton>().Where(item => ReferenceEquals(item.Anchorable, anchorable)).ToArray())
            {
                button.Release();
                bar.Items.Remove(button);
            }
        }
        UpdateNavigationPanelVisibility();
    }
    private void UpdateNavigationPanelVisibility()
    {
        if (injectedLeftDockPanel != null)
        {
            injectedLeftDockPanel.Visibility = Visibility.Visible;
        }

        if (injectedRightDockPanel != null)
        {
            injectedRightDockPanel.Visibility = new[] { rightTopBar, rightBottomBar, bottomRightBar }
                .OfType<ToggleDockButtonBar>().Any(bar => bar.Items.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    private void ObserveLayout() => ObserveLayout(Layout);
    private void ObserveLayout(LayoutRoot? layout)
    {
        if (observedLayout != null)
        {
            observedLayout.Updated -= OnToggleLayoutUpdated;
            observedLayout.ElementAdded -= OnToggleElementChanged;
            observedLayout.ElementRemoved -= OnToggleElementChanged;
        }
        observedLayout = IsDisposed ? null : layout;
        if (observedLayout != null)
        {
            observedLayout.Updated += OnToggleLayoutUpdated;
            observedLayout.ElementAdded += OnToggleElementChanged;
            observedLayout.ElementRemoved += OnToggleElementChanged;
        }
    }
    private void OnToggleElementChanged(object? sender, LayoutElementEventArgs args) => OnToggleLayoutUpdated(sender, args);
    private void OnToggleLayoutUpdated(object? sender, EventArgs e)
    {
        if (IsDisposed || settingUp || refreshQueued || !IsLoaded)
        {
            return;
        }

        refreshQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            refreshQueued = false;
            if (IsDisposed || !IsLoaded || leftTopBar == null)
            {
                return;
            }

            HideOrdinarySides();
            HashSet<LayoutAnchorable> currentTools = Layout.Descendents().OfType<LayoutAnchorable>().ToHashSet();
            foreach (ToggleDockButtonBar bar in Bars)
            {
                foreach (ToggleDockButton? button in bar.Items.OfType<ToggleDockButton>().Where(b => b.Anchorable is not { } tool || !currentTools.Contains(tool) || tool.IsHidden).ToArray())
                {
                    if (button.Anchorable?.Content is IToolbox toolbox)
                    {
                        UnregisterToolbox(toolbox);
                    }

                    button.Release();
                    bar.Items.Remove(button);
                }
            }

            foreach (LayoutAnchorSide side in new[] { Layout.LeftSide, Layout.RightSide, Layout.BottomSide }.OfType<LayoutAnchorSide>())
            {
                foreach (LayoutAnchorable tool in CollectAnchorables(side))
                {
                    if (!Bars.Any(bar => bar.ContainsAnchorable(tool)))
                    {
                        AddButton(tool, InitialZone(tool, side.Side));
                    }
                }
            }

            UpdateNavigationPanelVisibility();
            RefreshButtonStates();
        });
    }
    internal static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (T nested in Visuals<T>(child))
            {
                yield return nested;
            }
        }
    }

    private void QueuePinButtonUpdate()
    {
        if (!IsDisposed)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, UpdatePinButtonsToMinimize);
        }
    }

    private void UpdatePinButtonsToMinimize()
    {
        if (IsDisposed || !IsLoaded)
        {
            return;
        }

        foreach (AnchorablePaneTitle title in Visuals<AnchorablePaneTitle>(this))
        {
            // The source leaves the specialized title's own template buttons alone.
            if (title is ToggleAnchorablePaneTitle)
            {
                continue;
            }

            foreach (Button button in Visuals<Button>(title))
            {
                if (button.Name == "PART_AutoHidePin")
                {
                    ToolTipService.SetToolTip(button, "Minimize");
                    if (button.Content is not Border border)
                    {
                        border = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
                        button.Content = border;
                    }
                    border.Child = CreateMinimizeIcon();
                }
                else if (button.Name == "PART_HidePin")
                {
                    button.Visibility = Visibility.Collapsed;
                }
            }

            foreach (Controls.DropDownButton dropDown in Visuals<AvalonDock.Controls.DropDownButton>(title))
            {
                dropDown.Visibility = Visibility.Collapsed;
            }

            Grid? grid = Visuals<Grid>(title).FirstOrDefault();
            if (grid == null || grid.Children.OfType<Button>().Any(button => button.Name == "PART_ToggleMenu"))
            {
                continue;
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Button? autoHide = grid.Children.OfType<Button>().FirstOrDefault(button => button.Name == "PART_AutoHidePin");
            if (autoHide != null)
            {
                Grid.SetColumn(autoHide, grid.ColumnDefinitions.Count - 1);
            }

            Button menu = CreateThreeDotMenuButton(title);
            Grid.SetColumn(menu, 2);
            grid.Children.Add(menu);
        }
    }

    private static UIElement CreateMinimizeIcon() => new Microsoft.UI.Xaml.Shapes.Path
    {
        Data = new LineGeometry { StartPoint = new Windows.Foundation.Point(2, 11), EndPoint = new Windows.Foundation.Point(11, 11) },
        Stroke = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x55, 0x55, 0x55)),
        StrokeThickness = 1.5,
        Width = 13,
        Height = 13,
        Stretch = Stretch.None
    };

    private Button CreateThreeDotMenuButton(AnchorablePaneTitle title)
    {
        Microsoft.UI.Xaml.Shapes.Path ellipsis = (Microsoft.UI.Xaml.Shapes.Path)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <Path xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  Data="M64 360a56 56 0 1 0 0 112 56 56 0 1 0 0-112zm0-160a56 56 0 1 0 0 112 56 56 0 1 0 0-112zM120 96A56 56 0 1 0 8 96a56 56 0 1 0 112 0z" />
            """);
        ellipsis.Stretch = Stretch.Uniform;
        ellipsis.Width = 4;
        ellipsis.Height = 14;
        ellipsis.HorizontalAlignment = HorizontalAlignment.Center;
        ellipsis.VerticalAlignment = VerticalAlignment.Center;
        Button button = new()
        {
            Name = "PART_ToggleMenu",
            Content = ellipsis,
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 2, 0),
            IsTabStop = false
        };
        ellipsis.SetBinding(Microsoft.UI.Xaml.Shapes.Shape.FillProperty,
            new Binding { Source = button, Path = new PropertyPath(nameof(Button.Foreground)) });
        ToolTipService.SetToolTip(button, "Options");
        button.Click += (_, _) =>
        {
            if (title.Model is { } model)
            {
                BuildToggleContextMenu(model).ShowAt(button, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
            }
        };
        return button;
    }
}
