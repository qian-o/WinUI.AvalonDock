// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutAnchorableTabItem.cs
using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ReadOnlyPropertyGuard = AvalonDock.Compatibility.ReadOnlyPropertyGuard;

namespace AvalonDock.Controls;

/// <summary>Displays an anchorable tab header and forwards gestures to the shared drag service.</summary>
public class LayoutAnchorableTabItem : Control
{
    private static bool cancelMouseLeave;
    private bool observeModel = true;
    private ContentPresenter? headerHost;
    private DockingManager? manager;
    private LayoutRoot? observedRoot;
    private readonly List<(DependencyProperty Property, long Token)> managerTokens = new();

    /// <summary>Initializes an anchorable tab header.</summary>
    public LayoutAnchorableTabItem()
    {
        DefaultStyleKey = typeof(LayoutAnchorableTabItem);
        IsTabStop = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RegisterPropertyChangedCallback(TemplateProperty, (_, _) =>
        {
            ReleaseHeader();
            if (IsLoaded)
            {
                DispatcherQueue.TryEnqueue(RefreshHeaderPresentation);
            }
        });
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        AddHandler(PointerEnteredEvent, new PointerEventHandler((_, e) => OnMouseEnter(e)), true);
        AddHandler(PointerExitedEvent, new PointerEventHandler((_, e) => OnMouseLeave(e)), true);
    }

