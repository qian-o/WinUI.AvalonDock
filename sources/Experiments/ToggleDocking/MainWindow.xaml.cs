using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;
using Microsoft.UI.Xaml;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.ToggleDocking;

public sealed partial class MainWindow : Window
{
    private readonly DockLayoutService layoutService;
    private int nextDocumentNumber = 2;

    public MainWindow()
    {
        InitializeComponent();
        Title = "ToggleDocking：六区工作台";
        Closed += OnClosed;

        WorkspaceTool[] tools =
        [
            CreateTool("toggle-explorer", "资源管理器", DockZone.LeftTop, "左上工具区：适合项目树和导航。"),
            CreateTool("toggle-outline", "大纲", DockZone.LeftBottom, "左下工具区：适合结构化信息。"),
            CreateTool("toggle-properties", "属性", DockZone.RightTop, "右上工具区：适合检查当前项。"),
            CreateTool("toggle-diagnostics", "诊断", DockZone.RightBottom, "右下工具区：适合错误、警告和日志。"),
            CreateTool("toggle-output", "输出", DockZone.BottomLeft, "底左工具区：适合构建和调试输出。"),
            CreateTool("toggle-terminal", "终端", DockZone.BottomRight, "底右工具区：适合命令行和任务。")
        ];
        layoutService = new DockLayoutService(tools);
        layoutService.OpenDocument(new WorkspaceDocument
        {
            Id = "toggle-editor",
            Title = "工作区",
            Text = "ToggleDocking 使用同一棵布局树管理六个工具区。",
            IsModified = false
        });
        layoutService.OpenDocument(new WorkspaceDocument
        {
            Id = "toggle-notes",
            Title = "笔记",
            Text = "点击区域按钮，把当前工具移动到指定的 Zone。",
            IsModified = false
        });

        Manager.LayoutItemContainerStyleSelector = SampleStyles.CreateItemStyleSelector();
        Manager.DockLayout = layoutService.Layout;
        Manager.ActiveContentChanged += OnActiveContentChanged;
        SampleChecks.RunWhenLoaded(this, RunScenarioChecksAsync);
    }

    private static WorkspaceTool CreateTool(string id, string title, DockZone zone, string text)
    {
        return new WorkspaceTool
        {
            Id = id,
            Title = title,
            Zone = zone,
            IsOpenByDefault = true,
            Text = text
        };
    }

    private LayoutAnchorable? SelectedTool =>
        Manager.Layout?.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(item => ReferenceEquals(item.Content, Manager.ActiveContent))
        ?? Manager.Layout?.Descendents().OfType<LayoutAnchorable>().FirstOrDefault()
        ?? Manager.Layout?.Hidden.FirstOrDefault();

    private void AddDocument_Click(object sender, RoutedEventArgs e)
    {
        int number = nextDocumentNumber++;
        WorkspaceDocument document = new()
        {
            Id = $"toggle-document-{number}",
            Title = $"文档 {number}",
            Text = "动态文档由 DockLayoutService.OpenDocument 加入 MVVM 布局。",
            IsModified = false
        };
        layoutService.OpenDocument(document);
        Manager.ActiveContent = document;
        StatusText.Text = $"已新增 {document.Title}。";
    }

    private void ToggleTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = SelectedTool;
        if (tool is null)
        {
            StatusText.Text = "当前没有可切换的工具。";
            return;
        }

