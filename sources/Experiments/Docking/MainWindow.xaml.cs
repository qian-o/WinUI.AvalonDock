using System.Collections.ObjectModel;
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
        checks.Record("Classic 首次加载工具标签呈现通过。");

        await CheckDocumentChromeAsync(checks);

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
