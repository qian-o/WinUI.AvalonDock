// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/ContextMenuEx.cs.
using System.Collections;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Controls;

public class ContextMenuEx : MenuFlyout
{
    private readonly List<(object Model, MenuFlyoutItemBase Container)> generated = [];
    private readonly HashSet<MenuItemEx> defaultHeaders = [];
    private INotifyCollectionChanged? observedSource;
    private NotifyCollectionChangedEventHandler? observedHandler;
    private bool opening;
    private bool hasOpened;
    public ContextMenuEx()
    {
        // Native flyouts need their items before opening, including a source that
        // was previously empty. WPF performs this work in its OnOpened override.
        Opening += (_, _) =>
        {
            opening = true;
            try
            {
                OnOpened(new RoutedEventArgs());
            }
            finally { opening = false; }
        };
    }
    protected virtual DependencyObject GetContainerForItemOverride() => new MenuItemEx();
    protected virtual void OnOpened(RoutedEventArgs e)
    {
        // WinUI BindingExpression has no UpdateTarget; reattaching the same binding
        // refreshes the computed source without a second independent source callback.
        if (ReadLocalValue(ItemsSourceProperty) is BindingExpression expression)
        {
            BindingOperations.SetBinding(this, ItemsSourceProperty, expression.ParentBinding);
        }
        ObserveSource(ItemsSource as INotifyCollectionChanged);
        RefreshItems();
        hasOpened = true;
    }
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ContextMenuEx),
        new PropertyMetadata(null, (owner, _) => ((ContextMenuEx)owner).OnItemsSourceChanged()));
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value);
    }
    public static readonly DependencyProperty ItemContainerStyleProperty = DependencyProperty.Register(nameof(ItemContainerStyle), typeof(Style), typeof(ContextMenuEx),
        new PropertyMetadata(null, (owner, _) => ((ContextMenuEx)owner).OnItemContainerStyleChanged()));
    public Style? ItemContainerStyle
    {
        get => (Style?)GetValue(ItemContainerStyleProperty); set => SetValue(ItemContainerStyleProperty, value);
    }
    internal Action<MenuItemEx, object>? ConfigureItem
    {
        get; set;
    }
    private void OnItemContainerStyleChanged()
    {
        // A native container created before its style received the original fallback
        // header as a local value. Release only that value so a later style can win.
        foreach ((object Model, MenuFlyoutItemBase Container) entry in generated)
        {
            if (!ReferenceEquals(entry.Model, entry.Container) && entry.Container is MenuItemEx item && defaultHeaders.Remove(item)
                && ReferenceEquals(item.ReadLocalValue(MenuItemEx.HeaderProperty), entry.Model))
            {
                item.ClearValue(MenuItemEx.HeaderProperty);
            }
        }

        RefreshItems();
        if (hasOpened)
        {
            foreach ((object Model, MenuFlyoutItemBase Container) entry in generated)
            {
                if (ReferenceEquals(entry.Model, entry.Container) && entry.Container is MenuItemEx item)
                {
                    item.Style = ItemContainerStyle;
                }
            }
        }
    }
    private void OnItemsSourceChanged()
    {
        if (opening)
        {
            return;
        }

        ObserveSource(ItemsSource as INotifyCollectionChanged);
        RefreshItems();
    }
    private void ObserveSource(INotifyCollectionChanged? source)
    {
        if (ReferenceEquals(source, observedSource))
        {
            return;
        }

        if (observedSource != null && observedHandler != null)
        {
            observedSource.CollectionChanged -= observedHandler;
        }

        observedSource = source;
        observedHandler = null;
        if (source == null)
        {
            return;
        }
        // A source can outlive a closed menu. WPF uses a weak collection listener;
        // the native projection must not keep the flyout alive through this event.
        WeakReference<ContextMenuEx> menu = new(this);
        NotifyCollectionChangedEventHandler? handler = null;
        handler = (sender, args) =>
        {
            if (menu.TryGetTarget(out ContextMenuEx? target))
            {
                target.OnSourceCollectionChanged(sender, args);
            }
            else
            {
                source.CollectionChanged -= handler;
            }
        };
        observedHandler = handler;
        source.CollectionChanged += handler;
    }
    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        // WPF's generator retains a generated container on an in-place replacement.
        // Native MenuFlyout exposes only its concrete item collection.
        HashSet<MenuFlyoutItemBase>? replaced = null;
        if (args.Action == NotifyCollectionChangedAction.Replace && args.OldStartingIndex >= 0
            && args.OldItems is { } oldItems && args.NewItems is { } newItems && oldItems.Count == newItems.Count)
        {
            for (int offset = 0; offset < newItems.Count; offset++)
            {
                int index = args.OldStartingIndex + offset;
                if (index < generated.Count && ReferenceEquals(generated[index].Model, oldItems[offset])
                    && !ReferenceEquals(generated[index].Model, generated[index].Container)
                    && newItems[offset] is { } newItem && newItem is not MenuFlyoutItemBase)
                {
                    (replaced ??= []).Add(generated[index].Container);
                    generated[index] = (newItem, generated[index].Container);
                }
            }
        }

        RefreshItems(replaced);
    }
    private void RefreshItems(IReadOnlySet<MenuFlyoutItemBase>? replaced = null)
    {
        if (ItemsSource == null && generated.Count == 0)
        {
            return;
        }

        object[] models = ItemsSource?.Cast<object>().ToArray() ?? [];
        (object Model, MenuFlyoutItemBase Container)[] old = generated.ToArray();
        generated.Clear();
        foreach (object? model in models)
        {
            MenuFlyoutItemBase? retained = old.FirstOrDefault(entry => ReferenceEquals(entry.Model, model)
                && !generated.Any(next => ReferenceEquals(next.Container, entry.Container))).Container;
            MenuFlyoutItemBase container = retained ?? model as MenuFlyoutItemBase ?? GetContainerForItemOverride() as MenuFlyoutItemBase
                ?? throw new InvalidOperationException("A menu container must map to a native menu item.");
            if (!ReferenceEquals(model, container) && container is MenuItemEx item)
            {
                bool needsPreparation = retained == null || replaced?.Contains(container) == true;
                if (needsPreparation)
                {
                    item.DataContext = model;
                }

                item.Style = ItemContainerStyle;
                if (ConfigureItem != null)
                {
                    if (needsPreparation)
                    {
                        ConfigureItem(item, model);
                    }
                }
                else if (!item.HasStyledHeader)
                {
                    item.Header = model;
                    defaultHeaders.Add(item);
                }
            }
            generated.Add((model, container));
        }
        foreach ((object Model, MenuFlyoutItemBase Container) entry in old.Where(entry => !generated.Any(next => ReferenceEquals(next.Container, entry.Container))))
        {
            Items.Remove(entry.Container);
            if (entry.Container is MenuItemEx item)
            {
                defaultHeaders.Remove(item);
            }

            if (!ReferenceEquals(entry.Model, entry.Container))
            {
                entry.Container.DataContext = null;
            }
        }
        for (int index = 0; index < generated.Count; index++)
        {
            MenuFlyoutItemBase item = generated[index].Container;
            int previous = Items.IndexOf(item);
            if (previous == index)
            {
                continue;
            }

            if (previous >= 0)
            {
                Items.RemoveAt(previous);
            }

            Items.Insert(index, item);
        }
        if (opening)
        {
            foreach ((object Model, MenuFlyoutItemBase Container) entry in generated)
            {
                if (ReferenceEquals(entry.Model, entry.Container) && entry.Container is MenuItemEx item)
                {
                    item.Style = ItemContainerStyle;
                }
            }
        }
    }
}
