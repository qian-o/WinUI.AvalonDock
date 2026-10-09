using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock.Controls;

/// <summary>Owns the native parent of a manager-owned content view across template changes.</summary>
internal sealed class LayoutContentViewHost(Control owner, DependencyProperty layoutItemProperty)
{
    private ContentPresenter? contentHost;
    private ContentPresenter? hostedView;
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
        if (contentHost.GetBindingExpression(ContentPresenter.ContentProperty) != null)
        {
            // A consumer template owns its live binding, including an initially
            // unresolved value. Assigning Content here would replace that binding.
            hostedView = ReferenceEquals(contentHost.Content, view) ? view : null;
            return;
        }

        if (ReferenceEquals(contentHost.Content, view))
        {
            hostedView = view;
            return;
        }

        if (view != null)
        {
            Detach(view);
        }

        contentHost.Content = view;
        hostedView = view;
    }

    internal void RememberBoundTemplateHost()
    {
        if (LayoutItem?.IsViewExists() != true)
        {
            return;
        }

        // Consumer templates may bind LayoutItem.View without a named part. WinUI
        // disconnects their tree before Template changes; remember the actual owner
        // so the retired template can release its native binding and parent.
        ContentPresenter view = LayoutItem!.View;
        if (contentHost != null)
        {
            if (ReferenceEquals(contentHost.Content, view))
            {
                hostedView = view;
            }

            return;
        }

        contentHost = owner.FindVisualChildren<ContentPresenter>()
            .FirstOrDefault(host => ReferenceEquals(host.Content, view));
        hostedView = contentHost == null ? null : view;
    }

    internal void Release(bool preserveBinding = false)
    {
        // A named host's binding may resolve after Attach but before LayoutUpdated.
        // Recognize that current view without claiming unrelated consumer content.
        if (contentHost != null && LayoutItem?.IsViewExists() == true && ReferenceEquals(contentHost.Content, LayoutItem.View))
        {
            hostedView = LayoutItem.View;
        }

        // A still-current consumer template follows the LayoutItem DP change itself;
        // only a retired template must have that binding explicitly disconnected.
        if (contentHost != null && hostedView != null && ReferenceEquals(contentHost.Content, hostedView)
            && (!preserveBinding || contentHost.GetBindingExpression(ContentPresenter.ContentProperty) == null))
        {
            contentHost.Content = null;
        }

        contentHost = null;
        hostedView = null;
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
