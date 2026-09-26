// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Source: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/DockingManager.cs
using System.Collections;
using System.Collections.Specialized;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock;

public partial class DockingManager : Core.IDockingManager
{
    private LayoutSyncBridge? syncBridge;
    private ILayoutUpdateStrategy? installedAlignmentStrategy;
    private readonly Core.Serialization.ILayoutDtoMapper dtoMapper = new Serialization.LayoutDtoMapper();

    public bool SuspendDocumentsSourceBinding
    {
        get; set;
    }
    public bool SuspendAnchorablesSourceBinding
    {
        get; set;
    }
    [System.ComponentModel.Bindable(false)]
    [System.ComponentModel.Description("Gets whether auto-hidden anchorables are presented in the classic auto-hide flyout.")]
    [System.ComponentModel.Category("AutoHideWindow")]
    public bool SupportsAutoHideFlyout { get; protected set; } = true;

    Core.Serialization.ISerializableLayoutRoot Core.Serialization.ISerializableDockingManager.Layout
    {
        get => Layout;
        set => Layout = (LayoutRoot)value;
    }

    Core.Serialization.ILayoutDtoMapper Core.Serialization.ISerializableDockingManager.DtoMapper => dtoMapper;

    public static readonly DependencyProperty AutoHideDelayProperty = DependencyProperty.Register(
        nameof(AutoHideDelay), typeof(int), typeof(DockingManager),
        new PropertyMetadata(500, (owner, args) =>
            ((DockingManager)owner).autoHideWindowManager?.UpdateCloseDelay((int)args.NewValue)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the wait time in milliseconds that is applicable when the system AutoHides a LayoutAnchorableControl (reduces it to a side anchor).")]
    [System.ComponentModel.Category("AutoHideWindow")]
    public int AutoHideDelay
    {
        get => (int)GetValue(AutoHideDelayProperty);
        set => SetValue(AutoHideDelayProperty, value);
    }

    public static readonly DependencyProperty DockLayoutProperty = DependencyProperty.Register(
        nameof(DockLayout), typeof(Core.IRootDock), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnDockLayoutChanged(e.OldValue as Core.IRootDock, e.NewValue as Core.IRootDock)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the MVVM layout model for ViewModel-driven docking.")]
    [System.ComponentModel.Category("Layout")]
    public Core.IRootDock? DockLayout
    {
        get => (Core.IRootDock?)GetValue(DockLayoutProperty);
        set => SetValue(DockLayoutProperty, value);
    }

    protected virtual void OnDockLayoutChanged(Core.IRootDock? oldValue, Core.IRootDock? newValue)
    {
        syncBridge?.Detach();
        syncBridge = null;
        if (installedAlignmentStrategy != null && LayoutUpdateStrategy == installedAlignmentStrategy)
        {
            LayoutUpdateStrategy = null;
        }

        installedAlignmentStrategy = null;

        if (newValue != null)
        {
            syncBridge = new LayoutSyncBridge(this, newValue);
            if (LayoutUpdateStrategy == null)
            {
                installedAlignmentStrategy = new DockAlignmentStrategy(syncBridge.ContentToSideMap);
                LayoutUpdateStrategy = installedAlignmentStrategy;
            }
            syncBridge.Attach();
        }
    }

    public static readonly DependencyProperty DocumentsSourceProperty = DependencyProperty.Register(
        nameof(DocumentsSource), typeof(IEnumerable), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnDocumentsSourceChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the source collection of LayoutDocument objects.")]
    [System.ComponentModel.Category("Document")]
    public IEnumerable? DocumentsSource
    {
        get => (IEnumerable?)GetValue(DocumentsSourceProperty);
        set => SetValue(DocumentsSourceProperty, value);
    }

    public static readonly DependencyProperty AnchorablesSourceProperty = DependencyProperty.Register(
        nameof(AnchorablesSource), typeof(IEnumerable), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnAnchorablesSourceChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the source collection for all LayoutAnchorable objects managed in this framework.")]
    [System.ComponentModel.Category("Anchorable")]
    public IEnumerable? AnchorablesSource
    {
        get => (IEnumerable?)GetValue(AnchorablesSourceProperty);
        set => SetValue(AnchorablesSourceProperty, value);
    }

    protected virtual void OnDocumentsSourceChanged(DependencyPropertyChangedEventArgs e)
    {
        DetachDocumentsSource(Layout, e.OldValue as IEnumerable);
        AttachDocumentsSource(Layout, e.NewValue as IEnumerable);
    }

    protected virtual void OnAnchorablesSourceChanged(DependencyPropertyChangedEventArgs e)
    {
        DetachAnchorablesSource(Layout, e.OldValue as IEnumerable);
        AttachAnchorablesSource(Layout, e.NewValue as IEnumerable);
    }

    private bool suspendLayoutItemCreation;

    private void AttachDocumentsSource(LayoutRoot? layout, IEnumerable? documentsSource)
    {
        if (documentsSource == null)
        {
            return;
        }

        if (layout == null)
        {
            return;
        }

        HashSet<object?> documentsImported = new(
            layout.Descendents().OfType<LayoutDocument>().Select(d => d.Content),
            ReferenceEqualityComparer.Default);
        ImportDocuments(layout, documentsSource.OfType<object>().Where(item => !documentsImported.Contains(item)).ToArray(), false);
        if (documentsSource is INotifyCollectionChanged documentsSourceAsNotifier)
        {
            documentsSourceAsNotifier.CollectionChanged += DocumentsSourceElementsChanged;
        }
    }

    private void ImportDocuments(LayoutRoot layout, IEnumerable documents, bool requireOwnedRoot)
    {
        LayoutDocumentPane? documentPane = layout.LastFocusedDocument?.Parent as LayoutDocumentPane
            ?? layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        bool wasSuspended = suspendLayoutItemCreation;
        suspendLayoutItemCreation = true;
        try
        {
            foreach (object? content in documents)
            {
                LayoutDocument document = new()
                {
                    Content = content
                };
                bool inserted = LayoutUpdateStrategy?.BeforeInsertDocument(layout, document, documentPane) ?? false;
                if (!inserted)
                {
                    if (documentPane == null)
                    {
                        throw new InvalidOperationException("Layout must contains at least one LayoutDocumentPane in order to host documents");
                    }

                    documentPane.Children.Add(document);
                }

                LayoutUpdateStrategy?.AfterInsertDocument(layout, document);
                if (!requireOwnedRoot || document.Root?.Manager == this)
                {
                    CreateDocumentLayoutItem(document);
                }
            }
        }
        finally
        {
            suspendLayoutItemCreation = wasSuspended;
        }
    }

    private void DocumentsSourceElementsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Layout == null)
        {
            return;
        }
        // When deserializing documents are created automatically by the deserializer
        if (SuspendDocumentsSourceBinding)
        {
            return;
        }

        // handle remove
        if (e.Action == NotifyCollectionChangedAction.Remove ||
            e.Action == NotifyCollectionChangedAction.Replace)
        {
            if (e.OldItems != null)
            {
                HashSet<object?> oldItems = new(e.OldItems.Cast<object>(), ReferenceEqualityComparer.Default);
                LayoutDocument[] documentsToRemove = Layout.Descendents().OfType<LayoutDocument>().Where(d => oldItems.Contains(d.Content)).ToArray();
                foreach (LayoutDocument? documentToRemove in documentsToRemove)
                {
                    documentToRemove.Content = null;
                    documentToRemove.Parent?.RemoveChild(documentToRemove);
                    RemoveViewFromLogicalChild(documentToRemove);
                }
            }
        }

        // handle add
        if (e.NewItems != null && (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace))
        {
            ImportDocuments(Layout, e.NewItems, true);
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            object[] contents = DocumentsSource?.Cast<object>().ToArray() ?? [];
            LayoutDocument[] documentsToRemove = GetItemsToRemoveAfterReset<LayoutDocument>(contents);
            foreach (LayoutDocument documentToRemove in documentsToRemove)
            {
                (documentToRemove.Parent as ILayoutContainer)?.RemoveChild(
                    documentToRemove);
                RemoveViewFromLogicalChild(documentToRemove);
            }
            HashSet<object?> imported = new(Layout.Descendents().OfType<LayoutDocument>().Select(item => item.Content), ReferenceEqualityComparer.Default);
            ImportDocuments(Layout, contents.Where(item => !imported.Contains(item)), true);
        }

        Layout?.CollectGarbage();
    }

    private void DetachDocumentsSource(LayoutRoot? layout, IEnumerable? documentsSource)
    {
        if (documentsSource == null)
        {
            return;
        }

        if (layout == null)
        {
            return;
        }

        HashSet<object?> sourceItems = new(documentsSource.Cast<object>(), ReferenceEqualityComparer.Default);
        LayoutDocument[] documentsToRemove = layout.Descendents().OfType<LayoutDocument>()
            .Where(d => sourceItems.Contains(d.Content)).ToArray();

        foreach (LayoutDocument? documentToRemove in documentsToRemove)
        {
            (documentToRemove.Parent as ILayoutContainer)?.RemoveChild(
                documentToRemove);
            RemoveViewFromLogicalChild(documentToRemove);
        }

        if (documentsSource is INotifyCollectionChanged documentsSourceAsNotifier)
        {
            documentsSourceAsNotifier.CollectionChanged -= DocumentsSourceElementsChanged;
        }
    }

    private void AttachAnchorablesSource(LayoutRoot? layout, IEnumerable? anchorablesSource)
    {
        if (anchorablesSource == null)
        {
            return;
        }

        if (layout == null)
        {
            return;
        }

        HashSet<object?> anchorablesImported = new(
            layout.Descendents().OfType<LayoutAnchorable>().Select(d => d.Content),
            ReferenceEqualityComparer.Default);
        ImportAnchorables(layout, anchorablesSource.OfType<object>().Where(item => !anchorablesImported.Contains(item)).ToArray(), false);
        if (anchorablesSource is INotifyCollectionChanged anchorablesSourceAsNotifier)
        {
            anchorablesSourceAsNotifier.CollectionChanged += AnchorablesSourceElementsChanged;
        }
    }

    private void ImportAnchorables(LayoutRoot layout, IEnumerable anchorables, bool requireOwnedRoot)
    {
        LayoutAnchorablePane? pane = layout.ActiveContent?.Parent as LayoutAnchorablePane
            ?? layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(candidate => !candidate.IsHostedInFloatingWindow && candidate.GetSide() == AnchorSide.Right)
            ?? layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
        bool wasSuspended = suspendLayoutItemCreation;
        suspendLayoutItemCreation = true;
        try
        {
            foreach (object? content in anchorables)
            {
                LayoutAnchorable anchorable = new()
                {
                    Content = content
                };
                bool inserted = LayoutUpdateStrategy?.BeforeInsertAnchorable(layout, anchorable, pane) ?? false;
                if (!inserted)
                {
                    if (pane == null)
                    {
                        LayoutPanel panel = new()
                        {
                            Orientation = Orientation.Horizontal
                        };
                        if (layout.RootPanel != null)
                        {
                            panel.Children.Add(layout.RootPanel);
                        }

                        layout.RootPanel = panel;
                        pane = new LayoutAnchorablePane { DockWidth = new GridLength(200.0, GridUnitType.Pixel) };
                        panel.Children.Add(pane);
                    }
                    pane.Children.Add(anchorable);
                }

                LayoutUpdateStrategy?.AfterInsertAnchorable(layout, anchorable);
                if (!requireOwnedRoot || anchorable.Root?.Manager == this)
                {
                    CreateAnchorableLayoutItem(anchorable);
                }
            }
        }
        finally
        {
            suspendLayoutItemCreation = wasSuspended;
        }
    }

    private void AnchorablesSourceElementsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Layout == null)
        {
            return;
        }

        // When deserializing documents are created automatically by the deserializer
        if (SuspendAnchorablesSourceBinding)
        {
            return;
        }

        // handle remove
        if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace)
        {
            if (e.OldItems != null)
            {
                HashSet<object?> oldItems = new(e.OldItems.Cast<object>(), ReferenceEqualityComparer.Default);
                LayoutAnchorable[] anchorablesToRemove = Layout.Descendents().OfType<LayoutAnchorable>().Where(d => oldItems.Contains(d.Content)).ToArray();
                foreach (LayoutAnchorable? anchorableToRemove in anchorablesToRemove)
                {
                    anchorableToRemove.Content = null;
                    anchorableToRemove.Parent?.RemoveChild(anchorableToRemove);
                    RemoveViewFromLogicalChild(anchorableToRemove);
                }
            }
        }

        // handle add
        if (e.NewItems != null && (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace))
        {
            ImportAnchorables(Layout, e.NewItems, true);
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            object[] contents = AnchorablesSource?.Cast<object>().ToArray() ?? [];
            LayoutAnchorable[] anchorablesToRemove = GetItemsToRemoveAfterReset<LayoutAnchorable>(contents);
            foreach (LayoutAnchorable anchorableToRemove in anchorablesToRemove)
            {
                (anchorableToRemove.Parent as ILayoutContainer)?.RemoveChild(
                    anchorableToRemove);
                RemoveViewFromLogicalChild(anchorableToRemove);
            }
            HashSet<object?> imported = new(Layout.Descendents().OfType<LayoutAnchorable>().Select(item => item.Content), ReferenceEqualityComparer.Default);
            ImportAnchorables(Layout, contents.Where(item => !imported.Contains(item)), true);
        }

        Layout?.CollectGarbage();
    }

