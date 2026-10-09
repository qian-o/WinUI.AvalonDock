using System.Collections.ObjectModel;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;

namespace WinUI.AvalonDock.Experiments.Shared;

/// <summary>Verifies pinned upstream MVVM model behavior through real close and hide commands.</summary>
internal static class LayoutSyncSmokeTests
{
    // Call on the WinUI dispatcher with a dedicated manager that has no source bindings.
    // Assigning DockLayout invokes the real LayoutSyncBridge.Attach path.
    internal static async Task<IReadOnlyList<string>> RunAsync(DockingManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (!manager.DispatcherQueue.HasThreadAccess || manager.DockLayout != null
            || manager.DocumentsSource != null || manager.AnchorablesSource != null)
        {
            throw new ArgumentException("Use an unbound docking manager on its UI thread.", nameof(manager));
        }

        List<string> checks = [];
        LayoutRoot previousLayout = manager.Layout;
        WorkspaceTool toolModel = new() { Id = "close-parity-tool", Title = "Upstream close behavior", Zone = DockZone.LeftTop };
        ToolDock toolDock = new()
        {
            Alignment = DockAlignment.Left,
            VisibleDockables = new ObservableCollection<IDockable> { toolModel }
        };
        DocumentDock documentDock = new() { VisibleDockables = new ObservableCollection<IDockable>() };
        RootDock root = new()
        {
            VisibleDockables = new ObservableCollection<IDockable> { toolDock, documentDock },
            ActiveDockable = toolModel
        };
        int closedEvents = 0;
        int hiddenEvents = 0;
        void OnClosed(object? sender, AnchorableClosedEventArgs args) => closedEvents++;
        void OnHidden(object? sender, AnchorableHiddenEventArgs args) => hiddenEvents++;
        void CancelClosing(object? sender, AnchorableClosingEventArgs args) => args.Cancel = true;

        manager.AnchorableClosed += OnClosed;
        manager.AnchorableHidden += OnHidden;
        try
        {
            manager.Layout = new LayoutRoot
            {
                RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(new LayoutDocumentPane()))
            };
            manager.DockLayout = root;
            await Settle();
            LayoutAnchorable tool = manager.Layout.Descendents().OfType<LayoutAnchorable>()
                .Single(item => ReferenceEquals(item.Content, toolModel));
            tool.CanClose = true;
            LayoutAnchorableItem item = manager.GetLayoutItemFromModel(tool) as LayoutAnchorableItem
                ?? throw new InvalidOperationException("The manager did not create the tool's layout item.");
            Check(item.CloseCommand?.CanExecute(null) == true, "tool close command is enabled");

            manager.AnchorableClosing += CancelClosing;
            try
            {
                item.CloseCommand!.Execute(null);
            }
            finally
            {
                manager.AnchorableClosing -= CancelClosing;
            }
            await Settle();
            Check(ReferenceEquals(tool.Root, manager.Layout) && ContainsModel(), "canceled close retains layout and MVVM tool");
            Check(closedEvents == 0, "canceled close raises no completed close event");

            Check(item.HideCommand?.CanExecute(null) == true, "tool hide command is enabled");
            item.HideCommand!.Execute(null);
            await Settle();
            Check(tool.IsHidden && ContainsModel(), "hide retains the MVVM tool model");
            Check(hiddenEvents == 1 && closedEvents == 0, "hide completes independently of close");
            tool.Show();
            await Settle();
            Check(tool.IsVisible && ReferenceEquals(tool.Root, manager.Layout), "hidden tool can be shown again");

            item.CloseCommand!.Execute(null);
            await Settle();
            Check(closedEvents == 1 && hiddenEvents == 1, "close command performs actual close rather than hide");
            Check(tool.Root == null, "successful tool close removes native layout node");
            Check(ContainsModel(), "upstream tool close retains ToolDock.VisibleDockables model");
            Check(manager.AnchorablesSource?.Cast<object>().Any(value => ReferenceEquals(value, toolModel)) == true,
                "upstream tool close retains bridge anchorable source model");

            root.VisibleDockables = new ObservableCollection<IDockable> { toolDock, documentDock };
            await Settle();
            Check(ContainsModel()
                && manager.AnchorablesSource?.Cast<object>().Any(value => ReferenceEquals(value, toolModel)) == true,
                "upstream dock tree rebuild retains the tool model and bridge source");
            manager.DockLayout = null;
            manager.DockLayout = root;
            await Settle();
            Check(ContainsModel() && HasNativeTool(), "upstream layout reattachment imports the retained tool model");
            return checks;

            bool ContainsModel() => toolDock.VisibleDockables?.Any(value => ReferenceEquals(value, toolModel)) == true;
            bool HasNativeTool() => manager.Layout.Descendents().OfType<LayoutAnchorable>()
                .Any(value => ReferenceEquals(value.Content, toolModel));
            void Check(bool condition, string description)
            {
                if (!condition)
                {
                    throw new InvalidOperationException($"MVVM upstream behavior: {description}");
                }
                checks.Add($"PASS: {description}");
            }
        }
        finally
        {
            manager.AnchorableClosing -= CancelClosing;
            manager.AnchorableClosed -= OnClosed;
            manager.AnchorableHidden -= OnHidden;
            manager.DockLayout = null;
            manager.Layout = previousLayout;
        }
    }

    private static async Task Settle() => await Task.Delay(150);
}
