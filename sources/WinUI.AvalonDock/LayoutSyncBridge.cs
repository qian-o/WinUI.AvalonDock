// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/LayoutSyncBridge.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AvalonDock.Core;
using AvalonDock.Layout;

namespace AvalonDock;

/// <summary>
/// Represents the layout Sync Bridge.
/// </summary>
internal sealed class LayoutSyncBridge
{
    private readonly DockingManager manager;
    private readonly IRootDock rootDock;
    private ObservableCollection<object> documentModels = [];
    private ObservableCollection<object> anchorableModels = [];
    private bool isSyncing;
    private readonly Dictionary<object, AnchorSide> contentToSide = new(ReferenceEqualityComparer.Default);
    private readonly List<(INotifyCollectionChanged Source, NotifyCollectionChangedEventHandler Handler)> subscriptions = new();

    /// <summary>
    /// Gets a read-only view of the content-to-side mapping populated during tree walk.
    /// </summary>
    internal IReadOnlyDictionary<object, AnchorSide> ContentToSideMap => contentToSide;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutSyncBridge"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="rootDock">The root Dock.</param>
    public LayoutSyncBridge(DockingManager manager, IRootDock rootDock)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.rootDock = rootDock ?? throw new ArgumentNullException(nameof(rootDock));
    }

    /// <summary>
    /// Executes the attach operation.
    /// </summary>
    public void Attach()
    {
        documentModels = new ObservableCollection<object>();
        anchorableModels = new ObservableCollection<object>();

        CollectDockables(rootDock);

        manager.DocumentsSource = documentModels;
        manager.AnchorablesSource = anchorableModels;

        SyncWindowPolicyToManager();
        SyncActiveContentToManager();

        SubscribeToMvvm();
        SubscribeToManager();
    }

    /// <summary>
    /// Executes the detach operation.
    /// </summary>
    public void Detach()
    {
        UnsubscribeFromManager();
        UnsubscribeFromMvvm();

        manager.DocumentsSource = null;
        manager.AnchorablesSource = null;

        contentToSide.Clear();
        documentModels.Clear();
        anchorableModels.Clear();
    }

    private void CollectDockables(IDockable node)
    {
        if (node is IDocumentDock docDock && docDock.VisibleDockables != null)
        {
            foreach (IDockable child in docDock.VisibleDockables)
            {
                if (!ContainsReference(documentModels, child))
                {
                    documentModels.Add(child);
                }
            }

            SubscribeCollection(docDock.VisibleDockables, OnDocumentCollectionChanged);
        }
        else if (node is IToolDock toolDock && toolDock.VisibleDockables != null)
        {
            AnchorSide side = AlignmentToAnchorSide(toolDock.Alignment);
            foreach (IDockable child in toolDock.VisibleDockables)
            {
                if (!ContainsReference(anchorableModels, child))
                {
                    anchorableModels.Add(child);
                }

                contentToSide[child] = side;
            }

            SubscribeCollection(toolDock.VisibleDockables, OnAnchorableCollectionChanged);
        }

        if (node is IDock dock && dock.VisibleDockables != null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                if (child is IDock)
                {
                    CollectDockables(child);
                }
            }
        }
    }

    private void SubscribeCollection(IList<IDockable> list, NotifyCollectionChangedEventHandler handler)
    {
        if (list is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += handler;
            subscriptions.Add((ncc, handler));
        }
    }

    private void SubscribeToMvvm()
    {
        if (rootDock is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnRootDockPropertyChanged;
        }
    }

    private void UnsubscribeFromMvvm()
    {
        if (rootDock is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged -= OnRootDockPropertyChanged;
        }

        // The application may have replaced a collection or removed a dock subtree.
        // Release the exact instances observed on Attach, not only today's tree.
        foreach ((INotifyCollectionChanged Source, NotifyCollectionChangedEventHandler Handler) entry in subscriptions)
        {
            entry.Source.CollectionChanged -= entry.Handler;
        }

        subscriptions.Clear();
    }

    private void SubscribeToManager()
    {
        manager.ActiveContentChanged += OnManagerActiveContentChanged;
        manager.DocumentClosed += OnManagerDocumentClosed;
    }

    private void UnsubscribeFromManager()
    {
        manager.ActiveContentChanged -= OnManagerActiveContentChanged;
        manager.DocumentClosed -= OnManagerDocumentClosed;
    }

    private void OnDocumentCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (isSyncing || documentModels == null)
        {
            return;
        }

        isSyncing = true;
        try
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (object item in e.NewItems?.OfType<object>() ?? [])
                    {
                        if (!ContainsReference(documentModels, item))
                        {
                            documentModels.Add(item);
                        }
                    }

                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (object item in e.OldItems?.OfType<object>() ?? [])
                    {
                        RemoveReference(documentModels, item);
                    }

                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (object item in e.OldItems?.OfType<object>() ?? [])
                    {
                        RemoveReference(documentModels, item);
                    }

                    foreach (object item in e.NewItems?.OfType<object>() ?? [])
                    {
                        if (!ContainsReference(documentModels, item))
                        {
                            documentModels.Add(item);
                        }
                    }

                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    RebuildDocuments();
                    break;
            }
        }
        finally
        {
            isSyncing = false;
        }
    }

    private void OnAnchorableCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (isSyncing || anchorableModels == null)
        {
            return;
        }

        isSyncing = true;
        try
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AnchorSide? addSide = FindSideForSender(sender);
                    foreach (object item in e.NewItems?.OfType<object>() ?? [])
                    {
                        if (addSide.HasValue)
                        {
                            contentToSide[item] = addSide.Value;
                        }

                        if (!ContainsReference(anchorableModels, item))
                        {
                            anchorableModels.Add(item);
                        }
                    }

                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (object item in e.OldItems?.OfType<object>() ?? [])
                    {
                        RemoveReference(anchorableModels, item);
                        contentToSide.Remove(item);
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (object item in e.OldItems?.OfType<object>() ?? [])
                    {
                        RemoveReference(anchorableModels, item);
                        contentToSide.Remove(item);
                    }
                    AnchorSide? replacementSide = FindSideForSender(sender);
                    foreach (object item in e.NewItems?.OfType<object>() ?? [])
                    {
                        if (replacementSide.HasValue)
                        {
                            contentToSide[item] = replacementSide.Value;
                        }

                        if (!ContainsReference(anchorableModels, item))
                        {
                            anchorableModels.Add(item);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    RebuildAnchorables();
                    break;
            }
        }
        finally
        {
            isSyncing = false;
        }
    }

    private void OnRootDockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (isSyncing)
        {
            return;
        }

        if (e.PropertyName == nameof(IRootDock.ActiveDockable))
        {
            SyncActiveContentToManager();
        }
        else if (e.PropertyName == nameof(IRootDock.AllowFloatingWindows)
            || e.PropertyName == nameof(IRootDock.AllowDetachedWindows))
        {
            SyncWindowPolicyToManager();
        }
    }

    /// <summary>
    /// Pushes the window policy declared by the view model layout onto the docking manager.
    /// </summary>
    /// <remarks>
    /// One way by design: these are a decision the application makes about its layout, so the manager
    /// follows the view model and never writes back. It lets an application built with
    /// <c>AvalonDock.Mvvm</c> or the dependency injection package switch floating and standalone
    /// windows off without touching the view.
    /// </remarks>
    private void SyncWindowPolicyToManager()
    {
        manager.AllowFloatingWindows = rootDock.AllowFloatingWindows;
        manager.AllowDetachedWindows = rootDock.AllowDetachedWindows;
    }

    private void OnManagerActiveContentChanged(object? sender, EventArgs e)
    {
        if (isSyncing)
        {
            return;
        }

        isSyncing = true;
        try
        {
            object? active = manager.ActiveContent;
            if (active is IDockable dockable)
            {
                rootDock.ActiveDockable = dockable;
            }
            else
            {
                FindAndSetActiveDockable(active);
            }
        }
        finally
        {
            isSyncing = false;
        }
    }

    private void OnManagerDocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        if (isSyncing)
        {
            return;
        }

        isSyncing = true;
        try
        {
            object? content = e.Document.Content;
            if (documentModels != null)
            {
                RemoveReference(documentModels, content);
            }

            if (content is IDockable dockable)
            {
                RemoveDockableFromTree(rootDock, dockable, isDocument: true);
            }
        }
        finally
        {
            isSyncing = false;
        }
    }

    private bool RemoveDockableFromTree(IDockable node, IDockable target, bool isDocument)
    {
        if (isDocument && node is IDocumentDock docDock && RemoveDockableReference(docDock.VisibleDockables, target))
        {
            return true;
        }

        if (!isDocument && node is IToolDock toolDock && RemoveDockableReference(toolDock.VisibleDockables, target))
        {
            return true;
        }

        if (node is IDock dock && dock.VisibleDockables != null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                if (child is IDock && RemoveDockableFromTree(child, target, isDocument))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void SyncActiveContentToManager()
    {
        if (isSyncing)
        {
            return;
        }

        isSyncing = true;
        try
        {
            manager.ActiveContent = rootDock.ActiveDockable;
        }
        finally
        {
            isSyncing = false;
        }
    }

    private void FindAndSetActiveDockable(object? content)
    {
        if (content == null)
        {
            rootDock.ActiveDockable = null;
            return;
        }

        IDockable? match = documentModels?.OfType<IDockable>().FirstOrDefault(d => d.Context == content)
            ?? anchorableModels?.OfType<IDockable>().FirstOrDefault(d => d.Context == content);

        rootDock.ActiveDockable = match;
    }

    private void RebuildDocuments()
    {
        List<object> current = new();
        CollectDocumentsFromTree(rootDock, current);
        foreach (object? item in documentModels.ToArray())
        {
            if (!ContainsReference(current, item))
            {
                RemoveReference(documentModels, item);
            }
        }

        foreach (object item in current)
        {
            if (!ContainsReference(documentModels, item))
            {
                documentModels.Add(item);
            }
        }
    }

    private static void CollectDocumentsFromTree(IDockable node, List<object> documents)
    {
        if (node is IDocumentDock docDock && docDock.VisibleDockables != null)
        {
            foreach (IDockable child in docDock.VisibleDockables)
            {
                if (!ContainsReference(documents, child))
                {
                    documents.Add(child);
                }
            }
        }

        if (node is IDock dock && dock.VisibleDockables != null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                if (child is IDock)
                {
                    CollectDocumentsFromTree(child, documents);
                }
            }
        }
    }

    private void RebuildAnchorables()
    {
        List<object> current = new();
        Dictionary<object, AnchorSide> sides = new(ReferenceEqualityComparer.Default);
        CollectAnchorablesFromTree(rootDock, current, sides);
        contentToSide.Clear();
        foreach (KeyValuePair<object, AnchorSide> side in sides)
        {
            contentToSide.Add(side.Key, side.Value);
        }

        foreach (object? item in anchorableModels.ToArray())
        {
            if (!ContainsReference(current, item))
            {
                RemoveReference(anchorableModels, item);
            }
        }

        foreach (object item in current)
        {
            if (!ContainsReference(anchorableModels, item))
            {
                anchorableModels.Add(item);
            }
        }
    }

    private static void CollectAnchorablesFromTree(IDockable node, List<object> anchorables, Dictionary<object, AnchorSide> sides)
    {
        if (node is IToolDock toolDock && toolDock.VisibleDockables != null)
        {
            AnchorSide side = AlignmentToAnchorSide(toolDock.Alignment);
            foreach (IDockable child in toolDock.VisibleDockables)
            {
                if (!ContainsReference(anchorables, child))
                {
                    anchorables.Add(child);
                }

                sides[child] = side;
            }
        }

        if (node is IDock dock && dock.VisibleDockables != null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                if (child is IDock)
                {
                    CollectAnchorablesFromTree(child, anchorables, sides);
                }
            }
        }
    }

    private AnchorSide? FindSideForSender(object? sender)
    {
        return FindToolDockForCollection(rootDock, sender) is IToolDock td
            ? (AnchorSide?)AlignmentToAnchorSide(td.Alignment)
            : null;
    }

    private static IToolDock? FindToolDockForCollection(IDockable node, object? sender)
    {
        if (node is IToolDock toolDock && toolDock.VisibleDockables == sender)
        {
            return toolDock;
        }

        if (node is IDock dock && dock.VisibleDockables != null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                if (child is IDock)
                {
                    IToolDock? found = FindToolDockForCollection(child, sender);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
        }

        return null;
    }

    private static bool ContainsReference(IEnumerable<object> source, object? item) =>
        source.Any(candidate => ReferenceEquals(candidate, item));

    private static void RemoveReference(ObservableCollection<object> source, object? item)
    {
        for (int index = 0; index < source.Count; index++)
        {
            if (!ReferenceEquals(source[index], item))
            {
                continue;
            }

            source.RemoveAt(index);
            return;
        }
    }

    private static bool RemoveDockableReference(IList<IDockable>? source, IDockable target)
    {
        if (source == null)
        {
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            if (!ReferenceEquals(source[index], target))
            {
                continue;
            }

            source.RemoveAt(index);
            return true;
        }
        return false;
    }

    private static AnchorSide AlignmentToAnchorSide(DockAlignment alignment)
    {
        switch (alignment)
        {
            case DockAlignment.Left:
                return AnchorSide.Left;
            case DockAlignment.Right:
                return AnchorSide.Right;
            case DockAlignment.Top:
                return AnchorSide.Top;
            case DockAlignment.Bottom:
                return AnchorSide.Bottom;
            default:
                return AnchorSide.Right;
        }
    }
}
