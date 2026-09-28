using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.ToggleDocking;

public sealed partial class MainWindow
{
    private async Task CheckLayoutPrioritiesAsync(SampleChecks checks)
    {
        // The priority is applied when an auto-hidden tool opens. Reset to the
        // same asymmetric tree before each transition so its visible effect is measurable.
        Manager.DockLayout = null;
        foreach (DockLayoutPriority priority in new[]
        {
            DockLayoutPriority.Default,
            DockLayoutPriority.BottomFullWidth,
            DockLayoutPriority.SidesFullHeight
        })
        {
            Manager.LayoutPriority = DockLayoutPriority.Default;
            LayoutRoot root = new()
            {
                RootPanel = new LayoutPanel(new LayoutDocumentPane())
            };
            Manager.Layout = root;
            await SampleChecks.SettleAsync();

            LayoutAnchorable left = Tool("左侧基准", DockZone.LeftTop);
            LayoutAnchorable bottom = Tool("底部基准", DockZone.BottomLeft);
            LayoutAnchorable right = Tool("右侧基准", DockZone.RightTop);
            LayoutAnchorable trigger = Tool("展开触发", DockZone.RightTop, open: false);
            LayoutDocument document = new()
            {
                ContentId = "priority-document",
                Title = "优先级文档",
                Content = new WorkspaceDocument
                {
                    Id = "priority-document",
                    Title = "优先级文档",
                    Text = "同一布局基线，用工具展开检验三种布局优先级。"
                }
            };

            LayoutAnchorablePane leftPane = new(left)
            {
                DockWidth = new GridLength(220)
            };
            LayoutAnchorablePane bottomPane = new(bottom)
            {
                DockHeight = new GridLength(180)
            };
            LayoutAnchorablePane rightPane = new(right)
            {
                DockWidth = new GridLength(220)
            };
            LayoutDocumentPane documentPane = new(document);
            LayoutPanel leftAndDocument = new(leftPane)
            {
                Orientation = Orientation.Horizontal
            };
            leftAndDocument.Children.Add(documentPane);
            LayoutPanel leftBlock = new(leftAndDocument)
            {
                Orientation = Orientation.Vertical
            };
            leftBlock.Children.Add(bottomPane);
            LayoutPanel baseline = new(leftBlock)
            {
                Orientation = Orientation.Horizontal
            };
            baseline.Children.Add(rightPane);
            root.RootPanel = baseline;
            LayoutAnchorGroup hidden = new();
            root.RightSide!.Children.Add(hidden);
            hidden.Children.Add(trigger);
            await SampleChecks.SettleAsync();

            SampleChecks.Require(trigger.IsAutoHidden
                && ReferenceEquals(root.RootPanel, baseline)
                && ReferenceEquals(bottomPane.Parent, leftBlock)
                && ReferenceEquals(leftPane.Parent, leftAndDocument)
                && ReferenceEquals(rightPane.Parent, baseline),
                $"{priority} 使用固定 H(V(H(L,D),B),R) 基线且触发工具自动隐藏。");

            Rect baseLeft = Bounds(leftPane);
            Rect baseBottom = Bounds(bottomPane);
            Rect baseRight = Bounds(rightPane);
            checks.Record($"{priority} 展开前屏幕边界：L={Format(baseLeft)} B={Format(baseBottom)} R={Format(baseRight)}。");
            SampleChecks.Require(baseLeft.Width > 100 && baseBottom.Height > 80 && baseRight.Width > 100
                && baseBottom.Right < baseRight.Right - 100
                && baseLeft.Bottom < baseBottom.Bottom - 80,
                $"{priority} 固定基线的三个工具窗格均真实可见且几何不对称。");

            Manager.LayoutPriority = priority;
            Manager.ToggleAnchorable(trigger, DockZone.RightTop);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!trigger.IsAutoHidden, $"{priority} 的自动隐藏触发工具已展开。");

            Rect visibleLeft = Bounds(leftPane);
            Rect visibleBottom = Bounds(bottomPane);
            Rect visibleRight = Bounds(rightPane);
            Rect visibleDocument = Bounds(documentPane);
            checks.Record($"{priority} 展开后屏幕边界：L={Format(visibleLeft)} D={Format(visibleDocument)} B={Format(visibleBottom)} R={Format(visibleRight)}；根方向={root.RootPanel.Orientation}。");

            const double gap = 20;
            switch (priority)
            {
                case DockLayoutPriority.Default:
                    SampleChecks.Require(visibleBottom.Right <= visibleRight.Left + gap
                        && visibleRight.Bottom > visibleBottom.Bottom - gap
                        && visibleLeft.Bottom <= visibleBottom.Top + gap,
                        "Default 保留底部仅跨左块、右侧全高的基线几何。");
                    break;
                case DockLayoutPriority.BottomFullWidth:
                    SampleChecks.Require(visibleBottom.Left <= visibleLeft.Left + gap
                        && visibleBottom.Right >= visibleRight.Right - gap
                        && visibleRight.Bottom <= visibleBottom.Top + gap,
                        "BottomFullWidth 的底部窗格跨左右全宽，右侧窗格止于底部上沿。");
                    break;
                case DockLayoutPriority.SidesFullHeight:
                    SampleChecks.Require(visibleLeft.Bottom >= visibleBottom.Bottom - gap
                        && visibleRight.Bottom >= visibleBottom.Bottom - gap
                        && visibleBottom.Left >= visibleLeft.Right - gap
                        && visibleBottom.Right <= visibleRight.Left + gap,
                        "SidesFullHeight 的左右侧栏全高，底部窗格仅在侧栏之间。");
                    break;
            }
        }

        checks.Record("Toggle 三种布局优先级在同一基线的自动隐藏展开后具有不同的可视屏幕边界。");

        LayoutAnchorable Tool(string title, DockZone zone, bool open = true)
        {
            WorkspaceTool content = new()
            {
                Id = $"priority-{title}",
                Title = title,
                Zone = zone,
                IsOpenByDefault = open,
                Text = $"{title}：受控布局优先级场景。"
            };
            return new LayoutAnchorable { ContentId = content.Id, Title = title, Content = content };
        }

        Rect Bounds(object pane)
        {
            FrameworkElement view = pane switch
            {
                LayoutAnchorablePane anchorable => PageRoot.FindVisualChildren<LayoutAnchorablePaneControl>()
                    .Single(control => ReferenceEquals(control.Model, anchorable)),
                LayoutDocumentPane document => PageRoot.FindVisualChildren<LayoutDocumentPaneControl>()
                    .Single(control => ReferenceEquals(control.Model, document)),
                _ => throw new InvalidOperationException("优先级场景遇到未知窗格。")
            };
            return view.TransformToVisual(Manager).TransformBounds(new Rect(0, 0, view.ActualWidth, view.ActualHeight));
        }

        static string Format(Rect bounds) => $"({bounds.Left:F0},{bounds.Top:F0})–({bounds.Right:F0},{bounds.Bottom:F0})";
    }
}
