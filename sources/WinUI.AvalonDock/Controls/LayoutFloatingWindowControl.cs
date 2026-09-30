// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/LayoutFloatingWindowControl.cs
using System.ComponentModel;
using AvalonDock.Commands;
using AvalonDock.Compatibility;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using PropertyMetadata = Microsoft.UI.Xaml.PropertyMetadata;

namespace AvalonDock.Controls;

/// <summary>Hosts a floating layout in a native WinUI window.</summary>
[Microsoft.UI.Xaml.Data.Bindable]
public abstract partial class LayoutFloatingWindowControl : Window, ILayoutControl
{
    private readonly PropertyState state;
    private readonly ILayoutElement constructorModel;
    private IDockingWindowHost? host;
    private bool internalClose;
    private bool initialized;
    private bool closed;
    private bool bindingsEnabled = true;
    private LayoutRoot? observedRoot;
    private readonly HashSet<LayoutContent> observedContents = [];
    private bool applyingGeometry;
    private bool activationHookReady;
    private FloatingWindowState observedWindowState;
    private global::Windows.Foundation.Size? observedActualSize;
    private UIElement? keyInputRoot;
    private global::Windows.Foundation.Rect requestedBounds = new(80, 80, 600, 400);

    protected LayoutFloatingWindowControl(ILayoutElement model) : this(model, false) { }

    protected LayoutFloatingWindowControl(ILayoutElement model, bool isContentImmutable)
    {
        constructorModel = model ?? throw new ArgumentNullException(nameof(model));
        state = new PropertyState(this);
        InitializeKeyMetadata();
        SetValue(IsContentImmutableProperty, isContentImmutable);
        InitializeWindowTemplate();
        Activated += OnNativeActivated;
    }

    public abstract ILayoutElement Model
    {
        get;
    }
    public static readonly DependencyProperty IsContentImmutableProperty = Register(nameof(IsContentImmutable), typeof(bool), false);
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets wether the content can be modified.")]
    [Category("Other")]
    public bool IsContentImmutable => (bool?)GetValue(IsContentImmutableProperty) ?? false;
    private static readonly DependencyPropertyKey IsDraggingPropertyKey = new(Register(nameof(IsDragging), typeof(bool), false,
        (owner, args) => owner.OnIsDraggingChanged(args)), typeof(LayoutFloatingWindowControl), typeof(bool));
    public static readonly DependencyProperty IsDraggingProperty = IsDraggingPropertyKey.DependencyProperty;
    [System.ComponentModel.Bindable(true)]
    [Description("Gets wether this floating window is being dragged.")]
    [Category("FloatingWindow")]
    public bool IsDragging => (bool?)GetValue(IsDraggingProperty) ?? false;
    protected void SetIsDragging(bool value) => SetValue(IsDraggingPropertyKey, value);
    protected virtual void OnIsDraggingChanged(DependencyPropertyChangedEventArgs e)
    {
    }
    protected bool CloseInitiatedByUser => !internalClose;
    public static readonly DependencyProperty OwnedByDockingManagerWindowProperty = Register(nameof(OwnedByDockingManagerWindow), typeof(bool), true,
        (owner, _) => owner.ApplyWindowOptions());
    public bool OwnedByDockingManagerWindow
    {
        get => (bool?)GetValue(OwnedByDockingManagerWindowProperty) ?? false; set => SetValue(OwnedByDockingManagerWindowProperty, value);
    }
    public static readonly DependencyProperty AllowMinimizeProperty = Register(nameof(AllowMinimize), typeof(bool), false,
        (owner, _) => owner.ApplyWindowOptions());
    public bool AllowMinimize
    {
        get => (bool?)GetValue(AllowMinimizeProperty) ?? false; set => SetValue(AllowMinimizeProperty, value);
    }
    public static readonly DependencyProperty ResizeBorderThicknessProperty = Register(nameof(ResizeBorderThickness), typeof(Thickness), default(Thickness),
        (owner, _) => owner.ApplyWindowOptions());
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the resize border thickness for this floating window.")]
    [Category("FloatingWindow")]
    public Thickness ResizeBorderThickness
    {
        get => (Thickness?)GetValue(ResizeBorderThicknessProperty) ?? default; set => SetValue(ResizeBorderThicknessProperty, value);
    }
    public static readonly DependencyProperty IsMaximizedProperty = Register(nameof(IsMaximized), typeof(bool), false);
    public bool IsMaximized => (bool?)GetValue(IsMaximizedProperty) ?? false;
    private static readonly DependencyPropertyKey TotalMarginPropertyKey = new(Register(nameof(TotalMargin), typeof(Thickness), default(Thickness)), typeof(LayoutFloatingWindowControl), typeof(Thickness));
    public static readonly DependencyProperty TotalMarginProperty = TotalMarginPropertyKey.DependencyProperty;
    public Thickness TotalMargin
    {
        get => (Thickness?)GetValue(TotalMarginProperty) ?? default; protected set => SetValue(TotalMarginPropertyKey, value);
    }
    public static readonly DependencyPropertyKey ContentMinWidthPropertyKey = new(Register(nameof(ContentMinWidth), typeof(double), 0d,
        (owner, _) => owner.ApplyWindowOptions()), typeof(LayoutFloatingWindowControl), typeof(double));
    public static readonly DependencyPropertyKey ContentMinHeightPropertyKey = new(Register(nameof(ContentMinHeight), typeof(double), 0d,
        (owner, _) => owner.ApplyWindowOptions()), typeof(LayoutFloatingWindowControl), typeof(double));
    public static readonly DependencyProperty ContentMinWidthProperty = ContentMinWidthPropertyKey.DependencyProperty;
    public static readonly DependencyProperty ContentMinHeightProperty = ContentMinHeightPropertyKey.DependencyProperty;
    public double ContentMinWidth
    {
        get => (double?)GetValue(ContentMinWidthProperty) ?? 0; set => SetValue(ContentMinWidthPropertyKey, value);
    }
    public double ContentMinHeight
    {
        get => (double?)GetValue(ContentMinHeightProperty) ?? 0; set => SetValue(ContentMinHeightPropertyKey, value);
    }

