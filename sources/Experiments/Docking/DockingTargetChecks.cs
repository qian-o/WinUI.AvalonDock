using System.Collections;
using System.Reflection;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.AvalonDock.Experiments.Shared;
using Windows.Foundation;

namespace WinUI.AvalonDock.Experiments.Docking;

/// <summary>
/// 在经典示例的真实窗格中核对各类停靠目标的拖动会话、覆盖层帧与布局转换。
/// </summary>
internal static class DockingTargetChecks
{
    private enum AreaKind
    {
        Manager, ToolPane, DocumentPane, EmptyDocumentGroup
    }
    private enum Side
    {
        Left, Top, Right, Bottom, Inside
    }

    private sealed record Case(DropTargetType Type, bool ToolSource, AreaKind Area, Side Side, string Glyph,
        string Label);

    private static readonly Case[] Cases =
    [
        new(DropTargetType.DockingManagerDockLeft, true, AreaKind.Manager, Side.Left, "PART_DockingManagerDropTargetLeft", "工具→主窗口左侧"),
        new(DropTargetType.DockingManagerDockTop, true, AreaKind.Manager, Side.Top, "PART_DockingManagerDropTargetTop", "工具→主窗口上侧"),
        new(DropTargetType.DockingManagerDockRight, true, AreaKind.Manager, Side.Right, "PART_DockingManagerDropTargetRight", "工具→主窗口右侧"),
        new(DropTargetType.DockingManagerDockBottom, true, AreaKind.Manager, Side.Bottom, "PART_DockingManagerDropTargetBottom", "工具→主窗口下侧"),
        new(DropTargetType.AnchorablePaneDockLeft, true, AreaKind.ToolPane, Side.Left, "PART_AnchorablePaneDropTargetLeft", "工具→工具窗格左侧"),
        new(DropTargetType.AnchorablePaneDockTop, true, AreaKind.ToolPane, Side.Top, "PART_AnchorablePaneDropTargetTop", "工具→工具窗格上侧"),
        new(DropTargetType.AnchorablePaneDockRight, true, AreaKind.ToolPane, Side.Right, "PART_AnchorablePaneDropTargetRight", "工具→工具窗格右侧"),
        new(DropTargetType.AnchorablePaneDockBottom, true, AreaKind.ToolPane, Side.Bottom, "PART_AnchorablePaneDropTargetBottom", "工具→工具窗格下侧"),
        new(DropTargetType.AnchorablePaneDockInside, true, AreaKind.ToolPane, Side.Inside, "PART_AnchorablePaneDropTargetInto", "工具→工具窗格标签"),
        new(DropTargetType.DocumentPaneDockLeft, false, AreaKind.DocumentPane, Side.Left, "PART_DocumentPaneDropTargetLeft", "文档→文档窗格左侧"),
        new(DropTargetType.DocumentPaneDockTop, false, AreaKind.DocumentPane, Side.Top, "PART_DocumentPaneDropTargetTop", "文档→文档窗格上侧"),
        new(DropTargetType.DocumentPaneDockRight, false, AreaKind.DocumentPane, Side.Right, "PART_DocumentPaneDropTargetRight", "文档→文档窗格右侧"),
        new(DropTargetType.DocumentPaneDockBottom, false, AreaKind.DocumentPane, Side.Bottom, "PART_DocumentPaneDropTargetBottom", "文档→文档窗格下侧"),
        new(DropTargetType.DocumentPaneDockInside, false, AreaKind.DocumentPane, Side.Inside, "PART_DocumentPaneDropTargetInto", "文档→文档窗格标签"),
        new(DropTargetType.DocumentPaneGroupDockInside, false, AreaKind.EmptyDocumentGroup, Side.Inside,
            "PART_DocumentPaneDropTargetInto", "文档→空文档分组"),
        new(DropTargetType.DocumentPaneDockLeft, true, AreaKind.DocumentPane, Side.Left, "PART_DocumentPaneFullDropTargetLeft", "工具→文档窗格内左侧"),
        new(DropTargetType.DocumentPaneDockTop, true, AreaKind.DocumentPane, Side.Top, "PART_DocumentPaneFullDropTargetTop", "工具→文档窗格内上侧"),
        new(DropTargetType.DocumentPaneDockRight, true, AreaKind.DocumentPane, Side.Right, "PART_DocumentPaneFullDropTargetRight", "工具→文档窗格内右侧"),
        new(DropTargetType.DocumentPaneDockBottom, true, AreaKind.DocumentPane, Side.Bottom, "PART_DocumentPaneFullDropTargetBottom", "工具→文档窗格内下侧"),
        new(DropTargetType.DocumentPaneDockInside, true, AreaKind.DocumentPane, Side.Inside, "PART_DocumentPaneFullDropTargetInto", "工具→文档窗格标签"),
        new(DropTargetType.DocumentPaneDockAsAnchorableLeft, true, AreaKind.DocumentPane, Side.Left,
            "PART_DocumentPaneDropTargetLeftAsAnchorablePane", "工具→文档组外左侧"),
        new(DropTargetType.DocumentPaneDockAsAnchorableTop, true, AreaKind.DocumentPane, Side.Top,
            "PART_DocumentPaneDropTargetTopAsAnchorablePane", "工具→文档组外上侧"),
        new(DropTargetType.DocumentPaneDockAsAnchorableRight, true, AreaKind.DocumentPane, Side.Right,
            "PART_DocumentPaneDropTargetRightAsAnchorablePane", "工具→文档组外右侧"),
        new(DropTargetType.DocumentPaneDockAsAnchorableBottom, true, AreaKind.DocumentPane, Side.Bottom,
            "PART_DocumentPaneDropTargetBottomAsAnchorablePane", "工具→文档组外下侧"),
    ];