        DockZone zone = tool.Content is WorkspaceTool model ? model.Zone : DockZone.LeftTop;
        Manager.ToggleAnchorable(tool, zone);
        StatusText.Text = $"已切换 {tool.Title} 的展开状态。";
    }

    private void ShowHiddenTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = Manager.Layout?.Hidden.FirstOrDefault();
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
        LayoutAnchorable? tool = SelectedTool;
        if (tool is null)
        {
            StatusText.Text = "当前没有可拆分的工具。";
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

    private void MoveLeftTop_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.LeftTop);
    private void MoveLeftBottom_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.LeftBottom);
    private void MoveRightTop_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.RightTop);
    private void MoveRightBottom_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.RightBottom);
    private void MoveBottomLeft_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.BottomLeft);
    private void MoveBottomRight_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.BottomRight);

    private void MoveSelectedTool(DockZone zone)
    {
        MoveToolToZone(SelectedTool, zone);
    }

    private void MoveToolToZone(LayoutAnchorable? tool, DockZone zone)
    {
        if (tool is null)
        {
            StatusText.Text = "当前没有可移动的工具。";
            return;
        }

        Manager.MoveAnchorableToZone(tool, zone);
        if (tool.Content is WorkspaceTool model)
        {
            model.Zone = zone;
        }
        StatusText.Text = $"已将 {tool.Title} 移到 {zone}。";
    }

    private void BottomFullWidth_Click(object sender, RoutedEventArgs e)
    {
        Manager.LayoutPriority = DockLayoutPriority.BottomFullWidth;
        StatusText.Text = "布局优先级：底部工具区横跨全宽。";
    }

    private void SidesFullHeight_Click(object sender, RoutedEventArgs e)
    {
        Manager.LayoutPriority = DockLayoutPriority.SidesFullHeight;
        StatusText.Text = "布局优先级：左右工具区保持全高。";
    }

    private void DefaultLayoutPriority_Click(object sender, RoutedEventArgs e)
    {
        Manager.LayoutPriority = DockLayoutPriority.Default;
        StatusText.Text = "布局优先级：使用默认布局。";
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        PageRoot.RequestedTheme = PageRoot.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        StatusText.Text = PageRoot.RequestedTheme == ElementTheme.Dark ? "深色主题。" : "浅色主题。";
    }

    private void OnActiveContentChanged(object? sender, EventArgs e)
    {
        if (Manager.ActiveContent is not WorkspaceTool model)
        {
            return;
        }

        StatusText.Text = $"当前工具：{model.Title}（{model.Zone}）。";
    }

    public async Task RunScenarioChecksAsync(SampleChecks checks)
    {
        await SampleChecks.SettleAsync();
        SampleChecks.Require(layoutService.Anchorables.Count() == 6, "Toggle 初始六个工具模型已创建。");
        SampleChecks.Require(layoutService.Documents.Count() == 2, "Toggle 初始文档模型已创建。");
        foreach (WorkspaceTool tool in layoutService.Anchorables.OfType<WorkspaceTool>())
        {
            SampleChecks.Require(Manager.Layout.Descendents().OfType<LayoutAnchorable>().Any(item => ReferenceEquals(item.Content, tool)), $"Toggle 工具 {tool.Title} 已同步到布局。");
        }
        checks.Record("Toggle 六个工具区模型同步通过。");

        LayoutAnchorable selectedTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault()
            ?? throw new InvalidOperationException("Toggle 不存在可操作工具。");
        WorkspaceTool model = selectedTool.Content as WorkspaceTool
            ?? throw new InvalidOperationException("Toggle 工具内容不是 WorkspaceTool。");
        DockZone originalZone = model.Zone;
        MoveToolToZone(selectedTool, DockZone.RightBottom);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ReferenceEquals(selectedTool.Content, model), "Toggle 区域移动保留内容引用。");
        SampleChecks.Require(model.Zone == DockZone.RightBottom, "Toggle 工具模型 Zone 已更新为目标区域。");
        checks.Record($"Toggle 工具区域移动通过（{originalZone} -> {model.Zone}）。");

        selectedTool.Hide();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(Manager.Layout.Hidden.Contains(selectedTool), "Toggle 工具可以隐藏。");
        selectedTool.Show();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!Manager.Layout.Hidden.Contains(selectedTool), "Toggle 隐藏工具可以恢复。");
        checks.Record("Toggle 隐藏和恢复通过。");

        bool originalFloating = Manager.AllowDetachedWindows;
        Manager.AllowDetachedWindows = false;
        Manager.AllowFloatingWindows = false;
        SampleChecks.Require(!Manager.AllowDetachedWindows && !Manager.AllowFloatingWindows, "Toggle 窗口策略可以关闭。");
        Manager.AllowDetachedWindows = originalFloating;
        Manager.AllowFloatingWindows = originalFloating;
        checks.Record("Toggle 窗口策略通过。");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.Dispose();
    }
}