    private void DetachAnchorablesSource(LayoutRoot? layout, IEnumerable? anchorablesSource)
    {
        if (anchorablesSource == null)
        {
            return;
        }

        if (layout == null)
        {
            return;
        }

        HashSet<object?> sourceItems = new(anchorablesSource.Cast<object>(), ReferenceEqualityComparer.Default);
        LayoutAnchorable[] anchorablesToRemove = layout.Descendents().OfType<LayoutAnchorable>()
            .Where(d => sourceItems.Contains(d.Content)).ToArray();

        foreach (LayoutAnchorable? anchorableToRemove in anchorablesToRemove)
        {
            anchorableToRemove.Parent?.RemoveChild(anchorableToRemove);
            RemoveViewFromLogicalChild(anchorableToRemove);
        }

        if (anchorablesSource is INotifyCollectionChanged anchorablesSourceAsNotifier)
        {
            anchorablesSourceAsNotifier.CollectionChanged -= AnchorablesSourceElementsChanged;
        }
    }

    private TLayoutType[] GetItemsToRemoveAfterReset<TLayoutType>(IEnumerable source)
        where TLayoutType : LayoutContent
    {
        // Find remaining items in source
        HashSet<object?> itemsThatRemain = new(source.Cast<object>(), ReferenceEqualityComparer.Default);
        // Find the removed items that are not in the remaining collection
        return Layout.Descendents()
                     .OfType<TLayoutType>()
                     .Where(x => !itemsThatRemain.Contains(x.Content))
                     .ToArray();
    }

