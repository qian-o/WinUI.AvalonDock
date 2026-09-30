// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/LayoutAnchorableFloatingWindowControl.cs
using System.ComponentModel;
using System.Windows.Input;
using AvalonDock.Commands;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

[Microsoft.UI.Xaml.Data.Bindable]
public partial class LayoutAnchorableFloatingWindowControl : LayoutFloatingWindowControl
{
    private readonly LayoutAnchorableFloatingWindow model;
    private DockingManager? ownerAtInitialization;
    private LayoutAnchorablePane? observedPane;
    private bool observingModel;
    internal LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model) : this(model, false) { }
    internal LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model, bool isContentImmutable) : base(model, isContentImmutable)
    {
        this.model = model;
        HideWindowCommand = new RelayCommand<object>(OnExecuteHideWindowCommand, CanExecuteHideWindowCommand);
        CloseWindowCommand = new RelayCommand<object>(OnExecuteCloseWindowCommand, CanExecuteCloseWindowCommand);
        model.IsVisibleChanged += OnModelIsVisibleChanged;
    }
    public override ILayoutElement Model => model;
    public static readonly DependencyProperty SingleContentLayoutItemProperty = RegisterWindowProperty(nameof(SingleContentLayoutItem), typeof(LayoutItem),
        typeof(LayoutAnchorableFloatingWindowControl), null, (owner, args) => ((LayoutAnchorableFloatingWindowControl)owner).OnSingleContentLayoutItemChanged(args));
    [Bindable(true)]
    [Description("Gets/sets the layout item of the selected content when shown in a single anchorable pane.")]
    [Category("Anchorable")]
    public LayoutItem? SingleContentLayoutItem
    {
        get => (LayoutItem?)GetValue(SingleContentLayoutItemProperty); set => SetValue(SingleContentLayoutItemProperty, value);
    }
    protected virtual void OnSingleContentLayoutItemChanged(DependencyPropertyChangedEventArgs e)
    {
    }
    public ICommand HideWindowCommand
    {
        get;
    }
    public ICommand CloseWindowCommand
    {
        get;
    }
    public override void EnableBindings()
    {
        ObserveModel(true);
        SetModelBindings(true);
        UpdateModelPresentation();
        base.EnableBindings();
    }
    public override void DisableBindings()
    {
        ObserveModel(false);
        SetModelBindings(false);
        ObservePane(null);
        base.DisableBindings();
    }
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        ownerAtInitialization = Manager;
        if (Content == null && Manager is { } manager && model.RootPanel is { } rootPanel)
        {
            Content = manager.CreateUIElementForModel(rootPanel);
        }

        ObserveModel(true);
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (CloseInitiatedByUser && !HideWindowCommand.CanExecute(null))
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        ILayoutRoot? root = Model?.Root;
        if (root != null)
        {
            root.Manager?.RemoveFloatingWindow(this);
            root.CollectGarbage();
        }

        ObserveModel(false);
        model.IsVisibleChanged -= OnModelIsVisibleChanged;
        SingleContentLayoutItem = null;
        ObservePane(null);
        base.OnClosed(e);
        if (!CloseInitiatedByUser && root?.Manager is { } manager && ReferenceEquals(manager, ownerAtInitialization))
        {
            root.FloatingWindows.Remove(model);
        }
    }
    private void ObserveModel(bool enabled)
    {
        if (observingModel == enabled)
        {
            return;
        }

        observingModel = enabled;
        if (enabled)
        {
            model.PropertyChanged += OnModelPropertyChanged;
        }
        else
        {
            model.PropertyChanged -= OnModelPropertyChanged;
        }
    }
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LayoutAnchorableFloatingWindow.RootPanel):
                if (model.RootPanel == null)
                {
                    InternalClose();
                }

                break;
            case nameof(LayoutAnchorableFloatingWindow.IsVisible):
                if (!model.IsVisible && WindowHost?.IsVisible == true)
                {
                    Hide();
                }

                break;
        }
    }
    private void OnModelIsVisibleChanged(object? sender, EventArgs e)
    {
        if (model.IsVisible && WindowHost?.IsVisible == false && Manager?.IsLoaded == true)
        {
            Show();
        }
    }
    internal override void UpdateModelPresentation()
    {
        base.UpdateModelPresentation();
        if (!AreBindingsEnabled)
        {
            return;
        }

        ObservePane(model?.IsSinglePane == true ? model.SinglePane as LayoutAnchorablePane : null);
        if (model?.Root != null)
        {
            SingleContentLayoutItem = model.IsSinglePane && observedPane?.SelectedContent is { } content ? Manager?.GetLayoutItemFromModel(content) : null;
        }
    }
    private void ObservePane(LayoutAnchorablePane? pane)
    {
        if (ReferenceEquals(observedPane, pane))
        {
            return;
        }

        if (observedPane != null)
        {
            observedPane.PropertyChanged -= OnPaneChanged;
        }

        observedPane = pane;
        if (observedPane != null)
        {
            observedPane.PropertyChanged += OnPaneChanged;
        }
    }
    private void OnPaneChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutAnchorablePane.SelectedContent) or nameof(LayoutAnchorablePane.SelectedContentIndex))
        {
            UpdateModelPresentation();
        }
    }
    internal override void ExecuteNativeClose(CancelEventArgs args)
    {
        if (HideWindowCommand.CanExecute(null))
        {
            HideWindowCommand.Execute(null);
        }
        else if (CloseWindowCommand.CanExecute(null))
        {
            CloseWindowCommand.Execute(null);
        }

        args.Cancel = Model.Descendents().OfType<LayoutContent>().Any();
    }
    private bool CanExecuteHideWindowCommand(object parameter) =>
        CanExecuteContentCommand<LayoutAnchorable, LayoutAnchorableItem>(parameter,
            static content => content.CanHide, static item => item.HideCommand);

    private void OnExecuteHideWindowCommand(object parameter)
    {
        if (!ExecuteContentCommand<LayoutAnchorable, LayoutAnchorableItem>(parameter, static item => item.HideCommand))
        {
            return;
        }

        // 隐藏被取消时内容仍在浮动模型中，保留可见宿主供用户继续使用。
        if (!Model.Descendents().OfType<LayoutContent>().Any())
        {
            Hide();
        }
    }

    private bool CanExecuteCloseWindowCommand(object parameter) =>
        CanExecuteContentCommand<LayoutAnchorable, LayoutAnchorableItem>(parameter,
            static content => content.CanClose, static item => item.CloseCommand);

    private void OnExecuteCloseWindowCommand(object parameter) =>
        ExecuteContentCommand<LayoutAnchorable, LayoutAnchorableItem>(parameter, static item => item.CloseCommand);
}