    internal static async Task RunAsync(DockingManager manager, FrameworkElement root, object documentContent,
        object toolContent, SampleChecks checks, Point mainWindowTopLeft)
    {
        LayoutContent initialDocumentTarget = manager.Layout.Descendents().OfType<LayoutDocument>()
            .Single(item => !ReferenceEquals(item.Content, documentContent));
        LayoutContent initialToolTarget = manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Single(item => !ReferenceEquals(item.Content, toolContent));
        Dictionary<string, object> contentById = manager.Layout.Descendents().OfType<LayoutContent>()
            .Where(item => item.ContentId is { Length: > 0 } && item.Content is not null)
            .ToDictionary(item => item.ContentId!, item => item.Content!);
        SampleChecks.Require(contentById.Count >= 4, "19 类目标检查具有四个可恢复内容。 ");
        using MemoryStream baselineStream = new();
        new XmlLayoutSerializer(manager).Serialize(baselineStream);
        byte[] baseline = baselineStream.ToArray();
        string documentTargetId = initialDocumentTarget.ContentId!;
        string toolTargetId = initialToolTarget.ContentId!;
        List<string> failures = [];

        foreach (Case test in Cases)
        {
            try
            {
                await RestoreAsync();
                await VerifyAsync(test);
                checks.Record($"Classic {test.Label}：实际指示器、命中、预览帧和释放后布局通过。");
            }
            catch (Exception exception)
            {
                failures.Add($"{test.Label} ({test.Type}): {exception.GetBaseException()}");
            }
        }

        await CheckAdditionalAsync("文档源目标限制", VerifyDocumentSourceAreasAsync);
        await CheckAdditionalAsync("工具禁止停入文档区", VerifyToolCannotDockAsDocumentAsync);
        await CheckAdditionalAsync("文档多分组方向限制", VerifyMixedOrientationAsync);
        await CheckAdditionalAsync("文档浮窗相互停靠及浮窗外侧目标限制", () => VerifyFloatingHostAsync(false));
        await CheckAdditionalAsync("工具浮窗相互停靠", () => VerifyFloatingHostAsync(true));

