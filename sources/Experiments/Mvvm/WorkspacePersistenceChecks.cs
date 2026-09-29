using System.ComponentModel;
using System.Reflection;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.Mvvm;

internal static class WorkspacePersistenceChecks
{
    public static async Task RunAsync(DockingManager manager, WorkspaceViewModel viewModel, SampleChecks checks)
    {
        WorkspaceDocument firstDocument = viewModel.Documents.Single();
        WorkspaceDocument secondDocument = viewModel.OpenDocument("Dock After Restore", "Check restoration of a nontrivial XML layout.");
        WorkspaceDocument floatingDocument = viewModel.OpenDocument("Floating Close Check", "Check floating-document close cancellation and policy.");
        WorkspaceTool autoHideTool = viewModel.Tools.Single();
        WorkspaceTool floatingTool = CreateTool("persist-floating", "Floating Tool", DockZone.BottomLeft);
        WorkspaceTool detachedTool = CreateTool("persist-detached", "Detached Tool", DockZone.LeftTop);
        WorkspaceTool hiddenTool = CreateTool("persist-hidden", "Hidden Tool", DockZone.RightBottom);
        viewModel.AddToolForScenario(floatingTool);
        viewModel.AddToolForScenario(detachedTool);
        viewModel.AddToolForScenario(hiddenTool);
        await SampleChecks.SettleAsync();

        LayoutDocument first = CreateDocument(firstDocument);
        LayoutDocument second = CreateDocument(secondDocument);
        LayoutDocument floating = CreateDocument(floatingDocument);
        LayoutAnchorable autoHide = CreateAnchorable(autoHideTool);
        LayoutAnchorable toolToFloat = CreateAnchorable(floatingTool);
        LayoutAnchorable toolToDetach = CreateAnchorable(detachedTool);
        LayoutAnchorable toolToHide = CreateAnchorable(hiddenTool);
        LayoutDocumentPane topPane = new(first)
        {
            DockHeight = new GridLength(1, GridUnitType.Star)
        };
        LayoutDocumentPane bottomPane = new(second)
        {
            DockHeight = new GridLength(2, GridUnitType.Star)
        };
        bottomPane.Children.Add(floating);
        LayoutDocumentPaneGroup documentGroup = new(topPane)
        {
            Orientation = Orientation.Vertical
        };
        documentGroup.Children.Add(bottomPane);
        LayoutAnchorablePane leftPane = new(toolToDetach)
        {
            DockWidth = new GridLength(180)
        };
        LayoutAnchorablePane rightPane = new(autoHide)
        {
            DockWidth = new GridLength(210)
        };
        LayoutAnchorablePane bottomTools = new(toolToFloat)
        {
            DockHeight = new GridLength(160)
        };
        bottomTools.Children.Add(toolToHide);
        LayoutPanel center = new(documentGroup)
        {
            Orientation = Orientation.Vertical
        };
        center.Children.Add(bottomTools);
        LayoutPanel panel = new(leftPane)
        {
            Orientation = Orientation.Horizontal
        };
        panel.Children.Add(center);
        panel.Children.Add(rightPane);
        manager.Layout = new LayoutRoot { RootPanel = panel };
        await SampleChecks.SettleAsync();

        autoHide.AutoHideMinWidth = 70;
        autoHide.AutoHideMinHeight = 60;
        autoHide.AutoHideWidth = 80;
        autoHide.AutoHideHeight = 90;
        autoHide.ToggleAutoHide();
        toolToHide.Hide();
        floating.Float();
        toolToFloat.Float();
        manager.DetachAnchorableToWindow(toolToDetach);
        first.IsActive = true;
        await SampleChecks.SettleAsync();

        SampleChecks.Require(autoHide.IsAutoHidden && toolToHide.IsHidden
            && floating.IsFloating && toolToFloat.IsFloating && manager.IsDetached(toolToDetach),
            "Complex layout includes auto hide, hidden items, floating documents and tools, and a detached tool window.");
        Dictionary<string, object> contents = manager.Layout.Descendents().OfType<LayoutContent>()
            .ToDictionary(item => item.ContentId!, item => item.Content!);
        using MemoryStream saved = new();
        new XmlLayoutSerializer(manager).Serialize(saved);

        WorkspaceDocument addedAfterSave = viewModel.OpenDocument("Added After Save", "Check source-collection merge behavior during restore.");
        await SampleChecks.SettleAsync();
        SampleChecks.Require(manager.Layout.Descendents().OfType<LayoutDocument>()
            .Any(item => ReferenceEquals(item.Content, addedAfterSave)), "The MVVM source collection adds the new document to the layout after saving.");
        manager.DockAllFloatingWindows();
        manager.ReattachAllDetachedAnchorables();
        autoHide.ToggleAutoHide();
        toolToHide.Show();
        documentGroup.Orientation = Orientation.Horizontal;
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!manager.Layout.FloatingWindows.Any() && !manager.DetachedAnchorables.Any()
            && !autoHide.IsAutoHidden && !toolToHide.IsHidden, "Window and group states changed after saving.");

