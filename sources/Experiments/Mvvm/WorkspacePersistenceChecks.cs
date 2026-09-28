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
        WorkspaceDocument secondDocument = viewModel.OpenDocument("恢复后继续停靠", "检查非平凡 XML 布局恢复。 ");
        WorkspaceDocument floatingDocument = viewModel.OpenDocument("浮动关闭检查", "检查浮动文档的取消关闭与策略。 ");
        WorkspaceTool autoHideTool = viewModel.Tools.Single();
        WorkspaceTool floatingTool = CreateTool("persist-floating", "浮动工具", DockZone.BottomLeft);
        WorkspaceTool detachedTool = CreateTool("persist-detached", "独立工具", DockZone.LeftTop);
        WorkspaceTool hiddenTool = CreateTool("persist-hidden", "隐藏工具", DockZone.RightBottom);
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
            "复杂布局包含自动隐藏、隐藏、文档浮动、工具浮动和独立工具窗口。 ");
        Dictionary<string, object> contents = manager.Layout.Descendents().OfType<LayoutContent>()
            .ToDictionary(item => item.ContentId!, item => item.Content!);
        using MemoryStream saved = new();
        new XmlLayoutSerializer(manager).Serialize(saved);

        WorkspaceDocument addedAfterSave = viewModel.OpenDocument("保存后新增", "验证官方来源集合恢复时的合并语义。 ");
        await SampleChecks.SettleAsync();
        SampleChecks.Require(manager.Layout.Descendents().OfType<LayoutDocument>()
            .Any(item => ReferenceEquals(item.Content, addedAfterSave)), "保存后新文档已由 MVVM 来源集合加入布局。 ");
        manager.DockAllFloatingWindows();
        manager.ReattachAllDetachedAnchorables();
        autoHide.ToggleAutoHide();
        toolToHide.Show();
        documentGroup.Orientation = Orientation.Horizontal;
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!manager.Layout.FloatingWindows.Any() && !manager.DetachedAnchorables.Any()
            && !autoHide.IsAutoHidden && !toolToHide.IsHidden, "保存后已改变窗口和分组状态。 ");

        XmlLayoutSerializer serializer = new(manager);
        serializer.LayoutSerializationCallback += (_, args) => args.Content = contents[args.Model.ContentId!];
        saved.Position = 0;
        serializer.Deserialize(saved);
        await SampleChecks.SettleAsync();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(viewModel.Documents.Contains(addedAfterSave)
            && manager.Layout.Descendents().OfType<LayoutDocument>()
                .Count(item => ReferenceEquals(item.Content, addedAfterSave)) == 1,
            "官方反序列化保留来源集合中保存后新增的文档，且只导入一次。 ");
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
            "XML 恢复文档上下分组。 ");
        SampleChecks.Require(first.Parent is LayoutDocumentPane { DockHeight.IsStar: true, DockHeight.Value: 1 }
            && second.Parent is LayoutDocumentPane { DockHeight.IsStar: true, DockHeight.Value: 2 },
            "XML 恢复文档组 1:2 星号尺寸。 ");
        SampleChecks.Require(autoHide.IsAutoHidden && autoHide.FindParent<LayoutAnchorSide>()?.Side == AnchorSide.Right
            && toolToHide.IsHidden && floating.IsFloating && toolToFloat.IsFloating
            && manager.IsDetached(toolToDetach) && manager.FloatingWindows.Count() == 2,
            "XML 恢复各工具状态及两个真实浮动宿主、一个真实独立宿主。 ");
        SampleChecks.Require(autoHide.AutoHideMinWidth == 70 && autoHide.AutoHideMinHeight == 60
            && autoHide.AutoHideWidth == 80 && autoHide.AutoHideHeight == 90,
            "XML 恢复低于默认最小值的公开自动隐藏尺寸。 ");
        SampleChecks.Require(manager.Layout.Descendents().OfType<LayoutContent>().Count() == contents.Count
            + 1 && contents.Values.All(content => manager.Layout.Descendents().OfType<LayoutContent>()
                .Count(item => ReferenceEquals(item.Content, content)) == 1),
            "复杂 XML 恢复逐项保留同一内容引用，外加来源集合中的新文档，无重复节点。 ");
        ILayoutContainer? floatingPrevious = ((ILayoutPreviousContainer)floating).PreviousContainer;
        ILayoutContainer? toolPrevious = ((ILayoutPreviousContainer)toolToFloat).PreviousContainer;
        SampleChecks.Require(ReferenceEquals(floatingPrevious?.Root, manager.Layout),
            $"恢复后文档浮动原容器引用指向当前布局（{Describe(floatingPrevious)}）。 ");
        SampleChecks.Require(toolPrevious == null || ReferenceEquals(toolPrevious.Root, manager.Layout),
            $"恢复后工具浮动原容器若存在则指向当前布局（{Describe(toolPrevious)}）。 ");
        ILayoutContainer? autoHidePrevious = ((ILayoutPreviousContainer)autoHide.Parent!).PreviousContainer;
        SampleChecks.Require(autoHidePrevious is LayoutAnchorablePane { DockWidth.IsAbsolute: true, DockWidth.Value: 210 }
            && ReferenceEquals(autoHidePrevious.Root, manager.Layout),
            "恢复后自动隐藏组重连当前布局中的 210 像素原窗格。 ");
        checks.Record("复杂 XML 保存、改变布局、恢复分组/尺寸/隐藏/自动隐藏/浮动/独立窗口及内容引用通过。");

        await CheckFloatingCancellationAsync(manager, floating, toolToFloat, checks);
        DetachedAnchorableWindow detachedWindow = GetDetachedWindow(manager, toolToDetach);
        ILayoutContainer? detachedPrevious = ((ILayoutPreviousContainer)toolToDetach).PreviousContainer;
        ILayoutContainer? detachedParent = toolToDetach.Parent;
        bool detachedHidden = toolToDetach.IsHidden;
        detachedWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!manager.IsDetached(toolToDetach) && !toolToDetach.IsDetached
            && !toolToDetach.IsHidden && ReferenceEquals(toolToDetach.Parent, detachedPrevious),
            $"关闭独立窗口后同一工具返回 XML 记录的原容器（关闭前 Parent：{Describe(detachedParent)}，Hidden={detachedHidden}；Previous：{Describe(detachedPrevious)}；实际：{Describe(toolToDetach.Parent)}；detached={manager.IsDetached(toolToDetach)}/{toolToDetach.IsDetached} hidden={toolToDetach.IsHidden}）。 ");
        manager.DetachAnchorableToWindow(toolToDetach);
        viewModel.Layout.AllowDetachedWindows = false;
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!manager.DetachedAnchorables.Any() && !toolToDetach.IsDetached
            && ReferenceEquals(toolToDetach.Parent, detachedPrevious),
            "MVVM 禁止独立窗口会把同一工具附回原容器。 ");
        viewModel.Layout.AllowDetachedWindows = true;

        autoHide.CanAutoHide = false;
        LayoutAnchorableItem item = (LayoutAnchorableItem)manager.GetLayoutItemFromModel(autoHide)!;
        SampleChecks.Require(item.AutoHideCommand?.CanExecute(null) == false, "CanAutoHide=false 禁用固定命令。 ");
        item.AutoHideCommand?.Execute(null);
        SampleChecks.Require(autoHide.IsAutoHidden, "禁用命令执行后自动隐藏状态保持。 ");
        autoHide.CanAutoHide = true;
        item.AutoHideCommand?.Execute(null);
        SampleChecks.Require(!autoHide.IsAutoHidden && ReferenceEquals(autoHide.Parent?.Root, manager.Layout)
            && ReferenceEquals(autoHide.Parent, autoHidePrevious),
            "XML 恢复后自动隐藏工具仍可固定回当前布局。 ");
        toolToHide.Show();
        second.IsActive = true;
        SampleChecks.Require(ReferenceEquals(viewModel.Layout.ActiveDockable, secondDocument),
            "XML 恢复后视图活动文档仍回写官方 MVVM 模型。 ");
        second.CanFloat = false;
        LayoutItem documentItem = manager.GetLayoutItemFromModel(second)!;
        SampleChecks.Require(documentItem.FloatCommand?.CanExecute(null) == false, "CanFloat=false 禁用浮动命令。 ");
        documentItem.FloatCommand?.Execute(null);
        SampleChecks.Require(!second.IsFloating, "禁用浮动命令不会改变布局。 ");
        second.CanFloat = true;
        documentItem.FloatCommand?.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(second.IsFloating && manager.FloatingWindows.Any(window => window.Model.Descendents().Contains(second)),
            "XML 恢复后仍可创建真实浮动窗口。 ");
        viewModel.Layout.AllowFloatingWindows = false;
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!manager.Layout.FloatingWindows.Any() && !manager.FloatingWindows.Any()
            && !second.IsFloating && ReferenceEquals(second.Root, manager.Layout),
            "MVVM 禁止浮动会停回恢复后的文档并释放真实浮动宿主。 ");
        viewModel.Layout.AllowFloatingWindows = true;
        await CheckNewWindowPolicyAsync(manager, viewModel, second, toolToDetach, checks);
        (WorkspaceTool Model, AnchorSide Side)[] toolsBySide =
        [
            (CreateTool("persist-new-left", "新增左区工具", DockZone.LeftTop), AnchorSide.Left),
            (CreateTool("persist-new-right", "新增右区工具", DockZone.RightTop), AnchorSide.Right),
            (CreateTool("persist-new-bottom", "新增底区工具", DockZone.BottomLeft), AnchorSide.Bottom)
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
                $"来源集合新增的 {model.Title} 按 MVVM 对应区域停入 {side} 侧。 ");
        }

        CheckSourceIdentity(manager, viewModel);
        checks.Record("XML 恢复后的浮动/关闭取消、独立窗口关闭返回、策略命令及 MVVM 活动项继续使用通过。");
    }

    private static async Task CheckNewWindowPolicyAsync(DockingManager manager, WorkspaceViewModel viewModel,
        LayoutDocument document, LayoutAnchorable tool, SampleChecks checks)
    {
        LayoutItem documentItem = manager.GetLayoutItemFromModel(document)
            ?? throw new InvalidOperationException("MVVM 策略检查的文档布局项不存在。 ");
        LayoutAnchorableItem toolItem = manager.GetLayoutItemFromModel(tool) as LayoutAnchorableItem
            ?? throw new InvalidOperationException("MVVM 策略检查的工具布局项不存在。 ");
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
                "MVVM 窗口策略同步禁用文档浮动与工具独立窗口命令。 ");

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
                "MVVM 禁止新窗口时，命令和直接请求均被拒绝；原窗格、真实宿主与开始/完成事件保持不变。 ");

            viewModel.Layout.AllowFloatingWindows = true;
            viewModel.Layout.AllowDetachedWindows = true;
            await SampleChecks.SettleAsync();
            SampleChecks.Require(manager.AllowFloatingWindows && manager.AllowDetachedWindows
                && documentItem.FloatCommand?.CanExecute(null) == true
                && toolItem.DetachToWindowCommand?.CanExecute(null) == true,
                "MVVM 重新启用窗口策略后，文档浮动和工具独立窗口命令恢复可用。 ");

            documentItem.FloatCommand!.Execute(null);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(document.IsFloating && manager.FloatingWindows.Any(window =>
                    window.Model.Descendents().Contains(document))
                && floatingRequested == 1 && floated == 1,
                "MVVM 重新启用后，文档命令建立真实浮动窗口并各发送一次开始/完成事件。 ");
            document.Dock();
            await SampleChecks.SettleAsync();
            document.Float();
            await SampleChecks.SettleAsync();
            SampleChecks.Require(document.IsFloating && manager.FloatingWindows.Any(window =>
                    window.Model.Descendents().Contains(document))
                && floatingRequested == 2 && floated == 2,
                "MVVM 重新启用后，文档直接请求也建立真实浮动窗口。 ");
            document.Dock();
            await SampleChecks.SettleAsync();

            toolItem.DetachToWindowCommand!.Execute(null);
            await SampleChecks.SettleAsync();
            DetachedAnchorableWindow detachedWindow = GetDetachedWindow(manager, tool);
            SampleChecks.Require(manager.IsDetached(tool) && tool.IsDetached && !detachedWindow.IsClosed
                && detachedWindow.HasView && detachedChanges == 1,
                "MVVM 重新启用后，工具命令建立真实独立窗口并通知模型一次。 ");
            manager.ReattachAnchorable(tool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!manager.IsDetached(tool) && !tool.IsDetached
                && ReferenceEquals(tool.Parent, toolHome),
                "MVVM 策略检查完成后，工具返回原窗格。 ");
            manager.DetachAnchorableToWindow(tool);
            await SampleChecks.SettleAsync();
            DetachedAnchorableWindow directWindow = GetDetachedWindow(manager, tool);
            SampleChecks.Require(manager.IsDetached(tool) && tool.IsDetached && !directWindow.IsClosed
                && directWindow.HasView && detachedChanges == 3,
                "MVVM 重新启用后，工具直接请求也建立真实独立窗口。 ");
            manager.ReattachAnchorable(tool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!manager.IsDetached(tool) && ReferenceEquals(tool.Parent, toolHome),
                "MVVM 直接请求检查完成后，工具返回原窗格。 ");
            checks.Record("MVVM 禁止新浮窗/独立窗时命令与直接请求被拒绝，启用后真实窗口请求成功（受控 R）。");
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
        SampleChecks.Require(document.IsFloating && IsHostVisible(documentWindow), "CanClose=false 阻止浮动文档系统关闭。 ");
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
            "取消浮动文档关闭只通知一次并保留真实可见窗口。 ");

        LayoutAnchorableFloatingWindowControl toolWindow = manager.FloatingWindows.OfType<LayoutAnchorableFloatingWindowControl>()
            .Single(window => window.Model.Descendents().Contains(tool));
        tool.CanHide = false;
        toolWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(tool.IsFloating && IsHostVisible(toolWindow), "CanHide=false 阻止默认不可关闭工具的系统关闭。 ");
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
            "可关闭但不可隐藏工具的关闭取消保留真实窗口。 ");
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
            "取消浮动工具隐藏只通知一次并保留真实可见窗口。 ");
        EventHandler<CancelEventArgs> cancelModelHide = (_, args) => args.Cancel = true;
        tool.Hiding += cancelModelHide;
        toolWindow.Close();
        await SampleChecks.SettleAsync();
        tool.Hiding -= cancelModelHide;
        SampleChecks.Require(tool.IsFloating && IsHostVisible(toolWindow), "模型 Hiding 取消也保留浮动工具窗口。 ");
        toolWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(tool.IsHidden && !manager.FloatingWindows.Contains(toolWindow), "允许隐藏后释放工具浮动宿主。 ");
        documentWindow.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(document.Root == null && !manager.FloatingWindows.Contains(documentWindow), "允许关闭后移除浮动文档及宿主。 ");
        checks.Record("浮动文档 CanClose/DocumentClosing 和工具 AnchorableHiding/Hiding 取消通过。");
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
        Text = "用于布局持久化与窗口状态检查。 "
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
            "恢复后的官方 MVVM 文档/工具来源集合与布局内容引用双向严格相等。 ");
    }

    private static string Describe(ILayoutContainer? pane) => pane == null ? "空" :
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
