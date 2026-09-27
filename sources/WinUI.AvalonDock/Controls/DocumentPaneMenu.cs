using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Controls;

/// <summary>Connects the native tab template's document-list button to the preserved menu controls.</summary>
internal sealed class DocumentPaneMenu : IDisposable
{
    private readonly LayoutDocumentPane pane;
    private readonly DropDownButton button;
    private readonly ContextMenuEx menu;
    private readonly DockingManager manager;
    private readonly List<(DependencyProperty Property, long Token)> presentationTokens = [];
    internal DocumentPaneMenu(LayoutDocumentPane pane, DropDownButton button)
    {
        this.pane = pane;
        this.button = button;
        manager = pane.Root?.Manager ?? throw new ArgumentException("文档菜单的窗格必须附接到 DockingManager。", nameof(pane));
        menu = new ContextMenuEx { ConfigureItem = ConfigureItem };
        BindingOperations.SetBinding(menu, ContextMenuEx.ItemsSourceProperty,
            new Binding { Source = pane, Path = new PropertyPath(nameof(LayoutDocumentPane.ChildrenSorted)) });
        button.DropDownContextMenu = menu;
        pane.ChildrenCollectionChanged += OnChildrenChanged;
        manager.LayoutItemCreated += OnLayoutItemCreated;
        foreach (DependencyProperty? property in new[] { DockingManager.DocumentPaneMenuItemHeaderTemplateProperty, DockingManager.DocumentPaneMenuItemHeaderTemplateSelectorProperty,
            DockingManager.IconContentTemplateProperty, DockingManager.IconContentTemplateSelectorProperty })
        {
            presentationTokens.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) => RefreshPresentation())));
        }

        Refresh();
    }
    private void RefreshPresentation()
    {
        foreach (MenuItemEx item in menu.Items.OfType<MenuItemEx>())
        {
            if (item.DataContext is LayoutContent model)
            {
                ConfigureItem(item, model);
            }
        }
    }
    private void ConfigureItem(MenuItemEx item, object value)
    {
        LayoutContent model = (LayoutContent)value;
        item.Header = model;
        item.SetBinding(MenuFlyoutItem.TextProperty,
            new Binding { Source = model, Path = new PropertyPath(nameof(LayoutContent.Title)) });
        item.HeaderTemplate = manager.DocumentPaneMenuItemHeaderTemplate;
        item.HeaderTemplateSelector = manager.DocumentPaneMenuItemHeaderTemplateSelector;
        item.IconTemplate = manager.IconContentTemplate;
        item.IconTemplateSelector = manager.IconContentTemplateSelector;
        if (item.IconTemplate == null && item.IconTemplateSelector == null)
        {
            item.Icon = model.IconSource == null ? null : new Image { Source = model.IconSource, Width = 16, Height = 16 };
        }
        item.Command = manager.GetLayoutItemFromModel(model)?.ActivateCommand;
    }
    private void OnChildrenChanged(object? sender, EventArgs args) => Refresh();
    private void OnLayoutItemCreated(LayoutContent content)
    {
        foreach (MenuItemEx item in menu.Items.OfType<MenuItemEx>()
            .Where(item => ReferenceEquals(item.DataContext, content)))
        {
            item.Command = manager.GetLayoutItemFromModel(content)?.ActivateCommand;
        }
    }
    private void Refresh()
    {
        button.Visibility = pane.ChildrenCount == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    public void Dispose()
    {
        menu.Hide();
        pane.ChildrenCollectionChanged -= OnChildrenChanged;
        manager.LayoutItemCreated -= OnLayoutItemCreated;
        foreach ((DependencyProperty? property, long token) in presentationTokens)
        {
            manager.UnregisterPropertyChangedCallback(property, token);
        }

        presentationTokens.Clear();
        button.DropDownContextMenu = null;
        menu.ClearValue(ContextMenuEx.ItemsSourceProperty);
        menu.Items.Clear();
        menu.ConfigureItem = null;
    }
}