    private event EventHandler<Core.Events.DocumentCancelEventArgs>? coreDocumentClosing;
    event EventHandler<Core.Events.DocumentCancelEventArgs> Core.IDockingManager.DocumentClosing
    {
        add => coreDocumentClosing += value;
        remove => coreDocumentClosing -= value;
    }

    private event EventHandler<Core.Events.DocumentEventArgs>? coreDocumentClosed;
    event EventHandler<Core.Events.DocumentEventArgs> Core.IDockingManager.DocumentClosed
    {
        add => coreDocumentClosed += value;
        remove => coreDocumentClosed -= value;
    }

    private event EventHandler<Core.Events.AnchorableCancelEventArgs>? coreAnchorableClosing;
    event EventHandler<Core.Events.AnchorableCancelEventArgs> Core.IDockingManager.AnchorableClosing
    {
        add => coreAnchorableClosing += value;
        remove => coreAnchorableClosing -= value;
    }

    private event EventHandler<Core.Events.AnchorableEventArgs>? coreAnchorableClosed;
    event EventHandler<Core.Events.AnchorableEventArgs> Core.IDockingManager.AnchorableClosed
    {
        add => coreAnchorableClosed += value;
        remove => coreAnchorableClosed -= value;
    }

    private event EventHandler<Core.Events.AnchorableCancelEventArgs>? coreAnchorableHiding;
    event EventHandler<Core.Events.AnchorableCancelEventArgs> Core.IDockingManager.AnchorableHiding
    {
        add => coreAnchorableHiding += value;
        remove => coreAnchorableHiding -= value;
    }

