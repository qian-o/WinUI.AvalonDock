using System.Reflection;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.AvalonDock.Experiments.Shared;
using Windows.Foundation;

namespace WinUI.AvalonDock.Experiments.Docking;

/// <summary>Checks split-group shape and source order beyond single-pane drag scenarios.</summary>
internal static class DockingStructureChecks
{
    private sealed record Edge(DropTargetType Manager, DropTargetType Tool, DropTargetType Document,
        Orientation Orientation, bool After);

    private static readonly Edge[] Edges =
    [
        new(DropTargetType.DockingManagerDockLeft, DropTargetType.AnchorablePaneDockLeft,
            DropTargetType.DocumentPaneDockLeft, Orientation.Horizontal, false),
        new(DropTargetType.DockingManagerDockRight, DropTargetType.AnchorablePaneDockRight,
            DropTargetType.DocumentPaneDockRight, Orientation.Horizontal, true),
        new(DropTargetType.DockingManagerDockTop, DropTargetType.AnchorablePaneDockTop,
            DropTargetType.DocumentPaneDockTop, Orientation.Vertical, false),
        new(DropTargetType.DockingManagerDockBottom, DropTargetType.AnchorablePaneDockBottom,
            DropTargetType.DocumentPaneDockBottom, Orientation.Vertical, true),
    ];

    internal static void Run(SampleChecks checks)
    {
        foreach (Edge edge in Edges)
        {
            CheckManager(edge, alignedParent: true, alignedSource: true, singleParent: false);
            CheckManager(edge, alignedParent: true, alignedSource: false, singleParent: false);
            CheckManager(edge, alignedParent: false, alignedSource: true, singleParent: true);
            CheckManager(edge, alignedParent: false, alignedSource: true, singleParent: false);

            CheckToolPane(edge, alignedParent: true, alignedSource: true, singleParent: false, singleSource: false);
            CheckToolPane(edge, alignedParent: true, alignedSource: false, singleParent: false, singleSource: false);
            CheckToolPane(edge, alignedParent: true, alignedSource: false, singleParent: false, singleSource: true);
            CheckToolPane(edge, alignedParent: false, alignedSource: true, singleParent: true, singleSource: false);
            CheckToolPane(edge, alignedParent: false, alignedSource: true, singleParent: false, singleSource: false);

            CheckDocumentPane(edge, toolSource: false, directParent: false, alignedParent: true, allowMixed: true);
            CheckDocumentPane(edge, toolSource: false, directParent: false, alignedParent: false, allowMixed: true);
            CheckDocumentPane(edge, toolSource: false, directParent: false, alignedParent: false, allowMixed: false);
            CheckDocumentPane(edge, toolSource: false, directParent: true, alignedParent: false, allowMixed: true);
            CheckDocumentPane(edge, toolSource: true, directParent: false, alignedParent: true, allowMixed: true);
            CheckDocumentPane(edge, toolSource: true, directParent: false, alignedParent: false, allowMixed: true);
            CheckDocumentPane(edge, toolSource: true, directParent: false, alignedParent: false, allowMixed: false);
            CheckDocumentPane(edge, toolSource: true, directParent: true, alignedParent: false, allowMixed: true);
        }

        checks.Record("All four docking directions preserve pane order, group flattening/wrapping, split sizes, and mixed-orientation policy across 68 structural scenarios.");
    }

    private static void CheckManager(Edge edge, bool alignedParent, bool alignedSource, bool singleParent)
    {
        LayoutDocumentPane first = DocumentPane("existing-1");
        LayoutDocumentPane second = DocumentPane("existing-2");
        LayoutPanel originalRoot = new() { Orientation = alignedParent ? edge.Orientation : Other(edge.Orientation) };
        originalRoot.Children.Add(first);
        if (!singleParent)
        {
            originalRoot.Children.Add(second);
        }

        using DockingManager manager = new() { Layout = new LayoutRoot { RootPanel = originalRoot } };
        LayoutAnchorablePaneGroup source = ToolGroup(alignedSource ? edge.Orientation : Other(edge.Orientation), 2);
        ILayoutAnchorablePane[] sourcePanes = source.Children.ToArray();
        LayoutAnchorableFloatingWindow window = new() { RootPanel = source };
        InvokeDrop("DockingManagerDropTarget", manager, edge.Manager, window);

        ILayoutPanelElement[] existing = singleParent ? [first] : [first, second];
        if (!alignedParent && !singleParent)
        {
            SampleChecks.Require(manager.Layout.RootPanel.Orientation == edge.Orientation,
                $"{edge.Manager}: the replacement root has the chosen orientation.");
            RequireOrder(manager.Layout.RootPanel.Children, edge.After ? [originalRoot, source] : [source, originalRoot],
                $"{edge.Manager}: an orthogonal multi-pane root remains grouped.");
            RequireOrder(originalRoot.Children, existing, $"{edge.Manager}: existing panes retain their internal order.");
        }
        else
        {
            SampleChecks.Require(ReferenceEquals(manager.Layout.RootPanel, originalRoot)
                && originalRoot.Orientation == edge.Orientation, $"{edge.Manager}: the current root is retained.");
            ILayoutPanelElement[] transferred = alignedSource ? sourcePanes.Cast<ILayoutPanelElement>().ToArray() : [source];
            RequireOrder(originalRoot.Children, edge.After ? [.. existing, .. transferred] : [.. transferred, .. existing],
                $"{edge.Manager}: matching groups flatten in source order; orthogonal groups remain intact.");
        }
    }

