using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.Mvvm;

public sealed partial class MainWindow : Window
{
    private byte[]? savedLayout;

    public WorkspaceViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "MVVM 数据驱动工作区";
        Closed += OnClosed;
        Manager.LayoutItemContainerStyleSelector = SampleStyles.CreateItemStyleSelector();
        Manager.DocumentClosing += OnDocumentClosing;
        Manager.DocumentClosed += OnDocumentClosed;
        SampleChecks.RunWhenLoaded(this, RunScenarioChecksAsync);
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        using MemoryStream output = new();
        new XmlLayoutSerializer(Manager).Serialize(output);
        savedLayout = output.ToArray();
        ViewModel.SetStatus("MVVM 布局已保存到内存。");
    }

    private void RestoreLayout_Click(object sender, RoutedEventArgs e)
    {
        if (savedLayout is null)
        {
            ViewModel.SetStatus("请先保存布局。");
            return;
        }

        Dictionary<string, object> contents = [];
        foreach (LayoutContent item in Manager.Layout.Descendents().OfType<LayoutContent>())
        {
            if (item.ContentId is { Length: > 0 } contentId && item.Content is { } content)
            {
                contents[contentId] = content;
            }
        }

        XmlLayoutSerializer serializer = new(Manager);
        serializer.LayoutSerializationCallback += (_, args) =>
        {
            if (args.Model.ContentId is { } contentId && contents.TryGetValue(contentId, out object? content))
            {
                args.Content = content;
            }
        };
        using MemoryStream input = new(savedLayout);
        serializer.Deserialize(input);
        ViewModel.SetStatus("MVVM 布局已恢复，模型内容按 ContentId 重新连接。");
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        PageRoot.RequestedTheme = PageRoot.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        ViewModel.SetStatus(PageRoot.RequestedTheme == ElementTheme.Dark ? "深色主题。" : "浅色主题。");
    }

    private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        if (e.Document.Content is WorkspaceDocument document && document.IsModified)
        {
            e.Cancel = true;
            ViewModel.SetStatus($"已取消关闭修改中的 {document.Title}；请先保存。 ");
        }
    }

    private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        if (e.Document.Content is WorkspaceDocument document)
        {
            ViewModel.OnDocumentClosed(document);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.Dispose();
    }

    public async Task RunScenarioChecksAsync(SampleChecks checks)
    {
        SampleChecks.Require(ReferenceEquals(Manager.DockLayout, ViewModel.Layout), "DockLayout 已绑定到 ViewModel.Layout。");
        SampleChecks.Require(ViewModel.Documents.Count == 2, "初始文档模型已创建。");
        SampleChecks.Require(ViewModel.Tools.Count == 4, "初始工具模型已创建。");
        await SampleChecks.SettleAsync();
        CheckContentCount(checks, ViewModel.Documents, "初始文档");
        CheckContentCount(checks, ViewModel.Tools, "初始工具");

        WorkspaceDocument opened = ViewModel.OpenDocument("场景文档", "用于验证打开和活动项同步。");
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ReferenceEquals(ViewModel.Layout.ActiveDockable, opened), "打开文档后模型活动项已更新。");
        SampleChecks.Require(HasContent(opened), "打开文档已同步到 Manager 布局。");
        checks.Record("文档打开和活动项同步通过。");

        ViewModel.MarkModifiedCommand.Execute(null);
        SampleChecks.Require(opened.IsModified, "编辑命令可标记活动文档为已修改。");
        ViewModel.CloseActiveCommand.Execute(null);
        SampleChecks.Require(ViewModel.Documents.Contains(opened), "修改中的文档不会被命令关闭。");
        ViewModel.SaveCommand.Execute(null);
        ViewModel.CloseActiveCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!ViewModel.Documents.Contains(opened), "保存后文档可以被命令关闭。");
        checks.Record("IsModified 保存与关闭命令通过。");

        WorkspaceDocument closedFromTab = ViewModel.Documents.First();
        LayoutDocument tabDocument = Manager.Layout.Descendents().OfType<LayoutDocument>()
            .First(item => ReferenceEquals(item.Content, closedFromTab));
        Manager.GetLayoutItemFromModel(tabDocument)?.CloseCommand?.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!ViewModel.Documents.Contains(closedFromTab) && !HasContent(closedFromTab),
            "通过文档标签关闭后，MVVM 文档列表和布局树均移除同一模型。");
        ViewModel.NextCommand.Execute(null);
        SampleChecks.Require(ViewModel.ActiveDocument is { } active && ViewModel.Documents.Contains(active) && HasContent(active),
            "标签关闭后下一个文档命令仍指向布局内文档。");
        checks.Record("文档标签关闭与下一个文档模型同步通过。");

        ViewModel.NextCommand.Execute(null);
        WorkspaceDocument? beforeReplace = ViewModel.ActiveDocument;
        ViewModel.ReplaceCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(beforeReplace is null || !ViewModel.Documents.Contains(beforeReplace), "Replace 命令移除了旧文档。");
        SampleChecks.Require(ViewModel.ActiveDocument is not null && HasContent(ViewModel.ActiveDocument), "Replace 命令接入了新文档。");
        checks.Record("文档 Replace 命令通过。");

        ViewModel.ResetCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ViewModel.Documents.Count == 1, "Reset 命令保留一个初始文档。");
        CheckContentCount(checks, ViewModel.Documents, "Reset 后文档");

        ViewModel.AddToolCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ViewModel.Tools.Count == 5, "工具 Add 命令增加模型。");
        CheckContentCount(checks, ViewModel.Tools, "工具 Add");
        ViewModel.ReplaceToolCommand.Execute(null);
        await SampleChecks.SettleAsync();
        CheckContentCount(checks, ViewModel.Tools, "工具 Replace");
        ViewModel.ResetToolsCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ViewModel.Tools.Count == 1, "工具 Reset 命令保留一个工具。");
        CheckContentCount(checks, ViewModel.Tools, "工具 Reset");

        bool originalFloating = ViewModel.Layout.AllowFloatingWindows;
        ViewModel.ToggleFloatingCommand.Execute(null);
        SampleChecks.Require(ViewModel.Layout.AllowFloatingWindows != originalFloating, "窗口浮动策略由命令切换。");
        ViewModel.ToggleFloatingCommand.Execute(null);
        SampleChecks.Require(ViewModel.Layout.AllowFloatingWindows == originalFloating, "窗口浮动策略可恢复。");
        checks.Record("工具集合和窗口策略同步通过。");

        SaveLayout_Click(this, new RoutedEventArgs());
        RestoreLayout_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        CheckContentCount(checks, ViewModel.Documents, "恢复后文档");
        checks.Record("MVVM XML 布局恢复通过。");

        await WorkspacePersistenceChecks.RunAsync(Manager, ViewModel, checks);
    }

    private bool HasContent(object content)
    {
        return Manager.Layout.Descendents().OfType<LayoutContent>().Any(item => ReferenceEquals(item.Content, content));
    }

    private void CheckContentCount<T>(SampleChecks checks, IEnumerable<T> models, string label)
    {
        object[] expected = models.Cast<object>().ToArray();
        IEnumerable<LayoutContent> contents = typeof(T) == typeof(WorkspaceDocument)
            ? Manager.Layout.Descendents().OfType<LayoutDocument>()
            : Manager.Layout.Descendents().OfType<LayoutAnchorable>();
        object[] actual = contents
            .Select(item => item.Content)
            .Where(content => content is not null)
            .Cast<object>()
            .ToArray();
        SampleChecks.Require(actual.Length == expected.Length, $"{label}布局节点数量与模型集合一致，没有额外或重复项。");
        foreach (object model in expected)
        {
            SampleChecks.Require(actual.Any(content => ReferenceEquals(content, model)), $"{label}模型已在布局中找到。");
        }
        checks.Record($"{label}内容引用同步通过（{expected.Length}）。");
    }
}
