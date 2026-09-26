// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutDocumentControl.cs
using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ReadOnlyPropertyGuard = AvalonDock.Compatibility.ReadOnlyPropertyGuard;

namespace AvalonDock.Controls;

/// <summary>Displays a document through its manager-owned layout item.</summary>
public class LayoutDocumentControl : Control
{
    private ContentPresenter? contentHost;
    private bool observeModel = true;

    /// <summary>Initializes a document content control.</summary>
    public LayoutDocumentControl()
    {
        DefaultStyleKey = typeof(LayoutDocumentControl);
        IsTabStop = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RegisterPropertyChangedCallback(TemplateProperty, OnTemplateChanged);
        RegisterPropertyChangedCallback(LayoutItemProperty, (_, _) => AttachView());
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerButtonChanged), true);
        // WinUI reports another mouse button pressed during a chord as PointerMoved.
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerButtonChanged), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
    }

    /// <summary>Identifies the <see cref="Model"/> dependency property.</summary>
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(LayoutContent), typeof(LayoutDocumentControl),
        new PropertyMetadata(null, (sender, args) => ((LayoutDocumentControl)sender).OnNativeModelChanged(args)));

    /// <summary>Gets or sets the document model.</summary>
    public LayoutContent? Model
    {
        get => (LayoutContent?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private void OnNativeModelChanged(DependencyPropertyChangedEventArgs e)
    {
        ReleaseView(preserveBinding: true);
        if (e.OldValue is LayoutContent previous)
        {
            previous.PropertyChanged -= NativeModelParentChanged;
        }

        OnModelChanged(e);
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
        nameof(LayoutItem), typeof(LayoutItem), typeof(LayoutDocumentControl), null);

    /// <summary>Gets the manager-owned presentation item.</summary>
    public LayoutItem? LayoutItem => (LayoutItem?)GetValue(LayoutItemProperty);

    /// <summary>Sets the associated presentation item.</summary>
    protected void SetLayoutItem(LayoutItem? value) => ReadOnlyPropertyGuard.Set(this, LayoutItemProperty, value);

    /// <summary>Activates the model when the left pointer is released over its content.</summary>
    protected virtual void OnPreviewMouseLeftButtonUp(PointerRoutedEventArgs e) => SetIsActive();

    /// <summary>Activates the model when the left pointer is pressed over its content.</summary>
    protected virtual void OnMouseLeftButtonDown(PointerRoutedEventArgs e) => SetIsActive();

    /// <summary>Activates the model when the right pointer is pressed over its content.</summary>
    protected virtual void OnMouseRightButtonDown(PointerRoutedEventArgs e) => SetIsActive();

    private void SetIsActive()
    {
        if (Model != null && !Model.IsActive)
        {
            Model.IsActive = true;
        }
    }

    private void OnPointerButtonChanged(object? sender, PointerRoutedEventArgs e)
    {
        switch (e.GetCurrentPoint(this).Properties.PointerUpdateKind)
        {
            case Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed:
                OnMouseLeftButtonDown(e);
                break;
            case Microsoft.UI.Input.PointerUpdateKind.RightButtonPressed:
                OnMouseRightButtonDown(e);
                break;
        }
    }

    private void OnPointerReleased(object? sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased)
        {
            OnPreviewMouseLeftButtonUp(e);
        }
    }

    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LayoutContent.IsEnabled))
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

        if (Model.Parent is LayoutDocumentPane layoutDocumentPane)
        {
            layoutDocumentPane.SetNextSelectedIndex();
        }
    }

    private void NativeModelParentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutContent.Root) or nameof(LayoutContent.Parent))
        {
            UpdateModel();
        }
    }

    private void UpdateModel()
    {
        SetLayoutItem(observeModel ? Model?.Root?.Manager?.GetLayoutItemFromModel(Model) : null);
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
        LayoutUpdated -= RememberBoundTemplateHost;
        if (Model != null)
        {
            Model.PropertyChanged -= Model_PropertyChanged;
            Model.PropertyChanged -= NativeModelParentChanged;
        }
        ReleaseView(preserveBinding: true);
        SetLayoutItem(null);
        DispatcherQueue.TryEnqueue(() => { if (IsLoaded && !observeModel) { OnLoaded(this, null); } });
    }

    private void OnTemplateChanged(DependencyObject sender, DependencyProperty property)
    {
        ReleaseView();
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
        ContentPresenter? nextHost = GetTemplateChild("PART_ContentHost") as ContentPresenter;
        if (nextHost == null)
        {
            return;
        }

        if (!ReferenceEquals(contentHost, nextHost))
        {
            ReleaseView();
            contentHost = nextHost;
        }
        ContentPresenter? view = LayoutItem?.View;
        if (ReferenceEquals(contentHost.Content, view))
        {
            return;
        }

        if (view != null)
        {
            DetachView(view);
        }

        contentHost.Content = view;
    }

    private void RememberBoundTemplateHost(object? sender, object args)
    {
        if (contentHost != null || !observeModel || LayoutItem?.IsViewExists() != true)
        {
            return;
        }
        // Original consumer templates bind LayoutItem.View without a named part. WinUI
        // disconnects their tree before Template changes; retain only the actual owner
        // so the existing release path can clear its old native binding and parent.
        ContentPresenter view = LayoutItem.View;
        contentHost = this.FindVisualChildren<ContentPresenter>().FirstOrDefault(host => ReferenceEquals(host.Content, view));
    }

    private void ReleaseView(bool preserveBinding = false)
    {
        // A still-current consumer template follows the LayoutItem DP change itself;
        // only a retired template must have that binding explicitly disconnected.
        if (contentHost != null && (!preserveBinding || contentHost.GetBindingExpression(ContentPresenter.ContentProperty) == null))
        {
            contentHost.Content = null;
        }

        contentHost = null;
    }

    private static void DetachView(ContentPresenter view)
    {
        DependencyObject parent = view.Parent ?? VisualTreeHelper.GetParent(view);
        if (parent is ContentPresenter presenter && ReferenceEquals(presenter.Content, view))
        {
            presenter.Content = null;
        }
        else if (parent is ContentControl control && ReferenceEquals(control.Content, view))
        {
            control.Content = null;
        }
        else if (parent is Border border && ReferenceEquals(border.Child, view))
        {
            border.Child = null;
        }
        else if (parent is Panel panel)
        {
            panel.Children.Remove(view);
        }
    }
}
