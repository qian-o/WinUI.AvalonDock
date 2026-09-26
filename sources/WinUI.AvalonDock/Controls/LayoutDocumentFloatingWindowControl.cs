// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/LayoutDocumentFloatingWindowControl.cs
using System.ComponentModel;
using System.Windows.Input;
using AvalonDock.Commands;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

[Microsoft.UI.Xaml.Data.Bindable]
public partial class LayoutDocumentFloatingWindowControl : LayoutFloatingWindowControl
{
    private readonly LayoutDocumentFloatingWindow model;
    private DockingManager? ownerAtInitialization;
    private LayoutDocumentPaneGroup? observedRootPanel;
    internal LayoutDocumentFloatingWindowControl(LayoutDocumentFloatingWindow model) : this(model, false) { }
    internal LayoutDocumentFloatingWindowControl(LayoutDocumentFloatingWindow model, bool isContentImmutable) : base(model, isContentImmutable)
    {
        this.model = model;
        HideWindowCommand = new RelayCommand<object>(OnExecuteHideWindowCommand, CanExecuteHideWindowCommand);
        CloseWindowCommand = new RelayCommand<object>(OnExecuteCloseWindowCommand, CanExecuteCloseWindowCommand);
        Closed += (_, _) =>
        {
            if (OwnedByDockingManagerWindow && ownerAtInitialization?.IsLoaded == true)
            {
                PlatformServices.Coordinates.ActivateWindow(ownerAtInitialization);
            }
        };
    }
    public override ILayoutElement Model => model;
    public static readonly DependencyProperty SingleContentLayoutItemProperty = RegisterWindowProperty(nameof(SingleContentLayoutItem), typeof(LayoutItem),
        typeof(LayoutDocumentFloatingWindowControl), null, (owner, args) => ((LayoutDocumentFloatingWindowControl)owner).OnSingleContentLayoutItemChanged(args));
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
    public void HideOverlayWindow()
    {
        dropAreas = null;
        Manager?.HideOverlayWindow(this);
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        ownerAtInitialization = Manager;
        if (Content == null && Manager is { } manager && model.RootPanel is { } rootPanel)
        {
            Content = manager.CreateUIElementForModel(rootPanel);
        }

        observedRootPanel = model.RootPanel;
        if (observedRootPanel != null)
        {
            observedRootPanel.ChildrenCollectionChanged += RootPanelOnChildrenCollectionChanged;
        }
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (CloseInitiatedByUser)
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

        if (observedRootPanel != null)
        {
            observedRootPanel.ChildrenCollectionChanged -= RootPanelOnChildrenCollectionChanged;
        }

        observedRootPanel = null;
        SingleContentLayoutItem = null;
        base.OnClosed(e);
        if (!CloseInitiatedByUser && root?.Manager is { } manager && ReferenceEquals(manager, ownerAtInitialization))
        {
            root.FloatingWindows.Remove(model);
        }
    }
    private void RootPanelOnChildrenCollectionChanged(object? sender, EventArgs e)
    {
        if (model.RootPanel == null || model.RootPanel.Children.Count == 0)
        {
            InternalClose();
        }
    }
    internal override void ExecuteNativeClose(CancelEventArgs args)
    {
        CloseWindowCommand.Execute(null);
        args.Cancel = Model.Descendents().OfType<LayoutContent>().Any();
    }
    private bool CanExecuteHideWindowCommand(object parameter)
    {
        ILayoutRoot? root = Model?.Root;
        DockingManager? manager = root?.Manager;
        if (manager == null)
        {
            return false;
        }

        bool canExecute = false;
        foreach (LayoutContent? content in Model.Descendents().OfType<LayoutContent>().ToArray())
        {
            if (content is LayoutAnchorable anchorable && !anchorable.CanHide || !content.CanClose)
            {
                canExecute = false;
                break;
            }

            if (!(manager.GetLayoutItemFromModel(content) is LayoutItem layoutItem) || layoutItem.CloseCommand == null || !layoutItem.CloseCommand.CanExecute(parameter))
            {
                canExecute = false;
                break;
            }

            canExecute = true;
        }

        return canExecute;
    }

    private void OnExecuteHideWindowCommand(object parameter)
    {
        DockingManager? manager = Model.Root?.Manager;
        if (manager == null)
        {
            return;
        }
        foreach (LayoutContent? content in Model.Descendents().OfType<LayoutContent>().ToArray())
        {
            if (manager.GetLayoutItemFromModel(content) is LayoutItem layoutItem)
            {
                layoutItem.CloseCommand?.Execute(parameter);
            }
        }
    }

    private bool CanExecuteCloseWindowCommand(object parameter)
    {
        DockingManager? manager = Model?.Root?.Manager;
        if (manager == null)
        {
            return false;
        }

        bool canExecute = false;
        foreach (LayoutDocument? document in Model.Descendents().OfType<LayoutDocument>().ToArray())
        {
            if (!document.CanClose)
            {
                canExecute = false;
                break;
            }

            if (!(manager.GetLayoutItemFromModel(document) is LayoutDocumentItem documentLayoutItem) || documentLayoutItem.CloseCommand == null || !documentLayoutItem.CloseCommand.CanExecute(parameter))
            {
                canExecute = false;
                break;
            }

            canExecute = true;
        }

        return canExecute;
    }

    private void OnExecuteCloseWindowCommand(object parameter)
    {
        DockingManager? manager = Model.Root?.Manager;
        if (manager == null)
        {
            return;
        }
        foreach (LayoutDocument? document in Model.Descendents().OfType<LayoutDocument>().ToArray())
        {
            LayoutDocumentItem? documentLayoutItem = manager.GetLayoutItemFromModel(document) as LayoutDocumentItem;
            documentLayoutItem?.CloseCommand?.Execute(parameter);
        }
    }
}