    private event EventHandler<Core.Events.AnchorableEventArgs>? coreAnchorableHidden;
    event EventHandler<Core.Events.AnchorableEventArgs> Core.IDockingManager.AnchorableHidden
    {
        add => coreAnchorableHidden += value;
        remove => coreAnchorableHidden -= value;
    }

    private event EventHandler<Core.Events.ContentCancelEventArgs>? coreContentFloating;
    event EventHandler<Core.Events.ContentCancelEventArgs> Core.IDockingManager.ContentFloating
    {
        add => coreContentFloating += value;
        remove => coreContentFloating -= value;
    }

    private event EventHandler<Core.Events.ContentEventArgs>? coreContentFloated;
    event EventHandler<Core.Events.ContentEventArgs> Core.IDockingManager.ContentFloated
    {
        add => coreContentFloated += value;
        remove => coreContentFloated -= value;
    }

    private event EventHandler<Core.Events.ContentCancelEventArgs>? coreContentDocking;
    event EventHandler<Core.Events.ContentCancelEventArgs> Core.IDockingManager.ContentDocking
    {
        add => coreContentDocking += value;
        remove => coreContentDocking -= value;
    }

    private event EventHandler<Core.Events.ContentEventArgs>? coreContentDocked;
    event EventHandler<Core.Events.ContentEventArgs> Core.IDockingManager.ContentDocked
    {
        add => coreContentDocked += value;
        remove => coreContentDocked -= value;
    }
}