        await RestoreAsync();
        string? groupFailure = failures.FirstOrDefault(failure => failure.Contains(nameof(DropTargetType.DocumentPaneGroupDockInside)));
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "AvalonDock-target-untested.txt"),
            groupFailure is null
                ? "Pass：DocumentPaneGroupDockInside 已核对空组中心目标、预览与释放；无该项 Untested。" + Environment.NewLine
                : "Fail：DocumentPaneGroupDockInside 自动验收未通过：" + groupFailure + Environment.NewLine);
        SampleChecks.Require(failures.Count == 0, "Classic 目标验收失败：" + string.Join("；", failures));

        async Task CheckAdditionalAsync(string label, Func<Task> verify)
        {
            try
            {
                await RestoreAsync();
                await verify();
                checks.Record($"Classic {label}：实际宿主、目标与布局检查通过。");
            }
            catch (Exception exception)
            {
                failures.Add($"{label}: {exception.GetBaseException()}");
            }
        }

        async Task RestoreAsync()
        {
            XmlLayoutSerializer serializer = new(manager);
            serializer.LayoutSerializationCallback += (_, args) =>
            {
                if (args.Model.ContentId is { } id && contentById.TryGetValue(id, out object? content))
                {
                    args.Content = content;
                }
            };
            using MemoryStream input = new(baseline, writable: false);
            serializer.Deserialize(input);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!manager.Layout.Descendents().OfType<LayoutContent>().Any(item => item.IsFloating),
                "目标场景之间已恢复没有浮动项的基线。 ");
        }

        async Task VerifyAsync(Case test)
        {
            object sourceContent = test.ToolSource ? toolContent : documentContent;
            LayoutContent sourceModel = ModelFor(sourceContent);
            sourceModel.Float();
            await DockingDragChecks.WaitUntilAsync(() => sourceModel.IsFloating && WindowFor(sourceModel) is { IsLoaded: true },
                $"{test.Label} 的源未形成实际浮动窗口。 ");
            LayoutFloatingWindowControl sourceWindow = WindowFor(sourceModel)!;
            Park(sourceWindow, mainWindowTopLeft);

            if (test.Area == AreaKind.EmptyDocumentGroup)
            {
                LayoutContent otherDocument = ModelById(documentTargetId);
                otherDocument.Float();
                await DockingDragChecks.WaitUntilAsync(() => otherDocument.IsFloating && WindowFor(otherDocument) is { IsLoaded: true },
                    "空文档分组场景的另一文档未浮出。 ");
                LayoutFloatingWindowControl otherWindow = WindowFor(otherDocument)!;
                otherWindow.Width = 160;
                otherWindow.Height = 120;
                otherWindow.Left = mainWindowTopLeft.X + 1220;
                otherWindow.Top = mainWindowTopLeft.Y + 160;
            }

            await SampleChecks.SettleAsync();
            root.UpdateLayout();
            LayoutContent? targetModel = test.Area switch
            {
                AreaKind.ToolPane => ModelById(toolTargetId),
                AreaKind.DocumentPane => ModelById(documentTargetId),
                _ => null,
            };
            FrameworkElement area = test.Area switch
            {
                AreaKind.Manager => manager,
                AreaKind.ToolPane or AreaKind.DocumentPane => PaneFor(targetModel!),
                AreaKind.EmptyDocumentGroup => root.FindVisualChildren<LayoutDocumentPaneGroupControl>()
                    .Single(group => group.Model is LayoutDocumentPaneGroup model && !model.Children.Any(child => child.IsVisible)),
                _ => throw new InvalidOperationException("未知目标区域。"),
            };
            Rect areaBounds = DockingDragChecks.ScreenBounds(area);
            SampleChecks.Require(areaBounds.Width > 80 && areaBounds.Height > 80, $"{test.Label} 的目标窗格有可见范围。 ");

            using DockingDragChecks session = new(sourceWindow);
            session.Update(new Point(areaBounds.Left + 12, areaBounds.Top + Math.Min(65, areaBounds.Height / 4)));
            await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(item => DockingDragChecks.TargetType(item) == test.Type
                && ReferenceEquals(DockingDragChecks.TargetArea(item), area)), $"{test.Label} 没有在实际覆盖层生成目标。 ");
            Rect glyph = session.GlyphBounds(test.Glyph);
            object[] targetSnapshot = session.Targets();
            object? expectedTarget = targetSnapshot.FirstOrDefault(item => DockingDragChecks.TargetType(item) == test.Type
                && ReferenceEquals(DockingDragChecks.TargetArea(item), area)
                && DockingDragChecks.Near(DockingDragChecks.TargetBounds(item), glyph));
            if (expectedTarget is null)
            {
                throw new InvalidOperationException(
                    $"{test.Label} 的指示器边界与命中边界不一致：glyph={glyph} area={areaBounds} "
                    + $"sameType={string.Join(",", targetSnapshot.Where(item => DockingDragChecks.TargetType(item) == test.Type)
                        .Select(item => $"{DockingDragChecks.TargetBounds(item)}@{DockingDragChecks.TargetArea(item).GetType().Name}"))} "
                    + $"targetCount={targetSnapshot.Length}。");
            }
            SampleChecks.Require(DockingDragChecks.Near(DockingDragChecks.TargetBounds(expectedTarget), glyph),
                $"{test.Label} 的可视指示器与命中矩形相同。 ");
            int oldFrames = session.Frames;
            Point release = DockingDragChecks.Center(glyph);
            session.Update(release);
            await session.WaitForFrameAsync(oldFrames, $"{test.Label} 的目标预览没有呈现新帧。 ");
            SampleChecks.Require(session.ActiveTarget is { } active && DockingDragChecks.TargetType(active) == test.Type
                && ReferenceEquals(DockingDragChecks.TargetArea(active), area),
                $"{test.Label} 的实际命中类型正确：expected={test.Type}@{area.GetType().Name}, "
                + $"actual={DescribeTarget(session.ActiveTarget)} glyph={glyph} area={areaBounds}。");

            Rect preview = session.PreviewBounds();
            Rect previewArea = IsAsAnchorable(test.Type) ? DocumentGroupBounds(targetModel!) : areaBounds;
            VerifyPreview(test, preview, previewArea);
            SampleChecks.Require(session.Drop(release), $"{test.Label} 在实际指示器处完成释放。 ");
            await DockingDragChecks.WaitUntilAsync(() => !sourceModel.IsFloating,
                $"{test.Label} 释放后内容仍留在浮动窗口。 ");
            if (test.Area == AreaKind.EmptyDocumentGroup)
            {
                SampleChecks.Require(sourceModel.Parent is LayoutDocumentPane pane
                    && ReferenceEquals(pane.Parent, ((ILayoutControl)area).Model),
                    "空文档分组中心释放后文档进入目标分组。 ");
                return;
            }

            LayoutContent afterTarget = ModelById(test.Area == AreaKind.ToolPane ? toolTargetId : documentTargetId);
            if (test.Side == Side.Inside)
            {
                SampleChecks.Require(ReferenceEquals(sourceModel.Parent, afterTarget.Parent),
                    $"{test.Label} 释放后两个内容共享标签窗格。 ");
            }
            else
            {
                SampleChecks.Require(!ReferenceEquals(sourceModel.Parent, afterTarget.Parent),
                    $"{test.Label} 释放后形成独立窗格。 ");
                FrameworkElement? sourcePane = null;
                FrameworkElement? targetPane = null;
                await DockingDragChecks.WaitUntilAsync(() =>
                {
                    sourcePane = TryPaneFor(sourceModel);
                    targetPane = TryPaneFor(afterTarget);
                    return sourcePane?.IsLoaded == true && targetPane?.IsLoaded == true;
                }, $"{test.Label} 释放后可视窗格没有加载。 ");
                Rect sourceBounds = DockingDragChecks.ScreenBounds(sourcePane!);
                Rect targetBounds = DockingDragChecks.ScreenBounds(targetPane!);
                VerifySide(test, sourceBounds, targetBounds);
            }
        }

        async Task VerifyDocumentSourceAreasAsync()
        {
            LayoutContent source = ModelFor(documentContent);
            source.Float();
            await DockingDragChecks.WaitUntilAsync(() => source.IsFloating && WindowFor(source) is { IsLoaded: true },
                "文档源目标限制检查未形成浮窗。 ");
            object[] areas = GetDropAreas(manager, WindowFor(source)!);
            SampleChecks.Require(areas.Any(area => area is DropArea<LayoutDocumentPaneControl>)
                && !areas.Any(area => area is DropArea<DockingManager> or DropArea<LayoutAnchorablePaneControl>),
                "文档源仅能进入文档窗格或空文档分组，不出现管理器和工具窗格目标。 ");
        }

        async Task VerifyToolCannotDockAsDocumentAsync()
        {
            LayoutAnchorable source = (LayoutAnchorable)ModelFor(toolContent);
            source.Float();
            await DockingDragChecks.WaitUntilAsync(() => source.IsFloating && WindowFor(source) is { IsLoaded: true },
                "工具停靠限制检查未形成浮窗。 ");
            source.CanDockAsTabbedDocument = false;
            object[] areas = GetDropAreas(manager, WindowFor(source)!);
            SampleChecks.Require(areas.Any(area => area is DropArea<DockingManager> or DropArea<LayoutAnchorablePaneControl>)
                && !areas.Any(area => area is DropArea<LayoutDocumentPaneControl> or DropArea<LayoutDocumentPaneGroupControl>),
                "CanDockAsTabbedDocument=false 的工具不出现文档窗格及空文档分组目标。 ");
        }

        async Task VerifyMixedOrientationAsync()
        {
            bool previousAllowMixedOrientation = manager.AllowMixedOrientation;
            manager.AllowMixedOrientation = false;
            try
            {
                LayoutContent document = ModelById(documentTargetId);
                LayoutDocumentItem item = manager.GetLayoutItemFromModel(document) as LayoutDocumentItem
                    ?? throw new InvalidOperationException("多文档分组命令尚未创建。 ");
                var split = item.NewVerticalTabGroupCommand;
                SampleChecks.Require(split?.CanExecute(null) == true, "多文档分组命令可用。 ");
                split!.Execute(null);
                await SampleChecks.SettleAsync();
                SampleChecks.Require(document.Parent is LayoutDocumentPane { Parent: LayoutDocumentPaneGroup group }
                    && group.Orientation == Orientation.Horizontal && group.Children.Count(child => child.IsVisible) > 1,
                    "多文档分组已经形成水平方向的两个可见窗格。 ");

                LayoutContent tool = ModelFor(toolContent);
                tool.Float();
                await DockingDragChecks.WaitUntilAsync(() => tool.IsFloating && WindowFor(tool) is { IsLoaded: true },
                    "多文档分组方向检查未形成工具浮窗。 ");
                LayoutFloatingWindowControl sourceWindow = WindowFor(tool)!;
                Park(sourceWindow, mainWindowTopLeft);
                await SampleChecks.SettleAsync();
                FrameworkElement area = PaneFor(document);
                Rect bounds = DockingDragChecks.ScreenBounds(area);
                using DockingDragChecks session = new(sourceWindow);
                session.Update(DockingDragChecks.Center(bounds));
                await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(target => ReferenceEquals(DockingDragChecks.TargetArea(target), area)),
                    "多文档分组未显示文档窗格目标。 ");
                DropTargetType[] types = session.Targets().Where(target => ReferenceEquals(DockingDragChecks.TargetArea(target), area))
                    .Select(DockingDragChecks.TargetType).ToArray();
                SampleChecks.Require(types.Contains(DropTargetType.DocumentPaneDockLeft)
                    && types.Contains(DropTargetType.DocumentPaneDockRight)
                    && !types.Contains(DropTargetType.DocumentPaneDockTop)
                    && !types.Contains(DropTargetType.DocumentPaneDockBottom),
                    "AllowMixedOrientation=false 时多文档分组仅显示同父组方向的内部分割目标。 ");
            }
            finally
            {
                manager.AllowMixedOrientation = previousAllowMixedOrientation;
            }
        }

        async Task VerifyFloatingHostAsync(bool toolSource)
        {
            LayoutContent source = ModelFor(toolSource ? toolContent : documentContent);
            LayoutContent target = ModelById(toolSource ? toolTargetId : documentTargetId);
            source.Float();
            await DockingDragChecks.WaitUntilAsync(() => source.IsFloating && WindowFor(source) is { IsLoaded: true },
                "浮窗相互停靠的来源窗口尚未加载。 ");
            LayoutFloatingWindowControl sourceWindow = WindowFor(source)!;
            Park(sourceWindow, mainWindowTopLeft);
            target.Float();
            await DockingDragChecks.WaitUntilAsync(() => target.IsFloating && WindowFor(target) is { IsLoaded: true },
                "浮窗相互停靠的目标窗口尚未加载。 ");
            LayoutFloatingWindowControl targetWindow = WindowFor(target)!;
            targetWindow.Width = 420;
            targetWindow.Height = 300;
            targetWindow.Left = mainWindowTopLeft.X + 300;
            targetWindow.Top = mainWindowTopLeft.Y + 160;
            await SampleChecks.SettleAsync();

            object[] areas = GetDropAreas(targetWindow, sourceWindow);
            FrameworkElement area = toolSource
                ? areas.OfType<DropArea<LayoutAnchorablePaneControl>>().Single(dropArea => ReferenceEquals(dropArea.AreaElement.Model, target.Parent)).AreaElement
                : areas.OfType<DropArea<LayoutDocumentPaneControl>>().Single(dropArea => ReferenceEquals(dropArea.AreaElement.Model, target.Parent)).AreaElement;
            Rect areaBounds = DockingDragChecks.ScreenBounds(area);
            SampleChecks.Require(areaBounds.Width > 80 && areaBounds.Height > 80, "浮窗目标窗格有可见范围。 ");
            DropTargetType expectedType = toolSource ? DropTargetType.AnchorablePaneDockInside : DropTargetType.DocumentPaneDockInside;
            string glyphName = toolSource ? "PART_AnchorablePaneDropTargetInto" : "PART_DocumentPaneDropTargetInto";
            using DockingDragChecks session = new(sourceWindow);
            session.Update(DockingDragChecks.Center(areaBounds));
            await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(item => DockingDragChecks.TargetType(item) == expectedType
                && ReferenceEquals(DockingDragChecks.TargetArea(item), area)), "浮窗宿主没有生成目标中央停靠指示器。 ");
            Rect glyph = session.GlyphBounds(glyphName);
            SampleChecks.Require(session.Targets().Any(item => DockingDragChecks.TargetType(item) == expectedType
                && ReferenceEquals(DockingDragChecks.TargetArea(item), area)
                && DockingDragChecks.Near(DockingDragChecks.TargetBounds(item), glyph)),
                "浮窗中央指示器与实际命中矩形一致。 ");
            int beforeFrames = session.Frames;
            Point release = DockingDragChecks.Center(glyph);
            session.Update(release);
            await session.WaitForFrameAsync(beforeFrames, "浮窗相互停靠的预览未绘制。 ");
            SampleChecks.Require(session.ActiveTarget is { } active && DockingDragChecks.TargetType(active) == expectedType
                && ReferenceEquals(DockingDragChecks.TargetArea(active), area), "浮窗中央实际命中目标不正确。 ");
            Rect preview = session.PreviewBounds();
            SampleChecks.Require(DockingDragChecks.Near(preview, areaBounds), "浮窗相互停靠的中央预览覆盖目标窗格。 ");

            int docked = 0;
            EventHandler<ContentDockedEventArgs> onDocked = (_, _) => docked++;
            manager.ContentDocked += onDocked;
            try
            {
                SampleChecks.Require(session.Drop(release), "浮窗中央目标接受释放。 ");
                await DockingDragChecks.WaitUntilAsync(() => source.IsFloating
                    && ReferenceEquals(source.Parent, target.Parent)
                    && ReferenceEquals(WindowFor(source), WindowFor(target)),
                    "浮窗合并后来源和目标未保持同一浮动窗格。 ");
                int hostCount = toolSource
                    ? manager.FloatingWindows.OfType<LayoutAnchorableFloatingWindowControl>().Count()
                    : manager.FloatingWindows.OfType<LayoutDocumentFloatingWindowControl>().Count();
                SampleChecks.Require(hostCount == 1 && docked == 0,
                    "两个浮窗合并为一个，且浮窗间移动不发送 ContentDocked。 ");
            }
            finally
            {
                manager.ContentDocked -= onDocked;
            }

            if (!toolSource)
            {
                await VerifyFloatingDocumentOuterTargetsAsync(targetWindow, target);
            }
        }

        async Task VerifyFloatingDocumentOuterTargetsAsync(LayoutFloatingWindowControl documentWindow, LayoutContent target)
        {
            LayoutContent tool = ModelFor(toolContent);
            tool.Float();
            await DockingDragChecks.WaitUntilAsync(() => tool.IsFloating && WindowFor(tool) is { IsLoaded: true },
                "文档浮窗外侧目标限制检查未形成工具浮窗。 ");
            LayoutFloatingWindowControl sourceWindow = WindowFor(tool)!;
            Park(sourceWindow, mainWindowTopLeft);
            await SampleChecks.SettleAsync();
            FrameworkElement area = GetDropAreas(documentWindow, sourceWindow)
                .OfType<DropArea<LayoutDocumentPaneControl>>()
                .Single(dropArea => ReferenceEquals(dropArea.AreaElement.Model, target.Parent)).AreaElement;
            using DockingDragChecks session = new(sourceWindow);
            session.Update(DockingDragChecks.Center(DockingDragChecks.ScreenBounds(area)));
            await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(item => ReferenceEquals(DockingDragChecks.TargetArea(item), area)),
                "文档浮窗没有提供工具内侧停靠目标。 ");
            SampleChecks.Require(!session.Targets().Any(item => ReferenceEquals(DockingDragChecks.TargetArea(item), area)
                && IsAsAnchorable(DockingDragChecks.TargetType(item))),
                "文档浮动宿主不显示最外四个 AsAnchorable 目标。 ");
        }

        LayoutContent ModelFor(object content) => manager.Layout.Descendents().OfType<LayoutContent>()
            .Single(item => ReferenceEquals(item.Content, content));
        LayoutContent ModelById(string id) => manager.Layout.Descendents().OfType<LayoutContent>()
            .Single(item => item.ContentId == id);
        LayoutFloatingWindowControl? WindowFor(LayoutContent model) => manager.FloatingWindows
            .FirstOrDefault(window => window.Model.Descendents().OfType<LayoutContent>()
                .Any(item => ReferenceEquals(item, model)));
        FrameworkElement PaneFor(LayoutContent model) => TryPaneFor(model)
            ?? throw new InvalidOperationException($"内容 {model.ContentId} 所在窗格尚未进入可视树。");
        FrameworkElement? TryPaneFor(LayoutContent model) => root.FindVisualChildren<FrameworkElement>()
            .FirstOrDefault(element => element is ILayoutControl control && ReferenceEquals(control.Model, model.Parent)
                && element is LayoutDocumentPaneControl or LayoutAnchorablePaneControl);
        Rect DocumentGroupBounds(LayoutContent model)
        {
            LayoutDocumentPane pane = model.Parent as LayoutDocumentPane
                ?? throw new InvalidOperationException("文档未留在文档窗格。 ");
            LayoutDocumentPaneGroup group = pane.Parent as LayoutDocumentPaneGroup
                ?? throw new InvalidOperationException("文档窗格没有外层组。 ");
            FrameworkElement visual = root.FindVisualChildren<LayoutDocumentPaneGroupControl>()
                .Single(item => ReferenceEquals(item.Model, group));
            return DockingDragChecks.ScreenBounds(visual);
        }
        static string DescribeTarget(object? target) => target is null ? "none"
            : $"{DockingDragChecks.TargetType(target)}@{DockingDragChecks.TargetArea(target).GetType().Name} "
            + $"{DockingDragChecks.TargetBounds(target)}";
    }

    private static void Park(LayoutFloatingWindowControl window, Point mainWindowTopLeft)
    {
        window.Width = 160;
        window.Height = 120;
        window.Left = mainWindowTopLeft.X;
        window.Top = mainWindowTopLeft.Y;
    }

    private static bool IsAsAnchorable(DropTargetType type) => type is
        DropTargetType.DocumentPaneDockAsAnchorableLeft or DropTargetType.DocumentPaneDockAsAnchorableTop or
        DropTargetType.DocumentPaneDockAsAnchorableRight or DropTargetType.DocumentPaneDockAsAnchorableBottom;

    private static object[] GetDropAreas(object host, LayoutFloatingWindowControl source)
    {
        Type interfaceType = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls.IOverlayWindowHost")
            ?? throw new InvalidOperationException("找不到实际停靠覆盖层宿主接口。 ");
        MethodInfo method = interfaceType.GetMethod("GetDropAreas")
            ?? throw new InvalidOperationException("找不到实际宿主停靠区域入口。 ");
        IEnumerable areas = (IEnumerable)(method.Invoke(host, [source])
            ?? throw new InvalidOperationException("实际宿主没有提供停靠区域。 "));
        return areas.Cast<object>().ToArray();
    }

    private static void VerifyPreview(Case test, Rect actual, Rect area)
    {
        double tolerance = 3;
        bool near(double left, double right) => Math.Abs(left - right) <= tolerance;
        bool bounded = actual.Width > 0 && actual.Height > 0 && actual.Left >= area.Left - tolerance
            && actual.Top >= area.Top - tolerance && actual.Right <= area.Right + tolerance
            && actual.Bottom <= area.Bottom + tolerance;
        SampleChecks.Require(bounded, $"{test.Label} 的预览留在所属宿主范围内。 ");
        double fraction = IsAsAnchorable(test.Type) ? 1.0 / 3.0
            : test.Area == AreaKind.Manager ? 0 : test.Side == Side.Inside ? 1 : 0.5;
        bool correct = test.Side switch
        {
            Side.Left => near(actual.Left, area.Left) && near(actual.Height, area.Height)
                && (fraction == 0 ? actual.Width <= area.Width / 2 + tolerance : near(actual.Width, area.Width * fraction)),
            Side.Top => near(actual.Top, area.Top) && near(actual.Width, area.Width)
                && (fraction == 0 ? actual.Height <= area.Height / 2 + tolerance : near(actual.Height, area.Height * fraction)),
            Side.Right => near(actual.Right, area.Right) && near(actual.Height, area.Height)
                && (fraction == 0 ? actual.Width <= area.Width / 2 + tolerance : near(actual.Width, area.Width * fraction)),
            Side.Bottom => near(actual.Bottom, area.Bottom) && near(actual.Width, area.Width)
                && (fraction == 0 ? actual.Height <= area.Height / 2 + tolerance : near(actual.Height, area.Height * fraction)),
            Side.Inside => DockingDragChecks.Near(actual, area),
            _ => false,
        };
        SampleChecks.Require(correct, $"{test.Label} 的实际预览方位和面积符合原版。 ");
    }

    private static void VerifySide(Case test, Rect source, Rect target)
    {
        double tolerance = 4;
        bool correct = test.Side switch
        {
            Side.Left => source.Right <= target.Left + tolerance,
            Side.Top => source.Bottom <= target.Top + tolerance,
            Side.Right => source.Left >= target.Right - tolerance,
            Side.Bottom => source.Top >= target.Bottom - tolerance,
            _ => false,
        };
        SampleChecks.Require(correct, $"{test.Label} 的实际窗格落位与选中的预览方向一致。 ");
    }
}
