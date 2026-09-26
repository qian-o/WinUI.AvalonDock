// WinUI adaptation of DockingManager from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/DockingManager.cs
using System.ComponentModel;
using AvalonDock.Controls;
using AvalonDock.Core.Events;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager
{
    private readonly Dictionary<LayoutFloatingWindow, IDockingWindowHost> floatingHosts = [];
    private readonly Dictionary<LayoutAnchorable, IDockingWindowHost> detachedHosts = [];
    private bool synchronizingWindows;
    private readonly HashSet<IDockingWindowHost> managerHiddenHosts = [];

    internal void ExecuteCloseCommand(LayoutDocument document)
    {
        if (DocumentClosing != null)
        {
            DocumentClosingEventArgs argsClosing = new(document);
            DocumentClosing(this, argsClosing);
            if (argsClosing.Cancel)
            {
                return;
            }
        }

        DocumentCancelEventArgs coreClosingArgs = new(document);
        coreDocumentClosing?.Invoke(this, coreClosingArgs);
        if (coreClosingArgs.Cancel)
        {
            return;
        }

        // Get the document to activate after the close.
        LayoutDocument? documentToActivate = GetDocumentToActivate(document);

        if (!document.CloseDocument())
        {
            return;
        }

        RemoveViewFromLogicalChild(document);
        if (document.Content is UIElement uIElement)
        {
            InternalRemoveLogicalChild(uIElement);
        }

        DocumentClosed?.Invoke(this, new DocumentClosedEventArgs(document));
        coreDocumentClosed?.Invoke(this, new Core.Events.DocumentEventArgs(document));

        // get rid of the closed document content
        document.Content = null;

        // Activate the document determined to be the next active document.
        // This doesn't only update the layout, but also all related (dependency) properties.
        if (documentToActivate != null)
        {
            documentToActivate.IsActive = true;
        }
    }

    internal virtual void ExecuteCloseCommand(LayoutAnchorable anchorable)
    {
        if (!(anchorable is LayoutAnchorable model))
        {
            return;
        }

        AnchorableClosingEventArgs? closingArgs = null;
        AnchorableClosing?.Invoke(this, closingArgs = new AnchorableClosingEventArgs(model));
        if (closingArgs?.Cancel == true)
        {
            return;
        }

        AnchorableCancelEventArgs coreClosingArgs = new(model);
        coreAnchorableClosing?.Invoke(this, coreClosingArgs);
        if (coreClosingArgs.Cancel)
        {
            return;
        }

        bool wasDetached = IsDetached(model);
        if (wasDetached && !model.TestCanClose())
        {
            return;
        }
        // Reattach only after cancellation checks so a canceled close keeps its detached window.
        ReattachAnchorable(model);
        if (model.CloseAnchorable(wasDetached))
        {
            RemoveViewFromLogicalChild(model);
            AnchorableClosed?.Invoke(this, new AnchorableClosedEventArgs(model));
            coreAnchorableClosed?.Invoke(this, new Core.Events.AnchorableEventArgs(model));
        }
    }

    internal virtual void ExecuteHideCommand(LayoutAnchorable anchorable)
    {
        if (!(anchorable is LayoutAnchorable model))
        {
            return;
        }

        AnchorableHidingEventArgs? hidingArgs = null;
        AnchorableHiding?.Invoke(this, hidingArgs = new AnchorableHidingEventArgs(model));
        if (hidingArgs?.CloseInsteadOfHide == true)
        {
            ExecuteCloseCommand(model);
            return;
        }

        if (hidingArgs?.Cancel == true)
        {
            return;
        }

        AnchorableCancelEventArgs coreHidingArgs = new(model);
        coreAnchorableHiding?.Invoke(this, coreHidingArgs);
        if (coreHidingArgs.CloseInsteadOfHide)
        {
            ExecuteCloseCommand(model);
            return;
        }

        if (coreHidingArgs.Cancel)
        {
            return;
        }

        bool wasDetached = IsDetached(model);
        if (wasDetached && !model.TestCanHide())
        {
            return;
        }

        ReattachAnchorable(model);
        if (model.HideAnchorable(!wasDetached))
        {
            AnchorableHidden?.Invoke(this, new AnchorableHiddenEventArgs(model));
            coreAnchorableHidden?.Invoke(this, new Core.Events.AnchorableEventArgs(model));
        }
    }

    internal void ExecuteCloseAllButThisCommand(LayoutContent contentSelected)
    {
        foreach (LayoutContent? contentToClose in Layout.Descendents().OfType<LayoutContent>().Where(d => d != contentSelected && (d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow)).ToArray())
        {
            Close(contentToClose);
        }
    }

    internal void ExecuteCloseAllCommand(LayoutContent contentSelected)
    {
        foreach (LayoutContent? contentToClose in Layout.Descendents().OfType<LayoutContent>().Where(d => (d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow)).ToArray())
        {
            Close(contentToClose);
        }
    }

    internal void ExecuteFloatCommand(LayoutContent contentToFloat)
    {
        contentToFloat.Float();
    }
    internal void ExecuteContentActivateCommand(LayoutContent content) => content.IsActive = true;

    internal virtual void ExecuteAutoHideCommand(LayoutAnchorable anchorable)
    {
        ReattachAnchorable(anchorable);
        anchorable.ToggleAutoHide();
    }

    internal void ExecuteDockCommand(LayoutAnchorable anchorable)
    {
        if (!RaiseContentDocking(anchorable))
        {
            return;
        }

        ReattachAnchorable(anchorable);
        anchorable.Dock();
    }

    internal void ExecuteDockAsDocumentCommand(LayoutContent content)
    {
        if (!RaiseContentDocking(content))
        {
            return;
        }

        ReattachAnchorable(content as LayoutAnchorable);
        content.DockAsDocument();
    }

    internal bool RaiseContentDocking(LayoutContent content)
    {
        ContentDockingEventArgs args = new(content);
        ContentDocking?.Invoke(this, args);
        if (args.Cancel)
        {
            return false;
        }
        ContentCancelEventArgs coreArgs = new(content);
        coreContentDocking?.Invoke(this, coreArgs);
        return !coreArgs.Cancel;
    }

    internal void RaiseContentDocked(LayoutContent content)
    {
        ContentDocked?.Invoke(this, new ContentDockedEventArgs(content));
        coreContentDocked?.Invoke(this, new Core.Events.ContentEventArgs(content));
        SynchronizeWindowHosts();
    }

    internal virtual void StartDraggingFloatingWindowForContent(LayoutContent contentModel, bool startDrag = true)
    {
        LayoutFloatingWindowControl? control = StartFloatingContent(contentModel);
        if (control != null && startDrag)
        {
            control.WindowHost?.BeginMove();
        }
    }

    private LayoutFloatingWindowControl? StartFloatingContent(LayoutContent contentModel)
    {
        if (!TryBeginContentFloating(contentModel))
        {
            return null;
        }

        ReattachAnchorable(contentModel as LayoutAnchorable);
        LayoutFloatingWindowControl? control = null;
        if (contentModel.FindParent<LayoutFloatingWindow>() is { } existing
            && existing.Descendents().OfType<LayoutContent>().Count() == 1
            && floatingHosts.TryGetValue(existing, out IDockingWindowHost? existingHost))
        {
            controlsByHost.TryGetValue(existingHost, out control);
        }

        if (control == null)
        {
            control = CreateFloatingWindow(contentModel, false);
            if (control == null)
            {
                return null;
            }

            IDockingWindowHost createdHost = control.WindowHost
                ?? throw new InvalidOperationException("A created floating window must have a host.");
            NotifyFloatingControlCreated(createdHost);
        }
        // WPF uses DispatcherPriority.Send for ShowWhenHostWindowIsShown and the completed events.
        // Native pointer capture still needs the returned HWND synchronously; model/event completion
        // follows the original dispatcher phase without entering WinUI data drag-and-drop.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
        {
            if (!ReferenceEquals(contentModel.Root?.Manager, this) || control.IsClosed)
            {
                return;
            }

            if (IsLoaded)
            {
                control.Show();
            }
            else
            {
                if (control.WindowHost is { } host)
                {
                    managerHiddenHosts.Add(host);
                }
            }

            RaiseContentFloated(contentModel);
        });
        return control;
    }

    internal bool TryBeginContentFloating(LayoutContent contentModel)
    {
        if (!AllowFloatingWindows || !contentModel.CanFloat || contentModel.Root?.Manager != this)
        {
            return false;
        }

        ContentFloatingEventArgs args = new(contentModel);
        ContentFloating?.Invoke(this, args);
        if (args.Cancel)
        {
            return false;
        }

        ContentCancelEventArgs coreArgs = new(contentModel);
        coreContentFloating?.Invoke(this, coreArgs);
        return !coreArgs.Cancel && AllowFloatingWindows && contentModel.CanFloat && contentModel.Root?.Manager == this;
    }

    internal void RaiseContentFloated(LayoutContent contentModel)
    {
        ContentFloated?.Invoke(this, new ContentFloatedEventArgs(contentModel));
        coreContentFloated?.Invoke(this, new Core.Events.ContentEventArgs(contentModel));
    }
    private IDockingWindowHost CreateWindowHost(LayoutFloatingWindow model, string? title, bool owned)
    {
        LayoutContent? content = model.Descendents().OfType<LayoutContent>().FirstOrDefault();
        Rect bounds = GetFloatingBounds(content);
        ILayoutElementForFloatingWindow? group = model switch
        {
            LayoutDocumentFloatingWindow documents => documents.RootPanel,
            LayoutAnchorableFloatingWindow tools => tools.RootPanel,
            _ => null
        };
        if (group != null)
        {
            group.KeepInsideNearestMonitor();
            if (group.FloatingWidth > 0 && group.FloatingHeight > 0)
            {
                bounds = new Rect(group.FloatingLeft, group.FloatingTop, group.FloatingWidth, group.FloatingHeight);
            }
        }
        return CreatePublicWindowHost(model, title, bounds, placement: WindowPlacement.PreserveBounds);
    }

    private static Rect GetFloatingBounds(LayoutContent? content) => new(
        content?.FloatingLeft ?? 80, content?.FloatingTop ?? 80,
        content?.FloatingWidth > 0 ? content.FloatingWidth : 600,
        content?.FloatingHeight > 0 ? content.FloatingHeight : 400);

    private void AttachFloatingHost(LayoutFloatingWindow model, IDockingWindowHost host)
    {
        host.MoveChanged += (_, update) =>
        {
            OnWindowMove(model, host, update);
        };
        host.Closing += (_, args) =>
        {
            if (controlsByHost.ContainsKey(host))
            {
                return;
            }

            foreach (LayoutContent? content in model.Descendents().OfType<LayoutContent>().ToArray())
            {
                if (content is LayoutAnchorable tool)
                {
                    ExecuteHideCommand(tool);
                }
                else
                {
                    content.Close();
                }
                if (content.FindParent<LayoutFloatingWindow>() == model)
                {
                    args.Cancel = true;
                    break;
                }
            }
        };
        host.Closed += (_, _) =>
        {
            if (ReferenceEquals(chromeDragHost, host) || ReferenceEquals(paneDragHost, host)
                || ReferenceEquals(tabDragHost, host)
                || ReferenceEquals(overlayDragService?.FloatingWindow.Model, model))
            {
                EndContentDrag();
            }

            floatingHosts.Remove(model);
            if (!model.Descendents().OfType<LayoutContent>().Any())
            {
                Layout?.FloatingWindows.Remove(model);
            }
        };
    }

    private void SynchronizeWindowHosts()
    {
        if (synchronizingWindows)
        {
            return;
        }
        synchronizingWindows = true;
        try
        {
            foreach (LayoutAnchorable? anchorable in detachedEntries.Keys.Where(item => !ReferenceEquals(item.Root, Layout)).ToArray())
            {
                ReturnDetached(anchorable, false);
            }

            foreach (KeyValuePair<LayoutFloatingWindow, IDockingWindowHost> entry in floatingHosts.ToArray())
            {
                if (Layout?.FloatingWindows.Contains(entry.Key) != true || !ReferenceEquals(entry.Key.Root, Layout)
                    || !entry.Key.Descendents().OfType<LayoutContent>().Any())
                {
                    floatingHosts.Remove(entry.Key);
                    entry.Value.Dispose();
                    ReleaseFloatingControl(entry.Value);
                }
            }
            if (IsLoaded && Layout != null)
            {
                foreach (LayoutFloatingWindow? model in Layout.FloatingWindows.ToArray())
                {
                    if (floatingHosts.ContainsKey(model) || !ReferenceEquals(model.Root, Layout)
                        || !model.IsValid || !model.Descendents().OfType<LayoutContent>().Any())
                    {
                        continue;
                    }

                    if (!AllowFloatingWindows)
                    {
                        continue;
                    }

                    LayoutContent active = model.Descendents().OfType<LayoutContent>().First();
                    IDockingWindowHost host = CreateWindowHost(model, active.Title, true);
                    floatingHosts.Add(model, host);
                    AttachFloatingHost(model, host);
                    NotifyFloatingControlCreated(host);
                    host.Show();
                }
                foreach (LayoutAnchorable? tool in Layout.Descendents().OfType<LayoutAnchorable>().Where(tool => tool.IsDetached && !restoringDetachedLayout).ToArray())
                {
                    if (IsDetached(tool))
                    {
                        continue;
                    }

                    if (AllowDetachedWindows)
                    {
                        DetachAnchorableToWindow(tool);
                    }
                    else
                    {
                        tool.IsDetached = false;
                        if (tool.IsHidden)
                        {
                            tool.Show();
                        }
                    }
                }
            }
        }
        finally
        {
            synchronizingWindows = false;
        }
    }

    private void CloseWindowHosts(LayoutRoot? oldLayout)
    {
        CloseDetachedWindows(true, oldLayout);
        KeyValuePair<LayoutFloatingWindow, IDockingWindowHost>[] oldFloatingHosts = floatingHosts.ToArray();
        IDockingWindowHost[] remainingDetachedHosts = detachedHosts.Values.ToArray();
        floatingHosts.Clear();
        detachedHosts.Clear();
        managerHiddenHosts.Clear();
        foreach ((LayoutFloatingWindow? model, IDockingWindowHost? host) in oldFloatingHosts)
        {
            if (controlsByHost.TryGetValue(host, out LayoutFloatingWindowControl? control))
            {
                // The original manager calls InternalClose before disconnecting the old
                // root. A canceled close leaves its window and model alive but clears the
                // manager's floating-control list for the replacement layout.
                control.InternalClose();
                if (!host.IsClosed)
                {
                    controlsByHost.Remove(host);
                    floatingControls.Remove(control);
                }
            }
            else
            {
                host.Dispose();
                if (ReferenceEquals(model.Root, oldLayout))
                {
                    oldLayout?.FloatingWindows.Remove(model);
                }
            }
        }
        foreach (IDockingWindowHost? host in remainingDetachedHosts)
        {
            host.Dispose();
        }
    }

    private void HideWindowHosts()
    {
        CloseDetachedWindows(true);
        foreach (IDockingWindowHost? host in floatingHosts.Values.Concat(detachedHosts.Values).ToArray())
        {
            if (host.IsVisible)
            {
                managerHiddenHosts.Add(host);
            }

            if (controlsByHost.TryGetValue(host, out LayoutFloatingWindowControl? control))
            {
                control.DisableBindings();
            }

            host.Hide();
        }
    }

    private void ShowWindowHosts()
    {
        // WPF re-enables every retained floating control, including windows that were
        // already hidden before manager unload. Visibility intent is handled separately.
        foreach (IDockingWindowHost? host in floatingHosts.Values.ToArray())
        {
            if (!host.IsClosed && controlsByHost.TryGetValue(host, out LayoutFloatingWindowControl? control))
            {
                control.EnableBindings();
            }
        }

        foreach (IDockingWindowHost? host in managerHiddenHosts.ToArray())
        {
            if (host.IsClosed)
            {
                continue;
            }

            host.Show();
        }
        managerHiddenHosts.Clear();
    }
    private LayoutDocument? GetDocumentToActivate(LayoutDocument previousDocument)
    {
        ILayoutContainer? parentContainer = previousDocument.Parent;
        IEnumerable<LayoutDocument> siblingDocuments = parentContainer?.Children.OfType<LayoutDocument>() ?? Enumerable.Empty<LayoutDocument>();

        foreach (Tuple<LayoutDocument, LayoutDocument>? childPair in siblingDocuments.Zip(siblingDocuments.Skip(1), Tuple.Create))
        {
            if (childPair.Item2 == previousDocument)
            {
                return childPair.Item1;
            }
        }

        foreach (LayoutDocument document in Layout.Descendents().OfType<LayoutDocument>())
        {
            if (document.IsSelected)
            {
                return document;
            }
        }

        return null;
    }
    private void Close(LayoutContent contentToClose)
    {
        if (!contentToClose.CanClose)
        {
            return;
        }

        LayoutItem? layoutItem = GetLayoutItemFromModel(contentToClose);
        if (layoutItem?.CloseCommand != null)
        {
            if (layoutItem.CloseCommand.CanExecute(null))
            {
                layoutItem.CloseCommand.Execute(null);
            }
        }
        else
        {
            if (contentToClose is LayoutDocument document)
            {
                ExecuteCloseCommand(document);
            }
            else if (contentToClose is LayoutAnchorable anchorable)
            {
                ExecuteCloseCommand(anchorable);
            }
        }
    }
    private void RemoveViewFromLogicalChild(LayoutContent layoutContent)
    {
        if (layoutContent == null)
        {
            return;
        }

        LayoutItem? layoutItem = GetLayoutItemFromModel(layoutContent);
        if (layoutItem == null)
        {
            return;
        }

        if (layoutItem.IsViewExists())
        {
            InternalRemoveLogicalChild(layoutItem.View);
        }
    }
}
