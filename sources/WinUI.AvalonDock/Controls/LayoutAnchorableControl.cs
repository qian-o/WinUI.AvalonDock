// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutAnchorableControl.cs
using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ReadOnlyPropertyGuard = AvalonDock.Compatibility.ReadOnlyPropertyGuard;

namespace AvalonDock.Controls;

/// <summary>Displays an anchorable through its manager-owned layout item.</summary>
public class LayoutAnchorableControl : Control
{
    private WeakReference? activeContentOnNativeFocus;
    private readonly LayoutContentViewHost viewHost;
    private bool observeModel = true;
    private LayoutAnchorablePane? observedPane;

    /// <summary>Initializes an anchorable content control.</summary>
    public LayoutAnchorableControl()
    {
        DefaultStyleKey = typeof(LayoutAnchorableControl);
        viewHost = new LayoutContentViewHost(this, LayoutItemProperty);
        IsTabStop = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        GotFocus += OnGotFocus;
        GettingFocus += (_, _) => activeContentOnNativeFocus = new WeakReference(Model?.Root?.ActiveContent);
        PreviewKeyDown += (_, args) => Model?.Root?.Manager?.HandleNavigatorKey(args);
        RegisterPropertyChangedCallback(TemplateProperty, OnTemplateChanged);
        RegisterPropertyChangedCallback(LayoutItemProperty, (_, _) => AttachView());
    }

