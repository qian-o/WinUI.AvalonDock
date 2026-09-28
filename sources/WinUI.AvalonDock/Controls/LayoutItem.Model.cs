// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutItem.cs

using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Controls;

public abstract partial class LayoutItem
{
    private ContentPresenter? view;
    private DockingManager? logicalOwner;
    private UIElement? logicalContent;

    /// <summary>
    /// Gets the view.
    /// </summary>
    public ContentPresenter View
    {
        get
        {
            if (view != null)
            {
                return view;
            }

            view = new ContentPresenter();
            view.SetBinding(ContentPresenter.ContentProperty, new Binding { Path = new PropertyPath(nameof(ContentPresenter.Content)), Source = LayoutElement });
            if (LayoutElement?.Root == null)
            {
                return view;
            }

            view.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding { Path = new PropertyPath(nameof(DockingManager.LayoutItemTemplate)), Source = LayoutElement.Root.Manager });
            view.SetBinding(ContentPresenter.ContentTemplateSelectorProperty, new Binding { Path = new PropertyPath(nameof(DockingManager.LayoutItemTemplateSelector)), Source = LayoutElement.Root.Manager });
            logicalOwner?.InternalAddLogicalChild(view);
            return view;
        }
    }

    /// <summary>
    /// Raises the visibility changed event.
    /// </summary>
    protected virtual void OnVisibilityChanged()
    {
        if (LayoutElement != null && Visibility == Visibility.Collapsed)
        {
            LayoutElement.Close();
        }
    }

    /// <summary>
    /// Attaches the handler.
    /// </summary>
    /// <param name="model">The layout model.</param>
    internal virtual void Attach(LayoutContent model)
    {
        if (LayoutElement != null)
        {
            Detach();
        }

        LayoutElement = model;
        logicalOwner = model.Root?.Manager;
        logicalContent = model.Content as UIElement;
        Model = model.Content;
        InitDefaultCommands();
        LayoutElement.IsSelectedChanged += LayoutElement_IsSelectedChanged;
        LayoutElement.IsActiveChanged += LayoutElement_IsActiveChanged;
        LayoutElement.PropertyChanged += LayoutElement_PropertyChanged;
        DataContext = this;
    }

    /// <summary>
    /// Detaches the handler.
    /// </summary>
    internal virtual void Detach()
    {
        if (LayoutElement == null)
        {
            return;
        }

        logicalOwner?.InternalRemoveLogicalChild(logicalContent);
        logicalOwner?.InternalRemoveLogicalChild(view);
        logicalOwner = null;
        logicalContent = null;
        LayoutElement.IsSelectedChanged -= LayoutElement_IsSelectedChanged;
        LayoutElement.IsActiveChanged -= LayoutElement_IsActiveChanged;
        LayoutElement.PropertyChanged -= LayoutElement_PropertyChanged;
        ClearDefaultBindings();
        ReleaseStyleBindings();
        if (view != null)
        {
            view.ClearValue(ContentPresenter.ContentProperty);
            view.Content = null;
            view.ClearValue(ContentPresenter.ContentTemplateProperty);
            view.ClearValue(ContentPresenter.ContentTemplateSelectorProperty);
            view = null;
        }
        LayoutElement = null;
        Model = null;
        managerStyle = null;
        consumerStyle = null;
        containerStyle = null;
        base.ClearValue(StyleProperty);
    }

    /// <summary>
    /// Is view exists.
    /// </summary>
    /// <returns>true if the instance is view exists; otherwise, false.</returns>
    internal bool IsViewExists() => view != null;

    private void LayoutElement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LayoutContent.Content):
                logicalOwner?.InternalRemoveLogicalChild(logicalContent);
                logicalContent = LayoutElement?.Content as UIElement;
                logicalOwner?.InternalAddLogicalChild(logicalContent);
                break;
        }

        NotifyDefaultCommands();
    }

    private void LayoutElement_IsActiveChanged(object? sender, EventArgs e)
    {
        if (!isActiveReentrantFlag.CanEnter)
        {
            return;
        }

        using (isActiveReentrantFlag.Enter())
        {
            IsActive = LayoutElement?.IsActive == true;
        }
    }

    private void LayoutElement_IsSelectedChanged(object? sender, EventArgs e)
    {
        if (!isSelectedReentrantFlag.CanEnter)
        {
            return;
        }

        using (isSelectedReentrantFlag.Enter())
        {
            IsSelected = LayoutElement?.IsSelected == true;
        }
    }

    private static void OnToolTipChanged(DependencyObject s, DependencyPropertyChangedEventArgs e) => ((LayoutItem)s).OnToolTipChanged();

    private void OnToolTipChanged()
    {
        if (LayoutElement != null)
        {
            LayoutElement.ToolTip = ToolTipService.GetToolTip(this);
        }
    }

    private static void OnVisibilityChanged(DependencyObject s, DependencyPropertyChangedEventArgs e) => ((LayoutItem)s).OnVisibilityChanged();
}