        XmlLayoutSerializer serializer = new(manager);
        serializer.LayoutSerializationCallback += (_, args) => args.Content = contents[args.Model.ContentId!];
        saved.Position = 0;
        serializer.Deserialize(saved);
        await SampleChecks.SettleAsync();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(viewModel.Documents.Contains(addedAfterSave)
            && manager.Layout.Descendents().OfType<LayoutDocument>()
                .Count(item => ReferenceEquals(item.Content, addedAfterSave)) == 1,
            "Deserialization retains the document added to the source collection after saving exactly once.");
        CheckSourceIdentity(manager, viewModel);

        first = FindContent<LayoutDocument>(manager, firstDocument);
        second = FindContent<LayoutDocument>(manager, secondDocument);
        floating = FindContent<LayoutDocument>(manager, floatingDocument);
        autoHide = FindContent<LayoutAnchorable>(manager, autoHideTool);
        toolToFloat = FindContent<LayoutAnchorable>(manager, floatingTool);
        toolToDetach = FindContent<LayoutAnchorable>(manager, detachedTool);
        toolToHide = FindContent<LayoutAnchorable>(manager, hiddenTool);
        LayoutDocumentPaneGroup restoredGroup = first.FindParent<LayoutDocumentPaneGroup>()!;
        SampleChecks.Require(restoredGroup.Orientation == Orientation.Vertical && restoredGroup.ChildrenCount == 2,
            "XML restores vertically split document groups.");
        SampleChecks.Require(first.Parent is LayoutDocumentPane { DockHeight.IsStar: true, DockHeight.Value: 1 }
            && second.Parent is LayoutDocumentPane { DockHeight.IsStar: true, DockHeight.Value: 2 },
            "XML restores the document groups' 1:2 star sizes.");
        SampleChecks.Require(autoHide.IsAutoHidden && autoHide.FindParent<LayoutAnchorSide>()?.Side == AnchorSide.Right
            && toolToHide.IsHidden && floating.IsFloating && toolToFloat.IsFloating
            && manager.IsDetached(toolToDetach) && manager.FloatingWindows.Count() == 2,
            "XML restores tool states, two real floating hosts, and one real detached host.");
        SampleChecks.Require(autoHide.AutoHideMinWidth == 70 && autoHide.AutoHideMinHeight == 60
            && autoHide.AutoHideWidth == 80 && autoHide.AutoHideHeight == 90,
            "XML restores public auto-hide sizes below the default minimum.");
        SampleChecks.Require(manager.Layout.Descendents().OfType<LayoutContent>().Count() == contents.Count
            + 1 && contents.Values.All(content => manager.Layout.Descendents().OfType<LayoutContent>()
                .Count(item => ReferenceEquals(item.Content, content)) == 1),
            "Complex XML restore keeps the same content references plus the new source document without duplicate nodes.");
        ILayoutContainer? floatingPrevious = ((ILayoutPreviousContainer)floating).PreviousContainer;
        ILayoutContainer? toolPrevious = ((ILayoutPreviousContainer)toolToFloat).PreviousContainer;
        SampleChecks.Require(ReferenceEquals(floatingPrevious?.Root, manager.Layout),
            $"The floating document's previous container belongs to the current layout after restore ({Describe(floatingPrevious)}).");
        SampleChecks.Require(toolPrevious == null || ReferenceEquals(toolPrevious.Root, manager.Layout),
            $"The floating tool's previous container, if present, belongs to the current layout after restore ({Describe(toolPrevious)}).");
        ILayoutContainer? autoHidePrevious = ((ILayoutPreviousContainer)autoHide.Parent!).PreviousContainer;
        SampleChecks.Require(autoHidePrevious is LayoutAnchorablePane { DockWidth.IsAbsolute: true, DockWidth.Value: 210 }
            && ReferenceEquals(autoHidePrevious.Root, manager.Layout),
            "The restored auto-hide group reconnects to its original 210-pixel pane in the current layout.");
        checks.Record("Complex XML save, layout change, and restoration of groups, sizes, hidden, auto-hide, floating, detached windows, and content references");

