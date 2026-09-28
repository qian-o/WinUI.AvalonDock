using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WinUI.AvalonDock.Experiments.Shared;
using Windows.Foundation;

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
            Text = "这是一个由 DocumentsSource 导入的文档。可在编辑菜单标记为已修改，再测试可取消关闭。",
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
        Manager.Loaded += InitializeSampleAutoHideSizes;
        SampleChecks.RunWhenLoaded(this, RunScenarioChecksAsync);
    }

    private void InitializeSampleAutoHideSizes(object sender, RoutedEventArgs e)
    {
        Manager.Loaded -= InitializeSampleAutoHideSizes;
        foreach (LayoutAnchorable tool in Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Where(item => item.Content is WorkspaceTool))
        {
            tool.AutoHideWidth = 280;
            tool.AutoHideHeight = 180;
        }
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

    private void MarkActiveDocumentModified_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveLayoutContent?.Content is not WorkspaceDocument document)
        {
            StatusText.Text = "当前没有活动文档。";
            return;
        }

        document.IsModified = true;
        StatusText.Text = $"已标记 {document.Title} 为已修改。";
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

    private void ToggleToolFloating_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable[] availableTools = Manager.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
        LayoutAnchorable? tool = availableTools.FirstOrDefault(item => item.IsActive)
            ?? availableTools.FirstOrDefault(item => item.IsFloating && item.IsSelected)
            ?? availableTools.FirstOrDefault(item => item.IsSelected)
            ?? availableTools.FirstOrDefault(item => item.IsFloating)
            ?? availableTools.FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "当前没有工具窗格。";
            return;
        }

        if (tool.IsFloating)
        {
            tool.Dock();
            StatusText.Text = $"已停靠 {tool.Title}。";
        }
        else
        {
            tool.Float();
            StatusText.Text = $"已浮动 {tool.Title}。";
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

        TabViewItem[] initialToolTabs = PageRoot.FindVisualChildren<LayoutAnchorablePaneControl>()
            .SelectMany(pane => pane.TabItems.OfType<TabViewItem>())
            .ToArray();
        SampleChecks.Require(initialToolTabs.Length == tools.Count, "Classic 首次加载已创建全部工具标签。");
        SampleChecks.Require(initialToolTabs.All(tab => tab.Header is LayoutAnchorableTabItem { ActualWidth: > 0 } header
            && header.FindVisualChildren<TextBlock>().Any(text => text.Visibility == Visibility.Visible && text.ActualWidth > 0)),
            "Classic 首次加载的工具标签标题已完成测量并可见。");
        SampleChecks.Require(Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Where(tool => tool.Content is WorkspaceTool)
            .All(tool => tool.AutoHideWidth == 280 && tool.AutoHideHeight == 180),
            "Classic 首次加载为来源工具设置适合示例内容的自动隐藏弹窗尺寸。");
        checks.Record("Classic 首次加载工具标签呈现通过。");

        LayoutAnchorable styleTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().First();
        LayoutAnchorableItem styleItem = Manager.GetLayoutItemFromModel(styleTool) as LayoutAnchorableItem
            ?? throw new InvalidOperationException("Classic 工具布局项未创建。");
        SampleChecks.Require(!styleTool.CanClose, "Classic 工具默认不可关闭。");
        Style closeStyle = new(typeof(LayoutItem));
        closeStyle.Setters.Add(new Setter(LayoutItem.CanCloseProperty, true));
        Manager.LayoutItemContainerStyle = closeStyle;
        SampleChecks.Require(styleItem.CanClose && styleTool.CanClose,
            "Classic 布局项样式的 CanClose 设置优先于模型初始值。");
        styleItem.CanClose = false;
        Style nextCloseStyle = new(typeof(LayoutItem));
        nextCloseStyle.Setters.Add(new Setter(LayoutItem.CanCloseProperty, true));
        Manager.LayoutItemContainerStyle = nextCloseStyle;
        SampleChecks.Require(!styleItem.CanClose && !styleTool.CanClose,
            "Classic 消费者显式设置的本地值优先于后续样式。");
        styleItem.ClearValue(LayoutItem.CanCloseProperty);
        SampleChecks.Require(styleItem.CanClose && styleTool.CanClose,
            "Classic 清除本地值后重新采用样式的关闭策略。");
        Manager.LayoutItemContainerStyle = null;
        styleItem.CanClose = false;
        SampleChecks.Require(!styleItem.CanClose && !styleTool.CanClose, "Classic 移除样式后工具可恢复关闭策略。");
        checks.Record("Classic 布局项样式可覆盖初始模型值并恢复。");

        await CheckDocumentTabLayoutAsync(checks);
        await CheckDocumentChromeAsync(checks);
        await CheckDocumentDropAreaAfterFloatAsync(checks);
        await CheckContentOperationEventsAsync(checks);
        await DockingTargetChecks.RunAsync(Manager, PageRoot, documents[1], tools[1], checks,
            new Point(AppWindow.Position.X, AppWindow.Position.Y));

        LayoutAnchorable floatingTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().First();
        floatingTool.IsActive = true;
        ToggleToolFloating_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(floatingTool.IsFloating && !Manager.IsDetached(floatingTool), "工具菜单使用标准浮动布局，而不是独立窗口模式。");
        SampleChecks.Require(Manager.FloatingWindows.OfType<LayoutAnchorableFloatingWindowControl>()
            .Any(window => window.Model.Descendents().OfType<LayoutAnchorable>().Any(tool => ReferenceEquals(tool, floatingTool))),
            "工具菜单创建了可参与停靠拖放的标准浮动窗口。");
        ToggleToolFloating_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!floatingTool.IsFloating && !Manager.IsDetached(floatingTool), "工具菜单可将标准浮动工具停靠回原布局。");
        checks.Record("Classic 工具菜单标准浮动和停靠通过。");

        LayoutAnchorable[] pairedTools = Manager.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
        SampleChecks.Require(pairedTools.Length == 2 && ReferenceEquals(pairedTools[0].Parent, pairedTools[1].Parent),
            "Classic 两个初始工具共享一个窗格。");
        LayoutAnchorablePane homePane = (LayoutAnchorablePane)pairedTools[0].Parent!;
        LayoutAnchorableFloatingWindowControl pairedFloating = Manager.CreateFloatingWindow(pairedTools[0], false)
            as LayoutAnchorableFloatingWindowControl
            ?? throw new InvalidOperationException("Classic 工具浮动窗口未创建。");
        LayoutAnchorablePane floatingPane = (pairedFloating.Model as LayoutAnchorableFloatingWindow)?.SinglePane as LayoutAnchorablePane
            ?? throw new InvalidOperationException("Classic 工具浮动窗口没有窗格。");
        floatingPane.Children.Add(pairedTools[1]);
        pairedFloating.Show();
        SampleChecks.Require((pairedFloating.Model as LayoutAnchorableFloatingWindow)?.SinglePane is LayoutAnchorablePane { ChildrenCount: 2 },
            "Classic 两个工具共处同一浮动窗格。");
        await SampleChecks.SettleAsync();
        FrameworkElement? floatingRoot = pairedFloating.Content?.GetType().GetProperty("Content")?
            .GetValue(pairedFloating.Content) as FrameworkElement;
        LayoutAnchorablePaneControl? floatingTabs = floatingRoot
            .FindVisualChildren<LayoutAnchorablePaneControl>().FirstOrDefault();
        FrameworkElement? paneFill = floatingTabs?.FindVisualChildren<FrameworkElement>()
            .FirstOrDefault(surface => surface.Name == "PaneFill");
        FrameworkElement? paneOutline = floatingTabs?.FindVisualChildren<FrameworkElement>()
            .FirstOrDefault(surface => surface.Name == "PaneBorder");
        FrameworkElement? tabStrip = floatingTabs?.FindVisualChildren<FrameworkElement>()
            .FirstOrDefault(element => element.Name == "TabContainerGrid");
        global::Microsoft.UI.Xaml.Shapes.Path? fillPath = paneFill?.FindVisualChildren<global::Microsoft.UI.Xaml.Shapes.Path>().FirstOrDefault();
        global::Microsoft.UI.Xaml.Shapes.Path? outlinePath = paneOutline?.FindVisualChildren<global::Microsoft.UI.Xaml.Shapes.Path>().FirstOrDefault();
        SampleChecks.Require(floatingTabs is not null && floatingTabs.TabItems.Count == 2
            && floatingTabs.Model is LayoutAnchorablePane { IsDirectlyHostedInFloatingWindow: true, ChildrenCount: 2 }
            && tabStrip?.Visibility == Visibility.Visible && paneFill is not null && paneOutline is not null
            && paneFill is Grid { CornerRadius: { TopLeft: > 0 } }
            && paneOutline is Grid { CornerRadius: { TopLeft: > 0 } }
            && fillPath?.Fill is not null && fillPath.Stroke is null && outlinePath?.Fill is null
            && outlinePath?.Stroke is not null
            && Canvas.GetZIndex(paneOutline) > Canvas.GetZIndex(paneFill),
            "Classic 双工具浮动窗格保留两标签、圆角描边和上层轮廓。");
        pairedTools[0].IsActive = true;
        await SampleChecks.SettleAsync();
        SampleChecks.Require(outlinePath!.Stroke is SolidColorBrush activeStroke && activeStroke.Color.A > 0
            && floatingTabs!.BorderBrush is SolidColorBrush inactiveStroke && activeStroke.Color != inactiveStroke.Color,
            "Classic 双工具浮动窗格的活动描边可见。");
        checks.Record("Classic 双工具共用浮动窗格外观通过。");
        homePane.Children.Add(pairedTools[1]);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(floatingPane.ChildrenCount == 1 && floatingTabs!.TabItems.Count == 1
            && tabStrip!.Visibility == Visibility.Collapsed
            && paneOutline is Grid { CornerRadius: { TopLeft: 0 } }
            && paneFill is Grid { CornerRadius: { TopLeft: 0 } }
            && outlinePath.Stroke is SolidColorBrush singleStroke && singleStroke.Color.A == 0,
            "Classic 双工具浮动窗格缩回单工具时隐藏标签与轮廓。");
        pairedTools[0].Dock();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(pairedTools.All(tool => !tool.IsFloating) && Manager.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 2,
            "Classic 双工具浮动后可停靠回主布局。");

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
        Manager.ActiveContent = dirtyDocument;
        MarkActiveDocumentModified_Click(this, new RoutedEventArgs());
        SampleChecks.Require(dirtyDocument.IsModified, "编辑菜单可标记活动文档为已修改。");
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

        await CheckRightAutoHidePopupAsync(checks);
        await DockingTemplateChecks.RunAsync(Manager, PageRoot, documents.Single(), tools[0], tools[1],
            () => SaveLayout_Click(this, new RoutedEventArgs()),
            () => RestoreLayout_Click(this, new RoutedEventArgs()), checks);
    }

    private async Task CheckRightAutoHidePopupAsync(SampleChecks checks)
    {
        LayoutAnchorable tool = Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Single(item => item.Content is WorkspaceTool { Id: "classic-properties" });
        LayoutAnchorablePane rightPane = new()
        {
            DockWidth = new GridLength(280)
        };
        rightPane.Children.Add(tool);
        Manager.LayoutEngine.InsertPane(Manager.Layout, rightPane, AnchorSide.Right);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ReferenceEquals(tool.Parent, rightPane) && rightPane.GetSide() == AnchorSide.Right,
            "Classic 属性工具已进入真实右侧窗格。");

        tool.ToggleAutoHide();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(tool.IsAutoHidden && tool.FindParent<LayoutAnchorSide>()?.Side == AnchorSide.Right,
            "Classic 右侧工具切换到自动隐藏侧栏。");
        LayoutAnchorControl anchor = PageRoot.FindVisualChildren<LayoutAnchorControl>()
            .Single(item => ReferenceEquals(item.Model, tool));
        SampleChecks.Require(anchor.IsLoaded, "Classic 右侧自动隐藏标签已加载到真实主窗口。");
        tool.IsSelected = false;
        tool.IsSelected = true;
        await SampleChecks.SettleAsync();
        LayoutAutoHideWindowControl popup = Manager.AutoHideWindow
            ?? throw new InvalidOperationException("Classic 自动隐藏弹窗未创建。");
        SampleChecks.Require(ReferenceEquals(popup.Model, tool) && popup.Visibility == Visibility.Visible
            && Math.Abs(popup.ActualWidth - (tool.AutoHideWidth + Manager.GridSplitterWidth)) < 3,
            $"Classic 右侧自动隐藏真实弹窗使用 280 像素内容宽度，实际宽度 {popup.ActualWidth}。");

        LayoutAnchorableItem item = Manager.GetLayoutItemFromModel(tool) as LayoutAnchorableItem
            ?? throw new InvalidOperationException("Classic 属性工具布局项未创建。");
        SampleChecks.Require(item.AutoHideCommand?.CanExecute(null) == true,
            "Classic 右侧自动隐藏工具的固定命令可用。");
        item.AutoHideCommand!.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!tool.IsAutoHidden && ReferenceEquals(tool.Parent, rightPane)
            && rightPane.GetSide() == AnchorSide.Right,
            "Classic 属性工具固定后返回原右侧窗格。");
        checks.Record("Classic 来源工具自动隐藏宽度、右侧真实弹窗尺寸与固定返回通过。");
    }

    private async Task CheckContentOperationEventsAsync(SampleChecks checks)
    {
        foreach (LayoutContent source in new LayoutContent[]
        {
            Manager.Layout.Descendents().OfType<LayoutDocument>().First(),
            Manager.Layout.Descendents().OfType<LayoutAnchorable>().First(),
        })
        {
            string label = source is LayoutDocument ? "文档" : "工具";
            ILayoutContainer? originalParent = source.Parent;
            int floating = 0;
            int floated = 0;
            int docking = 0;
            int docked = 0;
            bool cancelFloat = true;
            bool cancelDock = true;
            EventHandler<ContentFloatingEventArgs> onFloating = (_, args) =>
            {
                if (ReferenceEquals(args.Content, source))
                {
                    floating++;
                    args.Cancel = cancelFloat;
                }
            };
            EventHandler<ContentFloatedEventArgs> onFloated = (_, args) =>
            {
                if (ReferenceEquals(args.Content, source))
                {
                    floated++;
                }
            };
            EventHandler<ContentDockingEventArgs> onDocking = (_, args) =>
            {
                if (ReferenceEquals(args.Content, source))
                {
                    docking++;
                    args.Cancel = cancelDock;
                }
            };
            EventHandler<ContentDockedEventArgs> onDocked = (_, args) =>
            {
                if (ReferenceEquals(args.Content, source))
                {
                    docked++;
                }
            };
            Manager.ContentFloating += onFloating;
            Manager.ContentFloated += onFloated;
            Manager.ContentDocking += onDocking;
            Manager.ContentDocked += onDocked;
            try
            {
                LayoutItem item = Manager.GetLayoutItemFromModel(source)
                    ?? throw new InvalidOperationException("事件检查的布局项未建立。");
                SampleChecks.Require(item.FloatCommand?.CanExecute(null) == true, $"Classic {label}允许正常浮动。");
                item.FloatCommand!.Execute(null);
                await SampleChecks.SettleAsync();
                SampleChecks.Require(floating == 1 && floated == 0 && !source.IsFloating
                    && ReferenceEquals(source.Parent, originalParent), $"Classic {label}取消浮动不改变布局或发送完成事件。");
                cancelFloat = false;
                item.FloatCommand.Execute(null);
                await SampleChecks.SettleAsync();
                SampleChecks.Require(floating == 2 && floated == 1 && source.IsFloating,
                    $"Classic {label}成功浮动只发送一次完成事件。");
                LayoutFloatingWindowControl window = Manager.FloatingWindows.Single(host => host.Model.Descendents().Contains(source));
                window.Width = 160;
                window.Height = 120;
                window.Left = AppWindow.Position.X;
                window.Top = AppWindow.Position.Y;
                await SampleChecks.SettleAsync();
                FrameworkElement pane = PageRoot.FindVisualChildren<FrameworkElement>().Single(element =>
                    element is ILayoutControl control && ReferenceEquals(control.Model, originalParent)
                    && element is LayoutDocumentPaneControl or LayoutAnchorablePaneControl);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    using DockingDragChecks session = new(window);
                    session.Update(DockingDragChecks.Center(DockingDragChecks.ScreenBounds(pane)));
                    string glyphName = source is LayoutDocument ? "PART_DocumentPaneDropTargetInto" : "PART_AnchorablePaneDropTargetInto";
                    await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(target =>
                        ReferenceEquals(DockingDragChecks.TargetArea(target), pane)), "事件检查目标未出现。");
                    Point release = DockingDragChecks.Center(session.GlyphBounds(glyphName));
                    session.Update(release);
                    session.Drop(release);
                    await SampleChecks.SettleAsync();
                    if (attempt == 0)
                    {
                        SampleChecks.Require(docking == 1 && docked == 0 && source.IsFloating,
                            $"Classic {label}取消停靠后保留浮窗且不发送完成事件。");
                        cancelDock = false;
                    }
                    else
                    {
                        SampleChecks.Require(docking == 2 && docked == 1 && !source.IsFloating
                            && ReferenceEquals(source.Parent, originalParent), $"Classic {label}成功停靠只发送一次完成事件并返回目标窗格。");
                    }
                }
                checks.Record($"Classic {label}浮动/停靠事件取消保持布局，成功完成各通知一次通过。");
            }
            finally
            {
                Manager.ContentFloating -= onFloating;
                Manager.ContentFloated -= onFloated;
                Manager.ContentDocking -= onDocking;
                Manager.ContentDocked -= onDocked;
            }
        }
    }

    private async Task CheckDocumentTabLayoutAsync(SampleChecks checks)
    {
        LayoutDocumentPaneControl pane = PageRoot.FindVisualChildren<LayoutDocumentPaneControl>().First();
        VerifyTabs("首次加载");

        LayoutDocument document = Manager.Layout.Descendents().OfType<LayoutDocument>().First();
        document.Float();
        await SampleChecks.SettleAsync();
        document.Dock();
        await SampleChecks.SettleAsync();
        VerifyTabs("浮动后回停");

        WorkspaceDocument longDocument = new()
        {
            Id = "classic-wide-tab-check",
            Title = "这是一个明显比普通文档标题更长的标签名称",
            Text = "检查文档标签的内容宽度。"
        };
        documents.Add(longDocument);
        await SampleChecks.SettleAsync();
        TabViewItem longTab = pane.TabItems.OfType<TabViewItem>().First(tab => ReferenceEquals(((LayoutContent)tab.Tag).Content, longDocument));
        SampleChecks.Require(longTab.ActualWidth > 100 && longTab.ActualWidth <= 240,
            "Classic 长文档标签按内容增长并受 240 像素上限约束。");
        documents.Remove(longDocument);
        await SampleChecks.SettleAsync();
        checks.Record("Classic 文档标签宽度及非按钮空白命中在首次加载、浮动回停后保持一致。");

        void VerifyTabs(string stage)
        {
            TabViewItem[] tabs = pane.TabItems.OfType<TabViewItem>().ToArray();
            SampleChecks.Require(tabs.Length == 2, $"Classic {stage}保留两个文档标签。");
            foreach (TabViewItem tab in tabs)
            {
                LayoutDocumentTabItem header = (LayoutDocumentTabItem)tab.Header;
                Button? closeButton = tab.FindVisualChildren<Button>().FirstOrDefault(button => button.Name == "CloseButton");
                double headerRight = header.TransformToVisual(tab).TransformBounds(new Rect(0, 0, header.ActualWidth, header.ActualHeight)).Right;
                double closeLeft = closeButton?.TransformToVisual(tab).TransformPoint(new Point(0, 0)).X ?? -1;
                SampleChecks.Require(Math.Abs(tab.ActualWidth - 100) < 1 && Math.Abs(tab.MinWidth - 100) < 0.1,
                    $"Classic {stage}短文档标签保持 100 像素下限。");
                SampleChecks.Require(closeButton is not null && closeLeft - headerRight >= 3,
                    $"Classic {stage}标题与关闭按钮之间存在可检查的空白。");
                Point blank = tab.TransformToVisual(PageRoot).TransformPoint(new Point((headerRight + closeLeft) / 2, 14));
                IReadOnlyList<UIElement> hits = VisualTreeHelper.FindElementsInHostCoordinates(blank, PageRoot).ToArray();
                SampleChecks.Require(hits.Contains(tab) && !hits.Contains(closeButton!),
                    $"Classic {stage}标题与关闭按钮之间由文档标签命中。");
            }
        }
    }

    private async Task CheckDocumentDropAreaAfterFloatAsync(SampleChecks checks)
    {
        foreach (Orientation orientation in new[] { Orientation.Vertical, Orientation.Horizontal })
        {
            string direction = orientation == Orientation.Vertical ? "上下" : "左右";
            LayoutDocument[] layoutDocuments = Manager.Layout.Descendents().OfType<LayoutDocument>().ToArray();
            SampleChecks.Require(layoutDocuments.Length == 2, $"Classic {direction}分组前保留两个初始文档。");
            LayoutDocument remainingDocument = layoutDocuments.Single(document => ReferenceEquals(document.Content, documents[0]));
            LayoutDocument floatedDocument = layoutDocuments.Single(document => ReferenceEquals(document.Content, documents[1]));
            LayoutDocumentItem item = Manager.GetLayoutItemFromModel(floatedDocument) as LayoutDocumentItem
                ?? throw new InvalidOperationException("Classic 分组文档布局项未创建。");
            ICommand splitCommand = (orientation == Orientation.Vertical ? item.NewHorizontalTabGroupCommand : item.NewVerticalTabGroupCommand)
                ?? throw new InvalidOperationException("Classic 文档分组命令未创建。");
            SampleChecks.Require(splitCommand.CanExecute(null), $"Classic 可创建{direction}标签组。");
            splitCommand.Execute(null);
            await SampleChecks.SettleAsync();
            LayoutDocumentPane remainingModel = remainingDocument.Parent as LayoutDocumentPane
                ?? throw new InvalidOperationException("Classic 剩余文档窗格不存在。");
            SampleChecks.Require(remainingModel.Parent is LayoutDocumentPaneGroup group && group.Orientation == orientation
                && !ReferenceEquals(remainingDocument.Parent, floatedDocument.Parent), $"Classic {direction}分组方向正确。");

            WorkspaceDocument dragModel = new()
            {
                Id = "classic-active-drag-check",
                Title = "会话检查文档",
                Text = "在已经显示停靠指示器的同一会话中改变布局。"
            };
            documents.Add(dragModel);
            await SampleChecks.SettleAsync();
            LayoutDocument dragDocument = Manager.Layout.Descendents().OfType<LayoutDocument>()
                .Single(document => ReferenceEquals(document.Content, dragModel));
            try
            {
                dragDocument.Float();
                await SampleChecks.SettleAsync();
                LayoutFloatingWindowControl draggingWindow = WindowFor(dragDocument);
                ParkWindow(draggingWindow);
                LayoutDocumentPaneControl pane = PaneFor(remainingModel);
                Rect splitBounds = DockingDragChecks.ScreenBounds(pane);
                using DockingDragChecks session = new(draggingWindow);
                Point originalCenter = DockingDragChecks.Center(splitBounds);
                session.Update(originalCenter);
                await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(target => IsCenter(target, pane)),
                    "实际会话未生成主文档窗格的中央目标。");
                DropArea<LayoutDocumentPaneControl> capturedArea = session.HostAreas(Manager)
                    .Single(area => ReferenceEquals(area.AreaElement, pane));
                SampleChecks.Require(session.ContainsArea(capturedArea)
                    && session.HostAreas(Manager).Any(area => ReferenceEquals(area, capturedArea)),
                    "Classic 初始目标来自实际宿主缓存并已进入拖动会话。");
                session.Update(DockingDragChecks.Center(session.GlyphBounds("PART_DocumentPaneDropTargetInto")));
                await session.WaitForFrameAsync(0, "初始中央指示器与预览未实际呈现。");
                SampleChecks.Require(DockingDragChecks.Centered(session.GlyphBounds("PART_DocumentPaneDropTargetInto"), splitBounds)
                    && DockingDragChecks.Near(session.PreviewBounds(), splitBounds),
                    $"Classic {direction}分组的实际指示器居中且预览覆盖当前窗格。");
                OverlayWindow originalOverlay = session.Overlay;
                int splitFrames = session.Frames;

                floatedDocument.Float();
                ParkWindow(WindowFor(floatedDocument));
                await DockingDragChecks.WaitUntilAsync(() =>
                {
                    LayoutDocumentPaneControl? currentPane = PageRoot.FindVisualChildren<LayoutDocumentPaneControl>()
                        .FirstOrDefault(current => ReferenceEquals(current.Model, remainingModel));
                    if (currentPane is not { IsLoaded: true })
                    {
                        return false;
                    }
                    Rect currentBounds = DockingDragChecks.ScreenBounds(currentPane);
                    return orientation == Orientation.Vertical ? currentBounds.Height > splitBounds.Height + 30
                        : currentBounds.Width > splitBounds.Width + 30;
                }, "浮出另一文档后，剩余窗格未完成扩展。");
                pane = PaneFor(remainingModel);
                Rect expandedBounds = DockingDragChecks.ScreenBounds(pane);
                session.Update(originalCenter);
                await session.WaitForFrameAsync(splitFrames, "同一拖动会话没有呈现扩展后的指示器。");
                SampleChecks.Require(ReferenceEquals(session.Overlay, originalOverlay), "Classic 布局变化后继续使用同一实际覆盖层会话。");
                DropArea<LayoutDocumentPaneControl> currentArea = session.HostAreas(Manager)
                    .Single(area => ReferenceEquals(area.AreaElement, pane));
                Point formerOtherHalf = orientation == Orientation.Vertical
                    ? new Point(expandedBounds.X + expandedBounds.Width / 2, (splitBounds.Bottom + expandedBounds.Bottom) / 2)
                    : new Point((splitBounds.Right + expandedBounds.Right) / 2, expandedBounds.Y + expandedBounds.Height / 2);
                session.Update(formerOtherHalf);
                SampleChecks.Require(!splitBounds.Contains(formerOtherHalf) && session.ContainsArea(currentArea)
                    && currentArea.DetectionRect.Contains(currentArea.TransformToDeviceDPI(formerOtherHalf)),
                    $"Classic {direction}分组原另一半区命中实际会话中的扩展窗格。");
                object centerTarget = session.Targets().Single(target => IsCenter(target, pane));
                Rect glyphBounds = session.GlyphBounds("PART_DocumentPaneDropTargetInto");
                SampleChecks.Require(DockingDragChecks.Centered(glyphBounds, expandedBounds)
                    && DockingDragChecks.Near(DockingDragChecks.TargetBounds(centerTarget), glyphBounds),
                    $"Classic {direction}分组浮出后，实际指示器及停靠目标的中心误差不超过 2 物理像素。");
                int expandedFrames = session.Frames;
                session.Update(DockingDragChecks.Center(glyphBounds));
                await session.WaitForFrameAsync(expandedFrames, "扩展窗格中央目标的预览未实际呈现。");
                SampleChecks.Require(session.ActiveTarget is { } active && IsCenter(active, pane)
                    && DockingDragChecks.Near(session.PreviewBounds(), expandedBounds),
                    "Classic 中央命中和实际预览共同覆盖扩展后的窗格。");

                Thickness originalMargin = pane.Margin;
                object? originalTarget = session.ActiveTarget;
                try
                {
                    int marginFrames = session.Frames;
                    pane.Margin = new Thickness(originalMargin.Left + 12, originalMargin.Top + 12,
                        originalMargin.Right + 12, originalMargin.Bottom + 12);
                    PageRoot.UpdateLayout();
                    await DockingDragChecks.WaitUntilAsync(() => DockingDragChecks.ScreenBounds(pane).Width < expandedBounds.Width - 16,
                        "对称内缩后实际窗格边界未改变。");
                    Rect insetBounds = DockingDragChecks.ScreenBounds(pane);
                    session.Update(DockingDragChecks.Center(glyphBounds));
                    await session.WaitForFrameAsync(marginFrames, "中央目标位置不变时，预览未跟随窗格边界重新呈现。");
                    SampleChecks.Require(ReferenceEquals(session.ActiveTarget, originalTarget)
                        && DockingDragChecks.Centered(session.GlyphBounds("PART_DocumentPaneDropTargetInto"), glyphBounds)
                        && DockingDragChecks.Near(session.PreviewBounds(), insetBounds),
                        "Classic 同一中央目标保持位置时，实际预览仍跟随窗格边界变化。");
                }
                finally
                {
                    int restoreFrames = session.Frames;
                    pane.Margin = originalMargin;
                    PageRoot.UpdateLayout();
                    session.Update(DockingDragChecks.Center(glyphBounds));
                    await session.WaitForFrameAsync(restoreFrames, "恢复窗格边界后预览未重新呈现。");
                }
                SampleChecks.Require(DockingDragChecks.Near(session.PreviewBounds(), DockingDragChecks.ScreenBounds(pane)),
                    "Classic 恢复后的实际中央预览覆盖当前窗格。");
                SampleChecks.Require(session.Drop(DockingDragChecks.Center(session.GlyphBounds("PART_DocumentPaneDropTargetInto"))),
                    "Classic 实际拖动会话提交了中央停靠目标。");
                await DockingDragChecks.WaitUntilAsync(() => !dragDocument.IsFloating
                    && ReferenceEquals(dragDocument.Parent, remainingDocument.Parent), "中央释放后文档未合并到实际目标窗格。");
                SampleChecks.Require(remainingModel.Children.Contains(dragDocument)
                    && PageRoot.FindVisualChildren<LayoutDocumentPaneControl>().Any(current => ReferenceEquals(current.Model, remainingModel)
                        && current.TabItems.OfType<TabViewItem>().Any(tab => tab.Tag is LayoutContent content && ReferenceEquals(content.Content, dragModel))),
                    "Classic 中央释放后的布局树和可见标签均包含同一文档内容。");
                checks.Record($"Classic {direction}分组动态变化的实际 DragService、目标、指示器、预览、绘制帧和中央释放通过。");
            }
            finally
            {
                if (floatedDocument.IsFloating)
                {
                    remainingModel.Children.Add(floatedDocument);
                    Manager.Layout.CollectGarbage();
                }
                documents.Remove(dragModel);
                await SampleChecks.SettleAsync();
            }
            SampleChecks.Require(documents.Count == 2 && !floatedDocument.IsFloating
                && ReferenceEquals(remainingDocument.Parent, floatedDocument.Parent), "Classic 会话检查后恢复两个同组停靠文档。");
        }

        LayoutDocumentPaneControl PaneFor(LayoutDocumentPane model) => PageRoot.FindVisualChildren<LayoutDocumentPaneControl>()
            .Single(pane => ReferenceEquals(pane.Model, model));
        LayoutFloatingWindowControl WindowFor(LayoutDocument document) => Manager.FloatingWindows
            .Single(window => window.Model.Descendents().OfType<LayoutDocument>().Any(current => ReferenceEquals(current, document)));
        static bool IsCenter(object target, LayoutDocumentPaneControl pane) => DockingDragChecks.TargetType(target) == DropTargetType.DocumentPaneDockInside
            && DockingDragChecks.TabIndex(target) == -1 && ReferenceEquals(DockingDragChecks.TargetArea(target), pane);
        void ParkWindow(LayoutFloatingWindowControl window)
        {
            window.Width = 160;
            window.Height = 120;
            window.Left = AppWindow.Position.X;
            window.Top = AppWindow.Position.Y;
        }
    }

    private async Task CheckDocumentChromeAsync(SampleChecks checks)
    {
        LayoutDocumentPaneControl? pane = PageRoot.FindVisualChildren<LayoutDocumentPaneControl>().FirstOrDefault();
        SampleChecks.Require(pane is not null, "Classic 文档窗格已加载。");
        TabViewItem[] tabs = pane!.TabItems.OfType<TabViewItem>().ToArray();
        SampleChecks.Require(tabs.Length == documents.Count, "Classic 初始文档标签已加载。");
        foreach (TabViewItem tab in tabs)
        {
            Button? closeButton = tab.FindVisualChildren<Button>().FirstOrDefault(button => button.Name == "CloseButton");
            SampleChecks.Require(closeButton is not null && Math.Abs(closeButton.ActualWidth - 20) < 1
                && Math.Abs(closeButton.ActualHeight - 20) < 1 && Math.Abs(tab.Padding.Right - 4) < 0.1,
                "Classic 文档标签关闭按钮和右侧内边距符合 WPFUI 尺寸。");
            SampleChecks.Require(closeButton!.Content is Viewbox { Width: 12, Height: 12, Child: PathIcon { Width: 12, Height: 12, Data: not null } },
                "Classic 文档标签关闭图形为 12 像素。");
        }
        checks.Record("Classic 文档标签关闭按钮尺寸和图形通过。");

        global::AvalonDock.Controls.DropDownButton? selectorButton = pane.FindVisualChildren<global::AvalonDock.Controls.DropDownButton>()
            .FirstOrDefault(button => button.Name == "MenuDropDownButton");
        SampleChecks.Require(selectorButton?.DropDownContextMenu is ContextMenuEx, "Classic 文档选择器已连接菜单。");
        ContextMenuEx menu = (ContextMenuEx)selectorButton!.DropDownContextMenu!;
        ElementTheme originalTheme = PageRoot.RequestedTheme;
        try
        {
            foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                PageRoot.RequestedTheme = theme;
                await SampleChecks.SettleAsync();
                SampleChecks.Require(pane.Background is SolidColorBrush surface
                    && surface.Color.A == 255
                    && surface.Color.R == (theme == ElementTheme.Light ? 0xF9 : 0x28)
                    && surface.Color.G == (theme == ElementTheme.Light ? 0xF9 : 0x28)
                    && surface.Color.B == (theme == ElementTheme.Light ? 0xF9 : 0x28),
                    $"Classic 文档表面符合{(theme == ElementTheme.Light ? "浅色" : "深色")}主题色。");
                menu.ShowAt(selectorButton);
                selectorButton.IsChecked = true;
                await SampleChecks.SettleAsync();

                Popup? popup = VisualTreeHelper.GetOpenPopupsForXamlRoot(PageRoot.XamlRoot)
                    .FirstOrDefault(candidate => candidate.Child is MenuFlyoutPresenter
                        || candidate.Child.FindVisualChildren<MenuFlyoutPresenter>().Any());
                MenuFlyoutPresenter? flyoutPresenter = popup?.Child as MenuFlyoutPresenter
                    ?? popup?.Child.FindVisualChildren<MenuFlyoutPresenter>().FirstOrDefault();
                SampleChecks.Require(flyoutPresenter is not null && flyoutPresenter.ActualTheme == theme,
                    $"Classic 文档选择器弹出菜单跟随{(theme == ElementTheme.Light ? "浅色" : "深色")}主题。");

                if (theme == ElementTheme.Light)
                {
                    MenuItemEx[] items = menu.Items.OfType<MenuItemEx>().ToArray();
                    SampleChecks.Require(items.Length == documents.Count, "Classic 文档选择器包含初始文档。");
                    double? firstLeft = null;
                    foreach (WorkspaceDocument document in documents)
                    {
                        MenuItemEx? item = items.FirstOrDefault(candidate => candidate.DataContext is LayoutContent model
                            && ReferenceEquals(model.Content, document));
                        ContentPresenter? titlePresenter = item?.FindVisualChildren<ContentPresenter>()
                            .FirstOrDefault(presenter => presenter.Name == "TextBlock");
                        TextBlock? title = titlePresenter?.FindVisualChildren<TextBlock>()
                            .FirstOrDefault(text => text.Text == document.Title);
                        Grid? itemRoot = item?.FindVisualChildren<Grid>().FirstOrDefault(grid => grid.Name == "LayoutRoot");
                        SampleChecks.Require(item is not null && item.Text == document.Title
                            && titlePresenter is not null && title is not null && itemRoot is not null
                            && item.HorizontalContentAlignment == HorizontalAlignment.Left
                            && titlePresenter.HorizontalContentAlignment == HorizontalAlignment.Left,
                            "Classic 文档选择器标题文案与左对齐设置正确。");
                        double titleLeft = title!.TransformToVisual(popup!.Child).TransformPoint(new Point(0, 0)).X;
                        double presenterLeft = titlePresenter!.TransformToVisual(itemRoot).TransformPoint(new Point(0, 0)).X;
                        SampleChecks.Require(item!.Icon is Image { Source: null }
                            && ((MenuFlyoutItem)item).Icon is null
                            && titlePresenter.Margin.Left < 1
                            && titleLeft - itemRoot!.TransformToVisual(popup.Child).TransformPoint(new Point(0, 0)).X < 20,
                            "Classic 无图标文档的菜单标题紧贴左侧，不预留空图标列。");
                        SampleChecks.Require(Math.Abs(presenterLeft - itemRoot!.Padding.Left - titlePresenter.Margin.Left) < 1,
                            "Classic 文档选择器标题无重复图标占位。");
                        SampleChecks.Require(firstLeft is null || Math.Abs(titleLeft - firstLeft.Value) < 1,
                            "Classic 长短文档标题具有相同左边界。");
                        firstLeft ??= titleLeft;

                        AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(item)
                            ?? FrameworkElementAutomationPeer.CreatePeerForElement(item);
                        SampleChecks.Require(peer?.GetName() == document.Title, "Classic 文档选择器自动化文案可读。");
                    }
                    checks.Record("Classic 文档选择器文案、左对齐和自动化名称通过。");

                    LayoutContent iconDocument = (LayoutContent)items[0].DataContext;
                    iconDocument.IconSource = new Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap(1, 1);
                    await SampleChecks.SettleAsync();
                    menu.Hide();
                    await SampleChecks.SettleAsync();
                    menu.ShowAt(selectorButton);
                    await SampleChecks.SettleAsync();
                    ContentPresenter? iconTitle = items[0].FindVisualChildren<ContentPresenter>()
                        .FirstOrDefault(presenter => presenter.Name == "TextBlock");
                    Viewbox? iconRoot = items[0].FindVisualChildren<Viewbox>()
                        .FirstOrDefault(viewbox => viewbox.Name == "IconRoot");
                    SampleChecks.Require(items[0].Icon is Image { Source: not null }
                        && ((MenuFlyoutItem)items[0]).Icon is not null
                        && iconRoot?.Visibility == Visibility.Visible && iconTitle?.Margin.Left >= 20,
                        "Classic 文档选择器在图标加载后预留图标列。");
                    checks.Record("Classic 文档图标加载后菜单占位通过。");
                }

                menu.Hide();
                selectorButton.IsChecked = false;
                await SampleChecks.SettleAsync();
            }
            checks.Record("Classic 文档选择器浅色和深色弹出主题通过。");
        }
        finally
        {
            menu.Hide();
            selectorButton.IsChecked = false;
            PageRoot.RequestedTheme = originalTheme;
        }

        global::Windows.Graphics.SizeInt32 originalSize = AppWindow.Size;
        WorkspaceDocument[] initialDocuments = documents.ToArray();
        try
        {
            for (int index = 0; index < 4; index++)
            {
                AddDocument_Click(this, new RoutedEventArgs());
            }

            AppWindow.Resize(new global::Windows.Graphics.SizeInt32(720, originalSize.Height));
            await SampleChecks.SettleAsync();
            TabViewItem[] overflowTabs = pane.TabItems.OfType<TabViewItem>().ToArray();
            SampleChecks.Require(overflowTabs.Length == 6 && overflowTabs.Any(tab => tab.Opacity == 0),
                "Classic 文档选择器可配合窄窗格隐藏溢出标签。");
            Rect selectorBounds = selectorButton.TransformToVisual(pane).TransformBounds(
                new Rect(0, 0, selectorButton.ActualWidth, selectorButton.ActualHeight));
            SampleChecks.Require(selectorBounds.Right <= pane.ActualWidth + 1 && selectorBounds.Left >= 0,
                "Classic 窄窗格中的文档选择按钮仍位于窗格内。");
            TabViewItem hiddenTab = overflowTabs.First(tab => tab.Opacity == 0);
            LayoutContent hiddenModel = (LayoutContent)hiddenTab.Tag;
            menu.ShowAt(selectorButton);
            await SampleChecks.SettleAsync();
            MenuItemEx? hiddenItem = menu.Items.OfType<MenuItemEx>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, hiddenModel));
            SampleChecks.Require(hiddenItem?.Command?.CanExecute(null) == true,
                "Classic 文档选择器提供溢出文档的激活命令。");
            hiddenItem!.Command!.Execute(null);
            menu.Hide();
            await SampleChecks.SettleAsync();
            SampleChecks.Require(hiddenModel.IsSelected && hiddenTab.Opacity > 0,
                "Classic 从选择器激活的溢出文档重新显示在标签条中。");
            checks.Record("Classic 窄窗格文档溢出、选择按钮与隐藏文档激活通过。");
        }
        finally
        {
            menu.Hide();
            AppWindow.Resize(originalSize);
            foreach (WorkspaceDocument extra in documents.Where(document => !initialDocuments.Contains(document)).ToArray())
            {
                documents.Remove(extra);
            }

            await SampleChecks.SettleAsync();
        }
    }

    private void CheckSourceContent<T>(SampleChecks checks, IEnumerable<T> models, string label)
    {
        object[] expected = models.Cast<object>().ToArray();
        object[] actual = Manager.Layout.Descendents().OfType<LayoutContent>()
            .Select(item => item.Content)
            .Where(content => content is T)
            .Cast<object>()
            .ToArray();
        SampleChecks.Require(actual.Length == expected.Length
            && actual.Distinct(System.Collections.Generic.ReferenceEqualityComparer.Instance).Count() == actual.Length
            && expected.Distinct(System.Collections.Generic.ReferenceEqualityComparer.Instance).Count() == expected.Length
            && expected.All(model => actual.Any(content => ReferenceEquals(content, model))),
            $"{label}内容身份集合准确，没有残留旧项或重复模型。");
        checks.Record($"{label}精确引用集合与重复检查通过（{expected.Length}）。");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.Dispose();
    }
}