    // WinUI Window is not a DependencyObject. These preserved inherited entry points forward to
    // private WinUI state; metadata callbacks forward to the public window's protected hooks.
    public object? GetValue(DependencyProperty dp) => IsTemplateProperty(dp) ? templateView.GetValue(dp) : state.GetValue(dp);
    public void SetValue(DependencyProperty dp, object? value)
    {
        VerifyWritable(dp);
        if (IsTemplateProperty(dp))
        {
            SetTemplateProperty(dp, value);
            return;
        }
        state.SetValue(dp, value);
        // A local null and an unset default have equal effective DP values but distinct
        // inheritance meaning, so this transition may not produce a metadata callback.
        if (dp == DataContextProperty && Content is FloatingWindowContentHost content)
        {
            content.UpdatePresentation();
        }
    }
    public void SetValue(DependencyPropertyKey key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        SetKeyValue(key, value, true);
    }
    public void ClearValue(DependencyProperty dp)
    {
        VerifyWritable(dp);
        if (IsTemplateProperty(dp))
        {
            ClearTemplateProperty(dp);
            return;
        }
        state.ClearValue(dp);
        if (dp == DataContextProperty && Content is FloatingWindowContentHost content)
        {
            content.UpdatePresentation();
        }
    }
    public void ClearValue(DependencyPropertyKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        SetKeyValue(key, key.DefaultValue, false);
    }
    public object? ReadLocalValue(DependencyProperty dp) => FindKey(dp) is { } key
        ? keyBaseValues.TryGetValue(key, out object? value) ? value : DependencyProperty.UnsetValue
        : IsTemplateProperty(dp) ? templateView.ReadLocalValue(dp) : state.ReadLocalValue(dp);
    public void SetBinding(DependencyProperty dp, BindingBase binding)
    {
        VerifyWritable(dp);
        if (IsTemplateProperty(dp))
        {
            if (dp == StyleProperty)
            {
                automaticWindowStyle = false;
            }

            BindingOperations.SetBinding(templateView, dp, binding);
            return;
        }
        BindingOperations.SetBinding(state, dp, binding);
        if (dp == DataContextProperty && Content is FloatingWindowContentHost content)
        {
            content.UpdatePresentation();
        }
    }
    private static void VerifyWritable(DependencyProperty dp)
    {
        ArgumentNullException.ThrowIfNull(dp);
        if (dp == IsDraggingProperty || dp == TotalMarginProperty || dp == ContentMinWidthProperty || dp == ContentMinHeightProperty)
        {
            throw new ArgumentException("A read-only dependency property can only be written with its DependencyPropertyKey.", nameof(dp));
        }
    }
    public bool IsLoaded => Content is FrameworkElement { IsLoaded: true };
    public double Left
    {
        get => host?.Geometry.Bounds.X ?? requestedBounds.X; set => SetBounds(value, Top, Width, Height);
    }
    public double Top
    {
        get => host?.Geometry.Bounds.Y ?? requestedBounds.Y; set => SetBounds(Left, value, Width, Height);
    }
    public double Width
    {
        get => host?.Geometry.Bounds.Width ?? requestedBounds.Width; set => SetBounds(Left, Top, value, Height);
    }
    public double Height
    {
        get => host?.Geometry.Bounds.Height ?? requestedBounds.Height; set => SetBounds(Left, Top, Width, value);
    }
    public new UIElement? Content
    {
        get => windowContent;
        set
        {
            if (IsLoaded && IsContentImmutable && !ReferenceEquals(windowContent, value))
            {
                return;
            }

            if (ReferenceEquals(windowContent, value))
            {
                return;
            }

            UIElement? previous = windowContent;
            SetKeyInputRoot(null);
            templateView.Content = null;
            LayoutViewBuilder.Release(previous);
            windowContent = value == null ? null : value is FloatingWindowContentHost ? value : new FloatingWindowContentHost(this) { Content = value };
            templateView.Content = windowContent;
            SetKeyInputRoot(windowContent);
        }
    }
    public void Show()
    {
        EnsureHost();
        host?.Show();
    }
    public void Hide() => host?.Hide();
    public new void Close()
    {
        if (host != null)
        {
            host.RequestClose();
        }
        else
        {
            base.Close();
        }
    }