        await CheckFloatingCancellationAsync(manager, floating, toolToFloat, checks);
        DetachedAnchorableWindow detachedWindow = GetDetachedWindow(manager, toolToDetach);
        ILayoutContainer? detachedPrevious = ((ILayoutPreviousContainer)toolToDetach).PreviousContainer;
        ILayoutContainer? detachedParent = toolToDetach.Parent;
        bool detachedHidden = toolToDetach.IsHidden;
        detachedWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!manager.IsDetached(toolToDetach) && !toolToDetach.IsDetached
            && !toolToDetach.IsHidden && ReferenceEquals(toolToDetach.Parent, detachedPrevious),
            $"Closing a detached window returns the tool to its XML-recorded container (parent before close: {Describe(detachedParent)}, hidden={detachedHidden}; previous: {Describe(detachedPrevious)}; actual: {Describe(toolToDetach.Parent)}; detached={manager.IsDetached(toolToDetach)}/{toolToDetach.IsDetached}, hidden={toolToDetach.IsHidden}).");
        manager.DetachAnchorableToWindow(toolToDetach);
        viewModel.Layout.AllowDetachedWindows = false;
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!manager.DetachedAnchorables.Any() && !toolToDetach.IsDetached
            && ReferenceEquals(toolToDetach.Parent, detachedPrevious),
            "Disabling detached windows in MVVM reattaches the same tool to its original container.");
        viewModel.Layout.AllowDetachedWindows = true;

        autoHide.CanAutoHide = false;
        LayoutAnchorableItem item = (LayoutAnchorableItem)manager.GetLayoutItemFromModel(autoHide)!;
        SampleChecks.Require(item.AutoHideCommand?.CanExecute(null) == false, "CanAutoHide=false disables the pin command.");
        item.AutoHideCommand?.Execute(null);
        SampleChecks.Require(autoHide.IsAutoHidden, "Executing the disabled command preserves auto-hide state.");
        autoHide.CanAutoHide = true;
        item.AutoHideCommand?.Execute(null);
        SampleChecks.Require(!autoHide.IsAutoHidden && ReferenceEquals(autoHide.Parent?.Root, manager.Layout)
            && ReferenceEquals(autoHide.Parent, autoHidePrevious),
            "The auto-hidden tool can still be pinned to the current layout after XML restore.");
        toolToHide.Show();
        second.IsActive = true;
        SampleChecks.Require(ReferenceEquals(viewModel.Layout.ActiveDockable, secondDocument),
            "The view's active document still updates the MVVM model after XML restore.");
        second.CanFloat = false;
        LayoutItem documentItem = manager.GetLayoutItemFromModel(second)!;
        SampleChecks.Require(documentItem.FloatCommand?.CanExecute(null) == false, "CanFloat=false disables the float command.");
        documentItem.FloatCommand?.Execute(null);
        SampleChecks.Require(!second.IsFloating, "Executing the disabled float command leaves the layout unchanged.");
        second.CanFloat = true;
        documentItem.FloatCommand?.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(second.IsFloating && manager.FloatingWindows.Any(window => window.Model.Descendents().Contains(second)),
            "A real floating window can still be created after XML restore.");
        viewModel.Layout.AllowFloatingWindows = false;
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!manager.Layout.FloatingWindows.Any() && !manager.FloatingWindows.Any()
            && !second.IsFloating && ReferenceEquals(second.Root, manager.Layout),
            "Disabling floating in MVVM docks the restored document and releases its real floating host.");
        viewModel.Layout.AllowFloatingWindows = true;
        await CheckNewWindowPolicyAsync(manager, viewModel, second, toolToDetach, checks);
        (WorkspaceTool Model, AnchorSide Side)[] toolsBySide =
        [
            (CreateTool("persist-new-left", "New Left Tool", DockZone.LeftTop), AnchorSide.Left),
            (CreateTool("persist-new-right", "New Right Tool", DockZone.RightTop), AnchorSide.Right),
            (CreateTool("persist-new-bottom", "New Bottom Tool", DockZone.BottomLeft), AnchorSide.Bottom)
        ];
        foreach ((WorkspaceTool model, _) in toolsBySide)
        {
            viewModel.AddToolForScenario(model);
        }

        await SampleChecks.SettleAsync();
        foreach ((WorkspaceTool model, AnchorSide side) in toolsBySide)
        {
            LayoutAnchorable added = FindContent<LayoutAnchorable>(manager, model);
            SampleChecks.Require(added.FindParent<LayoutAnchorSide>()?.Side == side,
                $"The source collection docks the new {model.Title} tool on the MVVM {side} side.");
        }

        CheckSourceIdentity(manager, viewModel);
        checks.Record("Floating and close cancellation, detached window return, policy commands, and MVVM active items after XML restore");
    }

    private static async Task CheckNewWindowPolicyAsync(DockingManager manager, WorkspaceViewModel viewModel,
        LayoutDocument document, LayoutAnchorable tool, SampleChecks checks)
    {
        LayoutItem documentItem = manager.GetLayoutItemFromModel(document)
            ?? throw new InvalidOperationException("The document layout item for the MVVM policy check is missing.");
        LayoutAnchorableItem toolItem = manager.GetLayoutItemFromModel(tool) as LayoutAnchorableItem
            ?? throw new InvalidOperationException("The tool layout item for the MVVM policy check is missing.");
        ILayoutContainer? documentHome = document.Parent;
        ILayoutContainer? toolHome = tool.Parent;
        int floatingRequested = 0;
        int floated = 0;
        int detachedChanges = 0;
        EventHandler<ContentFloatingEventArgs> onFloating = (_, args) =>
        {
            if (ReferenceEquals(args.Content, document))
            {
                floatingRequested++;
            }
        };
        EventHandler<ContentFloatedEventArgs> onFloated = (_, args) =>
        {
            if (ReferenceEquals(args.Content, document))
            {
                floated++;
            }
        };
        PropertyChangedEventHandler onToolChanged = (_, args) =>
        {
            if (args.PropertyName == nameof(LayoutAnchorable.IsDetached))
            {
                detachedChanges++;
            }
        };
        manager.ContentFloating += onFloating;
        manager.ContentFloated += onFloated;
        tool.PropertyChanged += onToolChanged;
        try
        {
            viewModel.Layout.AllowFloatingWindows = false;
            viewModel.Layout.AllowDetachedWindows = false;
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!manager.AllowFloatingWindows && !manager.AllowDetachedWindows
                && documentItem.FloatCommand?.CanExecute(null) == false
                && toolItem.DetachToWindowCommand?.CanExecute(null) == false,
                "The MVVM window policy disables document float and tool detach commands.");

            documentItem.FloatCommand?.Execute(null);
            document.Float();
            toolItem.DetachToWindowCommand?.Execute(null);
            manager.DetachAnchorableToWindow(tool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(ReferenceEquals(document.Parent, documentHome)
                && ReferenceEquals(tool.Parent, toolHome)
                && !document.IsFloating && !tool.IsDetached && !manager.IsDetached(tool)
                && !manager.Layout.FloatingWindows.Any() && !manager.FloatingWindows.Any()
                && !manager.DetachedAnchorables.Any() && floatingRequested == 0 && floated == 0
                && detachedChanges == 0,
                "When new windows are disallowed, commands and direct requests are rejected without changing panes, hosts, or start/completion events.");

            viewModel.Layout.AllowFloatingWindows = true;
            viewModel.Layout.AllowDetachedWindows = true;
            await SampleChecks.SettleAsync();
            SampleChecks.Require(manager.AllowFloatingWindows && manager.AllowDetachedWindows
                && documentItem.FloatCommand?.CanExecute(null) == true
                && toolItem.DetachToWindowCommand?.CanExecute(null) == true,
                "Re-enabling the MVVM window policy restores document float and tool detach commands.");

            documentItem.FloatCommand!.Execute(null);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(document.IsFloating && manager.FloatingWindows.Any(window =>
                    window.Model.Descendents().Contains(document))
                && floatingRequested == 1 && floated == 1,
                "After re-enabling, the document command creates a real floating window and emits one start and one completion event.");
            document.Dock();
            await SampleChecks.SettleAsync();
            document.Float();
            await SampleChecks.SettleAsync();
            SampleChecks.Require(document.IsFloating && manager.FloatingWindows.Any(window =>
                    window.Model.Descendents().Contains(document))
                && floatingRequested == 2 && floated == 2,
                "After re-enabling, a direct document request also creates a real floating window.");
            document.Dock();
            await SampleChecks.SettleAsync();

            toolItem.DetachToWindowCommand!.Execute(null);
            await SampleChecks.SettleAsync();
            DetachedAnchorableWindow detachedWindow = GetDetachedWindow(manager, tool);
            SampleChecks.Require(manager.IsDetached(tool) && tool.IsDetached && !detachedWindow.IsClosed
                && detachedWindow.HasView && detachedChanges == 1,
                "After re-enabling, the tool command creates a real detached window and notifies the model once.");
            manager.ReattachAnchorable(tool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!manager.IsDetached(tool) && !tool.IsDetached
                && ReferenceEquals(tool.Parent, toolHome),
                "The tool returns to its original pane after the MVVM policy check.");
            manager.DetachAnchorableToWindow(tool);
            await SampleChecks.SettleAsync();
            DetachedAnchorableWindow directWindow = GetDetachedWindow(manager, tool);
            SampleChecks.Require(manager.IsDetached(tool) && tool.IsDetached && !directWindow.IsClosed
                && directWindow.HasView && detachedChanges == 3,
                "After re-enabling, a direct tool request also creates a real detached window.");
            manager.ReattachAnchorable(tool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!manager.IsDetached(tool) && ReferenceEquals(tool.Parent, toolHome),
                "The tool returns to its original pane after the direct-request check.");
            checks.Record("MVVM commands and direct requests reject new floating and detached windows when disabled, then create real windows when enabled (controlled R)");
        }
        finally
        {
            manager.ContentFloating -= onFloating;
            manager.ContentFloated -= onFloated;
            tool.PropertyChanged -= onToolChanged;
            viewModel.Layout.AllowFloatingWindows = true;
            viewModel.Layout.AllowDetachedWindows = true;
            if (document.IsFloating)
            {
                document.Dock();
            }
            if (manager.IsDetached(tool))
            {
                manager.ReattachAnchorable(tool);
            }
        }
    }

    private static async Task CheckFloatingCancellationAsync(DockingManager manager, LayoutDocument document,
        LayoutAnchorable tool, SampleChecks checks)
    {
        LayoutDocumentFloatingWindowControl documentWindow = manager.FloatingWindows.OfType<LayoutDocumentFloatingWindowControl>()
            .Single(window => window.Model.Descendents().Contains(document));
        document.CanClose = false;
        documentWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(document.IsFloating && IsHostVisible(documentWindow), "CanClose=false prevents system closing of a floating document.");
        document.CanClose = true;
        int documentClosingCount = 0;
        EventHandler<DocumentClosingEventArgs> cancelDocument = (_, args) =>
        {
            if (ReferenceEquals(args.Document, document))
            {
                documentClosingCount++;
                args.Cancel = true;
            }
        };
        manager.DocumentClosing += cancelDocument;
        documentWindow.Close();
        await SampleChecks.SettleAsync();
        manager.DocumentClosing -= cancelDocument;
        SampleChecks.Require(documentClosingCount == 1 && document.IsFloating && IsHostVisible(documentWindow),
            "Canceling floating-document close notifies once and keeps the real window visible.");

        LayoutAnchorableFloatingWindowControl toolWindow = manager.FloatingWindows.OfType<LayoutAnchorableFloatingWindowControl>()
            .Single(window => window.Model.Descendents().Contains(tool));
        tool.CanHide = false;
        toolWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(tool.IsFloating && IsHostVisible(toolWindow), "CanHide=false prevents system closing of a tool that cannot be closed by default.");
        tool.CanClose = true;
        int toolClosingCount = 0;
        EventHandler<AnchorableClosingEventArgs> cancelToolClose = (_, args) =>
        {
            if (ReferenceEquals(args.Anchorable, tool))
            {
                toolClosingCount++;
                args.Cancel = true;
            }
        };
        manager.AnchorableClosing += cancelToolClose;
        toolWindow.Close();
        await SampleChecks.SettleAsync();
        manager.AnchorableClosing -= cancelToolClose;
        SampleChecks.Require(toolClosingCount == 1 && tool.IsFloating && IsHostVisible(toolWindow),
            "Canceling close on a closable but non-hideable tool keeps the real window.");
        tool.CanClose = false;
        tool.CanHide = true;
        int toolHidingCount = 0;
        EventHandler<AnchorableHidingEventArgs> cancelTool = (_, args) =>
        {
            if (ReferenceEquals(args.Anchorable, tool))
            {
                toolHidingCount++;
                args.Cancel = true;
            }
        };
        manager.AnchorableHiding += cancelTool;
        toolWindow.Close();
        await SampleChecks.SettleAsync();
        manager.AnchorableHiding -= cancelTool;
        SampleChecks.Require(toolHidingCount == 1 && tool.IsFloating && IsHostVisible(toolWindow),
            "Canceling floating-tool hide notifies once and keeps the real window visible.");
        EventHandler<CancelEventArgs> cancelModelHide = (_, args) => args.Cancel = true;
        tool.Hiding += cancelModelHide;
        toolWindow.Close();
        await SampleChecks.SettleAsync();
        tool.Hiding -= cancelModelHide;
        SampleChecks.Require(tool.IsFloating && IsHostVisible(toolWindow), "Canceling model Hiding also keeps the floating tool window.");
        toolWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(tool.IsHidden && !manager.FloatingWindows.Contains(toolWindow), "Allowing hide releases the tool's floating host.");
        documentWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(document.Root == null && !manager.FloatingWindows.Contains(documentWindow), "Allowing close removes the floating document and its host.");
        checks.Record("Floating-document CanClose/DocumentClosing and tool AnchorableHiding/Hiding cancellation");
    }

    private static LayoutDocument CreateDocument(WorkspaceDocument model) => new()
    {
        Content = model,
        ContentId = model.Id,
        Title = model.Title
    };

    private static LayoutAnchorable CreateAnchorable(WorkspaceTool model) => new()
    {
        Content = model,
        ContentId = model.Id,
        Title = model.Title
    };

    private static WorkspaceTool CreateTool(string id, string title, DockZone zone) => new()
    {
        Id = id,
        Title = title,
        Zone = zone,
        IsOpenByDefault = true,
        Text = "Used to check layout persistence and window state."
    };

    private static T FindContent<T>(DockingManager manager, object model) where T : LayoutContent =>
        manager.Layout.Descendents().OfType<T>().Single(item => ReferenceEquals(item.Content, model));

    private static void CheckSourceIdentity(DockingManager manager, WorkspaceViewModel viewModel)
    {
        object[] expected = viewModel.Documents.Cast<object>().Concat(viewModel.Tools).ToArray();
        object?[] actual = manager.Layout.Descendents().OfType<LayoutContent>().Select(item => item.Content).ToArray();
        SampleChecks.Require(actual.Length == expected.Length
            && actual.All(item => item != null && expected.Count(model => ReferenceEquals(model, item)) == 1)
            && expected.All(model => actual.Count(item => ReferenceEquals(item, model)) == 1),
            "The restored MVVM document/tool source collections and layout content references match exactly in both directions.");
    }

    private static string Describe(ILayoutContainer? pane) => pane == null ? "null" :
        $"{pane.GetType().Name} Root={pane.Root?.GetHashCode()} Parent={pane.Parent?.GetType().Name}";

    private static bool IsHostVisible(LayoutFloatingWindowControl window)
    {
        object? host = typeof(LayoutFloatingWindowControl).GetProperty("WindowHost", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window);
        return host != null && host.GetType().GetProperty("IsVisible")?.GetValue(host) is true;
    }

    private static DetachedAnchorableWindow GetDetachedWindow(DockingManager manager, LayoutAnchorable model)
    {
        System.Collections.IDictionary entries = (System.Collections.IDictionary)typeof(DockingManager)
            .GetField("detachedEntries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        object entry = entries[model]!;
        return (DetachedAnchorableWindow)entry.GetType().GetProperty("Window")!.GetValue(entry)!;
    }
}
