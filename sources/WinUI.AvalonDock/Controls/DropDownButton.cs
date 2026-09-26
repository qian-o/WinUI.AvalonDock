// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/DropDownButton.cs.
using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace AvalonDock.Controls;

public class DropDownButton : ToggleButton
{
    private MenuFlyout? openedMenu;
    public DropDownButton()
    {
        Unloaded += (_, _) => { DetachMenu(true); IsChecked = false; };
    }
    public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register(nameof(DropDownContextMenu), typeof(MenuFlyout), typeof(DropDownButton),
        new PropertyMetadata(null, (owner, args) => ((DropDownButton)owner).OnDropDownContextMenuChanged(args)));
    [System.ComponentModel.Bindable(true), Description("Gets/sets the drop down menu to show up when user click on an anchorable menu pin."), Category("Menu")]
    public MenuFlyout? DropDownContextMenu
    {
        get => (MenuFlyout?)GetValue(DropDownContextMenuProperty); set => SetValue(DropDownContextMenuProperty, value);
    }
    protected virtual void OnDropDownContextMenuChanged(DependencyPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(e.OldValue, openedMenu))
        {
            DetachMenu(false);
        }
    }
    public static readonly DependencyProperty DropDownContextMenuDataContextProperty = DependencyProperty.Register(nameof(DropDownContextMenuDataContext), typeof(object), typeof(DropDownButton), new PropertyMetadata(null));
    [System.ComponentModel.Bindable(true), Description("Gets/sets the DataContext to set for the DropDownContext menu property."), Category("Menu")]
    public object? DropDownContextMenuDataContext
    {
        get => GetValue(DropDownContextMenuDataContextProperty); set => SetValue(DropDownContextMenuDataContextProperty, value);
    }
    protected override void OnToggle()
    {
        OnClick();
        base.OnToggle();
    }
    protected virtual void OnClick()
    {
        MenuFlyout? menu = ReadLocalValue(DropDownContextMenuProperty) != DependencyProperty.UnsetValue ? DropDownContextMenu
            : Equals(Tag, "AvalonDock.DefaultContextMenu") && DropDownContextMenuDataContext is LayoutItem { LayoutElement: LayoutAnchorable tool } ? tool.Root?.Manager?.AnchorableContextMenu : DropDownContextMenu;
        if (menu == null || XamlRoot == null)
        {
            return;
        }

        DetachMenu(false);
        openedMenu = menu;
        menu.Closed += OnMenuClosed;
        MenuFlyoutContext.Show(menu, this, DropDownContextMenuDataContext, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
    }
    private void OnMenuClosed(object? sender, object args)
    {
        DetachMenu(false);
        IsChecked = false;
    }
    private void DetachMenu(bool hide)
    {
        MenuFlyout? menu = openedMenu;
        openedMenu = null;
        if (menu == null)
        {
            return;
        }

        menu.Closed -= OnMenuClosed;
        if (hide)
        {
            menu.Hide();
        }
    }
}
