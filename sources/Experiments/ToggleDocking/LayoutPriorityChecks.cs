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

            LayoutAnchorable left = Tool("Left Baseline", DockZone.LeftTop);
            LayoutAnchorable bottom = Tool("Bottom Baseline", DockZone.BottomLeft);
            LayoutAnchorable right = Tool("Right Baseline", DockZone.RightTop);
            LayoutAnchorable trigger = Tool("Expansion Trigger", DockZone.RightTop, open: false);
            LayoutDocument document = new()
            {
                ContentId = "priority-document",
                Title = "Priority Document",
                Content = new WorkspaceDocument
                {
                    Id = "priority-document",
                    Title = "Priority Document",
                    Text = "The same baseline layout tests three priorities when a tool expands."
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
                $"{priority} uses the fixed H(V(H(L,D),B),R) baseline with an auto-hidden trigger tool.");

            Rect baseLeft = Bounds(leftPane);
            Rect baseBottom = Bounds(bottomPane);
            Rect baseRight = Bounds(rightPane);
            checks.Record($"{priority} bounds before expansion: L={Format(baseLeft)} B={Format(baseBottom)} R={Format(baseRight)}.");
            SampleChecks.Require(baseLeft.Width > 100 && baseBottom.Height > 80 && baseRight.Width > 100
                && baseBottom.Right < baseRight.Right - 100
                && baseLeft.Bottom < baseBottom.Bottom - 80,
                $"{priority} shows three visible tool panes with asymmetric geometry in the fixed baseline.");

            Manager.LayoutPriority = priority;
            Manager.ToggleAnchorable(trigger, DockZone.RightTop);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!trigger.IsAutoHidden, $"{priority} expands the auto-hidden trigger tool.");

            Rect visibleLeft = Bounds(leftPane);
            Rect visibleBottom = Bounds(bottomPane);
            Rect visibleRight = Bounds(rightPane);
            Rect visibleDocument = Bounds(documentPane);
            checks.Record($"{priority} bounds after expansion: L={Format(visibleLeft)} D={Format(visibleDocument)} B={Format(visibleBottom)} R={Format(visibleRight)}; root orientation={root.RootPanel.Orientation}.");

            const double gap = 20;
            switch (priority)
            {
                case DockLayoutPriority.Default:
                    SampleChecks.Require(visibleBottom.Right <= visibleRight.Left + gap
                        && visibleRight.Bottom > visibleBottom.Bottom - gap
                        && visibleLeft.Bottom <= visibleBottom.Top + gap,
                        "Default preserves the baseline with the bottom pane under the left block and a full-height right pane.");
                    break;
                case DockLayoutPriority.BottomFullWidth:
                    SampleChecks.Require(visibleBottom.Left <= visibleLeft.Left + gap
                        && visibleBottom.Right >= visibleRight.Right - gap
                        && visibleRight.Bottom <= visibleBottom.Top + gap,
                        "BottomFullWidth spans the bottom pane across the full width and ends the right pane above it.");
                    break;
                case DockLayoutPriority.SidesFullHeight:
                    SampleChecks.Require(visibleLeft.Bottom >= visibleBottom.Bottom - gap
                        && visibleRight.Bottom >= visibleBottom.Bottom - gap
                        && visibleBottom.Left >= visibleLeft.Right - gap
                        && visibleBottom.Right <= visibleRight.Left + gap,
                        "SidesFullHeight keeps both sidebars full height with the bottom pane between them.");
                    break;
            }
        }

        checks.Record("Toggle layout priorities produce distinct visible bounds after auto-hide expansion from the same baseline.");

        LayoutAnchorable Tool(string title, DockZone zone, bool open = true)
        {
            WorkspaceTool content = new()
            {
                Id = $"priority-{title}",
                Title = title,
                Zone = zone,
                IsOpenByDefault = open,
                Text = $"{title}: controlled layout-priority scenario."
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
                _ => throw new InvalidOperationException("Unknown pane in layout-priority scenario.")
            };
            return view.TransformToVisual(Manager).TransformBounds(new Rect(0, 0, view.ActualWidth, view.ActualHeight));
        }

        static string Format(Rect bounds) => $"({bounds.Left:F0},{bounds.Top:F0})–({bounds.Right:F0},{bounds.Bottom:F0})";
    }
}