    private static void CheckToolPane(Edge edge, bool alignedParent, bool alignedSource, bool singleParent, bool singleSource)
    {
        LayoutAnchorablePane target = ToolPane("target");
        target.DockWidth = new GridLength(237);
        target.DockHeight = new GridLength(149);
        LayoutAnchorablePane sibling = ToolPane("sibling");
        LayoutAnchorablePaneGroup parent = new() { Orientation = alignedParent ? edge.Orientation : Other(edge.Orientation) };
        parent.Children.Add(target);
        if (!singleParent)
        {
            parent.Children.Add(sibling);
        }

        // This target needs only a model tree; no manager or native floating window
        // is attached, so the structural checks cannot open background windows.
        LayoutRoot root = new() { RootPanel = new LayoutPanel(parent) };
        LayoutAnchorablePaneGroup source = ToolGroup(alignedSource ? edge.Orientation : Other(edge.Orientation), singleSource ? 1 : 2);
        ILayoutAnchorablePane[] sourcePanes = source.Children.ToArray();
        FrameworkElement control = CreatePaneControl(typeof(LayoutAnchorablePaneControl), target);
        InvokeDrop("AnchorablePaneDropTarget", control, edge.Tool, new LayoutAnchorableFloatingWindow { RootPanel = source });

        if (!alignedParent && !singleParent)
        {
            LayoutAnchorablePaneGroup wrapper = target.Parent as LayoutAnchorablePaneGroup
                ?? throw new InvalidOperationException($"{edge.Tool}: the split wrapper is missing.");
            SampleChecks.Require(!ReferenceEquals(wrapper, parent) && wrapper.Orientation == edge.Orientation
                && wrapper.DockWidth == target.DockWidth && wrapper.DockHeight == target.DockHeight,
                $"{edge.Tool}: the new split group inherits the target dimensions.");
            RequireOrder(wrapper.Children, edge.After ? [target, source] : [source, target],
                $"{edge.Tool}: the original target and floating group preserve relative order.");
            RequireOrder(parent.Children, [wrapper, sibling], $"{edge.Tool}: the split occupies the target's former position.");
        }
        else
        {
            SampleChecks.Require(parent.Orientation == edge.Orientation && ReferenceEquals(target.Parent, parent),
                $"{edge.Tool}: the original pane group is retained.");
            ILayoutAnchorablePane[] transferred = alignedSource || singleSource ? sourcePanes : [source];
            ILayoutAnchorablePane[] expected = edge.After ? [target, .. transferred] : [.. transferred, target];
            if (!singleParent)
            {
                expected = [.. expected, sibling];
            }

            RequireOrder(parent.Children, expected, $"{edge.Tool}: source panes and untouched siblings retain their order.");
        }

        GC.KeepAlive(root);
    }

