using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.Docking;

internal static class DockingTemplateChecks
{
    internal static async Task RunAsync(DockingManager manager, FrameworkElement mainRoot,
        WorkspaceDocument sourceDocument, WorkspaceTool sourceTool, WorkspaceTool secondTool,
        Action saveLayout, Action restoreLayout, SampleChecks checks)
    {
        DataTemplate documentContent = Template("SmokeDocumentContentTemplate");
        DataTemplate anchorableContent = Template("SmokeAnchorableContentTemplate");
        WorkspaceContentTemplateSelector selector = new(documentContent, anchorableContent);
        manager.DocumentHeaderTemplate = Template("SmokeDocumentHeaderTemplate");
        manager.AnchorableHeaderTemplate = Template("SmokeAnchorableHeaderTemplate");
        manager.LayoutItemTemplate = null;
        manager.LayoutItemTemplateSelector = selector;
        manager.DocumentTitleTemplate = Template("SmokeDocumentTitleTemplate");
        manager.AnchorableTitleTemplate = Template("SmokeAnchorableTitleTemplate");

        LayoutDocument document = DocumentFor(manager, sourceDocument);
        LayoutAnchorable tool = ToolFor(manager, sourceTool);
        LayoutAnchorable partner = ToolFor(manager, secondTool);
        document.IsSelected = true;
        tool.IsSelected = true;
        await WaitForMarksAsync(mainRoot, sourceDocument, sourceTool, "停靠");
        VerifyContentSelector(manager, document, tool, selector, "停靠");
        checks.Record("Classic 自定义文档/工具标签模板及内容选择器在主窗口实际可见。");

        document.Float();
        await DockingDragChecks.WaitUntilAsync(() => FloatingWindowFor(manager, document) is { IsLoaded: true },
            "Classic 模板验收文档浮窗未加载。");
        LayoutFloatingWindowControl documentWindow = FloatingWindowFor(manager, document)!;
        FrameworkElement documentWindowRoot = WindowRoot(documentWindow);
        FrameworkElement documentContentRoot = ContentRoot(documentWindow);
        await WaitForMarksAsync(() => HasMark(documentContentRoot, "SmokeDocumentHeaderMark", sourceDocument.Title)
            && HasMark(documentContentRoot, "SmokeDocumentContentMark", sourceDocument.Id)
            && HasMark(documentWindowRoot, "SmokeDocumentTitleMark", sourceDocument.Title),
            () => $"标题根：{DescribeMarks(documentWindowRoot)} 内容根：{DescribeMarks(documentContentRoot)}",
            "Classic 文档浮窗没有同时显示自定义标签、内容和标题模板。");

        LayoutAnchorableFloatingWindowControl toolWindow = manager.CreateFloatingWindow(tool, false)
            as LayoutAnchorableFloatingWindowControl
            ?? throw new InvalidOperationException("Classic 模板验收工具浮窗未创建。");
        LayoutAnchorablePane toolPane = (toolWindow.Model as LayoutAnchorableFloatingWindow)?.SinglePane
            as LayoutAnchorablePane
            ?? throw new InvalidOperationException("Classic 模板验收工具浮窗没有窗格。");
        toolPane.Children.Add(partner);
        tool.IsSelected = true;
        toolWindow.Show();
        await DockingDragChecks.WaitUntilAsync(() => toolWindow.IsLoaded,
            "Classic 模板验收双工具浮窗未加载。");
        FrameworkElement toolWindowRoot = WindowRoot(toolWindow);
        FrameworkElement toolContentRoot = ContentRoot(toolWindow);
        await WaitForMarksAsync(() => HasMark(toolContentRoot, "SmokeAnchorableHeaderMark", sourceTool.Title)
            && HasMark(toolContentRoot, "SmokeAnchorableContentMark", sourceTool.Id)
            && HasMark(toolWindowRoot, "SmokeAnchorableTitleMark", sourceTool.Title),
            () => $"标题根：{DescribeMarks(toolWindowRoot)} 内容根：{DescribeMarks(toolContentRoot)}",
            "Classic 双工具浮窗没有同时显示自定义标签、内容和标题模板。");
        checks.Record("Classic 自定义模板在文档和双工具真实浮窗的标签、内容及标题栏可见。");

        saveLayout();
        restoreLayout();
        await DockingDragChecks.WaitUntilAsync(() =>
        {
            LayoutDocument? restoredDocument = manager.Layout.Descendents().OfType<LayoutDocument>()
                .FirstOrDefault(item => ReferenceEquals(item.Content, sourceDocument));
            LayoutAnchorable? restoredTool = manager.Layout.Descendents().OfType<LayoutAnchorable>()
                .FirstOrDefault(item => ReferenceEquals(item.Content, sourceTool));
            return restoredDocument is { IsFloating: true } && restoredTool is { IsFloating: true }
                && FloatingWindowFor(manager, restoredDocument) is { IsLoaded: true }
                && FloatingWindowFor(manager, restoredTool) is { IsLoaded: true };
        }, "Classic XML 恢复后文档或工具浮窗未重新加载。");

        LayoutDocument recoveredDocument = DocumentFor(manager, sourceDocument);
        LayoutAnchorable recoveredTool = ToolFor(manager, sourceTool);
        recoveredDocument.IsSelected = true;
        recoveredTool.IsSelected = true;
        LayoutFloatingWindowControl recoveredDocumentWindow = FloatingWindowFor(manager, recoveredDocument)!;
        LayoutFloatingWindowControl recoveredToolWindow = FloatingWindowFor(manager, recoveredTool)!;
        FrameworkElement recoveredDocumentWindowRoot = WindowRoot(recoveredDocumentWindow);
        FrameworkElement recoveredDocumentContentRoot = ContentRoot(recoveredDocumentWindow);
        FrameworkElement recoveredToolWindowRoot = WindowRoot(recoveredToolWindow);
        FrameworkElement recoveredToolContentRoot = ContentRoot(recoveredToolWindow);
        await WaitForMarksAsync(() => HasMark(recoveredDocumentContentRoot, "SmokeDocumentHeaderMark", sourceDocument.Title)
            && HasMark(recoveredDocumentContentRoot, "SmokeDocumentContentMark", sourceDocument.Id)
            && HasMark(recoveredDocumentWindowRoot, "SmokeDocumentTitleMark", sourceDocument.Title)
            && HasMark(recoveredToolContentRoot, "SmokeAnchorableHeaderMark", sourceTool.Title)
            && HasMark(recoveredToolContentRoot, "SmokeAnchorableContentMark", sourceTool.Id)
            && HasMark(recoveredToolWindowRoot, "SmokeAnchorableTitleMark", sourceTool.Title),
            () => $"文档标题：{DescribeMarks(recoveredDocumentWindowRoot)} 文档内容：{DescribeMarks(recoveredDocumentContentRoot)} 工具标题：{DescribeMarks(recoveredToolWindowRoot)} 工具内容：{DescribeMarks(recoveredToolContentRoot)}",
            "Classic XML 恢复后自定义模板未在两个真实浮窗中完整显示。");
        VerifyContentSelector(manager, recoveredDocument, recoveredTool, selector, "XML 恢复后");
        checks.Record("Classic XML 恢复后文档/工具浮窗仍显示自定义标签、内容选择器及标题模板。");
    }

