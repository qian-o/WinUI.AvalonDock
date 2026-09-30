using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock.Controls;

/// <summary>Owns the native parent of a manager-owned content view across template changes.</summary>
internal sealed class LayoutContentViewHost(Control owner, DependencyProperty layoutItemProperty)
{
    private ContentPresenter? contentHost;
    private LayoutItem? LayoutItem => (LayoutItem?)owner.GetValue(layoutItemProperty);

    internal void Attach(ContentPresenter? nextHost)
    {
        if (nextHost == null)
        {
            return;
        }

        if (!ReferenceEquals(contentHost, nextHost))
        {
            Release();
            contentHost = nextHost;
        }

        // Releasing an old parent can run consumer callbacks. Resolve the current
        // item afterwards, just as the owning controls did before sharing this host.
        ContentPresenter? view = LayoutItem?.View;
        if (ReferenceEquals(contentHost.Content, view))
        {
            return;
        }

        if (view != null)
        {
            Detach(view);
        }

        contentHost.Content = view;
    }

    internal void RememberBoundTemplateHost()
    {
        if (contentHost != null || LayoutItem?.IsViewExists() != true)
        {
            return;
        }

        // Consumer templates may bind LayoutItem.View without a named part. WinUI
        // disconnects their tree before Template changes; remember the actual owner
        // so the retired template can release its native binding and parent.
        ContentPresenter view = LayoutItem!.View;
        contentHost = owner.FindVisualChildren<ContentPresenter>()
            .FirstOrDefault(host => ReferenceEquals(host.Content, view));
    }

    internal void Release(bool preserveBinding = false)
    {
        // A still-current consumer template follows the LayoutItem DP change itself;
        // only a retired template must have that binding explicitly disconnected.
        if (contentHost != null && (!preserveBinding || contentHost.GetBindingExpression(ContentPresenter.ContentProperty) == null))
        {
            contentHost.Content = null;
        }

        contentHost = null;
    }

    private static void Detach(ContentPresenter view)
    {
        DependencyObject parent = view.Parent ?? VisualTreeHelper.GetParent(view);
        if (parent is ContentPresenter presenter && ReferenceEquals(presenter.Content, view))
        {
            presenter.Content = null;
        }
        else if (parent is ContentControl control && ReferenceEquals(control.Content, view))
        {
            control.Content = null;
        }
        else if (parent is Border border && ReferenceEquals(border.Child, view))
        {
            border.Child = null;
        }
        else if (parent is Panel panel)
        {
            panel.Children.Remove(view);
        }
    }
}
