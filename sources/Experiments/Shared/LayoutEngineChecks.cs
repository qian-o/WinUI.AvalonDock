using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using Microsoft.UI.Xaml.Controls;

namespace WinUI.AvalonDock.Experiments.Shared;

internal static class LayoutEngineChecks
{
    internal static void Run(SampleChecks checks)
    {
        foreach (ILayoutEngine engine in new ILayoutEngine[] { new DefaultLayoutEngine(), new ToggleLayoutEngine() })
        {
            foreach (AnchorSide side in Enum.GetValues<AnchorSide>())
            {
                foreach (Orientation initialOrientation in Enum.GetValues<Orientation>())
                {
                    CheckSideInsertion(engine, side, initialOrientation);
                }
            }
        }

        foreach (DockZone zone in Enum.GetValues<DockZone>())
        {
            CheckZoneInsertion(zone);
        }
        CheckNestedContentPanel();
        checks.Record("Classic/Toggle layout engines preserve four-side insertion, wrapper notifications, six-zone order, and existing nested content panels");
    }

    private static void CheckSideInsertion(ILayoutEngine engine, AnchorSide side, Orientation initialOrientation)
    {
        LayoutDocumentPane document = new(new LayoutDocument { Title = "Document" });
        LayoutPanel initial = new(document) { Orientation = initialOrientation };
        LayoutRoot root = new() { RootPanel = initial };
        LayoutAnchorablePane pane = new(new LayoutAnchorable { Title = "Tool" });
        int rootChanges = 0;
        bool wrapperPublishedEmpty = false;
        root.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LayoutRoot.RootPanel))
            {
                rootChanges++;
                wrapperPublishedEmpty = root.RootPanel.Children.Count == 0;
            }
        };

        engine.InsertPane(root, pane, side);

        bool horizontal = side is AnchorSide.Left or AnchorSide.Right;
        bool atStart = side is AnchorSide.Left or AnchorSide.Top;
        Orientation expected = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        // Toggle side panes use a content panel inside an existing vertical root.
        bool nestedContent = engine is ToggleLayoutEngine && horizontal && initialOrientation != expected;
        LayoutPanel parent = (LayoutPanel)pane.Parent!;
        SampleChecks.Require(parent.Orientation == expected && parent.Children.Count == 2
            && ReferenceEquals(parent.Children[atStart ? 0 : 1], pane)
            && ReferenceEquals(pane.Root, root) && ReferenceEquals(document.Root, root),
            $"{engine.GetType().Name} {side}/{initialOrientation} preserves pane order and root ownership.");

        if (initialOrientation == expected)
        {
            SampleChecks.Require(ReferenceEquals(root.RootPanel, initial) && rootChanges == 0,
                "Matching orientation inserts into the original root without replacing it.");
        }
        else if (nestedContent)
        {
            SampleChecks.Require(ReferenceEquals(root.RootPanel, initial) && ReferenceEquals(parent.Parent, initial)
                && ReferenceEquals(document.Parent, parent) && rootChanges == 0,
                "Toggle side insertion wraps the content child inside the original root.");
        }
        else
        {
            SampleChecks.Require(ReferenceEquals(parent, root.RootPanel)
                && ReferenceEquals(parent.Children[atStart ? 1 : 0], initial)
                && rootChanges == 1 && wrapperPublishedEmpty,
                "Root wrapping publishes the empty new root before transferring its children.");
        }
    }

    private static void CheckZoneInsertion(DockZone zone)
    {
        LayoutAnchorablePaneGroup leading = new(new LayoutAnchorablePane(new LayoutAnchorable { Title = "Leading" }));
        LayoutAnchorablePaneGroup trailing = new(new LayoutAnchorablePane(new LayoutAnchorable { Title = "Trailing" }));
        LayoutDocumentPane document = new(new LayoutDocument { Title = "Document" });
        LayoutPanel panel = new(leading)
        {
            Orientation = zone is DockZone.BottomLeft or DockZone.BottomRight ? Orientation.Vertical : Orientation.Horizontal
        };
        panel.Children.Add(document);
        panel.Children.Add(trailing);
        LayoutRoot root = new() { RootPanel = panel };
        LayoutAnchorablePane pane = new(new LayoutAnchorable { Title = "Inserted" });
        new ToggleLayoutEngine().InsertPaneForZone(root, pane, zone);

        int expectedIndex = zone switch
        {
            DockZone.LeftTop => 0,
            DockZone.LeftBottom => 1,
            DockZone.RightTop or DockZone.BottomLeft => 2,
            _ => 3
        };
        SampleChecks.Require(ReferenceEquals(root.RootPanel, panel) && panel.Children.IndexOf(pane) == expectedIndex
            && ReferenceEquals(pane.Root, root) && panel.Children.Where(child => !ReferenceEquals(child, pane))
                .SequenceEqual(new ILayoutPanelElement[] { leading, document, trailing }),
            $"{zone} preserves pane-group boundaries and the relative order of existing children.");
    }

    private static void CheckNestedContentPanel()
    {
        LayoutDocumentPane document = new(new LayoutDocument { Title = "Document" });
        LayoutPanel content = new(document) { Orientation = Orientation.Horizontal };
        LayoutPanel initial = new(content) { Orientation = Orientation.Vertical };
        LayoutRoot root = new() { RootPanel = initial };
        LayoutAnchorablePane left = new(new LayoutAnchorable { Title = "Left" });
        LayoutAnchorablePane right = new(new LayoutAnchorable { Title = "Right" });
        ToggleLayoutEngine engine = new();
        engine.InsertPaneForZone(root, left, DockZone.LeftBottom);
        engine.InsertPaneForZone(root, right, DockZone.RightTop);
        SampleChecks.Require(ReferenceEquals(root.RootPanel, initial) && ReferenceEquals(content.Parent, initial)
            && content.Children.SequenceEqual(new ILayoutPanelElement[] { left, document, right }),
            "Side zones reuse the existing nested content panel and retain the outer layout tree.");

        LayoutAnchorablePane bottom = new(new LayoutAnchorable { Title = "Bottom" });
        LayoutRoot horizontalRoot = new() { RootPanel = new LayoutPanel(new LayoutDocumentPane()) { Orientation = Orientation.Horizontal } };
        LayoutPanel former = horizontalRoot.RootPanel;
        engine.InsertPaneForZone(horizontalRoot, bottom, DockZone.BottomLeft);
        SampleChecks.Require(horizontalRoot.RootPanel.Orientation == Orientation.Vertical
            && horizontalRoot.RootPanel.Children.SequenceEqual(new ILayoutPanelElement[] { former, bottom }),
            "The first bottom zone wraps a horizontal root with the content before the tool pane.");
    }
}
