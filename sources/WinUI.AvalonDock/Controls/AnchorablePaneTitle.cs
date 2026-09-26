// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/AnchorablePaneTitle.cs
using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ReadOnlyPropertyGuard = AvalonDock.Compatibility.ReadOnlyPropertyGuard;

namespace AvalonDock.Controls;

/// <summary>Displays the active tool title and starts pane docking through the shared coordinator.</summary>
public class AnchorablePaneTitle : Control
{
    private bool observeModel = true;
    private ContentPresenter? titleHost;
    private DockingManager? manager;
    private LayoutRoot? observedRoot;
    private readonly List<(DependencyProperty Property, long Token)> managerTokens = new();

    /// <summary>Initializes an anchorable pane title.</summary>
    public AnchorablePaneTitle()
    {
        DefaultStyleKey = typeof(AnchorablePaneTitle);
        IsTabStop = false;
        IsHitTestVisible = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RegisterPropertyChangedCallback(TemplateProperty, (_, _) =>
        {
            ReleaseTitle();
            if (IsLoaded)
            {
                DispatcherQueue.TryEnqueue(RefreshTitlePresentation);
            }
        });
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        AddHandler(PointerExitedEvent, new PointerEventHandler((_, args) => OnMouseLeave(args)), true);
    }

    /// <summary>Identifies the model dependency property.</summary>
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(LayoutAnchorable), typeof(AnchorablePaneTitle),
        new PropertyMetadata(null, (sender, args) => ((AnchorablePaneTitle)sender).OnNativeModelChanged(args)));

    /// <summary>Gets or sets the tool represented by this title.</summary>
    [Bindable(true)]
    [Description("Gets/sets the LayoutAnchorable model attached of this view.")]
    [Category("Anchorable")]
    public LayoutAnchorable? Model
    {
        get => (LayoutAnchorable?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private void OnNativeModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LayoutAnchorable previous)
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

        RefreshTitlePresentation();
    }

    /// <summary>Updates model subscriptions and the associated layout item.</summary>
    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (Model != null)
        {
            SetLayoutItem(Model?.Root?.Manager?.GetLayoutItemFromModel(Model));
        }
        else
        {
            SetLayoutItem(null);
        }
    }

    /// <summary>Identifies the read-only CLR layout-item dependency property.</summary>
    public static readonly DependencyProperty LayoutItemProperty = ReadOnlyPropertyGuard.Register(
        nameof(LayoutItem), typeof(LayoutItem), typeof(AnchorablePaneTitle), null);

    /// <summary>Gets the layout item associated with the tool.</summary>
    [Bindable(true)]
    [Description("Gets the LayoutItem (LayoutAnchorableItem or LayoutDocumentItem) attached to this object.")]
    [Category("Layout")]
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);

    /// <summary>Sets the associated layout item.</summary>
    protected void SetLayoutItem(LayoutItem? value) => ReadOnlyPropertyGuard.Set(this, LayoutItemProperty, value);

    /// <summary>Receives pointer movement while native capture owns the drag session.</summary>
    protected virtual void OnMouseMove(PointerRoutedEventArgs e)
    {
        // The shared platform session continues tracking after the pointer leaves the title.
    }

    /// <summary>Receives pointer exit without tearing content from a detached window.</summary>
    protected virtual void OnMouseLeave(PointerRoutedEventArgs e)
    {
        // Drag targets and threshold transitions are owned by the shared pane/content session.
    }

    /// <summary>Starts a pane gesture unless movement or detached-window policy forbids it.</summary>
    protected virtual void OnMouseLeftButtonDown(PointerRoutedEventArgs e)
    {
        if (!IsLoaded || e.Handled || Model == null || !Model.CanMove || Model.Root?.Manager?.IsDetached(Model) == true)
        {
            return;
        }

        for (DependencyObject? current = this; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is LayoutAnchorablePaneControl { Model: LayoutAnchorablePane pane })
            {
                pane.Root?.Manager?.BeginPaneDrag(pane, this);
                return;
            }
        }
        Model.Root?.Manager?.BeginContentDrag(Model, this);
    }

    /// <summary>Activates the tool when the title receives a left-button release.</summary>
    protected virtual void OnMouseLeftButtonUp(PointerRoutedEventArgs e)
    {
        if (IsLoaded && Model != null)
        {
            Model.IsActive = true;
        }
    }

    internal void RefreshTitlePresentation()
    {
        if (!IsLoaded || !observeModel)
        {
            return;
        }

        ApplyTemplate();
        VisualStateManager.GoToState(this, Model?.CanClose == true ? "CloseTool" : "HideTool", false);
        VisualStateManager.GoToState(this, Model?.IsAutoHidden == true ? "AutoHidden" : "Pinned", false);
        ContentPresenter? next = GetTemplateChild("PART_Title") as ContentPresenter;
        if (!ReferenceEquals(titleHost, next))
        {
            ReleaseTitle();
            titleHost = next;
        }
        if (titleHost != null)
        {
            DataTemplate? template = manager?.AnchorableTitleTemplate;
            DataTemplateSelector? selector = manager?.AnchorableTitleTemplateSelector;
            if (!ReferenceEquals(titleHost.ContentTemplate, template))
            {
                titleHost.ContentTemplate = template;
            }

            if (!ReferenceEquals(titleHost.ContentTemplateSelector, selector))
            {
                titleHost.ContentTemplateSelector = selector;
            }

            object? content = template != null || selector != null ? (object?)Model : Model?.Title;
            if (!Equals(titleHost.Content, content))
            {
                titleHost.Content = content;
            }
        }
    }

    private bool IsCommandSource(object source)
    {
        for (DependencyObject? current = source as DependencyObject; current != null && !ReferenceEquals(current, this); current = VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase)
            {
                return true;
            }
        }

        return false;
    }

    private void OnPointerPressed(object? sender, PointerRoutedEventArgs e)
    {
        if (!IsCommandSource(e.OriginalSource)
            && e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed)
        {
            OnMouseLeftButtonDown(e);
        }
    }

    private void OnPointerMoved(object? sender, PointerRoutedEventArgs e)
    {
        // WinUI reports a second pressed mouse button during a chord as PointerMoved.
        if (!IsCommandSource(e.OriginalSource)
            && e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed)
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
        if (!IsCommandSource(e.OriginalSource) && e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased)
        {
            OnMouseLeftButtonUp(e);
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
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
        ReleaseTitle();
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutContent.Root) or nameof(LayoutContent.Parent))
        {
            UpdateLayoutItem();
        }
        else if (e.PropertyName is nameof(LayoutContent.Title) or nameof(LayoutContent.CanClose) or nameof(LayoutAnchorable.IsAutoHidden))
        {
            RefreshTitlePresentation();
        }
    }

    private void UpdateLayoutItem()
    {
        ObserveManagerPresentation();
        SetLayoutItem(Model is { } model ? manager?.GetLayoutItemFromModel(model) : null);
        RefreshTitlePresentation();
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
                foreach (DependencyProperty? property in new[] { DockingManager.AnchorableTitleTemplateProperty, DockingManager.AnchorableTitleTemplateSelectorProperty })
                {
                    managerTokens.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) => RefreshTitlePresentation())));
                }
            }
        }
    }

    private void ObserveRoot(LayoutRoot? root)
    {
        if (ReferenceEquals(observedRoot, root))
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

    private void ReleaseTitle()
    {
        if (titleHost != null)
        {
            ClearTitle(titleHost);
        }

        if (GetTemplateChild("PART_Title") is ContentPresenter current && !ReferenceEquals(current, titleHost))
        {
            ClearTitle(current);
        }

        titleHost = null;
    }

    private static void ClearTitle(ContentPresenter host)
    {
        host.Content = null;
        host.ContentTemplate = null;
        host.ContentTemplateSelector = null;
    }
}
