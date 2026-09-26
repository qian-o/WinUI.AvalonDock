// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/LayoutAnchorGroupControl.cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Controls;

public class LayoutAnchorGroupControl : Control, ILayoutControl
{
    private readonly LayoutAnchorGroup model;
    private readonly ObservableCollection<LayoutAnchorControl> childViews = new();
    private readonly NativeTemplateChildren templateChildren;
    private bool released;
    internal LayoutAnchorGroupControl(LayoutAnchorGroup model)
    {
        this.model = model;
        DefaultStyleKey = typeof(LayoutAnchorGroupControl);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/AutoHide.xaml");
        IsTabStop = false;
        templateChildren = new NativeTemplateChildren(this, childViews);
        CreateChildrenViews();
        // WinUI retemplating raises transient Unloaded events. Keep the original
        // collection observer until the owning manager releases this actual view.
        this.model.Children.CollectionChanged += ModelChildrenChanged;
    }
    public ILayoutElement Model => model;
    public ObservableCollection<LayoutAnchorControl> Children => childViews;
    private void ModelChildrenChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        LayoutAnchorControl[] previous = childViews.ToArray();
        try
        {
            OnModelChildrenCollectionChanged(args);
        }
        finally
        {
            foreach (LayoutAnchorControl? removed in previous.Where(view => !childViews.Contains(view)))
            {
                LayoutViewBuilder.Release(removed);
            }
        }
    }
    internal void ReleaseView()
    {
        if (released)
        {
            return;
        }

        released = true;
        model.Children.CollectionChanged -= ModelChildrenChanged;
        templateChildren.Dispose();
        foreach (LayoutAnchorControl? child in childViews.ToArray())
        {
            LayoutViewBuilder.Release(child);
        }

        childViews.Clear();
    }

    private void CreateChildrenViews()
    {
        DockingManager? manager = model.Root?.Manager;
        foreach (LayoutAnchorable childModel in model.Children)
        {
            LayoutAnchorControl lac = new(childModel);
            lac.SetBinding(LayoutAnchorControl.TemplateProperty, new Binding { Path = new PropertyPath(nameof(DockingManager.AnchorTemplate)), Source = manager });
            childViews.Add(lac);
        }
    }

    private void OnModelChildrenCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ||
            e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
        {
            if (e.OldItems != null)
            {
                {
                    foreach (object? childModel in e.OldItems)
                    {
                        childViews.Remove(childViews.First(cv => ReferenceEquals(cv.Model, childModel)));
                    }
                }
            }
        }

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            childViews.Clear();
        }

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add ||
            e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
        {
            if (e.NewItems != null)
            {
                DockingManager? manager = model.Root?.Manager;
                int insertIndex = e.NewStartingIndex;
                foreach (LayoutAnchorable childModel in e.NewItems)
                {
                    LayoutAnchorControl lac = new(childModel);
                    lac.SetBinding(LayoutAnchorControl.TemplateProperty, new Binding { Path = new PropertyPath(nameof(DockingManager.AnchorTemplate)), Source = manager });
                    childViews.Insert(insertIndex++, lac);
                }
            }
        }
    }

}
