// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/LayoutAnchorControl.cs.
using System.ComponentModel;
using AvalonDock.Compatibility;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

public class LayoutAnchorControl : Control, ILayoutControl
{
    private readonly LayoutAnchorable model;
    private DockingManager? manager;
    private DispatcherQueueTimer? openUpTimer;
    private readonly List<(DependencyProperty Property, long Token)> templateTokens = [];
    private bool attached;
    private bool released;
    private AnchorLabel? label;
    internal LayoutAnchorControl(LayoutAnchorable model)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        DefaultStyleKey = typeof(LayoutAnchorControl);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/AutoHide.xaml");
        IsTabStop = true;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && PlatformServices.PointerGestures.RegisterPrimaryPress(this) == 2)
            {
                OnMouseDoubleClick(e);
            }

            OnMouseDown(e);
        };
        PointerReleased += (_, e) => { if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased) { OnMouseRightButtonUp(e); } };
        PointerEntered += (_, e) => { VisualStateManager.GoToState(this, "PointerOver", false); OnMouseEnter(e); };
        PointerExited += (_, e) => { VisualStateManager.GoToState(this, "Normal", false); OnMouseLeave(e); };
        this.model.IsActiveChanged += OnModelIsActiveChanged;
        this.model.IsSelectedChanged += OnModelIsSelectedChanged;
        Refresh();
    }
    public ILayoutElement Model => model;
    public static readonly DependencyProperty SideProperty = ReadOnlyPropertyGuard.Register(nameof(Side), typeof(AnchorSide), typeof(LayoutAnchorControl), AnchorSide.Left);
    [System.ComponentModel.Bindable(true)]
    [Description("Gets the anchor side of the control.")]
    [Category("Anchor")]
    public AnchorSide Side => (AnchorSide)GetValue(SideProperty);
    protected void SetSide(AnchorSide value) => ReadOnlyPropertyGuard.Set(this, SideProperty, value);
    public new void SetValue(DependencyProperty dp, object value)
    {
        ReadOnlyPropertyGuard.VerifyWritable(dp, SideProperty);
        base.SetValue(dp, value);
    }
    public new void ClearValue(DependencyProperty dp)
    {
        ReadOnlyPropertyGuard.VerifyWritable(dp, SideProperty);
        base.ClearValue(dp);
    }
    public new void SetBinding(DependencyProperty dp, Microsoft.UI.Xaml.Data.BindingBase binding)
    {
        ReadOnlyPropertyGuard.VerifyWritable(dp, SideProperty);
        base.SetBinding(dp, binding);
    }
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        label = null;
        if (GetTemplateChild("PART_LabelHost") is ContentPresenter host)
        {
            host.Content = label = new AnchorLabel();
        }

        Refresh();
        VisualStateManager.GoToState(this, "Normal", false);
    }
    protected virtual void OnMouseDown(PointerRoutedEventArgs e)
    {

        if (!e.Handled && model.Root?.Manager is { } manager)
        {
            manager.ShowAutoHideWindow(this);
            model.IsActive = true;
            // WinUI does not move focus into a new child XamlRoot from model activation.
            manager.AutoHideWindow?.FocusContent();
        }
    }

    protected virtual void OnMouseDoubleClick(PointerRoutedEventArgs e)
    {

        if (!e.Handled && e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed
            && model.Root?.Manager is { AllowAnchorDoubleClickDock: true } manager)
        {
            manager.ExecuteAutoHideCommand(model);
            e.Handled = true;
        }
    }

    protected virtual void OnMouseRightButtonUp(PointerRoutedEventArgs e)
    {

        if (!e.Handled)
        {
            DockingManager? manager = model.Root?.Manager;
            if (manager == null || !manager.AllowAnchorRightClickContextMenu)
            {
                return;
            }

            LayoutItem? layoutItem = manager.GetLayoutItemFromModel(model);
            MenuFlyout? contextMenu = manager.AnchorableContextMenu;
            if (contextMenu == null || layoutItem == null)
            {
                return;
            }

            Point position = e.GetCurrentPoint(this).Position;
            // MenuFlyout opens after native pointer release; original item ownership is unchanged.
            DispatcherQueue.TryEnqueue(() =>
            {
                if (IsLoaded)
                {
                    MenuFlyoutContext.Show(contextMenu, this, layoutItem,
                    new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = position });
                }
            });
            e.Handled = true;
        }
    }

    protected virtual void OnMouseEnter(PointerRoutedEventArgs e)
    {

        // If the model wants to auto-show itself on hover then initiate the show action
        if (!e.Handled && model.CanShowOnHover)
        {
            openUpTimer = DispatcherQueue.CreateTimer();
            openUpTimer.Interval = TimeSpan.FromMilliseconds(400);
            openUpTimer.Tick += OnOpenUpTimerTick;
            openUpTimer.Start();
        }
    }

    protected virtual void OnMouseLeave(PointerRoutedEventArgs e)
    {
        if (openUpTimer != null)
        {
            openUpTimer.Tick -= OnOpenUpTimerTick;
            openUpTimer.Stop();
            openUpTimer = null;
        }

    }

    private void OnModelIsSelectedChanged(object? sender, EventArgs e)
    {
        if (!model.IsAutoHidden)
        {
            model.IsSelectedChanged -= new EventHandler(OnModelIsSelectedChanged);
        }
        else if (model.IsSelected)
        {
            if (CanShowAutoHideWindow())
            {
                model.Root?.Manager?.ShowAutoHideWindow(this);
            }

            model.IsSelected = false;
        }
    }

    private void OnModelIsActiveChanged(object? sender, EventArgs e)
    {
        if (!model.IsAutoHidden)
        {
            model.IsActiveChanged -= new EventHandler(OnModelIsActiveChanged);
        }
        else if (model.IsActive && CanShowAutoHideWindow())
        {
            model.Root?.Manager?.ShowAutoHideWindow(this);
        }
    }

    private bool CanShowAutoHideWindow()
    {
        DockingManager? manager = model.Root?.Manager;
        if (manager == null || !manager.SupportsAutoHideFlyout)
        {
            return false;
        }

        return model.Parent?.Parent is LayoutAnchorSide;
    }

    private void OnOpenUpTimerTick(DispatcherQueueTimer sender, object e)
    {
        sender.Tick -= OnOpenUpTimerTick;
        sender.Stop();
        if (ReferenceEquals(openUpTimer, sender))
        {
            openUpTimer = null;
        }

        model.Root?.Manager?.ShowAutoHideWindow(this);
    }
    internal void ReleaseView()
    {
        released = true;
        Detach();
        model.IsActiveChanged -= OnModelIsActiveChanged;
        model.IsSelectedChanged -= OnModelIsSelectedChanged;
    }
    private void Attach()
    {
        if (attached || released)
        {
            return;
        }

        attached = true;
        model.PropertyChanged += OnModelChanged;
        manager = model.Root?.Manager;
        if (manager != null)
        {
            foreach (DependencyProperty? property in new[] { DockingManager.AnchorableHeaderTemplateProperty, DockingManager.AnchorableHeaderTemplateSelectorProperty })
            {
                templateTokens.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) => Refresh())));
            }
        }
        Refresh();
        if (model.IsSelected)
        {
            OnModelIsSelectedChanged(model, EventArgs.Empty);
        }

        if (model.IsActive)
        {
            OnModelIsActiveChanged(model, EventArgs.Empty);
        }
    }
    private void Detach()
    {
        StopOpenTimer();
        if (attached)
        {
            model.PropertyChanged -= OnModelChanged;
            attached = false;
        }
        if (manager != null)
        {
            foreach ((DependencyProperty? property, long token) in templateTokens)
            {
                manager.UnregisterPropertyChangedCallback(property, token);
            }
        }

        templateTokens.Clear();
        label?.Clear();
        // WinUI unloads every old anchor during template/root replacement. Only the
        // currently hosted anchor may enter the original manager's specific-hide path.
        if (ReferenceEquals(manager?.AutoHideWindow?.Model, model))
        {
            manager.HideAutoHideWindow(this);
        }

        manager = null;
    }
    private void Refresh()
    {
        SetSide(model.FindParent<LayoutAnchorSide>()?.Side ?? AnchorSide.Left);
        VisualStateManager.GoToState(this, "Side" + Side, false);
        AutomationProperties.SetName(this, model.Title ?? string.Empty);
        ToolTipService.SetToolTip(this, model.Title);
        label?.Update(model, Side is AnchorSide.Left or AnchorSide.Right,
            manager?.AnchorableHeaderTemplate, manager?.AnchorableHeaderTemplateSelector);
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs args) => Refresh();
    private void StopOpenTimer()
    {
        if (openUpTimer == null)
        {
            return;
        }

        openUpTimer.Stop();
        openUpTimer.Tick -= OnOpenUpTimerTick;
        openUpTimer = null;
    }
    private sealed class AnchorLabel : Panel
    {
        private readonly ContentPresenter content = new();
        private bool vertical;
        internal AnchorLabel() => Children.Add(content);
        internal void Update(LayoutAnchorable model, bool isVertical, DataTemplate? template, DataTemplateSelector? selector)
        {
            // The WPFUI anchor uses the manager's original header template and model.
            // This existing native layout adapter replaces the group's WPF LayoutTransform.
            content.ContentTemplate = template;
            content.ContentTemplateSelector = selector;
            content.Content = template != null || selector != null ? model : model.Title;
            vertical = isVertical;
            InvalidateMeasure();
        }
        internal void Clear()
        {
            content.Content = null;
            content.ContentTemplate = null;
            content.ContentTemplateSelector = null;
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return vertical ? new Size(content.DesiredSize.Height, content.DesiredSize.Width) : content.DesiredSize;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            Size size = content.DesiredSize;
            content.Arrange(new Rect(0, 0, size.Width, size.Height));
            content.RenderTransform = vertical ? new CompositeTransform { Rotation = 90, TranslateX = size.Height } : null;
            return finalSize;
        }
    }
}
