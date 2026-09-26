// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutDocumentPaneControl.cs
using System.Collections.Specialized;
using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AvalonDock.Controls;

/// <summary>Displays the document pane using the preserved layout and selection contracts.</summary>
public class LayoutDocumentPaneControl : TabControlEx, ILayoutControl
{
    private readonly LayoutDocumentPane model;
    private readonly LayoutPanePresenter presenter;

    internal LayoutDocumentPaneControl(LayoutDocumentPane model, bool isVirtualizing, bool ignoreTabControlKeyBindingBindings = false)
        : base(isVirtualizing, ignoreTabControlKeyBindingBindings)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        IsTabStop = false;
        presenter = new LayoutPanePresenter(this, model);
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerInput), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerInput), true);
    }

    [Bindable(false)]
    [Description("Gets the layout model of this control.")]
    [Category("Other")]
    public ILayoutElement Model => model;

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (model.SelectedContent != null)
        {
            model.SelectedContent.IsActive = true;
        }
    }

    protected virtual void OnMouseLeftButtonDown(PointerRoutedEventArgs e)
    {
        if (!e.Handled && model.SelectedContent is { IsActive: false } selected)
        {
            selected.IsActive = true;
        }
    }

    protected virtual void OnMouseRightButtonDown(PointerRoutedEventArgs e)
    {
        if (!e.Handled && model.SelectedContent != null)
        {
            model.SelectedContent.IsActive = true;
        }
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (object? item in e.OldItems)
            {
                if (item is LayoutContent { TabItem: { } tabItem } layoutContent)
                {
                    tabItem.Model = null;
                    tabItem.ContextFlyout = null;
                    tabItem.Content = null;
                    Panel? panel = tabItem.FindVisualAncestor<Panel>();
                    if (panel != null)
                    {
                        panel.Children.Remove(tabItem);
                    }

                    layoutContent.TabItem = null;
                }
            }
        }
    }

    internal void InitializeView() => presenter.Attach();
    internal void ReleaseView() => presenter.Dispose();

    private void OnPointerInput(object? sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        switch (properties.PointerUpdateKind)
        {
            case Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed:
                OnMouseLeftButtonDown(e);
                break;
            case Microsoft.UI.Input.PointerUpdateKind.RightButtonPressed:
                OnMouseRightButtonDown(e);
                break;
        }
    }
}
