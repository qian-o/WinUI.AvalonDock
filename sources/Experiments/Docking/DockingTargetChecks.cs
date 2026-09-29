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
/// Checks drag sessions, overlay frames, and layout transitions for docking targets in the Classic sample's real panes.
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
        new(DropTargetType.DockingManagerDockLeft, true, AreaKind.Manager, Side.Left, "PART_DockingManagerDropTargetLeft", "Tool to main window left"),
        new(DropTargetType.DockingManagerDockTop, true, AreaKind.Manager, Side.Top, "PART_DockingManagerDropTargetTop", "Tool to main window top"),
        new(DropTargetType.DockingManagerDockRight, true, AreaKind.Manager, Side.Right, "PART_DockingManagerDropTargetRight", "Tool to main window right"),
        new(DropTargetType.DockingManagerDockBottom, true, AreaKind.Manager, Side.Bottom, "PART_DockingManagerDropTargetBottom", "Tool to main window bottom"),
        new(DropTargetType.AnchorablePaneDockLeft, true, AreaKind.ToolPane, Side.Left, "PART_AnchorablePaneDropTargetLeft", "Tool to tool pane left"),
        new(DropTargetType.AnchorablePaneDockTop, true, AreaKind.ToolPane, Side.Top, "PART_AnchorablePaneDropTargetTop", "Tool to tool pane top"),
        new(DropTargetType.AnchorablePaneDockRight, true, AreaKind.ToolPane, Side.Right, "PART_AnchorablePaneDropTargetRight", "Tool to tool pane right"),
        new(DropTargetType.AnchorablePaneDockBottom, true, AreaKind.ToolPane, Side.Bottom, "PART_AnchorablePaneDropTargetBottom", "Tool to tool pane bottom"),
        new(DropTargetType.AnchorablePaneDockInside, true, AreaKind.ToolPane, Side.Inside, "PART_AnchorablePaneDropTargetInto", "Tool to tool pane tab"),
        new(DropTargetType.DocumentPaneDockLeft, false, AreaKind.DocumentPane, Side.Left, "PART_DocumentPaneDropTargetLeft", "Document to document pane left"),
        new(DropTargetType.DocumentPaneDockTop, false, AreaKind.DocumentPane, Side.Top, "PART_DocumentPaneDropTargetTop", "Document to document pane top"),
        new(DropTargetType.DocumentPaneDockRight, false, AreaKind.DocumentPane, Side.Right, "PART_DocumentPaneDropTargetRight", "Document to document pane right"),
        new(DropTargetType.DocumentPaneDockBottom, false, AreaKind.DocumentPane, Side.Bottom, "PART_DocumentPaneDropTargetBottom", "Document to document pane bottom"),
        new(DropTargetType.DocumentPaneDockInside, false, AreaKind.DocumentPane, Side.Inside, "PART_DocumentPaneDropTargetInto", "Document to document pane tab"),
        new(DropTargetType.DocumentPaneGroupDockInside, false, AreaKind.EmptyDocumentGroup, Side.Inside,
            "PART_DocumentPaneDropTargetInto", "Document to empty document group"),
        new(DropTargetType.DocumentPaneDockLeft, true, AreaKind.DocumentPane, Side.Left, "PART_DocumentPaneFullDropTargetLeft", "Tool to document pane inner left"),
        new(DropTargetType.DocumentPaneDockTop, true, AreaKind.DocumentPane, Side.Top, "PART_DocumentPaneFullDropTargetTop", "Tool to document pane inner top"),
        new(DropTargetType.DocumentPaneDockRight, true, AreaKind.DocumentPane, Side.Right, "PART_DocumentPaneFullDropTargetRight", "Tool to document pane inner right"),
        new(DropTargetType.DocumentPaneDockBottom, true, AreaKind.DocumentPane, Side.Bottom, "PART_DocumentPaneFullDropTargetBottom", "Tool to document pane inner bottom"),
        new(DropTargetType.DocumentPaneDockInside, true, AreaKind.DocumentPane, Side.Inside, "PART_DocumentPaneFullDropTargetInto", "Tool to document pane tab"),
        new(DropTargetType.DocumentPaneDockAsAnchorableLeft, true, AreaKind.DocumentPane, Side.Left,
            "PART_DocumentPaneDropTargetLeftAsAnchorablePane", "Tool to document group outer left"),
        new(DropTargetType.DocumentPaneDockAsAnchorableTop, true, AreaKind.DocumentPane, Side.Top,
            "PART_DocumentPaneDropTargetTopAsAnchorablePane", "Tool to document group outer top"),
        new(DropTargetType.DocumentPaneDockAsAnchorableRight, true, AreaKind.DocumentPane, Side.Right,
            "PART_DocumentPaneDropTargetRightAsAnchorablePane", "Tool to document group outer right"),
        new(DropTargetType.DocumentPaneDockAsAnchorableBottom, true, AreaKind.DocumentPane, Side.Bottom,
            "PART_DocumentPaneDropTargetBottomAsAnchorablePane", "Tool to document group outer bottom"),
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
        SampleChecks.Require(contentById.Count >= 4, "The target checks have four restorable content items.");
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
                checks.Record($"Classic {test.Label}: indicator, hit target, preview frame, and post-drop layout passed.");
            }
            catch (Exception exception)
            {
                failures.Add($"{test.Label} ({test.Type}): {exception.GetBaseException()}");
            }
        }

        await CheckAdditionalAsync("document source target restrictions", VerifyDocumentSourceAreasAsync);
        await CheckAdditionalAsync("tool cannot dock into document area", VerifyToolCannotDockAsDocumentAsync);
        await CheckAdditionalAsync("document group orientation restrictions", VerifyMixedOrientationAsync);
        await CheckAdditionalAsync("document floating window docking and outer target restrictions", () => VerifyFloatingHostAsync(false));
        await CheckAdditionalAsync("tool floating window docking", () => VerifyFloatingHostAsync(true));

        await RestoreAsync();
        string? groupFailure = failures.FirstOrDefault(failure => failure.Contains(nameof(DropTargetType.DocumentPaneGroupDockInside)));
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "AvalonDock-target-untested.txt"),
            groupFailure is null
                ? "Pass: DocumentPaneGroupDockInside verified the empty group center target, preview, and drop; no Untested result for this case." + Environment.NewLine
                : "Fail: DocumentPaneGroupDockInside automated check failed: " + groupFailure + Environment.NewLine);
        SampleChecks.Require(failures.Count == 0, "Classic target checks failed: " + string.Join("; ", failures));

        async Task CheckAdditionalAsync(string label, Func<Task> verify)
        {
            try
            {
                await RestoreAsync();
                await verify();
                checks.Record($"Classic {label}: host, target, and layout checks passed.");
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
                "The baseline without floating items was restored between target scenarios.");
        }

        async Task VerifyAsync(Case test)
        {
            object sourceContent = test.ToolSource ? toolContent : documentContent;
            LayoutContent sourceModel = ModelFor(sourceContent);
            sourceModel.Float();
            await DockingDragChecks.WaitUntilAsync(() => sourceModel.IsFloating && WindowFor(sourceModel) is { IsLoaded: true },
                $"{test.Label}: the source did not create a floating window.");
            LayoutFloatingWindowControl sourceWindow = WindowFor(sourceModel)!;
            Park(sourceWindow, mainWindowTopLeft);

            if (test.Area == AreaKind.EmptyDocumentGroup)
            {
                LayoutContent otherDocument = ModelById(documentTargetId);
                otherDocument.Float();
                await DockingDragChecks.WaitUntilAsync(() => otherDocument.IsFloating && WindowFor(otherDocument) is { IsLoaded: true },
                    "The other document did not float in the empty document group scenario.");
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
                _ => throw new InvalidOperationException("Unknown target area."),
            };
            Rect areaBounds = DockingDragChecks.ScreenBounds(area);
            SampleChecks.Require(areaBounds.Width > 80 && areaBounds.Height > 80, $"{test.Label}: the target pane has visible bounds.");

            using DockingDragChecks session = new(sourceWindow);
            session.Update(new Point(areaBounds.Left + 12, areaBounds.Top + Math.Min(65, areaBounds.Height / 4)));
            await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(item => DockingDragChecks.TargetType(item) == test.Type
                && ReferenceEquals(DockingDragChecks.TargetArea(item), area)), $"{test.Label}: no target was generated in the real overlay.");
            Rect glyph = session.GlyphBounds(test.Glyph);
            object[] targetSnapshot = session.Targets();
            object? expectedTarget = targetSnapshot.FirstOrDefault(item => DockingDragChecks.TargetType(item) == test.Type
                && ReferenceEquals(DockingDragChecks.TargetArea(item), area)
                && DockingDragChecks.Near(DockingDragChecks.TargetBounds(item), glyph));
            if (expectedTarget is null)
            {
                throw new InvalidOperationException(
                    $"{test.Label}: indicator and hit bounds differ: glyph={glyph} area={areaBounds} "
                    + $"sameType={string.Join(",", targetSnapshot.Where(item => DockingDragChecks.TargetType(item) == test.Type)
                        .Select(item => $"{DockingDragChecks.TargetBounds(item)}@{DockingDragChecks.TargetArea(item).GetType().Name}"))} "
                    + $"targetCount={targetSnapshot.Length}.");
            }
            SampleChecks.Require(DockingDragChecks.Near(DockingDragChecks.TargetBounds(expectedTarget), glyph),
                $"{test.Label}: the visible indicator matches the hit rectangle.");
            int oldFrames = session.Frames;
            Point release = DockingDragChecks.Center(glyph);
            session.Update(release);
            await session.WaitForFrameAsync(oldFrames, $"{test.Label}: the target preview did not present a new frame.");
            SampleChecks.Require(session.ActiveTarget is { } active && DockingDragChecks.TargetType(active) == test.Type
                && ReferenceEquals(DockingDragChecks.TargetArea(active), area),
                $"{test.Label}: the hit target has the expected type: expected={test.Type}@{area.GetType().Name}, "
                + $"actual={DescribeTarget(session.ActiveTarget)} glyph={glyph} area={areaBounds}.");

            Rect preview = session.PreviewBounds();
            Rect previewArea = IsAsAnchorable(test.Type) ? DocumentGroupBounds(targetModel!) : areaBounds;
            VerifyPreview(test, preview, previewArea);
            SampleChecks.Require(session.Drop(release), $"{test.Label}: drop completed on the indicator.");
            await DockingDragChecks.WaitUntilAsync(() => !sourceModel.IsFloating,
                $"{test.Label}: content remained in a floating window after the drop.");
            if (test.Area == AreaKind.EmptyDocumentGroup)
            {
                SampleChecks.Require(sourceModel.Parent is LayoutDocumentPane pane
                    && ReferenceEquals(pane.Parent, ((ILayoutControl)area).Model),
                    "The document entered the target group after dropping on the empty group center.");
                return;
            }

            LayoutContent afterTarget = ModelById(test.Area == AreaKind.ToolPane ? toolTargetId : documentTargetId);
            if (test.Side == Side.Inside)
            {
                SampleChecks.Require(ReferenceEquals(sourceModel.Parent, afterTarget.Parent),
                    $"{test.Label}: both content items share a tab pane after the drop.");
            }
            else
            {
                SampleChecks.Require(!ReferenceEquals(sourceModel.Parent, afterTarget.Parent),
                    $"{test.Label}: the drop created a separate pane.");
                FrameworkElement? sourcePane = null;
                FrameworkElement? targetPane = null;
                await DockingDragChecks.WaitUntilAsync(() =>
                {
                    sourcePane = TryPaneFor(sourceModel);
                    targetPane = TryPaneFor(afterTarget);
                    return sourcePane?.IsLoaded == true && targetPane?.IsLoaded == true;
                }, $"{test.Label}: the visible pane did not load after the drop.");
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
                "The document source restriction check did not create a floating window.");
            object[] areas = GetDropAreas(manager, WindowFor(source)!);
            SampleChecks.Require(areas.Any(area => area is DropArea<LayoutDocumentPaneControl>)
                && !areas.Any(area => area is DropArea<DockingManager> or DropArea<LayoutAnchorablePaneControl>),
                "A document source has document pane or empty document group targets only, without manager or tool pane targets.");
        }

        async Task VerifyToolCannotDockAsDocumentAsync()
        {
            LayoutAnchorable source = (LayoutAnchorable)ModelFor(toolContent);
            source.Float();
            await DockingDragChecks.WaitUntilAsync(() => source.IsFloating && WindowFor(source) is { IsLoaded: true },
                "The tool docking restriction check did not create a floating window.");
            source.CanDockAsTabbedDocument = false;
            object[] areas = GetDropAreas(manager, WindowFor(source)!);
            SampleChecks.Require(areas.Any(area => area is DropArea<DockingManager> or DropArea<LayoutAnchorablePaneControl>)
                && !areas.Any(area => area is DropArea<LayoutDocumentPaneControl> or DropArea<LayoutDocumentPaneGroupControl>),
                "A tool with CanDockAsTabbedDocument=false has no document pane or empty document group targets.");
        }

        async Task VerifyMixedOrientationAsync()
        {
            bool previousAllowMixedOrientation = manager.AllowMixedOrientation;
            manager.AllowMixedOrientation = false;
            try
            {
                LayoutContent document = ModelById(documentTargetId);
                LayoutDocumentItem item = manager.GetLayoutItemFromModel(document) as LayoutDocumentItem
                    ?? throw new InvalidOperationException("The multiple document group command was not created.");
                var split = item.NewVerticalTabGroupCommand;
                SampleChecks.Require(split?.CanExecute(null) == true, "The multiple document group command can execute.");
                split!.Execute(null);
                await SampleChecks.SettleAsync();
                SampleChecks.Require(document.Parent is LayoutDocumentPane { Parent: LayoutDocumentPaneGroup group }
                    && group.Orientation == Orientation.Horizontal && group.Children.Count(child => child.IsVisible) > 1,
                    "The multiple document groups form two visible panes in a horizontal orientation.");

                LayoutContent tool = ModelFor(toolContent);
                tool.Float();
                await DockingDragChecks.WaitUntilAsync(() => tool.IsFloating && WindowFor(tool) is { IsLoaded: true },
                    "The multiple document group orientation check did not create a tool floating window.");
                LayoutFloatingWindowControl sourceWindow = WindowFor(tool)!;
                Park(sourceWindow, mainWindowTopLeft);
                await SampleChecks.SettleAsync();
                FrameworkElement area = PaneFor(document);
                Rect bounds = DockingDragChecks.ScreenBounds(area);
                using DockingDragChecks session = new(sourceWindow);
                session.Update(DockingDragChecks.Center(bounds));
                await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(target => ReferenceEquals(DockingDragChecks.TargetArea(target), area)),
                    "The multiple document groups did not show document pane targets.");
                DropTargetType[] types = session.Targets().Where(target => ReferenceEquals(DockingDragChecks.TargetArea(target), area))
                    .Select(DockingDragChecks.TargetType).ToArray();
                SampleChecks.Require(types.Contains(DropTargetType.DocumentPaneDockLeft)
                    && types.Contains(DropTargetType.DocumentPaneDockRight)
                    && !types.Contains(DropTargetType.DocumentPaneDockTop)
                    && !types.Contains(DropTargetType.DocumentPaneDockBottom),
                    "With AllowMixedOrientation=false, multiple document groups show only inner split targets matching the parent group orientation.");
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
                "The source floating window for floating window docking did not load.");
            LayoutFloatingWindowControl sourceWindow = WindowFor(source)!;
            Park(sourceWindow, mainWindowTopLeft);
            target.Float();
            await DockingDragChecks.WaitUntilAsync(() => target.IsFloating && WindowFor(target) is { IsLoaded: true },
                "The target floating window for floating window docking did not load.");
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
            SampleChecks.Require(areaBounds.Width > 80 && areaBounds.Height > 80, "The floating target pane has visible bounds.");
            DropTargetType expectedType = toolSource ? DropTargetType.AnchorablePaneDockInside : DropTargetType.DocumentPaneDockInside;
            string glyphName = toolSource ? "PART_AnchorablePaneDropTargetInto" : "PART_DocumentPaneDropTargetInto";
            using DockingDragChecks session = new(sourceWindow);
            session.Update(DockingDragChecks.Center(areaBounds));
            await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(item => DockingDragChecks.TargetType(item) == expectedType
                && ReferenceEquals(DockingDragChecks.TargetArea(item), area)), "The floating host did not generate a central docking indicator for the target.");
            Rect glyph = session.GlyphBounds(glyphName);
            SampleChecks.Require(session.Targets().Any(item => DockingDragChecks.TargetType(item) == expectedType
                && ReferenceEquals(DockingDragChecks.TargetArea(item), area)
                && DockingDragChecks.Near(DockingDragChecks.TargetBounds(item), glyph)),
                "The central floating window indicator matches the hit rectangle.");
            int beforeFrames = session.Frames;
            Point release = DockingDragChecks.Center(glyph);
            session.Update(release);
            await session.WaitForFrameAsync(beforeFrames, "The preview for docking floating windows was not drawn.");
            SampleChecks.Require(session.ActiveTarget is { } active && DockingDragChecks.TargetType(active) == expectedType
                && ReferenceEquals(DockingDragChecks.TargetArea(active), area), "The central hit target in the floating window is incorrect.");
            Rect preview = session.PreviewBounds();
            SampleChecks.Require(DockingDragChecks.Near(preview, areaBounds), "The central preview for floating window docking covers the target pane.");

            int docked = 0;
            EventHandler<ContentDockedEventArgs> onDocked = (_, _) => docked++;
            manager.ContentDocked += onDocked;
            try
            {
                SampleChecks.Require(session.Drop(release), "The central floating window target accepts the drop.");
                await DockingDragChecks.WaitUntilAsync(() => source.IsFloating
                    && ReferenceEquals(source.Parent, target.Parent)
                    && ReferenceEquals(WindowFor(source), WindowFor(target)),
                    "The source and target did not remain in the same floating pane after merging floating windows.");
                int hostCount = toolSource
                    ? manager.FloatingWindows.OfType<LayoutAnchorableFloatingWindowControl>().Count()
                    : manager.FloatingWindows.OfType<LayoutDocumentFloatingWindowControl>().Count();
                SampleChecks.Require(hostCount == 1 && docked == 0,
                    "Two floating windows merge into one, and moving between floating windows does not raise ContentDocked.");
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
                "The document floating window outer target restriction check did not create a tool floating window.");
            LayoutFloatingWindowControl sourceWindow = WindowFor(tool)!;
            Park(sourceWindow, mainWindowTopLeft);
            await SampleChecks.SettleAsync();
            FrameworkElement area = GetDropAreas(documentWindow, sourceWindow)
                .OfType<DropArea<LayoutDocumentPaneControl>>()
                .Single(dropArea => ReferenceEquals(dropArea.AreaElement.Model, target.Parent)).AreaElement;
            using DockingDragChecks session = new(sourceWindow);
            session.Update(DockingDragChecks.Center(DockingDragChecks.ScreenBounds(area)));
            await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(item => ReferenceEquals(DockingDragChecks.TargetArea(item), area)),
                "The document floating window did not provide an inner docking target for the tool.");
            SampleChecks.Require(!session.Targets().Any(item => ReferenceEquals(DockingDragChecks.TargetArea(item), area)
                && IsAsAnchorable(DockingDragChecks.TargetType(item))),
                "The document floating host does not show the four outer AsAnchorable targets.");
        }

        LayoutContent ModelFor(object content) => manager.Layout.Descendents().OfType<LayoutContent>()
            .Single(item => ReferenceEquals(item.Content, content));
        LayoutContent ModelById(string id) => manager.Layout.Descendents().OfType<LayoutContent>()
            .Single(item => item.ContentId == id);
        LayoutFloatingWindowControl? WindowFor(LayoutContent model) => manager.FloatingWindows
            .FirstOrDefault(window => window.Model.Descendents().OfType<LayoutContent>()
                .Any(item => ReferenceEquals(item, model)));
        FrameworkElement PaneFor(LayoutContent model) => TryPaneFor(model)
            ?? throw new InvalidOperationException($"The pane containing {model.ContentId} has not entered the visual tree.");
        FrameworkElement? TryPaneFor(LayoutContent model) => root.FindVisualChildren<FrameworkElement>()
            .FirstOrDefault(element => element is ILayoutControl control && ReferenceEquals(control.Model, model.Parent)
                && element is LayoutDocumentPaneControl or LayoutAnchorablePaneControl);
        Rect DocumentGroupBounds(LayoutContent model)
        {
            LayoutDocumentPane pane = model.Parent as LayoutDocumentPane
                ?? throw new InvalidOperationException("The document did not remain in a document pane.");
            LayoutDocumentPaneGroup group = pane.Parent as LayoutDocumentPaneGroup
                ?? throw new InvalidOperationException("The document pane has no outer group.");
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
            ?? throw new InvalidOperationException("The docking overlay host interface was not found.");
        MethodInfo method = interfaceType.GetMethod("GetDropAreas")
            ?? throw new InvalidOperationException("The host docking area entry point was not found.");
        IEnumerable areas = (IEnumerable)(method.Invoke(host, [source])
            ?? throw new InvalidOperationException("The host did not provide any docking areas."));
        return areas.Cast<object>().ToArray();
    }

    private static void VerifyPreview(Case test, Rect actual, Rect area)
    {
        double tolerance = 3;
        bool near(double left, double right) => Math.Abs(left - right) <= tolerance;
        bool bounded = actual.Width > 0 && actual.Height > 0 && actual.Left >= area.Left - tolerance
            && actual.Top >= area.Top - tolerance && actual.Right <= area.Right + tolerance
            && actual.Bottom <= area.Bottom + tolerance;
        SampleChecks.Require(bounded, $"{test.Label}: the preview stays within its host bounds.");
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
        SampleChecks.Require(correct, $"{test.Label}: the preview direction and area match the original behavior.");
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
        SampleChecks.Require(correct, $"{test.Label}: the resulting pane position matches the selected preview direction.");
    }
}
