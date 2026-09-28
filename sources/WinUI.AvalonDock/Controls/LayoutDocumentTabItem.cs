// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutDocumentTabItem.cs
using System.ComponentModel;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using ReadOnlyPropertyGuard = AvalonDock.Compatibility.ReadOnlyPropertyGuard;

namespace AvalonDock.Controls;

/// <summary>Displays the header and model association of a document tab.</summary>
public class LayoutDocumentTabItem : ContentControl
{
    private bool observeModel = true;
    private LayoutRoot? observedRoot;
    /// <summary>Identifies the <see cref="Model"/> dependency property.</summary>
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(LayoutContent), typeof(LayoutDocumentTabItem),
        new PropertyMetadata(null, (sender, args) => ((LayoutDocumentTabItem)sender).OnNativeModelChanged(args)));

    /// <summary>Identifies the <see cref="LayoutItem"/> dependency property.</summary>
    public static readonly DependencyProperty LayoutItemProperty = ReadOnlyPropertyGuard.Register(
        nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutDocumentTabItem), null);

    /// <summary>Initializes a document tab header.</summary>
    public LayoutDocumentTabItem()
    {
        DefaultStyleKey = typeof(LayoutDocumentTabItem);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        AddHandler(PointerEnteredEvent, new PointerEventHandler((_, e) => OnMouseEnter(e)), true);
        AddHandler(PointerExitedEvent, new PointerEventHandler((_, e) => OnMouseLeave(e)), true);
    }

    /// <summary>Gets or sets the layout content represented by this tab.</summary>
    [System.ComponentModel.Bindable(true)]
    [Description("Gets wether this floating window is being dragged.")]
    [Category("Other")]
    public LayoutContent? Model
    {
        get => (LayoutContent?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Gets the manager-owned presentation item for this tab.</summary>
    [System.ComponentModel.Bindable(true)]
    [Description("Gets the LayoutItem attached to this tag item.")]
    [Category("Other")]
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);

    private void OnNativeModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LayoutContent previous)
        {
            previous.PropertyChanged -= OnModelPropertyChanged;
            if (ReferenceEquals(previous.TabItem, this))
            {
                previous.TabItem = null;
            }
        }

        if (observeModel && Model is { } model)
        {
            model.PropertyChanged += OnModelPropertyChanged;
        }

        OnModelChanged(e);
        ObserveRoot(observeModel ? Model?.Root as LayoutRoot : null);
        if (!observeModel && Model is { } disconnected)
        {
            SetLayoutItem(null);
            if (ReferenceEquals(disconnected.TabItem, this))
            {
                disconnected.TabItem = null;
            }
        }
    }

    /// <summary>Updates the association when the tab model changes.</summary>
    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        LayoutItem? layoutItem = (Model?.Root?.Manager)?.GetLayoutItemFromModel(Model);
        SetLayoutItem(layoutItem);
        if (layoutItem != null && Model is { } model)
        {
            model.TabItem = this;
        }
    }

    /// <summary>Sets the associated presentation item.</summary>
    protected void SetLayoutItem(LayoutItem? value) => ReadOnlyPropertyGuard.Set(this, LayoutItemProperty, value);

    /// <summary>Activates the document and starts tracking through the shared native drag service.</summary>
    protected virtual void OnMouseLeftButtonDown(PointerRoutedEventArgs e)
        => BeginDrag(this, e.GetCurrentPoint(this).Position);

    internal void BeginDragFromTab(TabViewItem tab, PointerRoutedEventArgs e)
        => BeginDrag(tab, e.GetCurrentPoint(tab).Position);

    private void BeginDrag(FrameworkElement origin, Point pressPosition)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (Model is { } model)
        {
            model.IsActive = true;
            int clickCount = PlatformServices.PointerGestures.RegisterPrimaryPress(this);
            if (model is LayoutDocument { CanMove: false })
            {
                return;
            }

            if (clickCount != 1)
            {
                return;
            }

            model.Root?.Manager?.BeginContentDrag(model, origin, pressPosition);
        }
    }

    /// <summary>Receives pointer movement; the active native service owns drag thresholds and docking.</summary>
    protected virtual void OnMouseMove(PointerRoutedEventArgs e)
    {
        // Input tracking continues through the platform service even after capture leaves XAML.
    }

    /// <summary>Receives pointer release; the active native service completes its capture session.</summary>
    protected virtual void OnMouseLeftButtonUp(PointerRoutedEventArgs e)
    {
        // Shared drag completion must not be duplicated by the XAML event adapter.
    }

    /// <summary>Receives native pointer exit while capture owns the pending press.</summary>
    protected virtual void OnMouseLeave(PointerRoutedEventArgs e)
    {
        // HWND capture does not reliably deliver XAML exit for this gesture.
        // The native input session observes the original header boundary.
    }

    /// <summary>Receives native pointer entry while the captured service owns gesture state.</summary>
    protected virtual void OnMouseEnter(PointerRoutedEventArgs e)
    {
        // WinUI can synthesize entry when the HWND takes capture for this press.
        // The native session observes real pre-threshold exits through screen bounds.
    }

    /// <summary>Runs the document close command for a middle-button press.</summary>
    protected virtual void OnMouseDown(PointerRoutedEventArgs e)
    {
        if (IsLoaded && e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.MiddleButtonPressed
            && LayoutItem?.CloseCommand is { } close && close.CanExecute(null))
        {
            close.Execute(null);
        }
    }

    private void OnPointerPressed(object? sender, PointerRoutedEventArgs e)
    {
        OnMouseDown(e);
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed)
        {
            OnMouseLeftButtonDown(e);
        }
    }

    private void OnPointerMoved(object? sender, PointerRoutedEventArgs e)
    {
        // WinUI reports a second pressed mouse button during a chord as PointerMoved.
        PointerUpdateKind changed = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (changed is Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed
            or Microsoft.UI.Input.PointerUpdateKind.RightButtonPressed
            or Microsoft.UI.Input.PointerUpdateKind.MiddleButtonPressed)
        {
            OnMouseDown(e);
            if (changed == Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed)
            {
                OnMouseLeftButtonDown(e);
            }
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
        if (Model is { } model)
        {
            model.PropertyChanged -= OnModelPropertyChanged;
            model.PropertyChanged += OnModelPropertyChanged;
        }

        UpdateLayoutItem();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        observeModel = false;
        if (Model is { } model)
        {
            model.PropertyChanged -= OnModelPropertyChanged;
            if (ReferenceEquals(model.TabItem, this))
            {
                model.TabItem = null;
            }
        }

        SetLayoutItem(null);
        ObserveRoot(null);
        // WinUI can deliver Unloaded after a container was already reparented by the
        // native list. Reconcile the final live state without retaining a detached header.
        DispatcherQueue.TryEnqueue(() => { if (IsLoaded && !observeModel) { OnLoaded(this, null); } });
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutContent.Root) or nameof(LayoutContent.Parent))
        {
            UpdateLayoutItem();
        }
    }

    private void UpdateLayoutItem()
    {
        ObserveRoot(observeModel ? Model?.Root as LayoutRoot : null);
        SetLayoutItem(observeModel ? Model?.Root?.Manager?.GetLayoutItemFromModel(Model) : null);
        if (Model is { } model && LayoutItem is not null)
        {
            model.TabItem = this;
        }
        else if (Model != null && ReferenceEquals(Model.TabItem, this))
        {
            Model.TabItem = null;
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
}
