using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock.Controls;

// WinUI can disconnect a template before notifying its Template DP callback, while
// old ItemsControls still parent control-valued items. Retain those hosts until release.
internal sealed class NativeTemplateChildren : IDisposable
{
    private readonly Control owner;
    private readonly IEnumerable views;
    private readonly long token;
    private readonly List<ItemsControl> lists = [];
    internal NativeTemplateChildren(Control owner, IEnumerable views)
    {
        this.owner = owner;
        this.views = views;
        token = owner.RegisterPropertyChangedCallback(Control.TemplateProperty, (_, _) => Release());
        owner.LayoutUpdated += OnLayoutUpdated;
    }
    public void Dispose()
    {
        owner.LayoutUpdated -= OnLayoutUpdated;
        owner.UnregisterPropertyChangedCallback(Control.TemplateProperty, token);
        Release();
    }
    private void Release()
    {
        foreach (ItemsControl? list in lists.Concat(FindLists(owner)).Distinct())
        {
            if (ReferenceEquals(list.ItemsSource, views))
            {
                list.ItemsSource = null;
            }
        }

        lists.Clear();
    }
    private void OnLayoutUpdated(object? sender, object args)
    {
        lists.Clear();
        lists.AddRange(FindLists(owner).Where(list => ReferenceEquals(list.ItemsSource, views)));
    }
    private static IEnumerable<ItemsControl> FindLists(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is ItemsControl list)
            {
                yield return list;
            }

            foreach (ItemsControl nested in FindLists(child))
            {
                yield return nested;
            }
        }
    }
}
