// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / DockingManager.cs detached-window extension points.
using System.ComponentModel;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock;

public partial class DockingManager
{
    private bool restoringDetachedLayout;
    private LayoutRoot? pendingDetachedRestoreRoot;
    private LayoutAnchorable[] pendingDetachedRestores = [];
    private bool pendingDetachedRestoreQueued;
    private readonly Dictionary<LayoutAnchorable, DetachedEntry> detachedEntries = [];
    private void RestoreDetachedAnchorables(LayoutRoot layout)
    {
        if (isDisposed || layout == null)
        {
            return;
        }

        LayoutAnchorable[] toDetach = layout.Descendents().OfType<LayoutAnchorable>().Where(tool => tool.IsDetached).ToArray();
        if (toDetach.Length == 0)
        {
            return;
        }

        if (!AllowDetachedWindows)
        {
            foreach (LayoutAnchorable? anchorable in toDetach)
            {
                anchorable.IsDetached = false;
            }

            foreach (LayoutAnchorable? anchorable in toDetach.Where(tool => tool.IsHidden))
            {
                anchorable.Show();
            }

            return;
        }

        // WinUI cannot construct a detached Window before the manager gets an XamlRoot.
        // A loaded manager follows the original immediate flag-clear phase; an
        // unmounted one retains serialized flags until Loaded for native hosting.
        if (IsLoaded)
        {
            foreach (LayoutAnchorable? anchorable in toDetach)
            {
                anchorable.IsDetached = false;
            }
        }

        pendingDetachedRestoreRoot = layout;
        pendingDetachedRestores = toDetach;
        SchedulePendingDetachedRestore();
    }

