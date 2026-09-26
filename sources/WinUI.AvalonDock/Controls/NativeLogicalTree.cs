using System.Collections;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace AvalonDock.Controls;

/// <summary>Supplies original logical-tree queries from native content collections and existing manager ownership.</summary>
internal static class NativeLogicalTree
{
    private static readonly ConditionalWeakTable<DependencyObject, WeakReference<DockingManager>> Owners = new();

    internal static void Register(object? child, DockingManager manager)
    {
        if (child is not DependencyObject element)
        {
            return;
        }

        Owners.Remove(element);
        Owners.Add(element, new WeakReference<DockingManager>(manager));
    }

    internal static void Unregister(object? child, DockingManager manager)
    {
        if (child is DependencyObject element && Owners.TryGetValue(element, out WeakReference<DockingManager>? owner)
            && owner.TryGetTarget(out DockingManager? current) && ReferenceEquals(current, manager))
        {
            Owners.Remove(element);
        }
    }

    internal static DependencyObject? GetParent(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Owners.TryGetValue(element, out WeakReference<DockingManager>? owner) && owner.TryGetTarget(out DockingManager? manager))
        {
            return manager;
        }

        return (element as FrameworkElement)?.Parent;
    }

    internal static IEnumerable GetChildren(DependencyObject element) => element switch
    {
        DockingManager manager => Enumerate(manager.LogicalChildrenPublic),
        Compatibility.ChildWindowHost host => Enumerate(host.GetLogicalChildren()),
        // Original panes bind ItemsSource; WPF excludes those data items from its
        // logical children even though the native adapter creates explicit containers.
        LayoutDocumentPaneControl or LayoutAnchorablePaneControl => Array.Empty<object>(),
        TabView tabs => tabs.TabItemsSource == null ? tabs.TabItems : Array.Empty<object>(),
        Panel panel => panel.Children,
        Border border => Child(border.Child),
        Viewbox viewbox => Child(viewbox.Child),
        UserControl user => Child(user.Content),
        ContentControl content => Child(content.Content),
        ItemsControl items => items.ItemsSource == null ? items.Items : Array.Empty<object>(),
        Popup popup => Child(popup.Child),
        ContextMenuEx { ItemsSource: not null } => Array.Empty<object>(),
        MenuFlyout menu => menu.Items,
        MenuFlyoutSubItem menu => menu.Items,
        _ => Array.Empty<object>(),
    };

    private static IEnumerable Child(object? child)
    {
        if (child != null)
        {
            yield return child;
        }
    }

    private static IEnumerable Enumerate(IEnumerator children)
    {
        try
        {
            while (children.MoveNext())
            {
                yield return children.Current;
            }
        }
        finally { (children as IDisposable)?.Dispose(); }
    }
}
