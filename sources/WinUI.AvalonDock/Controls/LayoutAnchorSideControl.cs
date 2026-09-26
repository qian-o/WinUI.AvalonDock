// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/LayoutAnchorSideControl.cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using AvalonDock.Compatibility;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Controls;

public class LayoutAnchorSideControl : Control, ILayoutControl
{
    private readonly LayoutAnchorSide model;
    private readonly ObservableCollection<LayoutAnchorGroupControl> childViews = new();
    private readonly NativeTemplateChildren templateChildren;
    private bool released;
    private bool initialized;
    internal LayoutAnchorSideControl(LayoutAnchorSide model)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        DefaultStyleKey = typeof(LayoutAnchorSideControl);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/AutoHide.xaml");
        IsTabStop = false;
        templateChildren = new NativeTemplateChildren(this, childViews);
        CreateChildrenViews();
        // WinUI retemplating raises transient Unloaded events. Keep the original
        // collection observer until the owning manager releases this actual view.
        this.model.Children.CollectionChanged += ModelChildrenChanged;
        UpdateSide();
        RefreshVisibility();
        Loaded += (_, _) => { if (!initialized) { initialized = true; OnInitialized(EventArgs.Empty); } };
    }
    public ILayoutElement Model => model;
    public ObservableCollection<LayoutAnchorGroupControl> Children => childViews;
    public static readonly DependencyProperty IsLeftSideProperty = ReadOnlyPropertyGuard.Register(nameof(IsLeftSide), typeof(bool), typeof(LayoutAnchorSideControl), false);
    public static readonly DependencyProperty IsTopSideProperty = ReadOnlyPropertyGuard.Register(nameof(IsTopSide), typeof(bool), typeof(LayoutAnchorSideControl), false);
    public static readonly DependencyProperty IsRightSideProperty = ReadOnlyPropertyGuard.Register(nameof(IsRightSide), typeof(bool), typeof(LayoutAnchorSideControl), false);
    public static readonly DependencyProperty IsBottomSideProperty = ReadOnlyPropertyGuard.Register(nameof(IsBottomSide), typeof(bool), typeof(LayoutAnchorSideControl), false);
    [System.ComponentModel.Bindable(true)]
    [Description("Gets wether the control is anchored to left side.")]
    [Category("Anchor")]
    public bool IsLeftSide => (bool?)GetValue(IsLeftSideProperty) ?? false;
    [System.ComponentModel.Bindable(true)]
    [Description("Gets wether the control is anchored to top side.")]
    [Category("Anchor")]
    public bool IsTopSide => (bool?)GetValue(IsTopSideProperty) ?? false;
    [System.ComponentModel.Bindable(true)]
    [Description("Gets wether the control is anchored to right side.")]
    [Category("Anchor")]
    public bool IsRightSide => (bool?)GetValue(IsRightSideProperty) ?? false;
    [System.ComponentModel.Bindable(true)]
    [Description("Gets whether the control is anchored to bottom side.")]
    [Category("Anchor")]
    public bool IsBottomSide => (bool?)GetValue(IsBottomSideProperty) ?? false;
    protected void SetIsLeftSide(bool value) => ReadOnlyPropertyGuard.Set(this, IsLeftSideProperty, value);
    protected void SetIsTopSide(bool value) => ReadOnlyPropertyGuard.Set(this, IsTopSideProperty, value);
    protected void SetIsRightSide(bool value) => ReadOnlyPropertyGuard.Set(this, IsRightSideProperty, value);
    protected void SetIsBottomSide(bool value) => ReadOnlyPropertyGuard.Set(this, IsBottomSideProperty, value);
    private static void VerifyWritable(DependencyProperty dp) => ReadOnlyPropertyGuard.VerifyWritable(dp, IsLeftSideProperty, IsTopSideProperty, IsRightSideProperty, IsBottomSideProperty);
    public new void SetValue(DependencyProperty dp, object value)
    {
        VerifyWritable(dp);
        base.SetValue(dp, value);
    }
    public new void ClearValue(DependencyProperty dp)
    {
        VerifyWritable(dp);
        base.ClearValue(dp);
    }
    public new void SetBinding(DependencyProperty dp, Microsoft.UI.Xaml.Data.BindingBase binding)
    {
        VerifyWritable(dp);
        base.SetBinding(dp, binding);
    }
    protected virtual void OnInitialized(EventArgs e) => UpdateSide();
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        RefreshVisibility();
    }
    internal void RefreshVisibility()
    {
        // Native empty side presenters must measure to zero; Toggle owns separate sidebars.
        Visibility = model.ChildrenCount == 0 || model.Root?.Manager is ToggleDockingManager ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ModelChildrenChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        LayoutAnchorGroupControl[] previous = childViews.ToArray();
        try
        {
            OnModelChildrenCollectionChanged(sender, args);
        }
        finally
        {
            foreach (LayoutAnchorGroupControl? removed in previous.Where(view => !childViews.Contains(view)))
            {
                LayoutViewBuilder.Release(removed);
            }

            RefreshVisibility();
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
        foreach (LayoutAnchorGroupControl? child in childViews.ToArray())
        {
            LayoutViewBuilder.Release(child);
        }

        childViews.Clear();
    }

    private void CreateChildrenViews()
    {
        DockingManager manager = model.Root?.Manager ?? throw new InvalidOperationException("The side must belong to a docking manager before its views are created.");
        foreach (LayoutAnchorGroup childModel in model.Children)
        {
            childViews.Add(manager.CreateUIElementForModel(childModel) as LayoutAnchorGroupControl ?? throw new InvalidOperationException("The anchor group must have an anchor group view."));
        }
    }

    private void OnModelChildrenCollectionChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null &&
            (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ||
            e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace))
        {
            foreach (object? childModel in e.OldItems)
            {
                childViews.Remove(childViews.First(cv => ReferenceEquals(cv.Model, childModel)));
            }
        }

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            childViews.Clear();
        }

        if (e.NewItems != null &&
            (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add ||
            e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace))
        {
            DockingManager manager = model.Root?.Manager ?? throw new InvalidOperationException("The side must belong to a docking manager before its views are created.");
            int insertIndex = e.NewStartingIndex;
            foreach (LayoutAnchorGroup childModel in e.NewItems)
            {
                childViews.Insert(insertIndex++, manager.CreateUIElementForModel(childModel) as LayoutAnchorGroupControl ?? throw new InvalidOperationException("The anchor group must have an anchor group view."));
            }
        }
    }

    private void UpdateSide()
    {
        switch (model.Side)
        {
            case AnchorSide.Left:
                SetIsLeftSide(true);
                break;
            case AnchorSide.Top:
                SetIsTopSide(true);
                break;
            case AnchorSide.Right:
                SetIsRightSide(true);
                break;
            case AnchorSide.Bottom:
                SetIsBottomSide(true);
                break;
        }
    }

}