    private void SchedulePendingDetachedRestore()
    {
        if (isDisposed || pendingDetachedRestoreRoot == null || pendingDetachedRestoreQueued)
        {
            return;
        }

        if (!IsLoaded)
        {
            // An unattached manager has no native owner. Preserve the saved flags
            // until Loaded rather than turning pending state into a visible host.
            return;
        }
        pendingDetachedRestoreQueued = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            pendingDetachedRestoreQueued = false;
            LayoutRoot? layout = pendingDetachedRestoreRoot;
            if (isDisposed || !IsLoaded || layout == null || !ReferenceEquals(layout, Layout))
            {
                return;
            }

            LayoutAnchorable[] toDetach = pendingDetachedRestores;
            pendingDetachedRestoreRoot = null;
            pendingDetachedRestores = [];
            foreach (LayoutAnchorable anchorable in toDetach)
            {
                anchorable.IsDetached = false;
            }

            foreach (LayoutAnchorable anchorable in toDetach)
            {
                if (ReferenceEquals(anchorable.Root, layout) && !IsDetached(anchorable))
                {
                    DetachAnchorableToWindow(anchorable);
                }
            }
        });
    }

    private void CancelPendingDetachedRestore()
    {
        LayoutRoot? root = pendingDetachedRestoreRoot;
        LayoutAnchorable[] anchorables = pendingDetachedRestores;
        pendingDetachedRestoreRoot = null;
        pendingDetachedRestores = [];
        pendingDetachedRestoreQueued = false;
        foreach (LayoutAnchorable anchorable in anchorables)
        {
            if (root != null && ReferenceEquals(anchorable.Root, root))
            {
                anchorable.IsDetached = true;
            }
        }
    }

    public IEnumerable<LayoutAnchorable> DetachedAnchorables => detachedEntries.Keys.ToList();
    public bool IsDetached(LayoutAnchorable anchorable) => anchorable != null && detachedEntries.ContainsKey(anchorable);
    public void DetachAnchorableToWindow(LayoutAnchorable anchorable)
    {
        if (isDisposed || !AllowDetachedWindows)
        {
            return;
        }

        if (anchorable == null || IsDetached(anchorable))
        {
            return;
        }

        if (!(GetLayoutItemFromModel(anchorable) is LayoutAnchorableItem layoutItem))
        {
            return;
        }

        ContentPresenter view = layoutItem.View;
        if (view == null)
        {
            return;
        }

        bool wasHidden = anchorable.IsHidden;
        object? restoreState = DetachFromLayout(anchorable);
        DetachedAnchorableWindow? window = null;
        try
        {
            DisconnectDetachedView(view);
            InternalRemoveLogicalChild(view);
            window = new DetachedAnchorableWindow(anchorable, view, CreateDetachedWindowHeader(anchorable));
            window.UpdateThemeResources(null, Theme);
            DetachedEntry entry = new(window, restoreState,
                (_, _) => ReturnDetached(anchorable, true),
                (_, _) => ReturnDetached(anchorable, false, keepDetachedFlag: true));
            detachedEntries.Add(anchorable, entry);
            detachedHosts.Add(anchorable, window.WindowHost);
            anchorable.PropertyChanged += OnDetachedModelChanged;
            anchorable.IsDetached = true;
            window.WindowHost.Closed += entry.Closed;
            window.WindowHost.OwnerClosed += entry.OwnerClosed;
            window.Show();
            OnDetachedAnchorablesChanged(anchorable);
        }
        catch
        {
            if (detachedEntries.Remove(anchorable, out DetachedEntry? failed))
            {
                failed.Window.WindowHost.Closed -= failed.Closed;
                failed.Window.WindowHost.OwnerClosed -= failed.OwnerClosed;
            }
            detachedHosts.Remove(anchorable);
            anchorable.PropertyChanged -= OnDetachedModelChanged;
            window?.ReleaseView();
            window?.WindowHost.Dispose();
            InternalAddLogicalChild(view);
            anchorable.IsDetached = false;
            if (!wasHidden && ReferenceEquals(anchorable.Root, Layout))
            {
                ReturnToLayout(anchorable, restoreState);
            }

            throw;
        }
    }
    public void ReattachAnchorable(LayoutAnchorable? anchorable) => ReturnDetached(anchorable, true);
    public void ReattachAllDetachedAnchorables()
    {
        foreach (LayoutAnchorable? anchorable in detachedEntries.Keys.ToArray())
        {
            ReattachAnchorable(anchorable);
        }
    }
    protected virtual object? DetachFromLayout(LayoutAnchorable anchorable)
    {
        anchorable?.HideAnchorable(false);
        return null;
    }
    protected virtual void ReturnToLayout(LayoutAnchorable anchorable, object? restoreState)
    {
        if (anchorable?.IsHidden == true)
        {
            anchorable.Show();
        }
    }
    protected virtual FrameworkElement CreateDetachedWindowHeader(LayoutAnchorable anchorable) => new AnchorablePaneTitle { Model = anchorable };
    protected virtual void OnDetachedAnchorablesChanged(LayoutAnchorable anchorable)
    {
    }
    protected void ActivateDetachedWindow(LayoutAnchorable anchorable)
    {
        if (anchorable != null && detachedEntries.TryGetValue(anchorable, out DetachedEntry? entry))
        {
            entry.Window.ActivateHost();
        }
    }
    private void ReturnDetached(LayoutAnchorable? anchorable, bool returnToLayout, bool keepDetachedFlag = false, LayoutRoot? formerLayout = null)
    {
        if (anchorable == null || !detachedEntries.Remove(anchorable, out DetachedEntry? entry))
        {
            return;
        }

        detachedHosts.Remove(anchorable);
        entry.Window.WindowHost.Closed -= entry.Closed;
        entry.Window.WindowHost.OwnerClosed -= entry.OwnerClosed;
        anchorable.PropertyChanged -= OnDetachedModelChanged;
        managerHiddenHosts.Remove(entry.Window.WindowHost);
        if (!keepDetachedFlag)
        {
            anchorable.IsDetached = false;
        }

        ContentPresenter? view = entry.Window.ReleaseView();
        if (!isDisposed && view != null && ReferenceEquals(anchorable.Root, formerLayout ?? Layout))
        {
            InternalAddLogicalChild(view);
        }

        if (!entry.Window.IsClosed)
        {
            entry.Window.WindowHost.Dispose();
        }

        if (!isDisposed && returnToLayout && ReferenceEquals(anchorable.Root, formerLayout ?? Layout))
        {
            ReturnToLayout(anchorable, entry.RestoreState);
        }

        if (!isDisposed)
        {
            OnDetachedAnchorablesChanged(anchorable);
        }
    }
    private void OnDetachedModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (isDisposed || sender is not LayoutAnchorable anchorable || args.PropertyName is not (nameof(LayoutElement.Root) or nameof(LayoutElement.Parent))
            || !detachedEntries.TryGetValue(anchorable, out DetachedEntry? entry))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!isDisposed && detachedEntries.TryGetValue(anchorable, out DetachedEntry? current) && ReferenceEquals(current, entry)
                && (!ReferenceEquals(anchorable.Root, Layout) || !Layout.Descendents().Contains(anchorable)))
            {
                ReturnDetached(anchorable, false);
            }
        });
    }
    private void CloseDetachedWindows(bool returnToLayout, LayoutRoot? formerLayout = null)
    {
        bool previous = synchronizingWindows;
        synchronizingWindows = true;
        try
        {
            foreach (LayoutAnchorable? anchorable in detachedEntries.Keys.ToArray())
            {
                ReturnDetached(anchorable, returnToLayout, keepDetachedFlag: true, formerLayout: formerLayout);
            }
        }
        finally { synchronizingWindows = previous; }
    }
    private static void DisconnectDetachedView(ContentPresenter view)
    {
        switch (VisualTreeHelper.GetParent(view))
        {
            case ContentPresenter presenter when ReferenceEquals(presenter.Content, view):
                presenter.Content = null;
                break;
            case ContentControl control when ReferenceEquals(control.Content, view):
                control.Content = null;
                break;
            case Panel panel:
                panel.Children.Remove(view);
                break;
            case Border border when ReferenceEquals(border.Child, view):
                border.Child = null;
                break;
        }
    }
    private sealed record DetachedEntry(DetachedAnchorableWindow Window, object? RestoreState,
        EventHandler Closed, EventHandler OwnerClosed);
}
