// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager auto-hide presentation.
using AvalonDock.Compatibility;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PropertyMetadata = Microsoft.UI.Xaml.PropertyMetadata;

namespace AvalonDock;

public partial class DockingManager
{
    private ContentPresenter? autoHideArea;
    private AutoHideWindowManager? autoHideWindowManager;

    public static readonly DependencyProperty AllowAnchorDoubleClickDockProperty = DependencyProperty.Register(nameof(AllowAnchorDoubleClickDock), typeof(bool), typeof(DockingManager), new PropertyMetadata(false));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether double-clicking an auto-hide anchor tab toggles the docked (pinned) state.")]
    [System.ComponentModel.Category("Anchor")]
    public bool AllowAnchorDoubleClickDock
    {
        get => (bool)GetValue(AllowAnchorDoubleClickDockProperty); set => SetValue(AllowAnchorDoubleClickDockProperty, value);
    }
    public static readonly DependencyProperty AllowAnchorRightClickContextMenuProperty = DependencyProperty.Register(nameof(AllowAnchorRightClickContextMenu), typeof(bool), typeof(DockingManager), new PropertyMetadata(false));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether right-clicking an auto-hide anchor tab shows the anchorable context menu.")]
    [System.ComponentModel.Category("Anchor")]
    public bool AllowAnchorRightClickContextMenu
    {
        get => (bool)GetValue(AllowAnchorRightClickContextMenuProperty); set => SetValue(AllowAnchorRightClickContextMenuProperty, value);
    }
    public static readonly DependencyProperty AnchorableContextMenuProperty = DependencyProperty.Register(nameof(AnchorableContextMenu), typeof(MenuFlyout), typeof(DockingManager),
        new PropertyMetadata(null, OnContextMenuPropertyChanged));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the ContextMenu to show on an anchorable.")]
    [System.ComponentModel.Category("Anchorable")]
    public MenuFlyout? AnchorableContextMenu
    {
        get => (MenuFlyout?)GetValue(AnchorableContextMenuProperty); set => SetValue(AnchorableContextMenuProperty, value);
    }

    public static readonly DependencyProperty AnchorSideTemplateProperty = DependencyProperty.Register(nameof(AnchorSideTemplate), typeof(ControlTemplate), typeof(DockingManager), new PropertyMetadata(null));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the ControlTemplate used to render LayoutAnchorSideControl.")]
    [System.ComponentModel.Category("Anchor")]
    public ControlTemplate? AnchorSideTemplate
    {
        get => (ControlTemplate?)GetValue(AnchorSideTemplateProperty); set => SetValue(AnchorSideTemplateProperty, value);
    }

    public static readonly DependencyProperty AnchorGroupTemplateProperty = DependencyProperty.Register(nameof(AnchorGroupTemplate), typeof(ControlTemplate), typeof(DockingManager), new PropertyMetadata(null));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the ControlTemplate used to render LayoutAnchorGroupControl.")]
    [System.ComponentModel.Category("Anchor")]
    public ControlTemplate? AnchorGroupTemplate
    {
        get => (ControlTemplate?)GetValue(AnchorGroupTemplateProperty); set => SetValue(AnchorGroupTemplateProperty, value);
    }

    public static readonly DependencyProperty AnchorTemplateProperty = DependencyProperty.Register(nameof(AnchorTemplate), typeof(ControlTemplate), typeof(DockingManager), new PropertyMetadata(null));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the ControlTemplate used to render a LayoutAnchorControl.")]
    [System.ComponentModel.Category("Anchor")]
    public ControlTemplate? AnchorTemplate
    {
        get => (ControlTemplate?)GetValue(AnchorTemplateProperty); set => SetValue(AnchorTemplateProperty, value);
    }

