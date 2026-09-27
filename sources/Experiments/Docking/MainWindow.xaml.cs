using System.Collections.ObjectModel;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.Docking;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<WorkspaceDocument> documents = [];
    private readonly ObservableCollection<WorkspaceTool> tools = [];
    private byte[]? savedLayout;
    private int nextDocumentNumber = 3;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Docking 经典工作区";
        Closed += OnClosed;

        Manager.LayoutItemContainerStyleSelector = SampleStyles.CreateItemStyleSelector();
        Manager.DocumentsSource = documents;
        Manager.AnchorablesSource = tools;
        Manager.DocumentClosing += OnDocumentClosing;
        Manager.DocumentClosed += OnDocumentClosed;
        Manager.AnchorableHiding += OnAnchorableHiding;
        Manager.ContentFloated += OnContentFloated;
        Manager.ContentDocked += OnContentDocked;

        documents.Add(new WorkspaceDocument
        {
            Id = "classic-editor",
            Title = "编辑器",
            Text = "这是一个由 DocumentsSource 导入的文档。修改文本后可以测试可取消关闭。",
            IsModified = false
        });
        documents.Add(new WorkspaceDocument
        {
            Id = "classic-readme",
            Title = "项目说明",
            Text = "拖动标签到停靠引导，或从文档菜单创建新的标签组。",
            IsModified = false
        });
        tools.Add(new WorkspaceTool
        {
            Id = "classic-solution",
            Title = "解决方案",
            Zone = DockZone.LeftTop,
            Text = "工具窗格支持隐藏、自动隐藏、浮动和独立窗口。"
        });
        tools.Add(new WorkspaceTool
        {
            Id = "classic-properties",
            Title = "属性",
            Zone = DockZone.LeftBottom,
            Text = "同一个 AnchorablesSource 可以提供多个工具模型。"
        });
        SampleChecks.RunWhenLoaded(this, RunScenarioChecksAsync);
    }

    private LayoutContent? ActiveLayoutContent =>
        Manager.Layout.Descendents().OfType<LayoutContent>()
            .FirstOrDefault(item => ReferenceEquals(item.Content, Manager.ActiveContent))
        ?? Manager.Layout.Descendents().OfType<LayoutContent>().FirstOrDefault(item => item.IsSelected);

    private void AddDocument_Click(object sender, RoutedEventArgs e)
    {
        int number = nextDocumentNumber++;
        WorkspaceDocument document = new()
        {
            Id = $"classic-document-{number}",
            Title = $"文档 {number}",
            Text = "运行时加入的文档会立即进入 DocumentsSource 和布局树。",
            IsModified = false
        };
        documents.Add(document);
        Manager.ActiveContent = document;
        StatusText.Text = $"已新增 {document.Title}。";
    }

    private void ReplaceDocument_Click(object sender, RoutedEventArgs e)
    {
        if (documents.Count == 0)
        {
            AddDocument_Click(sender, e);
            return;
        }

        WorkspaceDocument replacement = new()
        {
            Id = $"classic-document-{nextDocumentNumber++}",
            Title = "替换后的文档",
            Text = "这个文档通过 ObservableCollection.Replace 进入布局源。",
            IsModified = false
        };
        documents[0] = replacement;
        Manager.ActiveContent = replacement;
        StatusText.Text = "已替换 DocumentsSource 的第一个文档。";
    }

    private void ResetDocuments_Click(object sender, RoutedEventArgs e)
    {
        documents.Clear();
        WorkspaceDocument resetDocument = new()
        {
            Id = "classic-reset-document",
            Title = "重置后的文档",
            Text = "这个文档通过 DocumentsSource.Reset 重新导入。",
            IsModified = false
        };
        documents.Add(resetDocument);
        Manager.ActiveContent = resetDocument;
        StatusText.Text = "已重置 DocumentsSource 并重新导入文档。";
    }

    private void CloseActive_Click(object sender, RoutedEventArgs e)
    {
        LayoutContent? content = ActiveLayoutContent;
        content?.Close();
        StatusText.Text = content is null ? "当前没有活动项。" : $"已请求关闭 {content.Title}。";
    }

    private void FloatActive_Click(object sender, RoutedEventArgs e)
    {
        LayoutContent? content = ActiveLayoutContent;
        content?.Float();
        StatusText.Text = content is null ? "当前没有活动项。" : $"已请求浮动 {content.Title}。";
    }

    private void DockActive_Click(object sender, RoutedEventArgs e)
    {
        LayoutContent? content = ActiveLayoutContent;
        content?.Dock();
        StatusText.Text = content is null ? "当前没有活动项。" : $"已重新停靠 {content.Title}。";
    }

    private void AutoHideTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "当前没有工具窗格。";
            return;
        }

        tool.ToggleAutoHide();
        StatusText.Text = $"已切换 {tool.Title} 的自动隐藏状态。";
    }

    private void ShowHiddenTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = Manager.Layout.Hidden.FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "当前没有隐藏工具。";
            return;
        }

        tool.Show();
        StatusText.Text = $"已显示 {tool.Title}。";
    }

    private void DetachTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "当前没有工具窗格。";
            return;
        }

        if (Manager.IsDetached(tool))
        {
            Manager.ReattachAllDetachedAnchorables();
            StatusText.Text = $"已附回 {tool.Title}。";
        }
        else
        {
            Manager.DetachAnchorableToWindow(tool);
            StatusText.Text = $"已拆分 {tool.Title}。";
        }
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        using MemoryStream output = new();
        new XmlLayoutSerializer(Manager).Serialize(output);
        savedLayout = output.ToArray();
        StatusText.Text = "当前布局已保存到内存。";
    }

    private void RestoreLayout_Click(object sender, RoutedEventArgs e)
    {
        if (savedLayout is null)
        {
            StatusText.Text = "请先保存布局。";
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
        StatusText.Text = "布局已恢复，内容按 ContentId 重新连接。";
    }

    private void AllowCloseModified_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = AllowCloseModifiedMenuItem.IsChecked
            ? "已允许关闭修改中的文档。"
            : "关闭修改中的文档仍会被取消。";
    }

    private void ToggleWindowPolicy_Click(object sender, RoutedEventArgs e)
    {
        Manager.AllowFloatingWindows = !Manager.AllowFloatingWindows;
        Manager.AllowDetachedWindows = Manager.AllowFloatingWindows;
        StatusText.Text = Manager.AllowFloatingWindows ? "已允许浮动和独立工具窗口。" : "已禁止新的浮动和独立工具窗口。";
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        PageRoot.RequestedTheme = PageRoot.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        StatusText.Text = PageRoot.RequestedTheme == ElementTheme.Dark ? "深色主题。" : "浅色主题。";
    }

    private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        if (e.Document.Content is WorkspaceDocument document && document.IsModified && !AllowCloseModifiedMenuItem.IsChecked)
        {
            e.Cancel = true;
            StatusText.Text = $"已取消关闭修改中的 {document.Title}；在“编辑”菜单中启用允许关闭修改项后重试。";
        }
    }

    private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        if (e.Document.Content is WorkspaceDocument document)
        {
            documents.Remove(document);
        }
    }

    private void OnAnchorableHiding(object? sender, AnchorableHidingEventArgs e)
    {
        StatusText.Text = $"正在隐藏 {e.Anchorable.Title}。";
    }

    private void OnContentFloated(object? sender, ContentFloatedEventArgs e)
    {
        StatusText.Text = $"{e.Content.Title} 已浮动。";
    }

    private void OnContentDocked(object? sender, ContentDockedEventArgs e)
    {
        StatusText.Text = $"{e.Content.Title} 已停靠。";
    }

    public async Task RunScenarioChecksAsync(SampleChecks checks)
    {
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents.Count == 2, "Classic 初始文档源已创建。");
        SampleChecks.Require(tools.Count == 2, "Classic 初始工具源已创建。");
        CheckSourceContent(checks, documents, "Classic 初始文档");
        CheckSourceContent(checks, tools, "Classic 初始工具");

        AddDocument_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents.Count == 3, "DocumentsSource Add 已生效。");
        CheckSourceContent(checks, documents, "Add 后文档");

        ReplaceDocument_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents[0].Title == "替换后的文档", "DocumentsSource Replace 已生效。");
        CheckSourceContent(checks, documents, "Replace 后文档");

        ResetDocuments_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents.Count == 1 && documents[0].Id == "classic-reset-document", "DocumentsSource Reset 已生效。");
        CheckSourceContent(checks, documents, "Reset 后文档");

        WorkspaceDocument dirtyDocument = documents[0];
        dirtyDocument.Text += " 修改";
        LayoutDocument? layoutDocument = Manager.Layout.Descendents().OfType<LayoutDocument>()
            .FirstOrDefault(item => ReferenceEquals(item.Content, dirtyDocument));
        SampleChecks.Require(layoutDocument is not null, "修改文档已连接到布局项。");
        layoutDocument!.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents.Contains(dirtyDocument), "修改文档的关闭请求已取消。");
        AllowCloseModifiedMenuItem.IsChecked = true;
        layoutDocument.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!documents.Contains(dirtyDocument), "允许关闭后文档可关闭。");
        checks.Record("Classic 可取消关闭和源集合同步通过。");

        WorkspaceDocument restored = new()
        {
            Id = "classic-smoke-document",
            Title = "Smoke 文档",
            Text = "ContentId 恢复测试。",
            IsModified = false
        };
        documents.Add(restored);
        Manager.ActiveContent = restored;
        SaveLayout_Click(this, new RoutedEventArgs());
        RestoreLayout_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        CheckSourceContent(checks, documents, "恢复后文档");
        SampleChecks.Require(Manager.Layout.Descendents().OfType<LayoutContent>().Any(item => ReferenceEquals(item.Content, restored)), "恢复后 ContentId 重新连接内容。");
        checks.Record("Classic XML ContentId 恢复通过。");

        bool originalFloating = Manager.AllowFloatingWindows;
        ToggleWindowPolicy_Click(this, new RoutedEventArgs());
        SampleChecks.Require(Manager.AllowFloatingWindows != originalFloating, "Classic 浮动策略可切换。");
        ToggleWindowPolicy_Click(this, new RoutedEventArgs());
        SampleChecks.Require(Manager.AllowFloatingWindows == originalFloating, "Classic 浮动策略可恢复。");
        checks.Record("Classic 窗口策略通过。");
    }

    private void CheckSourceContent<T>(SampleChecks checks, IEnumerable<T> models, string label)
    {
        object[] expected = models.Cast<object>().ToArray();
        object[] actual = Manager.Layout.Descendents().OfType<LayoutContent>()
            .Select(item => item.Content)
            .Where(content => content is not null)
            .Cast<object>()
            .ToArray();
        foreach (object model in expected)
        {
            SampleChecks.Require(actual.Any(content => ReferenceEquals(content, model)), $"{label}内容已在布局中找到。");
        }
        checks.Record($"{label}引用同步通过（{expected.Length}）。");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.Dispose();
    }
}