    public virtual void EnableBindings()
    {
    }
    public virtual void DisableBindings()
    {
    }
    protected virtual void OnInitialized(EventArgs e)
    {
    }
    protected virtual void OnClosing(CancelEventArgs e)
    {
        // The pinned WPF base hook activates the owner before a floating window closes,
        // preventing the owner from being minimized by the native close transition. Map
        // Owner.Activate through the existing coordinate capability and keep it internal.
        try
        {
            if (OwnedByDockingManagerWindow && Manager?.IsLoaded == true)
            {
                PlatformServices.Coordinates.ActivateWindow(Manager);
            }
        }
        catch (Exception)
        {
            // Owner activation can fail during native destruction; allow the close to continue.
            return;
        }
    }
    protected virtual void OnClosed(EventArgs e)
    {
    }
    protected virtual void OnStateChanged(EventArgs e)
    {
        if (host?.Geometry is { IsMinimized: false } geometry)
        {
            UpdateMaximizedState(geometry.IsMaximized);
        }
    }
    protected virtual void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        Manager?.HandleNavigatorKey(e);
        if (e.Handled || Manager?.AllowMovingFloatingWindowWithKeyboard != true)
        {
            return;
        }

        const double step = 10;
        switch (e.Key)
        {
            case global::Windows.System.VirtualKey.Left:
                Left -= step;
                e.Handled = true;
                break;
            case global::Windows.System.VirtualKey.Right:
                Left += step;
                e.Handled = true;
                break;
            case global::Windows.System.VirtualKey.Up:
                Top -= step;
                e.Handled = true;
                break;
            case global::Windows.System.VirtualKey.Down:
                Top += step;
                e.Handled = true;
                break;
        }
    }

    internal static DependencyProperty RegisterWindowProperty(string name, Type propertyType, Type ownerType, object? defaultValue,
        Action<LayoutFloatingWindowControl, DependencyPropertyChangedEventArgs>? changed) =>
        DependencyProperty.Register(name, propertyType, ownerType, new PropertyMetadata(defaultValue,
            (sender, args) =>
            {
                LayoutFloatingWindowControl owner = ((PropertyState)sender).Owner;
                if (owner.initializingKeyMetadata)
                {
                    return;
                }

                changed?.Invoke(owner, args);
                owner.templateView?.RefreshBindings();
            }));
    private static DependencyProperty Register(string name, Type propertyType, object? defaultValue,
        Action<LayoutFloatingWindowControl, DependencyPropertyChangedEventArgs>? changed = null) =>
        RegisterWindowProperty(name, propertyType, typeof(LayoutFloatingWindowControl), defaultValue, changed);

    internal IDockingWindowHost? WindowHost => host;
    internal DockingManager? Manager => (Model ?? constructorModel)?.Root?.Manager;
    internal bool IsClosed => closed;
    internal bool AreBindingsEnabled => bindingsEnabled;
    internal void AttachHost(IDockingWindowHost windowHost)
    {
        host = windowHost;
        UpdateWindowPresentation();
        host.Closing += OnHostClosing;
        host.Closed += OnHostClosed;
        host.GeometryChanged += OnGeometryChanged;
        host.UserResizeStarted += OnUserResizeStarted;
        host.InteractionCompleted += OnHostInteractionCompleted;
        host.CaptionContextRequested += OnCaptionContextRequested;
        SetKeyInputRoot(Content);
        if (!initialized)
        {
            initialized = true;
            OnInitialized(EventArgs.Empty);
        }
        SetModelBindings(true);
        UpdateModelPresentation();
        ApplyWindowOptions();
        bool maximized = (Model ?? constructorModel).Descendents()
            .OfType<ILayoutElementForFloatingWindow>().Any(element => element.IsMaximized);
        observedWindowState = maximized ? FloatingWindowState.Maximized : FloatingWindowState.Restored;
        UpdateMaximizedState(maximized);
        host.SetMaximized(maximized);
        OnGeometryChanged(host, host.Geometry);
    }

    internal void InternalClose()
    {
        internalClose = true;
        if (host != null)
        {
            host.RequestClose();
        }
        else
        {
            base.Close();
        }
    }
    internal void MarkInternalClose() => internalClose = true;
    private void SetBounds(double left, double top, double width, double height)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Window bounds must be finite with positive dimensions.");
        }

        requestedBounds = new global::Windows.Foundation.Rect(left, top, width, height);
        host?.SetBounds(requestedBounds);
    }
    private void EnsureHost()
    {
        if (host != null || closed)
        {
            return;
        }

        ILayoutElement? model = Model ?? constructorModel;
        DockingManager? manager = model?.Root?.Manager;
        if (manager == null)
        {
            throw new InvalidOperationException("A floating window must belong to a docking manager before it is shown.");
        }

        LayoutContent? content = model.Descendents().OfType<LayoutContent>().FirstOrDefault();
        Rect bounds = new(content?.FloatingLeft ?? 80, content?.FloatingTop ?? 80,
            content?.FloatingWidth > 0 ? content.FloatingWidth : 600, content?.FloatingHeight > 0 ? content.FloatingHeight : 400);
        UIElement visual = Content ?? (model is ILayoutContainer container && container.Children.FirstOrDefault() is { } child ? manager.CreateUIElementForModel(child) : null)
            ?? throw new InvalidOperationException("A floating window must contain a supported layout pane before it is shown.");
        AttachHost(PlatformServices.CreateWindowHostService(manager).Attach(this, visual, Title ?? string.Empty, bounds, OwnedByDockingManagerWindow));
    }
    internal void SetDraggingState(bool value) => SetIsDragging(value);
    internal void SetModelBindings(bool enabled)
    {
        bindingsEnabled = enabled;
        if (observedRoot != null)
        {
            observedRoot.Updated -= OnRootUpdated;
        }

        foreach (LayoutContent content in observedContents)
        {
            content.PropertyChanged -= OnContentPropertyChanged;
        }

        observedContents.Clear();
        observedRoot = enabled ? (Model ?? constructorModel)?.Root as LayoutRoot : null;
        if (observedRoot != null)
        {
            observedRoot.Updated += OnRootUpdated;
        }
    }
    internal virtual void UpdateModelPresentation()
    {
        if (!bindingsEnabled || closed)
        {
            return;
        }

        ILayoutElement? model = Model ?? constructorModel;
        HashSet<LayoutContent> contents = model?.Descendents().OfType<LayoutContent>().ToHashSet() ?? [];
        foreach (LayoutContent? previous in observedContents.Where(content => !contents.Contains(content)).ToArray())
        {
            previous.PropertyChanged -= OnContentPropertyChanged;
            observedContents.Remove(previous);
        }
        foreach (LayoutContent? current in contents)
        {
            if (observedContents.Add(current))
            {
                current.PropertyChanged += OnContentPropertyChanged;
            }
        }

        LayoutContent? content = model?.Descendents().OfType<LayoutContent>().FirstOrDefault(item => item.IsActive)
            ?? model?.Descendents().OfType<LayoutContent>().FirstOrDefault();
        if (content != null)
        {
            Title = content.Title ?? string.Empty;
        }

        templateView.RefreshBindings();
        UpdateCaptionPresentation();
        (templateView.CloseWindowCommand as RelayCommand<object>)?.RaiseCanExecuteChanged();
        (templateView.HideWindowCommand as RelayCommand<object>)?.RaiseCanExecuteChanged();
        if (model is LayoutAnchorableFloatingWindow { RootPanel: { } tools })
        {
            ContentMinWidth = Math.Max(initialContentMinWidth, tools.CalculatedDockMinWidth());
            ContentMinHeight = Math.Max(initialContentMinHeight, tools.CalculatedDockMinHeight());
        }
    }
    internal virtual void ExecuteNativeClose(CancelEventArgs args)
    {
    }

    private void ApplyWindowOptions()
    {
        if (host == null || closed || applyingGeometry)
        {
            return;
        }

        if (windowContent is FrameworkElement content)
        {
            content.MinWidth = ContentMinWidth;
            content.MinHeight = ContentMinHeight;
        }
        Size decoration = TemplateDecoration;
        host.SetOptions(OwnedByDockingManagerWindow, AllowMinimize, new global::Windows.Foundation.Size(ContentMinWidth + decoration.Width, ContentMinHeight + decoration.Height), ResizeBorderThickness);
    }
    private void OnHostClosing(object? sender, CancelEventArgs e)
    {
        if (CloseInitiatedByUser)
        {
            ExecuteNativeClose(e);
        }

        if (!e.Cancel)
        {
            OnClosing(e);
        }
    }
    private void OnHostClosed(object? sender, EventArgs e)
    {
        if (closed)
        {
            return;
        }

        closed = true;
        SetKeyInputRoot(null);
        SetModelBindings(false);
        if (host is { } windowHost)
        {
            windowHost.Closing -= OnHostClosing;
            windowHost.Closed -= OnHostClosed;
            windowHost.GeometryChanged -= OnGeometryChanged;
            windowHost.UserResizeStarted -= OnUserResizeStarted;
            windowHost.InteractionCompleted -= OnHostInteractionCompleted;
            windowHost.CaptionContextRequested -= OnCaptionContextRequested;
        }
        ReleaseCaptionObservers();
        SetIsDragging(false);
        OnClosed(e);
    }
    private void OnNativeActivated(object? sender, WindowActivatedEventArgs e)
    {
        ILayoutElement model = Model ?? constructorModel;
        if (closed || model.Root?.Manager == null)
        {
            return;
        }

        if (!activationHookReady)
        {
            // WinUI 可能先激活原生窗口，再加载窗格。
            if (!initialized || !IsLoaded)
            {
                return;
            }

            activationHookReady = true;
        }

        bool isActive = e.WindowActivationState != WindowActivationState.Deactivated;
        bool isSinglePane = model switch
        {
            LayoutAnchorableFloatingWindow tools => tools.IsSinglePane,
            LayoutDocumentFloatingWindow documents => documents.IsSinglePane,
            _ => false,
        };
        if (isSinglePane)
        {
            LayoutFloatingWindowControlHelper.ActiveTheContentOfSinglePane(this, isActive);
        }
        else
        {
            LayoutFloatingWindowControlHelper.ActiveTheContentOfMultiPane(this, isActive);
        }
    }
    private void OnGeometryChanged(object? sender, WindowGeometry geometry)
    {
        applyingGeometry = true;
        try
        {
            FloatingWindowState windowState = geometry.IsMaximized ? FloatingWindowState.Maximized
                : geometry.IsMinimized ? FloatingWindowState.Minimized : FloatingWindowState.Restored;
            if (observedWindowState != windowState)
            {
                observedWindowState = windowState;
                OnStateChanged(EventArgs.Empty);
            }
            if (host?.Geometry != geometry)
            {
                return;
            }

            Size actualSize = geometry.ActualSize;
            if (observedActualSize is not { } previousSize || previousSize.Width != actualSize.Width || previousSize.Height != actualSize.Height)
            {
                observedActualSize = actualSize;
                OnSizeChanged(actualSize);
            }
        }
        finally { applyingGeometry = false; }
    }
    private void OnHostInteractionCompleted(object? sender, EventArgs e) => UpdatePositionAndSizeOfPanes();
    private void UpdateMaximizedState(bool isMaximized)
    {
        foreach (ILayoutElementForFloatingWindow element in (Model ?? constructorModel).Descendents().OfType<ILayoutElementForFloatingWindow>())
        {
            element.IsMaximized = isMaximized;
        }

        if (IsMaximized != isMaximized)
        {
            SetValue(IsMaximizedProperty, isMaximized);
            UpdateCaptionPresentation();
        }
        UpdatePositionAndSizeOfPanes();
    }
    internal void UpdatePositionAndSizeOfPanes()
    {
        if (closed || host?.IsClosed == true)
        {
            return;
        }

        foreach (ILayoutElementForFloatingWindow posElement in (Model ?? constructorModel).Descendents().OfType<ILayoutElementForFloatingWindow>())
        {
            posElement.FloatingLeft = Left;
            posElement.FloatingTop = Top;
            posElement.FloatingWidth = Width;
            posElement.FloatingHeight = Height;
            posElement.RaiseFloatingPropertiesUpdated();
        }
    }
    private void OnSizeChanged(global::Windows.Foundation.Size actualSize)
    {
        foreach (ILayoutElementForFloatingWindow posElement in (Model ?? constructorModel).Descendents().OfType<ILayoutElementForFloatingWindow>())
        {
            posElement.FloatingWidth = actualSize.Width;
            posElement.FloatingHeight = actualSize.Height;
            posElement.RaiseFloatingPropertiesUpdated();
        }
    }
    private enum FloatingWindowState
    {
        Restored, Minimized, Maximized
    }
    private void OnUserResizeStarted(object? sender, EventArgs args) => SizeToContent = Compatibility.SizeToContent.Manual;
    private void OnRootUpdated(object? sender, EventArgs e) => UpdateModelPresentation();
    private void OnContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutContent.Title) or nameof(LayoutContent.IsSelected) or nameof(LayoutContent.IsActive)
            or nameof(LayoutContent.CanClose) or nameof(LayoutAnchorable.CanHide))
        {
            UpdateModelPresentation();
        }
    }
    private void OnNativePreviewKeyDown(object? sender, KeyRoutedEventArgs e) => OnPreviewKeyDown(e);
    private void SetKeyInputRoot(UIElement? element)
    {
        if (keyInputRoot != null)
        {
            keyInputRoot.PreviewKeyDown -= OnNativePreviewKeyDown;
        }

        keyInputRoot = element is FloatingWindowContentHost ? null : element;
        if (keyInputRoot != null)
        {
            keyInputRoot.PreviewKeyDown += OnNativePreviewKeyDown;
        }
    }
    private sealed class PropertyState(LayoutFloatingWindowControl owner) : DependencyObject
    {
        internal LayoutFloatingWindowControl Owner { get; } = owner;
    }
}