    private static void CheckDocumentPane(Edge edge, bool toolSource, bool directParent, bool alignedParent, bool allowMixed)
    {
        LayoutDocumentPane target = DocumentPane("target");
        target.DockWidth = new GridLength(237);
        target.DockHeight = new GridLength(149);
        LayoutDocumentPane sibling = DocumentPane("sibling");
        LayoutDocumentPaneGroup parent = new() { Orientation = alignedParent ? edge.Orientation : Other(edge.Orientation) };
        LayoutPanel panel = new();
        if (directParent)
        {
            panel.Children.Add(target);
        }
        else
        {
            parent.Children.Add(target);
            parent.Children.Add(sibling);
            panel.Children.Add(parent);
        }

        using DockingManager manager = new()
        {
            Layout = new LayoutRoot { RootPanel = panel },
            AllowMixedOrientation = allowMixed,
        };
        FrameworkElement control = CreatePaneControl(typeof(LayoutDocumentPaneControl), target);
        LayoutFloatingWindow window;
        ILayoutDocumentPane transferred;
        if (toolSource)
        {
            LayoutAnchorablePaneGroup source = ToolGroup(edge.Orientation, 2);
            LayoutAnchorable[] contents = source.Descendents().OfType<LayoutAnchorable>().ToArray();
            window = new LayoutAnchorableFloatingWindow { RootPanel = source };
            InvokeDrop("DocumentPaneDropTarget", control, edge.Document, window);
            transferred = contents[0].Parent as ILayoutDocumentPane
                ?? throw new InvalidOperationException($"{edge.Document}: the tool did not enter a document pane.");
            SampleChecks.Require(contents.All(content => ReferenceEquals(content.Parent, transferred)),
                $"{edge.Document}: all imported tools share the destination pane.");
            RequireOrder(((LayoutDocumentPane)transferred).Children.Select(content => content.ContentId), ["source-0", "source-1"],
                $"{edge.Document}: tool contents retain their tab order when transferred into a document pane.");
        }
        else
        {
            LayoutDocumentPaneGroup source = new() { Orientation = Other(edge.Orientation) };
            source.Children.Add(DocumentPane("source-0"));
            source.Children.Add(DocumentPane("source-1"));
            window = new LayoutDocumentFloatingWindow { RootPanel = source };
            transferred = source;
            InvokeDrop("DocumentPaneDropTarget", control, edge.Document, window);
        }

        LayoutDocumentPaneGroup result = target.Parent as LayoutDocumentPaneGroup
            ?? throw new InvalidOperationException($"{edge.Document}: the resulting document group is missing.");
        SampleChecks.Require(result.Orientation == edge.Orientation,
            $"{edge.Document}: the resulting document split has the chosen orientation.");
        ILayoutDocumentPane[] expected = edge.After ? [target, transferred] : [transferred, target];
        if (!directParent && (alignedParent || !allowMixed))
        {
            SampleChecks.Require(ReferenceEquals(parent, result), $"{edge.Document}: the existing document group is retained.");
            expected = [.. expected, sibling];
        }
        else if (!directParent)
        {
            RequireOrder(parent.Children, [result, sibling], $"{edge.Document}: the mixed split replaces only the target pane.");
            SampleChecks.Require(parent.Orientation == Other(edge.Orientation), $"{edge.Document}: the surrounding group keeps its orientation.");
        }

        RequireOrder(result.Children, expected, $"{edge.Document}: source and target document groups retain their relative order.");
        if (!toolSource && (directParent || !alignedParent && allowMixed))
        {
            SampleChecks.Require(result.DockWidth == target.DockWidth && result.DockHeight == target.DockHeight,
                $"{edge.Document}: wrapping a document pane retains its split dimensions.");
        }
    }

    private static LayoutDocumentPane DocumentPane(string id)
        => new(new LayoutDocument { ContentId = id, Title = id });

    private static LayoutAnchorablePane ToolPane(string id)
        => new(new LayoutAnchorable { ContentId = id, Title = id });

    private static LayoutAnchorablePaneGroup ToolGroup(Orientation orientation, int count)
    {
        LayoutAnchorablePaneGroup result = new() { Orientation = orientation };
        for (int i = 0; i < count; i++)
        {
            result.Children.Add(ToolPane($"source-{i}"));
        }

        return result;
    }

    private static Orientation Other(Orientation orientation)
        => orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;

    private static FrameworkElement CreatePaneControl(Type type, ILayoutElement model)
        => (FrameworkElement)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
            [model, false, false], null)!;

    private static void InvokeDrop(string targetName, FrameworkElement control, DropTargetType type, LayoutFloatingWindow window)
    {
        Type targetType = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls." + targetName, throwOnError: true)!;
        object target = Activator.CreateInstance(targetType, BindingFlags.Instance | BindingFlags.NonPublic, null,
            [control, new Rect(0, 0, 20, 20), type], null)!;
        MethodInfo drop = targetType.GetMethod("Drop", BindingFlags.Instance | BindingFlags.NonPublic, null,
            [window.GetType()], null)!;
        drop.Invoke(target, [window]);
    }

    private static void RequireOrder<T>(IEnumerable<T> actual, IEnumerable<T> expected, string message)
        => SampleChecks.Require(actual.SequenceEqual(expected), message);
}