    /// <summary>Identifies the <see cref="Model"/> dependency property.</summary>
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(LayoutAnchorable), typeof(LayoutAnchorableControl),
        new PropertyMetadata(null, (sender, args) => ((LayoutAnchorableControl)sender).OnNativeModelChanged(args)));

    /// <summary>Gets or sets the anchorable model.</summary>
    [Bindable(true)]
    [Description("Gets/sets the model attached to this view.")]
    [Category("Other")]
    public LayoutAnchorable? Model
    {
        get => (LayoutAnchorable?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private void OnNativeModelChanged(DependencyPropertyChangedEventArgs e)
    {
        viewHost.Release(preserveBinding: true);
        if (e.OldValue is LayoutContent previous)
        {
            previous.PropertyChanged -= NativeModelParentChanged;
        }

        OnModelChanged(e);
        UpdateContentPresentation();
        if (observeModel && Model != null)
        {
            Model.PropertyChanged += NativeModelParentChanged;
        }
        else if (!observeModel && Model != null)
        {
            Model.PropertyChanged -= Model_PropertyChanged;
            SetLayoutItem(null);
        }
    }

    /// <summary>Updates the model subscription and presentation item.</summary>
    protected virtual void OnModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue != null)
        {
            ((LayoutContent)e.OldValue).PropertyChanged -= Model_PropertyChanged;
        }

        if (Model != null)
        {
            Model.PropertyChanged += Model_PropertyChanged;
            SetLayoutItem(Model?.Root?.Manager?.GetLayoutItemFromModel(Model));
        }
        else
        {
            SetLayoutItem(null);
        }
    }

    /// <summary>Identifies the read-only CLR <see cref="LayoutItem"/> property.</summary>
    public static readonly DependencyProperty LayoutItemProperty = ReadOnlyPropertyGuard.Register(
        nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutAnchorableControl), null);

    /// <summary>Gets the manager-owned presentation item.</summary>
    [Bindable(true)]
    [Description("Gets the the LayoutItem attached to this tag item.")]
    [Category("Other")]
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);

    /// <summary>Sets the associated presentation item.</summary>
    protected void SetLayoutItem(LayoutItem? value) => ReadOnlyPropertyGuard.Set(this, LayoutItemProperty, value);

    /// <summary>Activates the anchorable when keyboard focus enters its content.</summary>
    protected virtual void OnGotKeyboardFocus(RoutedEventArgs e)
    {
        if (Model != null)
        {
            Model.IsActive = true;
        }
    }

    private void OnGotFocus(object? sender, RoutedEventArgs e)
    {
        // WinUI queues GotFocus. An intervening activation supersedes this notification
        // even when its XamlRoot still remembers the old editor.
        if (IsLoaded && (ReferenceEquals(Model?.Root?.ActiveContent, activeContentOnNativeFocus.GetValueOrDefault<LayoutContent>()) || Model?.IsActive == true)
            && e.OriginalSource is UIElement element && Platforms.PlatformServices.Focus.HasKeyboardFocus(element))
        {
            OnGotKeyboardFocus(e);
        }
    }

    private void Model_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LayoutAnchorable.IsEnabled))
        {
            return;
        }

        if (Model == null)
        {
            return;
        }

        IsEnabled = Model.IsEnabled;
        if (IsEnabled || !Model.IsActive)
        {
            return;
        }

        if (Model.Parent != null && Model.Parent is LayoutAnchorablePane layoutAnchorablePane)
        {
            layoutAnchorablePane.SetNextSelectedIndex();
        }
    }

    private void UpdateContentPresentation()
    {
        // Native visual states map the reference template's original model triggers.
        LayoutAnchorablePane? pane = observeModel ? Model?.Parent as LayoutAnchorablePane : null;
        if (!ReferenceEquals(observedPane, pane))
        {
            if (observedPane != null)
            {
                observedPane.PropertyChanged -= OnPanePresentationChanged;
            }

            observedPane = pane;
            if (observedPane != null)
            {
                observedPane.PropertyChanged += OnPanePresentationChanged;
            }
        }
        VisualStateManager.GoToState(this, Model == null ? "ContentEmpty"
            : Model.IsFloating && pane?.IsDirectlyHostedInFloatingWindow == true ? "ContentFloating" : "ContentDocked", false);
    }
    private void OnPanePresentationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(LayoutAnchorablePane.IsDirectlyHostedInFloatingWindow))
        {
            UpdateContentPresentation();
        }
    }

    private void NativeModelParentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutContent.Root) or nameof(LayoutContent.Parent))
        {
            UpdateModel();
        }

        if (e.PropertyName is nameof(LayoutContent.Root) or nameof(LayoutContent.Parent) or nameof(LayoutContent.IsFloating))
        {
            UpdateContentPresentation();
        }
    }

    private void UpdateModel()
    {
        SetLayoutItem(observeModel ? Model?.Root?.Manager?.GetLayoutItemFromModel(Model) : null);
        UpdateContentPresentation();
        AttachView();
    }

    private void OnLoaded(object? sender, RoutedEventArgs? e)
    {
        bool resumingNativeView = !observeModel;
        observeModel = true;
        if (Model != null)
        {
            Model.PropertyChanged -= Model_PropertyChanged;
            Model.PropertyChanged += Model_PropertyChanged;
            Model.PropertyChanged -= NativeModelParentChanged;
            Model.PropertyChanged += NativeModelParentChanged;
            if (resumingNativeView)
            {
                Model_PropertyChanged(Model, new PropertyChangedEventArgs(nameof(LayoutContent.IsEnabled)));
            }
        }
        UpdateModel();
        LayoutUpdated -= RememberBoundTemplateHost;
        LayoutUpdated += RememberBoundTemplateHost;
        RememberBoundTemplateHost(this, EventArgs.Empty);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        observeModel = false;
        if (observedPane != null)
        {
            observedPane.PropertyChanged -= OnPanePresentationChanged;
        }

        observedPane = null;
        LayoutUpdated -= RememberBoundTemplateHost;
        if (Model != null)
        {
            Model.PropertyChanged -= Model_PropertyChanged;
            Model.PropertyChanged -= NativeModelParentChanged;
        }
        viewHost.Release(preserveBinding: true);
        SetLayoutItem(null);
        DispatcherQueue.TryEnqueue(() => { if (IsLoaded && !observeModel) { OnLoaded(this, null); } });
    }

    private void OnTemplateChanged(DependencyObject sender, DependencyProperty property)
    {
        viewHost.Release();
        if (IsLoaded)
        {
            DispatcherQueue.TryEnqueue(AttachView);
        }
    }

    private void AttachView()
    {
        if (!IsLoaded || !observeModel)
        {
            return;
        }

        ApplyTemplate();
        UpdateContentPresentation();
        viewHost.Attach(GetTemplateChild("PART_ContentHost") as ContentPresenter);
    }

    private void RememberBoundTemplateHost(object? sender, object args)
    {
        if (observeModel)
        {
            viewHost.RememberBoundTemplateHost();
        }
    }
}