    /// <summary>Identifies the <see cref="Model"/> dependency property.</summary>
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(LayoutContent), typeof(LayoutAnchorableTabItem),
        new PropertyMetadata(null, (sender, args) => ((LayoutAnchorableTabItem)sender).OnNativeModelChanged(args)));

    /// <summary>Gets or sets the model represented by this header.</summary>
    [Bindable(true)]
    [Description("Gets/sets the model attached to the anchorable tab item.")]
    [Category("Other")]
    public LayoutContent? Model
    {
        get => (LayoutContent?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private void OnNativeModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LayoutContent previous)
        {
            previous.PropertyChanged -= OnModelPropertyChanged;
        }

        if (observeModel && Model != null)
        {
            Model.PropertyChanged += OnModelPropertyChanged;
        }

        OnModelChanged(e);
        ObserveManagerPresentation();
        if (!observeModel)
        {
            SetLayoutItem(null);
        }

        RefreshHeaderPresentation();
    }

    /// <summary>Updates the model and manager subscriptions.</summary>
    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        // A native container can receive a new model before its root is attached.
        SetLayoutItem(Model?.Root?.Manager?.GetLayoutItemFromModel(Model));
    }

    /// <summary>Identifies the read-only CLR <see cref="LayoutItem"/> property.</summary>
    public static readonly DependencyProperty LayoutItemProperty = ReadOnlyPropertyGuard.Register(
        nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutAnchorableTabItem), null);

    /// <summary>Gets the manager-owned layout item.</summary>
    [Bindable(true)]
    [Description("Gets the the LayoutItem attached to this tag item.")]
    [Category("Other")]
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);

    /// <summary>Sets the associated layout item.</summary>
    protected void SetLayoutItem(LayoutItem? value) => ReadOnlyPropertyGuard.Set(this, LayoutItemProperty, value);

    /// <summary>Starts native tracking when the tool permits moving.</summary>
    protected virtual void OnMouseLeftButtonDown(PointerRoutedEventArgs e)
    {
        if (!IsLoaded || Model is LayoutAnchorable { CanMove: false })
        {
            return;
        }
        // WinUI capture can move to the native host before this header receives a move event.
        // A previous selection's leave suppression must not cancel a new press.
        cancelMouseLeave = false;
        Model?.Root?.Manager?.BeginContentDrag(Model, this);
    }

    /// <summary>Receives movement while the native service owns the docking gesture.</summary>
    protected virtual void OnMouseMove(PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            cancelMouseLeave = false;
        }
    }

    /// <summary>Activates the tool on left-button release, including nonmovable tools.</summary>
    protected virtual void OnMouseLeftButtonUp(PointerRoutedEventArgs e)
    {
        if (IsLoaded && Model != null)
        {
            Model.IsActive = true;
        }
    }

    /// <summary>Receives pointer exit while native capture remains active.</summary>
    protected virtual void OnMouseLeave(PointerRoutedEventArgs e)
    {
        if (cancelMouseLeave && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Model?.Root?.Manager?.CancelPendingContentDrag(Model, this);
        }

        cancelMouseLeave = false;
    }

    internal static void CancelMouseLeave() => cancelMouseLeave = true;

    /// <summary>Receives pointer entry while shared rules decide whether reordering is allowed.</summary>
    protected virtual void OnMouseEnter(PointerRoutedEventArgs e)
    {
        // Pane and group CanRepositionItems policies are enforced by the shared coordinator.
    }

    internal void RefreshHeaderPresentation()
    {
        if (!IsLoaded || !observeModel)
        {
            return;
        }

        ApplyTemplate();
        ContentPresenter? nextHost = GetTemplateChild("PART_Header") as ContentPresenter;
        if (!ReferenceEquals(headerHost, nextHost))
        {
            ReleaseHeader();
            headerHost = nextHost;
        }
        if (headerHost == null)
        {
            return;
        }

        DataTemplate? template = manager?.AnchorableHeaderTemplate;
        DataTemplateSelector? selector = manager?.AnchorableHeaderTemplateSelector;
        headerHost.ContentTemplate = template;
        headerHost.ContentTemplateSelector = selector;
        headerHost.Content = template != null || selector != null ? Model : Model?.Title;
    }

    private void OnPointerPressed(object? sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed)
        {
            OnMouseLeftButtonDown(e);
        }
    }

    private void OnPointerMoved(object? sender, PointerRoutedEventArgs e)
    {
        // WinUI reports a second pressed button during a chord as PointerMoved.
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed)
        {
            OnMouseLeftButtonDown(e);
        }
        else
        {
            OnMouseMove(e);
        }
    }

    private void OnPointerReleased(object? sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased)
        {
            OnMouseLeftButtonUp(e);
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs? e)
    {
        observeModel = true;
        if (Model != null)
        {
            Model.PropertyChanged -= OnModelPropertyChanged;
            Model.PropertyChanged += OnModelPropertyChanged;
        }
        UpdateLayoutItem();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        observeModel = false;
        if (Model != null)
        {
            Model.PropertyChanged -= OnModelPropertyChanged;
        }

        DetachManager();
        ObserveRoot(null);
        SetLayoutItem(null);
        ReleaseHeader();
        DispatcherQueue.TryEnqueue(() => { if (IsLoaded && !observeModel) { OnLoaded(this, null); } });
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutContent.Root) or nameof(LayoutContent.Parent))
        {
            UpdateLayoutItem();
        }
        else if (e.PropertyName == nameof(LayoutContent.Title))
        {
            RefreshHeaderPresentation();
        }
    }

    private void UpdateLayoutItem()
    {
        ObserveManagerPresentation();
        SetLayoutItem(Model is { } model ? manager?.GetLayoutItemFromModel(model) : null);
        RefreshHeaderPresentation();
    }

    private void ObserveManagerPresentation()
    {
        ObserveRoot(observeModel ? Model?.Root as LayoutRoot : null);
        DockingManager? nextManager = observeModel ? Model?.Root?.Manager : null;
        if (!ReferenceEquals(manager, nextManager))
        {
            DetachManager();
            manager = nextManager;
            if (manager != null)
            {
                foreach (DependencyProperty? property in new[] { DockingManager.AnchorableHeaderTemplateProperty, DockingManager.AnchorableHeaderTemplateSelectorProperty })
                {
                    managerTokens.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) => RefreshHeaderPresentation())));
                }
            }
        }
    }

    private void ObserveRoot(LayoutRoot? root)
    {
        if (ReferenceEquals(root, observedRoot))
        {
            return;
        }

        if (observedRoot != null)
        {
            observedRoot.PropertyChanged -= OnRootPropertyChanged;
        }

        observedRoot = root;
        if (observedRoot != null)
        {
            observedRoot.PropertyChanged += OnRootPropertyChanged;
        }
    }

    private void OnRootPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutRoot.Manager))
        {
            UpdateLayoutItem();
        }
    }

    private void DetachManager()
    {
        if (manager != null)
        {
            foreach ((DependencyProperty? property, long token) in managerTokens)
            {
                manager.UnregisterPropertyChangedCallback(property, token);
            }
        }

        managerTokens.Clear();
        manager = null;
    }

    private void ReleaseHeader()
    {
        if (headerHost != null)
        {
            ClearHeader(headerHost);
        }

        if (GetTemplateChild("PART_Header") is ContentPresenter current && !ReferenceEquals(current, headerHost))
        {
            ClearHeader(current);
        }

        headerHost = null;
    }

    private static void ClearHeader(ContentPresenter host)
    {
        host.Content = null;
        host.ContentTemplate = null;
        host.ContentTemplateSelector = null;
    }
}
