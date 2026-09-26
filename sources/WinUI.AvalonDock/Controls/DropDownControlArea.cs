// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/DropDownControlArea.cs.
using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace AvalonDock.Controls;

public class DropDownControlArea : ContentControl
{
    public DropDownControlArea()
    {
        IsTabStop = false;
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        Unloaded += (_, _) => { MenuFlyout? menu = ResolveMenu(); if (ReferenceEquals(menu?.Target, this)) { menu.Hide(); } };
    }
    public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register(nameof(DropDownContextMenu), typeof(MenuFlyout), typeof(DropDownControlArea), new PropertyMetadata(null));
    [System.ComponentModel.Bindable(true), Description("Gets/sets the drop down menu to show up when user click on an anchorable menu pin."), Category("Menu")]
    public MenuFlyout? DropDownContextMenu
    {
        get => (MenuFlyout?)GetValue(DropDownContextMenuProperty); set => SetValue(DropDownContextMenuProperty, value);
    }
    public static readonly DependencyProperty DropDownContextMenuDataContextProperty = DependencyProperty.Register(nameof(DropDownContextMenuDataContext), typeof(object), typeof(DropDownControlArea), new PropertyMetadata(null));
    [System.ComponentModel.Bindable(true), Description("Gets/sets the DataContext to set for the DropDownContext menu property."), Category("Menu")]
    public object? DropDownContextMenuDataContext
    {
        get => GetValue(DropDownContextMenuDataContextProperty); set => SetValue(DropDownContextMenuDataContextProperty, value);
    }
    private void OnPointerReleased(object? sender, PointerRoutedEventArgs args)
    {
        if (ResolveMenu() is not { } menu || args.GetCurrentPoint(this).Properties.PointerUpdateKind != Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased)
        {
            return;
        }

        Point position = args.GetCurrentPoint(this).Position;
        // WinUI bubbles this event after child handlers; handledEventsToo preserves
        // the source class preview handler's right-release behavior (issue 225).
        args.Handled = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (IsLoaded && ReferenceEquals(menu, ResolveMenu()))
            {
                MenuFlyoutContext.Show(menu, this, DropDownContextMenuDataContext,
                new FlyoutShowOptions { Position = position });
            }
        });
    }
    private MenuFlyout? ResolveMenu() => ReadLocalValue(DropDownContextMenuProperty) != DependencyProperty.UnsetValue ? DropDownContextMenu
        : Equals(Tag, "AvalonDock.DefaultContextMenu") && DropDownContextMenuDataContext is LayoutItem { LayoutElement: LayoutContent model } ? model is LayoutAnchorable
            ? model.Root?.Manager?.AnchorableContextMenu : model.Root?.Manager?.DocumentContextMenu : DropDownContextMenu;
}