    private static DataTemplate Template(string key) => Application.Current.Resources[key] as DataTemplate
        ?? throw new InvalidOperationException($"Classic 模板验收资源 {key} 未找到。");

    private static LayoutDocument DocumentFor(DockingManager manager, WorkspaceDocument content)
        => manager.Layout.Descendents().OfType<LayoutDocument>()
            .Single(item => ReferenceEquals(item.Content, content));

    private static LayoutAnchorable ToolFor(DockingManager manager, WorkspaceTool content)
        => manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Single(item => ReferenceEquals(item.Content, content));

    private static LayoutFloatingWindowControl? FloatingWindowFor(DockingManager manager, LayoutContent content)
        => manager.FloatingWindows.FirstOrDefault(window => window.Model.Descendents()
            .OfType<LayoutContent>().Any(item => ReferenceEquals(item, content)));

    private static FrameworkElement WindowRoot(LayoutFloatingWindowControl window)
        => ((Window)window).Content as FrameworkElement
            ?? throw new InvalidOperationException("Classic 模板验收浮窗没有真实可视根元素。");

    private static FrameworkElement ContentRoot(LayoutFloatingWindowControl window)
        => window.Content?.GetType().GetProperty("RootVisual")?.GetValue(window.Content) as FrameworkElement
            ?? throw new InvalidOperationException("Classic 模板验收浮窗子 XAML 根尚未连接。");

