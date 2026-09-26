// WinUI adaptation of DockingManager from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/DockingManager.cs
using System.Collections.Specialized;
using System.ComponentModel;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock;

[ContentProperty(Name = nameof(Layout))]
[TemplatePart(Name = "PART_LayoutHost", Type = typeof(ContentPresenter))]
public partial class DockingManager : Control, IDisposable
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<LayoutContent, WeakReference<DockingManager>> LayoutItemOwners = new();
    private readonly ILayoutEngine layoutEngine = new DefaultLayoutEngine();
    private readonly List<LayoutItem> layoutItems = [];
    private ContentPresenter? layoutHost;
    private UIElement? layoutView;
    private bool insideInternalSetActiveContent;
    private bool refreshPending;
    private bool rootCommandRequeryPending;
    private LayoutRoot? rootCommandRequeryRoot;
    private LayoutRoot? attachedLayout;
    private bool replacingNullLayout;
    private LayoutRoot? layoutBeforeNullReplacement;
    private bool isDisposed;

    public DockingManager()
    {
        InitializeLogicalPresentation();
        InitializeTemplateProperties();
        DefaultStyleKey = typeof(DockingManager);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Layout = new LayoutRoot
        {
            RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(new LayoutDocumentPane()))
        };
    }

    /// <summary>在 UI 线程永久释放视图、原生窗口和外部订阅，保留可序列化的布局内容；临时卸载无需调用。</summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
        refreshPending = false;
        rootCommandRequeryPending = false;
        rootCommandRequeryRoot = null;
        layoutBeforeNullReplacement = null;
        LayoutRoot? oldLayout = attachedLayout;
        if (oldLayout != null)
        {
            oldLayout.PropertyChanged -= OnLayoutRootPropertyChanged;
            oldLayout.Updated -= OnLayoutRootUpdated;
            oldLayout.ElementAdded -= Layout_ElementAdded;
            oldLayout.ElementRemoved -= Layout_ElementRemoved;
            oldLayout.FloatingWindows.CollectionChanged -= OnFloatingWindowsChanged;
        }

        ReleaseSources();
        CancelPendingDetachedRestore();
        navigatorWindow?.Abort();
        navigatorWindow = null;
        EndContentDrag();
        dockingOverlay.Dispose();
        FocusElementManager.FinalizeFocusManagement(this);
        autoHideWindowManager?.Dispose();
        autoHideWindowManager = null;
        ReleaseAutoHideViews();
        CloseWindowHosts(oldLayout, preserveLayout: true);
        ReleaseLayoutView();
        DetachLayoutItems();
        ClearLogicalChildrenList();
        attachedLayout = null;
        layoutHost = null;
        autoHideArea = null;
    }

    internal bool IsDisposed => isDisposed;

    public event EventHandler? LayoutChanging;
    public event EventHandler? LayoutChanged;
    public event EventHandler? ActiveContentChanged;
    public event EventHandler<DocumentClosingEventArgs>? DocumentClosing;
    public event EventHandler<DocumentClosedEventArgs>? DocumentClosed;
    public event EventHandler<AnchorableClosingEventArgs>? AnchorableClosing;
    public event EventHandler<AnchorableClosedEventArgs>? AnchorableClosed;
    public event EventHandler<AnchorableHidingEventArgs>? AnchorableHiding;
    public event EventHandler<AnchorableHiddenEventArgs>? AnchorableHidden;
    public event EventHandler<ContentFloatingEventArgs>? ContentFloating;
    public event EventHandler<ContentFloatedEventArgs>? ContentFloated;
    public event EventHandler<ContentDockingEventArgs>? ContentDocking;
    public event EventHandler<ContentDockedEventArgs>? ContentDocked;

    public virtual ILayoutEngine LayoutEngine => layoutEngine;

    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout), typeof(LayoutRoot), typeof(DockingManager),
        new PropertyMetadata(null, LayoutPropertyChanged));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the layout root of the layout tree managed in this framework.")]
    [System.ComponentModel.Category("Layout")]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public LayoutRoot Layout
    {
        get => (LayoutRoot)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public static readonly DependencyProperty ActiveContentProperty = DependencyProperty.Register(
        nameof(ActiveContent), typeof(object), typeof(DockingManager),
        new PropertyMetadata(null, OnActiveContentChanged));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the content that is currently active (document,anchoreable, or null).")]
    [System.ComponentModel.Category("Other")]
    public object? ActiveContent
    {
        get => GetValue(ActiveContentProperty);
        set => SetValue(ActiveContentProperty, value);
    }

    public static readonly DependencyProperty LayoutUpdateStrategyProperty = DependencyProperty.Register(
        nameof(LayoutUpdateStrategy), typeof(ILayoutUpdateStrategy), typeof(DockingManager),
        new PropertyMetadata(null));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the layout strategy class that can be to called by the framework when it needs to position a LayoutAnchorable inside an existing layout.")]
    [System.ComponentModel.Category("Layout")]
    public ILayoutUpdateStrategy? LayoutUpdateStrategy
    {
        get => (ILayoutUpdateStrategy?)GetValue(LayoutUpdateStrategyProperty);
        set => SetValue(LayoutUpdateStrategyProperty, value);
    }

    public static readonly DependencyProperty LayoutItemTemplateProperty = DependencyProperty.Register(
        nameof(LayoutItemTemplate), typeof(DataTemplate), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnLayoutItemTemplateChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplate used to render anchorable and document content.")]
    [System.ComponentModel.Category("Layout")]
    public DataTemplate? LayoutItemTemplate
    {
        get => (DataTemplate?)GetValue(LayoutItemTemplateProperty);
        set => SetValue(LayoutItemTemplateProperty, value);
    }

    public static readonly DependencyProperty LayoutItemTemplateSelectorProperty = DependencyProperty.Register(
        nameof(LayoutItemTemplateSelector), typeof(DataTemplateSelector), typeof(DockingManager),
        new PropertyMetadata(null, (d, e) => ((DockingManager)d).OnLayoutItemTemplateSelectorChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplateSelector to select a DataTemplate of an anchorable.")]
    [System.ComponentModel.Category("Layout")]
    public DataTemplateSelector? LayoutItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(LayoutItemTemplateSelectorProperty);
        set => SetValue(LayoutItemTemplateSelectorProperty, value);
    }

    public static readonly DependencyProperty DocumentHeaderTemplateProperty = DependencyProperty.Register(
        nameof(DocumentHeaderTemplate), typeof(DataTemplate), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnTemplatePropertyChanged(args)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplate to use for document headers.")]
    [System.ComponentModel.Category("Document")]
    public DataTemplate? DocumentHeaderTemplate
    {
        get => (DataTemplate?)GetValue(DocumentHeaderTemplateProperty);
        set => SetValue(DocumentHeaderTemplateProperty, value);
    }

    public static readonly DependencyProperty DocumentHeaderTemplateSelectorProperty = DependencyProperty.Register(
        nameof(DocumentHeaderTemplateSelector), typeof(DataTemplateSelector), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnDocumentHeaderTemplateSelectorChanged(args)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplateSelector that can be used for selecting a DataTemplates for a document header.")]
    [System.ComponentModel.Category("Document")]
    public DataTemplateSelector? DocumentHeaderTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(DocumentHeaderTemplateSelectorProperty);
        set => SetValue(DocumentHeaderTemplateSelectorProperty, value);
    }

    public static readonly DependencyProperty AnchorableHeaderTemplateProperty = DependencyProperty.Register(
        nameof(AnchorableHeaderTemplate), typeof(DataTemplate), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnTemplatePropertyChanged(args)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplate to use for a header of an anchorable")]
    [System.ComponentModel.Category("Anchorable")]
    public DataTemplate? AnchorableHeaderTemplate
    {
        get => (DataTemplate?)GetValue(AnchorableHeaderTemplateProperty);
        set => SetValue(AnchorableHeaderTemplateProperty, value);
    }

    public static readonly DependencyProperty AnchorableHeaderTemplateSelectorProperty = DependencyProperty.Register(
        nameof(AnchorableHeaderTemplateSelector), typeof(DataTemplateSelector), typeof(DockingManager),
        new PropertyMetadata(null, (owner, args) => ((DockingManager)owner).OnAnchorableHeaderTemplateSelectorChanged(args)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the DataTemplateSelector to use for selecting the DataTemplate for the header of an anchorable.")]
    [System.ComponentModel.Category("Anchorable")]
    public DataTemplateSelector? AnchorableHeaderTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(AnchorableHeaderTemplateSelectorProperty);
        set => SetValue(AnchorableHeaderTemplateSelectorProperty, value);
    }

    public static readonly DependencyProperty GridSplitterWidthProperty = DependencyProperty.Register(
        nameof(GridSplitterWidth), typeof(double), typeof(DockingManager),
        new PropertyMetadata(6.0));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the width of a grid splitter")]
    [System.ComponentModel.Category("Other")]
    public double GridSplitterWidth
    {
        get => (double)GetValue(GridSplitterWidthProperty);
        set => SetValue(GridSplitterWidthProperty, value);
    }

    public static readonly DependencyProperty GridSplitterHeightProperty = DependencyProperty.Register(
        nameof(GridSplitterHeight), typeof(double), typeof(DockingManager),
        new PropertyMetadata(6.0));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets the height of a grid splitter")]
    [System.ComponentModel.Category("Other")]
    public double GridSplitterHeight
    {
        get => (double)GetValue(GridSplitterHeightProperty);
        set => SetValue(GridSplitterHeightProperty, value);
    }

    public static readonly DependencyProperty AllowMixedOrientationProperty = DependencyProperty.Register(
        nameof(AllowMixedOrientation), typeof(bool), typeof(DockingManager),
        new PropertyMetadata(false));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether the DockingManager should allow mixed orientation for document panes.")]
    [System.ComponentModel.Category("Other")]
    public bool AllowMixedOrientation
    {
        get => (bool)GetValue(AllowMixedOrientationProperty);
        set => SetValue(AllowMixedOrientationProperty, value);
    }

    public static readonly DependencyProperty AllowFloatingWindowsProperty = DependencyProperty.Register(
        nameof(AllowFloatingWindows), typeof(bool), typeof(DockingManager),
        new PropertyMetadata(true, (d, e) => ((DockingManager)d).OnAllowFloatingWindowsChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether content can be torn off into floating windows.")]
    [System.ComponentModel.Category("FloatingWindow")]
    public bool AllowFloatingWindows
    {
        get => (bool)GetValue(AllowFloatingWindowsProperty);
        set => SetValue(AllowFloatingWindowsProperty, value);
    }

    public static readonly DependencyProperty AllowDetachedWindowsProperty = DependencyProperty.Register(
        nameof(AllowDetachedWindows), typeof(bool), typeof(DockingManager),
        new PropertyMetadata(true, (d, e) => ((DockingManager)d).OnAllowDetachedWindowsChanged(e)));

    [System.ComponentModel.Bindable(true)]
    [System.ComponentModel.Description("Gets or sets a value indicating whether anchorables can be moved into standalone top level windows.")]
    [System.ComponentModel.Category("Anchorable")]
    public bool AllowDetachedWindows
    {
        get => (bool)GetValue(AllowDetachedWindowsProperty);
        set => SetValue(AllowDetachedWindowsProperty, value);
    }

    private void OnLayoutChanging(LayoutRoot newLayout)
    {
        if (isDisposed)
        {
            return;
        }

        LayoutChanging?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnLayoutChanged(LayoutRoot? oldLayout, LayoutRoot newLayout)
    {
        if (isDisposed)
        {
            return;
        }

        // The original LayoutChanged notification precedes detached-window restore.
        // Suppress only native host admission while the old and new roots exchange.
        pendingDetachedRestoreRoot = null;
        pendingDetachedRestores = [];
        restoringDetachedLayout = true;
        try
        {
            navigatorWindow?.Abort();
            oldLayout = attachedLayout;
            if (oldLayout != null)
            {
                oldLayout.PropertyChanged -= OnLayoutRootPropertyChanged;
                oldLayout.Updated -= OnLayoutRootUpdated;
                oldLayout.ElementAdded -= Layout_ElementAdded;
                oldLayout.ElementRemoved -= Layout_ElementRemoved;
                oldLayout.FloatingWindows.CollectionChanged -= OnFloatingWindowsChanged;
            }

            CloseWindowHosts(oldLayout);
            ReleaseLayoutView();
            if (oldLayout != null)
            {
                DetachDocumentsSource(oldLayout, DocumentsSource);
                DetachAnchorablesSource(oldLayout, AnchorablesSource);
                if (oldLayout.Manager == this)
                {
                    oldLayout.Manager = null;
                }
            }

            DetachLayoutItems();
            ClearLogicalChildrenList();
            if (newLayout != null)
            {
                newLayout.Manager = this;
                newLayout.PropertyChanged += OnLayoutRootPropertyChanged;
                newLayout.Updated += OnLayoutRootUpdated;
                newLayout.ElementAdded += Layout_ElementAdded;
                newLayout.ElementRemoved += Layout_ElementRemoved;
                newLayout.FloatingWindows.CollectionChanged += OnFloatingWindowsChanged;
                AttachLayoutItems();
                AttachDocumentsSource(newLayout, DocumentsSource);
                AttachAnchorablesSource(newLayout, AnchorablesSource);
            }
            attachedLayout = newLayout;
            if (newLayout != null && !AllowFloatingWindows)
            {
                DockAllFloatingWindows();
            }

            ReleaseAutoHideViews();
            InitializeAutoHideViews();
            if (IsLoaded && ReferenceEquals(Layout?.Manager, this))
            {
                RefreshLayoutView();
            }

            SynchronizeWindowHosts();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
            RequeryLayoutItemCommands();
        }
        finally { restoringDetachedLayout = false; }
        RestoreDetachedAnchorables(newLayout);
    }

    private static void OnActiveContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        DockingManager manager = (DockingManager)d;
        if (!manager.isDisposed)
        {
            manager.InternalSetActiveContent(e.NewValue);
            manager.OnActiveContentChanged(e);
        }
    }

    protected virtual void OnActiveContentChanged(DependencyPropertyChangedEventArgs e) => ActiveContentChanged?.Invoke(this, EventArgs.Empty);

    protected virtual void OnLayoutItemTemplateChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    protected virtual void OnLayoutItemTemplateSelectorChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    protected virtual void OnAllowFloatingWindowsChanged(DependencyPropertyChangedEventArgs e)
    {
        if (isDisposed)
        {
            return;
        }

        if (!(bool)e.NewValue)
        {
            DockAllFloatingWindows();
        }
        RequeryLayoutItemCommands();
    }

    protected virtual void OnAllowDetachedWindowsChanged(DependencyPropertyChangedEventArgs e)
    {
        if (isDisposed)
        {
            return;
        }

        if (!(bool)e.NewValue)
        {
            ReattachAllDetachedAnchorables();
        }
        RequeryLayoutItemCommands();
    }

    private void RequeryLayoutItemCommands()
    {
        // WPF's CommandManager broadcasts this after either policy callback. Native
        // RelayCommands owned by this manager need the corresponding explicit event.
        foreach (LayoutItem item in layoutItems.ToArray())
        {
            item.NotifyDefaultCommands();
            if (item is LayoutAnchorableItem tool)
            {
                tool.NotifyToolCommands();
            }
        }
    }

    public LayoutItem? GetLayoutItemFromModel(LayoutContent content)
    {
        return layoutItems.FirstOrDefault(item => item.LayoutElement == content);
    }

    protected override void OnApplyTemplate()
    {
        if (isDisposed)
        {
            base.OnApplyTemplate();
            return;
        }

        HideAutoHideWindow();
        ReleaseLayoutView();
        if (!ReferenceEquals(Layout?.Manager, this))
        {
            ReleaseAutoHideViews();
        }

        base.OnApplyTemplate();
        layoutHost = GetTemplateChild("PART_LayoutHost") as ContentPresenter;
        autoHideArea = GetTemplateChild("PART_AutoHideArea") as ContentPresenter;
        InitializeAutoHideViews();
        if (ReferenceEquals(Layout?.Manager, this))
        {
            RefreshLayoutView();
        }
    }

    internal UIElement? CreateUIElementForModel(ILayoutElement? model)
    {
        if (isDisposed)
        {
            return null;
        }

        if (model is LayoutPanel typedLayoutPanel)
        {
            return LayoutViewBuilder.Initialize(new LayoutPanelControl(typedLayoutPanel));
        }

        if (model is LayoutAnchorablePaneGroup typedLayoutAnchorablePaneGroup)
        {
            return LayoutViewBuilder.Initialize(new LayoutAnchorablePaneGroupControl(typedLayoutAnchorablePaneGroup));
        }

        if (model is LayoutDocumentPaneGroup typedLayoutDocumentPaneGroup)
        {
            return LayoutViewBuilder.Initialize(new LayoutDocumentPaneGroupControl(typedLayoutDocumentPaneGroup));
        }

        if (model is LayoutAnchorSide typedLayoutAnchorSide)
        {
            LayoutAnchorSideControl templateModelView = new(typedLayoutAnchorSide);
            templateModelView.SetBinding(TemplateProperty, new Microsoft.UI.Xaml.Data.Binding { Path = new PropertyPath(nameof(AnchorSideTemplate)), Source = this });
            return templateModelView;
        }

        if (model is LayoutAnchorGroup typedLayoutAnchorGroup)
        {
            LayoutAnchorGroupControl templateModelView = new(typedLayoutAnchorGroup);
            templateModelView.SetBinding(TemplateProperty, new Microsoft.UI.Xaml.Data.Binding { Path = new PropertyPath(nameof(AnchorGroupTemplate)), Source = this });
            return templateModelView;
        }

        if (model is LayoutDocumentPane typedLayoutDocumentPane)
        {
            LayoutDocumentPaneControl templateModelView = new(typedLayoutDocumentPane, IsVirtualizingDocument, IgnoreTabControlKeyBindings);
            templateModelView.SetBinding(StyleProperty, new Microsoft.UI.Xaml.Data.Binding { Path = new PropertyPath(nameof(DocumentPaneControlStyle)), Source = this });
            templateModelView.InitializeView();
            return templateModelView;
        }

        if (model is LayoutAnchorablePane typedLayoutAnchorablePane)
        {
            LayoutAnchorablePaneControl templateModelView = new(typedLayoutAnchorablePane, IsVirtualizingAnchorable);
            templateModelView.SetBinding(StyleProperty, new Microsoft.UI.Xaml.Data.Binding { Path = new PropertyPath(nameof(AnchorablePaneControlStyle)), Source = this });
            templateModelView.InitializeView();
            return templateModelView;
        }

        // WinUI Window is not a UIElement. Floating models are created and registered
        // by the native window-host path, never returned here as replacement child views.
        if (model is LayoutDocument layoutDocument)
        {
            LayoutDocumentControl templateModelView = new()
            {
                Model = layoutDocument
            };
            return templateModelView;
        }

        return null;
    }

    internal void RequestViewRefresh()
    {
        if (isDisposed || refreshPending || !IsLoaded)
        {
            return;
        }
        refreshPending = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            refreshPending = false;
            if (!isDisposed && IsLoaded && ReferenceEquals(Layout?.Manager, this))
            {
                RefreshLayoutView();
            }
        }))
        {
            refreshPending = false;
        }
    }

    private static void LayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        DockingManager manager = (DockingManager)d;
        if (manager.isDisposed)
        {
            return;
        }

        if (e.NewValue is not LayoutRoot root)
        {
            manager.layoutBeforeNullReplacement = e.OldValue as LayoutRoot;
            manager.replacingNullLayout = true;
            try
            {
                manager.Layout = new LayoutRoot
                {
                    RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(new LayoutDocumentPane()))
                };
            }
            finally
            {
                manager.replacingNullLayout = false;
                manager.layoutBeforeNullReplacement = null;
            }
            return;
        }
        if (manager.replacingNullLayout)
        {
            LayoutRoot? oldLayout = manager.layoutBeforeNullReplacement;
            manager.replacingNullLayout = false;
            manager.layoutBeforeNullReplacement = null;
            manager.OnLayoutChanged(oldLayout, root);
            return;
        }
        manager.OnLayoutChanging(root);
        manager.OnLayoutChanged(e.OldValue as LayoutRoot, root);
    }

    private void OnLoaded(object sender, RoutedEventArgs? e)
    {
        if (isDisposed)
        {
            return;
        }

        if (ReferenceEquals(Layout?.Manager, this))
        {
            InitializeAutoHideViews();
        }
        else
        {
            ReleaseAutoHideViews();
        }

        autoHideWindowManager ??= new AutoHideWindowManager(this);
        FocusElementManager.SetupFocusManagement(this);
        SynchronizeWindowHosts();
        ShowWindowHosts();
        if (ReferenceEquals(Layout?.Manager, this))
        {
            RefreshLayoutView();
        }

        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;
        SchedulePendingDetachedRestore();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (isDisposed)
        {
            return;
        }

        SizeChanged -= OnSizeChanged;
        FocusElementManager.FinalizeFocusManagement(this);
        navigatorWindow?.Abort();
        ReleaseAutoHideViews();
        EndContentDrag();
        HideWindowHosts();
        ReleaseLayoutView();
        // A native reparent can report Unloaded after the manager has re-entered a live
        // tree. Restore the final attached state instead of leaving its side views null.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!isDisposed && IsLoaded && LeftSidePanel == null)
            {
                OnLoaded(this, null);
            }
        });
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (LayoutRootPanel == null || RightSidePanel == null || LeftSidePanel == null
            || TopSidePanel == null || BottomSidePanel == null)
        {
            return;
        }

        double width = Math.Max(ActualWidth - GridSplitterWidth - RightSidePanel.ActualWidth - LeftSidePanel.ActualWidth, 0);
        double height = Math.Max(ActualHeight - GridSplitterHeight - TopSidePanel.ActualHeight - BottomSidePanel.ActualHeight, 0);

        LayoutRootPanel.AdjustFixedChildrenPanelSizes(new global::Windows.Foundation.Size(width, height));
    }

    private void OnLayoutRootPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (isDisposed)
        {
            return;
        }

        if (e.PropertyName == nameof(LayoutRoot.ActiveContent))
        {
            if (Layout.ActiveContent != null)
            {
                FocusElementManager.SetFocusOnLastElement(Layout.ActiveContent);
            }

            if (!insideInternalSetActiveContent)
            {
                ActiveContent = Layout.ActiveContent?.Content;
            }
        }
        if (e.PropertyName == nameof(LayoutRoot.RootPanel))
        {
            // The original manager publishes the new LayoutRootPanel in this property-change
            // callback. WinUI also needs to release the old rooted visual before replacing it.
            if (layoutHost != null)
            {
                RefreshLayoutView();
            }
        }
    }

    private void OnLayoutRootUpdated(object? sender, EventArgs e)
    {
        if (isDisposed)
        {
            return;
        }

        if (IsLoaded)
        {
            InitializeAutoHideViews();
        }

        SynchronizeWindowHosts();
        rootCommandRequeryRoot = sender as LayoutRoot;
        if (rootCommandRequeryPending)
        {
            return;
        }

        rootCommandRequeryPending = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            rootCommandRequeryPending = false;
            LayoutRoot? root = rootCommandRequeryRoot;
            rootCommandRequeryRoot = null;
            if (!isDisposed && root != null && ReferenceEquals(root, Layout) && ReferenceEquals(root.Manager, this))
            {
                RequeryLayoutItemCommands();
            }
        });
    }

    private void OnFloatingWindowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeWindowHosts();
    }

    private void InternalSetActiveContent(object? contentObject)
    {
        // BugFix for first issue in #59
        List<LayoutContent> list = Layout.Descendents().OfType<LayoutContent>().ToList();
        LayoutContent? layoutContent = list.FirstOrDefault(lc => ReferenceEquals(lc, contentObject) || lc.Content == contentObject);
        insideInternalSetActiveContent = true;
        Layout.ActiveContent = layoutContent;
        insideInternalSetActiveContent = false;
    }

    private void ForgetLayoutItemOwner(LayoutContent? content)
    {
        if (content != null && LayoutItemOwners.TryGetValue(content, out WeakReference<DockingManager>? ownerReference)
            && ownerReference.TryGetTarget(out DockingManager? owner) && ReferenceEquals(owner, this))
        {
            LayoutItemOwners.Remove(content);
        }
    }

    private void RefreshLayoutView()
    {
        if (isDisposed || layoutHost == null)
        {
            return;
        }
        // The original RootPanel property notification assigns one replacement control.
        // The native DP callback releases the old XamlRoot child and presents the new one.
        LayoutRootPanel = Layout?.RootPanel is { } panel
            ? CreateUIElementForModel(panel) as LayoutPanelControl : null;
    }

    private void ReleaseLayoutView()
    {
        if (layoutHost != null)
        {
            layoutHost.Content = null;
        }
        LayoutViewBuilder.Release(layoutView);
        layoutView = null;
        LayoutRootPanel = null;
    }
}