    public static readonly DependencyProperty LeftSidePanelProperty = DependencyProperty.Register(nameof(LeftSidePanel), typeof(LayoutAnchorSideControl), typeof(DockingManager), new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnLeftSidePanelChanged(e)));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the LayoutAnchorSideControl that is displayed as left side panel control.")]
    [System.ComponentModel.Category("Side Panel")]
    public LayoutAnchorSideControl? LeftSidePanel
    {
        get => (LayoutAnchorSideControl?)GetValue(LeftSidePanelProperty); set => SetValue(LeftSidePanelProperty, value);
    }
    protected virtual void OnLeftSidePanelChanged(DependencyPropertyChangedEventArgs e)
    {
        InternalRemoveLogicalChild(e.OldValue);
        InternalAddLogicalChild(e.NewValue);
        if (!ReferenceEquals(e.OldValue, e.NewValue))
        {
            LayoutViewBuilder.Release(e.OldValue as UIElement);
        }
    }

    public static readonly DependencyProperty TopSidePanelProperty = DependencyProperty.Register(nameof(TopSidePanel), typeof(LayoutAnchorSideControl), typeof(DockingManager), new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnTopSidePanelChanged(e)));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the LayoutAnchorSideControl that is displayed as top side panel control.")]
    [System.ComponentModel.Category("Side Panel")]
    public LayoutAnchorSideControl? TopSidePanel
    {
        get => (LayoutAnchorSideControl?)GetValue(TopSidePanelProperty); set => SetValue(TopSidePanelProperty, value);
    }
    protected virtual void OnTopSidePanelChanged(DependencyPropertyChangedEventArgs e)
    {
        InternalRemoveLogicalChild(e.OldValue);
        InternalAddLogicalChild(e.NewValue);
        if (!ReferenceEquals(e.OldValue, e.NewValue))
        {
            LayoutViewBuilder.Release(e.OldValue as UIElement);
        }
    }

    public static readonly DependencyProperty RightSidePanelProperty = DependencyProperty.Register(nameof(RightSidePanel), typeof(LayoutAnchorSideControl), typeof(DockingManager), new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnRightSidePanelChanged(e)));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the LayoutAnchorSideControl that is displayed as right side panel control.")]
    [System.ComponentModel.Category("Side Panel")]
    public LayoutAnchorSideControl? RightSidePanel
    {
        get => (LayoutAnchorSideControl?)GetValue(RightSidePanelProperty); set => SetValue(RightSidePanelProperty, value);
    }
    protected virtual void OnRightSidePanelChanged(DependencyPropertyChangedEventArgs e)
    {
        InternalRemoveLogicalChild(e.OldValue);
        InternalAddLogicalChild(e.NewValue);
        if (!ReferenceEquals(e.OldValue, e.NewValue))
        {
            LayoutViewBuilder.Release(e.OldValue as UIElement);
        }
    }

    public static readonly DependencyProperty BottomSidePanelProperty = DependencyProperty.Register(nameof(BottomSidePanel), typeof(LayoutAnchorSideControl), typeof(DockingManager), new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnBottomSidePanelChanged(e)));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the LayoutAnchorSideControl that is displayed as bottom side panel control.")]
    [System.ComponentModel.Category("Side Panel")]
    public LayoutAnchorSideControl? BottomSidePanel
    {
        get => (LayoutAnchorSideControl?)GetValue(BottomSidePanelProperty); set => SetValue(BottomSidePanelProperty, value);
    }
    protected virtual void OnBottomSidePanelChanged(DependencyPropertyChangedEventArgs e)
    {
        InternalRemoveLogicalChild(e.OldValue);
        InternalAddLogicalChild(e.NewValue);
        if (!ReferenceEquals(e.OldValue, e.NewValue))
        {
            LayoutViewBuilder.Release(e.OldValue as UIElement);
        }
    }

    public static readonly DependencyProperty AutoHideWindowProperty = ReadOnlyPropertyGuard.Register(nameof(AutoHideWindow), typeof(LayoutAutoHideWindowControl), typeof(DockingManager), null, (d, e) => ((DockingManager)d).OnAutoHideWindowChanged(e));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets the LayoutAutoHideWindowControl that is currently shown as autohide window.")]
    [System.ComponentModel.Category("AutoHideWindow")]
    public LayoutAutoHideWindowControl? AutoHideWindow => (LayoutAutoHideWindowControl?)GetValue(AutoHideWindowProperty);
    protected void SetAutoHideWindow(LayoutAutoHideWindowControl? value) => ReadOnlyPropertyGuard.Set(this, AutoHideWindowProperty, value);
    public new void SetValue(DependencyProperty dp, object? value)
    {
        ReadOnlyPropertyGuard.VerifyWritable(dp, AutoHideWindowProperty);
        if (IsCoercedTemplate(dp))
        {
            SetTemplateValue(dp, value);
        }
        else
        {
            base.SetValue(dp, value);
        }
    }
    public new void ClearValue(DependencyProperty dp)
    {
        ReadOnlyPropertyGuard.VerifyWritable(dp, AutoHideWindowProperty);
        if (IsCoercedTemplate(dp))
        {
            ClearTemplateValue(dp);
        }
        else
        {
            base.ClearValue(dp);
        }
    }
    protected virtual void OnAutoHideWindowChanged(DependencyPropertyChangedEventArgs e)
    {
        InternalRemoveLogicalChild(e.OldValue);
        InternalAddLogicalChild(e.NewValue);
        if (!ReferenceEquals(e.OldValue, e.NewValue))
        {
            (e.OldValue as LayoutAutoHideWindowControl)?.Dispose();
        }
    }

    private void InitializeAutoHideViews()
    {
        if (!ReferenceEquals(Layout?.Manager, this))
        {
            return;
        }

        if (!ReferenceEquals(LeftSidePanel?.Model, Layout.LeftSide))
        {
            LeftSidePanel = CreateUIElementForModel(Layout.LeftSide) as LayoutAnchorSideControl;
        }

        if (!ReferenceEquals(TopSidePanel?.Model, Layout.TopSide))
        {
            TopSidePanel = CreateUIElementForModel(Layout.TopSide) as LayoutAnchorSideControl;
        }

        if (!ReferenceEquals(RightSidePanel?.Model, Layout.RightSide))
        {
            RightSidePanel = CreateUIElementForModel(Layout.RightSide) as LayoutAnchorSideControl;
        }

        if (!ReferenceEquals(BottomSidePanel?.Model, Layout.BottomSide))
        {
            BottomSidePanel = CreateUIElementForModel(Layout.BottomSide) as LayoutAnchorSideControl;
        }

        AutoHideWindow?.ValidateModel();
        foreach (LayoutAnchorSideControl? panel in new[] { LeftSidePanel, TopSidePanel, RightSidePanel, BottomSidePanel })
        {
            panel?.RefreshVisibility();
        }

        if (AutoHideWindow == null)
        {
            SetAutoHideWindow(new LayoutAutoHideWindowControl());
        }
    }
    private void ReleaseAutoHideViews()
    {
        HideAutoHideWindow();
        LeftSidePanel = null;
        TopSidePanel = null;
        RightSidePanel = null;
        BottomSidePanel = null;
        SetAutoHideWindow(null);
    }
    internal void ShowAutoHideWindow(LayoutAnchorControl anchor)
    {
        if (!IsLoaded || !SupportsAutoHideFlyout || autoHideArea == null || anchor?.Model is not LayoutAnchorable model
            || model.Root?.Manager != this || model.Parent is not LayoutAnchorGroup group || group.Parent is not LayoutAnchorSide side
            || !group.Children.Contains(model) || !side.Children.Contains(group))
        {
            return;
        }

        InitializeAutoHideViews();
        autoHideWindowManager ??= new AutoHideWindowManager(this);
        autoHideWindowManager.ShowAutoHideWindow(anchor);
        autoHideArea.Content = AutoHideWindow;
        AutoHideWindow?.UpdateExtent();
    }
    internal void HideAutoHideWindow(LayoutAnchorControl? anchor = null) => autoHideWindowManager?.HideAutoWindow(anchor);
    internal FrameworkElement GetAutoHideAreaElement() => autoHideArea ?? (FrameworkElement)this;
}
