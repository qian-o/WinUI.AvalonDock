using System.Collections;
using System.Reflection;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
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
            CreateTool("toggle-explorer", "资源管理器", DockZone.LeftTop, "\uE838", "左上工具区：适合项目树和导航。"),
            CreateTool("toggle-outline", "大纲", DockZone.LeftBottom, "\uE8A5", "左下工具区：适合结构化信息。"),
            CreateTool("toggle-properties", "属性", DockZone.RightTop, "\uE713", "右上工具区：适合检查当前项。"),
            CreateTool("toggle-diagnostics", "诊断", DockZone.RightBottom, "\uE814", "右下工具区：适合错误、警告和日志。"),
            CreateTool("toggle-output", "输出", DockZone.BottomLeft, "\uE756", "底左工具区：适合构建和调试输出。"),
            CreateTool("toggle-terminal", "终端", DockZone.BottomRight, "\uE756", "底右工具区：适合命令行和任务。")
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

    private static WorkspaceTool CreateTool(string id, string title, DockZone zone, string glyph, string text)
    {
        return new WorkspaceTool
        {
            Id = id,
            Title = title,
            Zone = zone,
            Icon = new FontIcon { Glyph = glyph, FontSize = 20 },
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
            Manager.ReattachAnchorable(tool);
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

        ToggleDockButton[] buttons = PageRoot.FindVisualChildren<ToggleDockButton>().ToArray();
        SampleChecks.Require(buttons.Length == 6, "Toggle 六个工具均已生成侧栏按钮。");
        SampleChecks.Require(buttons.All(button => Math.Abs(button.ActualWidth - Manager.ButtonSize) < 0.5
            && button.FindVisualChildren<Border>().Any(border => border.Name == "Indicator")),
            "Toggle 侧栏按钮使用 WPFUI 尺寸和选中指示条模板。");
        SampleChecks.Require(PageRoot.FindVisualChildren<Grid>().Any(grid => grid.Name == "PART_ToggleNavigationGrid")
            && PageRoot.FindVisualChildren<Border>().Any(border => border.Name == "PART_LeftNavigationFrame")
            && PageRoot.FindVisualChildren<Border>().Any(border => border.Name == "PART_RightNavigationFrame"),
            "Toggle 管理器已加载左右导航框模板。");
        LayoutAnchorablePaneControl[] panes = PageRoot.FindVisualChildren<LayoutAnchorablePaneControl>().ToArray();
        SampleChecks.Require(panes.Length > 0 && panes.All(pane => pane.FindVisualChildren<Border>()
            .Any(border => border.Name == "PaneFrame" && border.CornerRadius.TopLeft == 6)),
            "Toggle 工具窗格使用圆角边框模板。");
        SampleChecks.Require(panes.Where(pane => ((LayoutAnchorablePane)pane.Model).ChildrenCount == 1)
            .All(pane => pane.FindVisualChildren<Border>().Any(border => border.Name == "TabStrip" && border.Visibility == Visibility.Collapsed)),
            "Toggle 单工具窗格隐藏了底部标签条。");
        SampleChecks.Require(PageRoot.FindVisualChildren<ToggleAnchorablePaneTitle>()
            .All(title => Math.Abs(title.ActualHeight - 32) < 0.5),
            "Toggle 专用标题栏使用 32 像素高度。");
        Border navigationFrame = PageRoot.FindVisualChildren<Border>()
            .First(border => border.Name == "PART_LeftNavigationFrame");
        ElementTheme originalTheme = PageRoot.RequestedTheme;
        PageRoot.RequestedTheme = ElementTheme.Light;
        await SampleChecks.SettleAsync();
        global::Windows.UI.Color lightSurface = ((SolidColorBrush)navigationFrame.Background).Color;
        PageRoot.RequestedTheme = ElementTheme.Dark;
        await SampleChecks.SettleAsync();
        global::Windows.UI.Color darkSurface = ((SolidColorBrush)navigationFrame.Background).Color;
        PageRoot.RequestedTheme = originalTheme;
        SampleChecks.Require(lightSurface != darkSurface, "Toggle 导航框响应 Light/Dark 主题资源切换。");
        checks.Record("Toggle WPFUI 专用管理器、侧栏和窗格模板通过。");

        await CheckZonePreviewGeometryAsync(checks);

        LayoutAnchorable selectedTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault()
            ?? throw new InvalidOperationException("Toggle 不存在可操作工具。");
        WorkspaceTool model = selectedTool.Content as WorkspaceTool
            ?? throw new InvalidOperationException("Toggle 工具内容不是 WorkspaceTool。");
        DockZone originalZone = model.Zone;
        MoveToolToZone(selectedTool, DockZone.RightBottom);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ReferenceEquals(selectedTool.Content, model), "Toggle 区域移动保留内容引用。");
        SampleChecks.Require(model.Zone == DockZone.RightBottom, "Toggle 工具模型 Zone 已更新为目标区域。");
        ToggleDockButton movedButton = PageRoot.FindVisualChildren<ToggleDockButton>()
            .Single(button => ReferenceEquals(button.Anchorable, selectedTool));
        Border movedIndicator = movedButton.FindVisualChildren<Border>().Single(border => border.Name == "Indicator");
        Rect indicatorBounds = movedIndicator.TransformToVisual(movedButton)
            .TransformBounds(new Rect(0, 0, movedIndicator.ActualWidth, movedIndicator.ActualHeight));
        SampleChecks.Require(movedButton.Zone == DockZone.RightBottom
            && movedIndicator.HorizontalAlignment == HorizontalAlignment.Right
            && Math.Abs(indicatorBounds.Right - movedButton.ActualWidth) < 0.5
            && Math.Abs((indicatorBounds.Top + indicatorBounds.Bottom) / 2 - movedButton.ActualHeight / 2) < 0.5,
            "Toggle 工具移动到右侧区域后，指示条精确贴合按钮右边并保持垂直居中。");
        checks.Record($"Toggle 工具区域移动通过（{originalZone} -> {model.Zone}）。");

        selectedTool.Hide();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(Manager.Layout.Hidden.Contains(selectedTool), "Toggle 工具可以隐藏。");
        selectedTool.Show();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!Manager.Layout.Hidden.Contains(selectedTool), "Toggle 隐藏工具可以恢复。");
        checks.Record("Toggle 隐藏和恢复通过。");

        MethodInfo hideFromMenu = typeof(ToggleDockingManager).GetMethod("HideAnchorableFromMenu", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 Toggle 菜单隐藏入口。");
        int canceledHides = 0;
        EventHandler<AnchorableHidingEventArgs> cancelHide = (_, args) =>
        {
            canceledHides++;
            args.Cancel = true;
        };
        Manager.AnchorableHiding += cancelHide;
        try
        {
            hideFromMenu.Invoke(Manager, [selectedTool]);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(canceledHides == 1 && !selectedTool.IsHidden
                && PageRoot.FindVisualChildren<ToggleDockButton>().Any(button => ReferenceEquals(button.Anchorable, selectedTool)),
                "Toggle 取消隐藏后保留原侧栏按钮。");

            Manager.DetachAnchorableToWindow(selectedTool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(Manager.IsDetached(selectedTool), "Toggle 工具已进入独立窗口。");
            hideFromMenu.Invoke(Manager, [selectedTool]);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(canceledHides == 2 && Manager.IsDetached(selectedTool) && !selectedTool.IsHidden,
                "Toggle 取消隐藏后保留独立窗口。");

            MethodInfo floatFromMenu = typeof(ToggleDockingManager).GetMethod("FloatAnchorableFromMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("找不到 Toggle 菜单浮动入口。");
            int canceledFloats = 0;
            EventHandler<ContentFloatingEventArgs> cancelFloat = (_, args) =>
            {
                canceledFloats++;
                args.Cancel = true;
            };
            Manager.ContentFloating += cancelFloat;
            try
            {
                floatFromMenu.Invoke(Manager, [selectedTool]);
                await SampleChecks.SettleAsync();
                SampleChecks.Require(canceledFloats == 1 && Manager.IsDetached(selectedTool) && !selectedTool.IsFloating,
                    "Toggle 取消浮动后保留独立窗口。");
            }
            finally
            {
                Manager.ContentFloating -= cancelFloat;
            }
        }
        finally
        {
            Manager.AnchorableHiding -= cancelHide;
            Manager.ReattachAnchorable(selectedTool);
        }
        await SampleChecks.SettleAsync();
        checks.Record("Toggle 菜单隐藏与浮动取消时保留侧栏按钮和独立窗口。");

        LayoutAnchorable secondTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .First(tool => !ReferenceEquals(tool, selectedTool));
        try
        {
            Manager.DetachAnchorableToWindow(selectedTool);
            Manager.DetachAnchorableToWindow(secondTool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(Manager.IsDetached(selectedTool) && Manager.IsDetached(secondTool),
                "Toggle 两个工具可同时使用独立窗口。");
            selectedTool.IsActive = true;
            DetachTool_Click(this, new RoutedEventArgs());
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!Manager.IsDetached(selectedTool) && Manager.IsDetached(secondTool),
                "Toggle 附回当前工具不会附回其他独立窗口。");
        }
        finally
        {
            Manager.ReattachAnchorable(selectedTool);
            Manager.ReattachAnchorable(secondTool);
        }
        checks.Record("Toggle 当前工具附回仅影响选中工具。");

        bool originalFloating = Manager.AllowDetachedWindows;
        Manager.AllowDetachedWindows = false;
        Manager.AllowFloatingWindows = false;
        SampleChecks.Require(!Manager.AllowDetachedWindows && !Manager.AllowFloatingWindows, "Toggle 窗口策略可以关闭。");
        Manager.AllowDetachedWindows = originalFloating;
        Manager.AllowFloatingWindows = originalFloating;
        checks.Record("Toggle 窗口策略通过。");
    }

    private async Task CheckZonePreviewGeometryAsync(SampleChecks checks)
    {
        Dictionary<DockZone, LayoutAnchorable> tools = Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Where(tool => tool.Content is WorkspaceTool)
            .ToDictionary(tool => ((WorkspaceTool)tool.Content!).Zone);
        Type overlayType = typeof(ToggleDockingManager).Assembly.GetType("AvalonDock.Controls.ToggleDockDragOverlay")
            ?? throw new InvalidOperationException("找不到 Toggle 拖动指示层。");
        ConstructorInfo constructor = overlayType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        using IDisposable overlay = (IDisposable)constructor.Invoke([Manager, tools[DockZone.LeftTop]]);
        MethodInfo update = overlayType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 Toggle 指示层更新入口。");
        MethodInfo hit = overlayType.GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 Toggle 指示层命中入口。");
        FieldInfo zones = overlayType.GetField("zones", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 Toggle 指示区域。");
        PropertyInfo failure = overlayType.GetProperty("Failure", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 Toggle 指示层故障状态。");

        (DockZone First, DockZone Second)[] pairs =
        [
            (DockZone.LeftTop, DockZone.LeftBottom),
            (DockZone.RightTop, DockZone.RightBottom),
            (DockZone.BottomLeft, DockZone.BottomRight)
        ];
        foreach ((DockZone first, DockZone second) in pairs)
        {
            VerifyPair(first, second, splitEvenly: false);

            Manager.ToggleAnchorable(tools[first], first);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(tools[first].IsAutoHidden && !tools[second].IsAutoHidden,
                $"Toggle {first} 收起后仅 {second} 展开。");
            VerifyPair(first, second, splitEvenly: true);

            Manager.ToggleAnchorable(tools[first], first);
            Manager.ToggleAnchorable(tools[second], second);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!tools[first].IsAutoHidden && tools[second].IsAutoHidden,
                $"Toggle {second} 收起后仅 {first} 展开。");
            VerifyPair(first, second, splitEvenly: true);

            Manager.ToggleAnchorable(tools[first], first);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(tools[first].IsAutoHidden && tools[second].IsAutoHidden,
                $"Toggle {first}/{second} 均已收起。");
            VerifyPair(first, second, splitEvenly: true);

            Manager.ToggleAnchorable(tools[first], first);
            Manager.ToggleAnchorable(tools[second], second);
            await SampleChecks.SettleAsync();
        }

        SampleChecks.Require(failure.GetValue(overlay) == null, "Toggle 拖动指示层正常绘制。");
        checks.Record("Toggle 六区指示在双窗格、单窗格和空白分组时正确切分并命中。");

        void VerifyPair(DockZone first, DockZone second, bool splitEvenly)
        {
            update.Invoke(overlay, [new Point(0, 0), true]);
            IEnumerable entries = (IEnumerable)(zones.GetValue(overlay)
                ?? throw new InvalidOperationException("Toggle 指示区域尚未生成。"));
            Dictionary<DockZone, Rect> bounds = [];
            foreach (object entry in entries)
            {
                Type zoneType = entry.GetType();
                if (zoneType.GetProperty("Label")?.GetValue(entry) == null)
                {
                    continue;
                }

                DockZone target = (DockZone)(zoneType.GetProperty("Target")?.GetValue(entry)
                    ?? throw new InvalidOperationException("Toggle 指示区域缺少目标。"));
                Rect rectangle = (Rect)(zoneType.GetProperty("Bounds")?.GetValue(entry)
                    ?? throw new InvalidOperationException("Toggle 指示区域缺少边界。"));
                bounds.Add(target, rectangle);
            }

            Rect firstBounds = bounds[first];
            Rect secondBounds = bounds[second];
            SampleChecks.Require(firstBounds.Width > 0 && firstBounds.Height > 0
                && secondBounds.Width > 0 && secondBounds.Height > 0,
                $"Toggle {first}/{second} 指示区域均非空。");
            Rect overlap = firstBounds;
            overlap.Intersect(secondBounds);
            SampleChecks.Require(overlap.IsEmpty || overlap.Width <= 0.5 || overlap.Height <= 0.5,
                $"Toggle {first}/{second} 指示区域不重叠。");

            if (splitEvenly)
            {
                if (first is DockZone.BottomLeft)
                {
                    SampleChecks.Require(Math.Abs(firstBounds.Width - secondBounds.Width) <= 1.5
                        && Math.Abs(firstBounds.Right - secondBounds.Left) <= 1.5
                        && Math.Abs(firstBounds.Top - secondBounds.Top) <= 1.5
                        && Math.Abs(firstBounds.Height - secondBounds.Height) <= 1.5,
                        "Toggle 底部空白分组沿水平方向均分。");
                }
                else
                {
                    SampleChecks.Require(Math.Abs(firstBounds.Height - secondBounds.Height) <= 1.5
                        && Math.Abs(firstBounds.Bottom - secondBounds.Top) <= 1.5
                        && Math.Abs(firstBounds.Left - secondBounds.Left) <= 1.5
                        && Math.Abs(firstBounds.Width - secondBounds.Width) <= 1.5,
                        $"Toggle {first}/{second} 空白分组沿垂直方向均分。");
                }
            }

            Point firstCenter = new(firstBounds.X + firstBounds.Width / 2, firstBounds.Y + firstBounds.Height / 2);
            Point secondCenter = new(secondBounds.X + secondBounds.Width / 2, secondBounds.Y + secondBounds.Height / 2);
            SampleChecks.Require((DockZone?)hit.Invoke(overlay, [firstCenter]) == first
                && (DockZone?)hit.Invoke(overlay, [secondCenter]) == second,
                $"Toggle {first}/{second} 指示区域的中心命中正确。");
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.Dispose();
    }
}
