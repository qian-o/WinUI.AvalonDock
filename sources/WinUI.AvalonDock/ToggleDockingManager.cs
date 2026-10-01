// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), ToggleDockingManager.cs.
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

public enum DockLayoutPriority
{
    BottomFullWidth, SidesFullHeight, Default
}

public partial class ToggleDockingManager : DockingManager
{
    private readonly ToggleLayoutEngine layoutEngine = new();
    public override ILayoutEngine LayoutEngine => layoutEngine;

    public static readonly DependencyProperty LayoutPriorityProperty = DependencyProperty.Register(nameof(LayoutPriority), typeof(DockLayoutPriority), typeof(ToggleDockingManager),
        new PropertyMetadata(DockLayoutPriority.BottomFullWidth, (owner, _) => ((ToggleDockingManager)owner).ApplyLayoutPriority()));
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
                anchorableZones.Clear();
                initializedToolboxes.Clear();
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
        if (oldLayout == null)
        {
            return;
        }

        StopZoneDrag();
        detachedZones.Clear();
        anchorableZones.Clear();
        initializedToolboxes.Clear();
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
}
