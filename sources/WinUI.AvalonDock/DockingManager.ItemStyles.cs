// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager.cs layout-item lifetime and styles.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792
using AvalonDock.Controls;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

public partial class DockingManager
{
    internal event Action<LayoutContent>? LayoutItemCreated;

    public static readonly DependencyProperty LayoutItemContainerStyleProperty = DependencyProperty.Register(
        nameof(LayoutItemContainerStyle), typeof(Style), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnLayoutItemContainerStyleChanged(args)));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the Style to apply to a LayoutDocumentItem object.")]
    [System.ComponentModel.Category("Layout")]
    public Style? LayoutItemContainerStyle
    {
        get => (Style?)GetValue(LayoutItemContainerStyleProperty); set => SetValue(LayoutItemContainerStyleProperty, value);
    }
    protected virtual void OnLayoutItemContainerStyleChanged(DependencyPropertyChangedEventArgs e) => AttachLayoutItems();
    public static readonly DependencyProperty LayoutItemContainerStyleSelectorProperty = DependencyProperty.Register(
        nameof(LayoutItemContainerStyleSelector), typeof(StyleSelector), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnLayoutItemContainerStyleSelectorChanged(args)));
    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the StyleSelector to select the Style for a LayoutDocumentItem.")]
    [System.ComponentModel.Category("Layout")]
    public StyleSelector? LayoutItemContainerStyleSelector
    {
        get => (StyleSelector?)GetValue(LayoutItemContainerStyleSelectorProperty); set => SetValue(LayoutItemContainerStyleSelectorProperty, value);
    }
    protected virtual void OnLayoutItemContainerStyleSelectorChanged(DependencyPropertyChangedEventArgs e) => AttachLayoutItems();
    private void Layout_ElementRemoved(object? sender, LayoutElementEventArgs e)
    {
        if (suspendLayoutItemCreation)
        {
            return;
        }

        if (e.Element is LayoutFloatingWindow)
        {
            SynchronizeWindowHosts();
        }

        CollectLayoutItemsDeleted();
    }

    private void Layout_ElementAdded(object? sender, LayoutElementEventArgs e)
    {
        if (suspendLayoutItemCreation)
        {
            return;
        }

        foreach (LayoutContent? content in Layout.Descendents().OfType<LayoutContent>().ToList())
        {
            if (content is LayoutDocument document)
            {
                CreateDocumentLayoutItem(document);
            }
            else if (content is LayoutAnchorable anchorable)
            {
                CreateAnchorableLayoutItem(anchorable);
            }
        }

        CollectLayoutItemsDeleted();
    }

    // Native root subscriptions are paired once in OnLayoutChanged; style reapplication
    // uses this original traversal without installing duplicate lifetime handlers.
    private void AttachLayoutItems()
    {
        if (Layout == null)
        {
            return;
        }

        foreach (LayoutDocument? document in Layout.Descendents().OfType<LayoutDocument>().ToArray())
        {
            CreateDocumentLayoutItem(document);
        }

        foreach (LayoutAnchorable? anchorable in Layout.Descendents().OfType<LayoutAnchorable>().ToArray())
        {
            CreateAnchorableLayoutItem(anchorable);
        }
    }

    private void ApplyStyleToLayoutItem(LayoutItem layoutItem)
    {
        layoutItem.ClearDefaultBindingsForManager();
        Style? style = LayoutItemContainerStyle
            ?? LayoutItemContainerStyleSelector?.SelectStyle(layoutItem.Model, layoutItem);
        layoutItem.ApplyManagerStyle(style);
        layoutItem.SetDefaultBindingsForManager();
    }

    private void CreateAnchorableLayoutItem(LayoutAnchorable contentToAttach)
    {
        LayoutItem? existing = layoutItems.FirstOrDefault(item => item.LayoutElement == contentToAttach);
        if (existing != null)
        {
            ApplyStyleToLayoutItem(existing);
            return;
        }

        ClaimLayoutItemOwnership(contentToAttach);
        LayoutAnchorableItem layoutItem = new();
        layoutItem.Attach(contentToAttach);
        layoutItems.Add(layoutItem);
        ApplyStyleToLayoutItem(layoutItem);
        if (contentToAttach.Content is UIElement)
        {
            InternalAddLogicalChild(contentToAttach.Content);
        }

        LayoutItemCreated?.Invoke(contentToAttach);
    }

    private void CreateDocumentLayoutItem(LayoutDocument contentToAttach)
    {
        LayoutItem? existing = layoutItems.FirstOrDefault(item => item.LayoutElement == contentToAttach);
        if (existing != null)
        {
            ApplyStyleToLayoutItem(existing);
            return;
        }

        ClaimLayoutItemOwnership(contentToAttach);
        LayoutDocumentItem layoutItem = new();
        layoutItem.Attach(contentToAttach);
        layoutItems.Add(layoutItem);
        ApplyStyleToLayoutItem(layoutItem);
        if (contentToAttach.Content is UIElement)
        {
            InternalAddLogicalChild(contentToAttach.Content);
        }

        LayoutItemCreated?.Invoke(contentToAttach);
    }

    private bool collectLayoutItemsPending;

    private void CollectLayoutItemsDeleted()
    {
        if (collectLayoutItemsPending)
        {
            return;
        }

        collectLayoutItemsPending = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            collectLayoutItemsPending = false;
            foreach (LayoutItem? itemToRemove in layoutItems.Where(item => !ReferenceEquals(item.LayoutElement?.Root, Layout)).ToArray())
            {
                ForgetLayoutItemOwner(itemToRemove.LayoutElement);
                itemToRemove.Detach();
                layoutItems.Remove(itemToRemove);
            }
        }))
        {
            collectLayoutItemsPending = false;
        }
    }

    private void DetachLayoutItems()
    {
        foreach (LayoutItem item in layoutItems)
        {
            ForgetLayoutItemOwner(item.LayoutElement);
            item.Detach();
        }
        layoutItems.Clear();
    }

    private void ClaimLayoutItemOwnership(LayoutContent content)
    {
        // Native presenters must leave their previous XamlRoot before a different manager
        // attaches the same editor. Deferred same-manager collection keeps the original item.
        if (LayoutItemOwners.TryGetValue(content, out WeakReference<DockingManager>? ownerReference)
            && ownerReference.TryGetTarget(out DockingManager? owner) && !ReferenceEquals(owner, this)
            && owner.GetLayoutItemFromModel(content) is { } previousItem)
        {
            owner.layoutItems.Remove(previousItem);
            previousItem.Detach();
        }
        LayoutItemOwners.Remove(content);
        LayoutItemOwners.Add(content, new WeakReference<DockingManager>(this));
    }
}