    private static async Task WaitForMarksAsync(FrameworkElement root, WorkspaceDocument document,
        WorkspaceTool tool, string stage)
    {
        await DockingDragChecks.WaitUntilAsync(() => HasMark(root, "SmokeDocumentHeaderMark", document.Title)
            && HasMark(root, "SmokeDocumentContentMark", document.Id)
            && HasMark(root, "SmokeAnchorableHeaderMark", tool.Title)
            && HasMark(root, "SmokeAnchorableContentMark", tool.Id),
            $"Classic {stage}状态未显示文档/工具自定义标签与内容模板。");
    }

    private static bool HasMark(FrameworkElement root, string name, string? expectedTag)
        => root.FindVisualChildren<TextBlock>().Any(mark => mark.Name == name
            && Equals(mark.Tag, expectedTag)
            && mark.IsLoaded && mark.XamlRoot is not null
            && mark.Visibility == Visibility.Visible && mark.ActualWidth > 0 && mark.ActualHeight > 0);

    private static string DescribeMarks(FrameworkElement root)
        => string.Join("; ", root.FindVisualChildren<TextBlock>()
            .Where(mark => mark.Name.StartsWith("Smoke", StringComparison.Ordinal))
            .Select(mark => $"{mark.Name}:Tag={mark.Tag},Loaded={mark.IsLoaded},Visible={mark.Visibility},Size={mark.ActualWidth:F1}x{mark.ActualHeight:F1}"));

    private static async Task WaitForMarksAsync(Func<bool> ready, Func<string> describe, string message)
    {
        try
        {
            await DockingDragChecks.WaitUntilAsync(ready, message);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{message} {describe()}", exception);
        }
    }

    private static void VerifyContentSelector(DockingManager manager, LayoutDocument document,
        LayoutAnchorable tool, DataTemplateSelector selector, string stage)
    {
        LayoutItem documentItem = manager.GetLayoutItemFromModel(document)
            ?? throw new InvalidOperationException($"Classic {stage}文档布局项未创建。");
        LayoutItem toolItem = manager.GetLayoutItemFromModel(tool)
            ?? throw new InvalidOperationException($"Classic {stage}工具布局项未创建。");
        SampleChecks.Require(ReferenceEquals(documentItem.View.ContentTemplateSelector, selector)
            && ReferenceEquals(toolItem.View.ContentTemplateSelector, selector)
            && documentItem.View.ContentTemplate is null && toolItem.View.ContentTemplate is null,
            $"Classic {stage}内容 presenter 没有采用消费者的 DataTemplateSelector。");
    }

    private sealed class WorkspaceContentTemplateSelector(DataTemplate document, DataTemplate tool) : DataTemplateSelector
    {
        protected override DataTemplate SelectTemplateCore(object item)
            => item is WorkspaceTool ? tool : document;

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
            => SelectTemplateCore(item);
    }
}
