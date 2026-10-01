// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/ToggleDockButtonBar.cs.
using System.ComponentModel;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

public class ToggleDockButton : ToggleButton
{
    // Native ResourceDictionary accepts string/type keys rather than WPF ComponentResourceKey.
    public static readonly string ForegroundBrushKey = "AvalonDock.Controls.ToggleDockButton.ForegroundBrush";
    private bool suppressClick;
    private bool released;
    private bool pressed;
    private bool iconRefreshQueued;
    private Point pressPoint;
    private ContentPresenter? iconHost;
    private Image? imageHost;
    private ContentPresenter? textHost;
    private readonly RotatedHeader rotatedHeader = new();
    private readonly RoutedEventHandler clickHandler;
    private ToggleDockingManager? observedManager;
    private LayoutAnchorable? observedAnchorable;
    private readonly List<(DependencyProperty Property, long Token)> managerTokens = [];
    public ToggleDockButton()
    {
        DefaultStyleKey = typeof(ToggleDockButton);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/Toggle.xaml");
        Loaded += (_, _) => Refresh();
        clickHandler = (_, _) => OnClick();
        Click += clickHandler;
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, args) => { if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { OnMouseLeftButtonDown(args); } }), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler((_, args) =>
        {
            if (args.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased)
            {
                OnMouseRightButtonUp(args);
            }
            else
            {
                OnMouseLeftButtonUp(args);
            }
        }), true);
        PointerMoved += (_, args) => OnMouseMove(args);
        foreach (DependencyProperty? property in new[] { IconContentProperty, IconTemplateProperty, IconSourceProperty })
        {
            RegisterPropertyChangedCallback(property, (_, _) => RefreshIcon());
        }

        RegisterPropertyChangedCallback(IsAnchorableFocusedProperty, (_, _) => VisualStateManager.GoToState(this, IsAnchorableFocused ? "AnchorableFocused" : "AnchorableUnfocused", false));
        RegisterPropertyChangedCallback(TemplateProperty, (_, _) =>
        {
            if (textHost != null)
            {
                textHost.Content = null;
            }

            if (iconHost != null)
            {
                iconHost.Content = null;
            }

            iconHost = null;
            imageHost = null;
            textHost = null;
            if (iconRefreshQueued)
            {
                return;
            }

            iconRefreshQueued = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                iconRefreshQueued = false;
                if (IsLoaded)
                {
                    RefreshIcon();
                }
            });
        });
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => RefreshIcon());
    }
    public static readonly DependencyProperty AnchorableProperty = DependencyProperty.Register(nameof(Anchorable), typeof(LayoutAnchorable), typeof(ToggleDockButton), new PropertyMetadata(null, (owner, _) => ((ToggleDockButton)owner).Refresh()));
    public LayoutAnchorable? Anchorable
    {
        get => (LayoutAnchorable?)GetValue(AnchorableProperty); set => SetValue(AnchorableProperty, value);
    }
    public static readonly DependencyProperty ZoneProperty = DependencyProperty.Register(nameof(Zone), typeof(DockZone), typeof(ToggleDockButton),
        new PropertyMetadata(DockZone.LeftTop, (owner, _) => ((ToggleDockButton)owner).UpdateZoneState()));
    public DockZone Zone
    {
        get => (DockZone)GetValue(ZoneProperty); set => SetValue(ZoneProperty, value);
    }
    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(nameof(IconSource), typeof(ImageSource), typeof(ToggleDockButton), new PropertyMetadata(null));
    public ImageSource? IconSource
    {
        get => (ImageSource?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value);
    }
    public static readonly DependencyProperty IconContentProperty = DependencyProperty.Register(nameof(IconContent), typeof(object), typeof(ToggleDockButton), new PropertyMetadata(null));
    public object? IconContent
    {
        get => GetValue(IconContentProperty); set => SetValue(IconContentProperty, value);
    }
    public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(ToggleDockButton), new PropertyMetadata(null));
    public DataTemplate? IconTemplate
    {
        get => (DataTemplate?)GetValue(IconTemplateProperty); set => SetValue(IconTemplateProperty, value);
    }
    public static readonly DependencyProperty IsAnchorableFocusedProperty = DependencyProperty.Register(nameof(IsAnchorableFocused), typeof(bool), typeof(ToggleDockButton), new PropertyMetadata(false));
    public bool IsAnchorableFocused
    {
        get => (bool)GetValue(IsAnchorableFocusedProperty); set => SetValue(IsAnchorableFocusedProperty, value);
    }
    protected virtual void OnClick()
    {
        if (suppressClick)
        {
            suppressClick = false;
            return;
        }
        if (!released && Anchorable?.Root?.Manager is ToggleDockingManager manager)
        {
            manager.ToggleAnchorable(Anchorable, Zone);
        }
    }
    protected override void OnToggle()
    {
        // WinUI dispatches Click after this hook; keep the source OnClick after consumer handlers.
        Click -= clickHandler;
        Click += clickHandler;
        base.OnToggle();
    }
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateZoneState();
        VisualStateManager.GoToState(this, IsAnchorableFocused ? "AnchorableFocused" : "AnchorableUnfocused", false);
        RefreshIcon();
    }
    protected virtual void OnMouseLeftButtonDown(PointerRoutedEventArgs e)
    {
        suppressClick = false;
        pressed = true;
        pressPoint = e.GetCurrentPoint(this).Position;
    }
    protected virtual void OnMouseLeftButtonUp(PointerRoutedEventArgs e) => pressed = false;
    protected virtual void OnMouseMove(PointerRoutedEventArgs e)
    {
        if (!pressed || released || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point point = e.GetCurrentPoint(this).Position;
        Size threshold = PlatformServices.PointerGestures.GetDragThreshold(this);
        double scale = XamlRoot.RasterizationScale;
        if (Math.Abs(point.X - pressPoint.X) * scale <= threshold.Width && Math.Abs(point.Y - pressPoint.Y) * scale <= threshold.Height)
        {
            return;
        }

        pressed = false;
        suppressClick = true;
        ReleasePointerCaptures();
        if (FindParent<ToggleDockButtonBar>(this) == null)
        {
            return;
        }

        if (Anchorable?.Root?.Manager is ToggleDockingManager manager)
        {
            manager.BeginZoneDrag(Anchorable, this, pressPosition: pressPoint);
        }
    }
    internal static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T match)
            {
                return match;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }
    protected virtual void OnMouseRightButtonUp(PointerRoutedEventArgs e)
    {
        if (Anchorable?.Root?.Manager is not ToggleDockingManager manager)
        {
            return;
        }

        MenuFlyout menu = manager.BuildToggleContextMenu(Anchorable);
        MenuFlyoutItem hide = new()
        {
            Text = global::AvalonDock.Properties.Resources.Anchorable_Hide,
            IsEnabled = (manager.GetLayoutItemFromModel(Anchorable) as LayoutAnchorableItem)?.HideCommand?.CanExecute(null) == true
        };
        hide.Click += (_, _) => manager.HideAnchorableFromMenu(Anchorable);
        menu.Items.Insert(0, hide);
        menu.Items.Insert(1, new MenuFlyoutSeparator());
        menu.ShowAt(this);
        e.Handled = true;
    }
    internal void Refresh()
    {
        if (!ReferenceEquals(observedAnchorable, Anchorable))
        {
            if (observedAnchorable != null)
            {
                observedAnchorable.PropertyChanged -= OnAnchorablePropertyChanged;
            }
            observedAnchorable = released ? null : Anchorable;
            if (observedAnchorable != null)
            {
                observedAnchorable.PropertyChanged += OnAnchorablePropertyChanged;
            }
        }

        if (released || Anchorable == null)
        {
            return;
        }

        LayoutAnchorable tool = Anchorable;
        Content = tool.Title;
        AutomationProperties.SetName(this, tool.Title ?? string.Empty);
        IsChecked = !tool.IsAutoHidden;
        IconContent = ToggleDock.GetIcon(tool) ?? (tool.Content as IToolbox)?.Icon;
        IconSource = IconContent == null ? tool.IconSource : null;
        IconTemplate = ToggleDock.GetIconTemplate(tool);
        object? tip = ToggleDock.GetToolTip(tool) ?? tool.Title;
        ToolTipService.SetToolTip(this, tool.Content is IToolbox toolbox && !string.IsNullOrWhiteSpace(toolbox.Shortcut) ? $"{tip} ({toolbox.Shortcut})" : tip);
        if (tool.Root?.Manager is ToggleDockingManager manager)
        {
            if (!ReferenceEquals(manager, observedManager))
            {
                ClearManager();
                observedManager = manager;
                foreach (DependencyProperty? property in new[] { DockingManager.AnchorableHeaderTemplateProperty, DockingManager.AnchorableHeaderTemplateSelectorProperty })
                {
                    managerTokens.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) => RefreshIcon())));
                }
            }
            SetBinding(WidthProperty, new Binding { Source = manager, Path = new PropertyPath(nameof(manager.ButtonSize)) });
            SetBinding(MinHeightProperty, new Binding { Source = manager, Path = new PropertyPath(nameof(manager.ButtonSize)) });
            if (manager.Resources.TryGetValue(ForegroundBrushKey, out object? resource) && resource is Brush brush)
            {
                Foreground = brush;
            }
        }
        RefreshIcon();
        UpdateZoneState();
    }
    private void OnAnchorablePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is null or "" or nameof(LayoutContent.Title) or nameof(LayoutContent.IconSource) or nameof(LayoutContent.Content))
        {
            Refresh();
        }
    }
    private void UpdateZoneState() => VisualStateManager.GoToState(this,
        Zone is DockZone.RightTop or DockZone.RightBottom or DockZone.BottomRight ? "IndicatorRight" : "IndicatorLeft", false);
    private void RefreshIcon()
    {
        if (released || !IsLoaded)
        {
            return;
        }

        iconHost ??= GetTemplateChild("PART_IconContent") as ContentPresenter;
        imageHost ??= GetTemplateChild("PART_Icon") as Image;
        textHost ??= GetTemplateChild("PART_Text") as ContentPresenter;
        if (iconHost != null)
        {
            if (!ReferenceEquals(iconHost.Content, IconContent))
            {
                iconHost.Content = IconContent;
            }

            if (!ReferenceEquals(iconHost.ContentTemplate, IconTemplate))
            {
                iconHost.ContentTemplate = IconTemplate;
            }

            Visibility visibility = IconContent == null ? Visibility.Collapsed : Visibility.Visible;
            if (iconHost.Visibility != visibility)
            {
                iconHost.Visibility = visibility;
            }
        }
        if (imageHost != null)
        {
            if (!ReferenceEquals(imageHost.Source, IconSource))
            {
                imageHost.Source = IconSource;
            }

            Visibility visibility = IconContent == null && IconSource != null ? Visibility.Visible : Visibility.Collapsed;
            if (imageHost.Visibility != visibility)
            {
                imageHost.Visibility = visibility;
            }
        }
        if (textHost != null)
        {
            if (!ReferenceEquals(textHost.Content, rotatedHeader))
            {
                textHost.Content = rotatedHeader;
            }

            DataTemplate? template = observedManager?.AnchorableHeaderTemplate;
            DataTemplateSelector? selector = observedManager?.AnchorableHeaderTemplateSelector;
            object? content = template != null || selector != null ? Anchorable : Content;
            if (!ReferenceEquals(rotatedHeader.Presenter.ContentTemplate, template))
            {
                rotatedHeader.Presenter.ContentTemplate = template;
            }

            if (!ReferenceEquals(rotatedHeader.Presenter.ContentTemplateSelector, selector))
            {
                rotatedHeader.Presenter.ContentTemplateSelector = selector;
            }

            if (!Equals(rotatedHeader.Presenter.Content, content))
            {
                rotatedHeader.Presenter.Content = content;
            }

            if (!ReferenceEquals(rotatedHeader.Presenter.Foreground, Foreground))
            {
                rotatedHeader.Presenter.Foreground = Foreground;
            }

            Visibility visibility = IconContent == null && IconSource == null ? Visibility.Visible : Visibility.Collapsed;
            if (textHost.Visibility != visibility)
            {
                textHost.Visibility = visibility;
            }
        }
    }
    internal void Release()
    {
        released = true;
        if (observedAnchorable != null)
        {
            observedAnchorable.PropertyChanged -= OnAnchorablePropertyChanged;
            observedAnchorable = null;
        }
        ClearManager();
        if (iconHost != null)
        {
            iconHost.Content = null;
        }

        if (textHost != null)
        {
            textHost.Content = null;
        }

        IconContent = null;
        IconSource = null;
        ClearValue(WidthProperty);
        ClearValue(MinHeightProperty);
    }
    private void ClearManager()
    {
        if (observedManager != null)
        {
            foreach ((DependencyProperty Property, long Token) token in managerTokens)
            {
                observedManager.UnregisterPropertyChangedCallback(token.Property, token.Token);
            }
        }

        managerTokens.Clear();
        observedManager = null;
    }
    private sealed class RotatedHeader : Panel
    {
        internal ContentPresenter Presenter { get; } = new();
        private readonly CompositeTransform rotation = new() { Rotation = -90 };
        internal RotatedHeader()
        {
            Presenter.RenderTransform = rotation;
            Children.Add(Presenter);
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            Presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Size(Presenter.DesiredSize.Height, Presenter.DesiredSize.Width);
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            Size size = Presenter.DesiredSize;
            Presenter.Arrange(new Rect(0, 0, size.Width, size.Height));
            if (rotation.TranslateY != size.Width)
            {
                rotation.TranslateY = size.Width;
            }
            return finalSize;
        }
    }
}
