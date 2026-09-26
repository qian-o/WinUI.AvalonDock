// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/DockingManager.cs
using Microsoft.UI.Xaml;

namespace AvalonDock;

public partial class DockingManager
{
    public static readonly DependencyProperty GridSplitterVerticalStyleProperty = DependencyProperty.Register(
        nameof(GridSplitterVerticalStyle), typeof(Style), typeof(DockingManager), new PropertyMetadata(null));

    public Style? GridSplitterVerticalStyle
    {
        get => (Style?)GetValue(GridSplitterVerticalStyleProperty);
        set => SetValue(GridSplitterVerticalStyleProperty, value);
    }

    public static readonly DependencyProperty GridSplitterHorizontalStyleProperty = DependencyProperty.Register(
        nameof(GridSplitterHorizontalStyle), typeof(Style), typeof(DockingManager), new PropertyMetadata(null));

    public Style? GridSplitterHorizontalStyle
    {
        get => (Style?)GetValue(GridSplitterHorizontalStyleProperty);
        set => SetValue(GridSplitterHorizontalStyleProperty, value);
    }

    public static readonly DependencyProperty LayoutRootPanelProperty = DependencyProperty.Register(
        nameof(LayoutRootPanel), typeof(Controls.LayoutPanelControl), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnNativeLayoutRootPanelChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the layout LayoutPanelControl which is attached to the Layout.Root property.")]
    [System.ComponentModel.Category("Layout")]
    public Controls.LayoutPanelControl? LayoutRootPanel
    {
        get => (Controls.LayoutPanelControl?)GetValue(LayoutRootPanelProperty);
        set => SetValue(LayoutRootPanelProperty, value);
    }

    protected virtual void OnLayoutRootPanelChanged(DependencyPropertyChangedEventArgs e)
    {
        InternalRemoveLogicalChild(e.OldValue);
        InternalAddLogicalChild(e.NewValue);
    }

    private void OnNativeLayoutRootPanelChanged(DependencyPropertyChangedEventArgs e)
    {
        OnLayoutRootPanelChanged(e);
        // WPF's template binding updates the panel independently of this overridable
        // logical-child hook. WinUI's native ContentPresenter needs that projection here.
        if (layoutHost != null)
        {
            layoutHost.Content = e.NewValue;
        }
        if (!ReferenceEquals(e.OldValue, e.NewValue))
        {
            Controls.LayoutViewBuilder.Release(e.OldValue as UIElement);
        }
        layoutView = e.NewValue as UIElement;
    }
}
