using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.Mvvm;

internal static class SourceCollectionChecks
{
    public static void Run(SampleChecks checks)
    {
        using DockingManager manager = new();
        CheckSource<LayoutDocument>(manager, source => manager.DocumentsSource = source);
        CheckSource<LayoutAnchorable>(manager, source => manager.AnchorablesSource = source);
        checks.Record("Document and tool sources preserve distinct equal models, removal order, Reset node identity, and detached subscriptions");

        CheckDockLayout(manager);
        checks.Record("DockLayout bridge preserves distinct equal models, replacement alignment, Reset node identity, and original collection detachment");
    }

    private static void CheckSource<T>(DockingManager manager, Action<IEnumerable?> setSource) where T : LayoutContent
    {
        EqualDockable first = CreateModel("first");
        EqualDockable second = CreateModel("second");
        EqualDockable replacement = CreateModel("replacement");
        ResetCollection<object> source = [first, second];
        setSource(source);
        T firstNode = Find<T>(manager, first);
        T secondNode = Find<T>(manager, second);
        bool contentClearedBeforeParentChange = false;
        firstNode.PropertyChanging += (_, args) =>
        {
            if (args.PropertyName == nameof(LayoutContent.Parent))
            {
                contentClearedBeforeParentChange = firstNode.Content == null;
            }
        };

        source[0] = replacement;
        T replacementNode = Find<T>(manager, replacement);
        SampleChecks.Require(firstNode.Parent == null && firstNode.Content == null
            && contentClearedBeforeParentChange && ReferenceEquals(Find<T>(manager, second), secondNode),
            $"{typeof(T).Name} Replace clears the removed content before parent notifications and keeps the equal sibling node.");

        source.ResetTo(second, replacement);
        SampleChecks.Require(ReferenceEquals(Find<T>(manager, second), secondNode)
            && ReferenceEquals(Find<T>(manager, replacement), replacementNode),
            $"{typeof(T).Name} Reset retains both existing node instances when equal model references remain.");
        source.ResetTo(second);
        SampleChecks.Require(replacementNode.Parent == null && ReferenceEquals(replacementNode.Content, replacement)
            && ReferenceEquals(Find<T>(manager, second), secondNode),
            $"{typeof(T).Name} Reset removes only the obsolete reference and retains its Content on the detached node.");

        setSource(null);
        source.Add(first);
        SampleChecks.Require(secondNode.Parent == null && ReferenceEquals(secondNode.Content, second)
            && !manager.Layout.Descendents().OfType<T>().Any(),
            $"{typeof(T).Name} source detachment retains node Content and releases the old collection subscription.");
    }

    private static void CheckDockLayout(DockingManager manager)
    {
        EqualDockable firstDocument = CreateModel("bridge-document-first");
        EqualDockable secondDocument = CreateModel("bridge-document-second");
        EqualDockable newDocument = CreateModel("bridge-document-replacement");
        EqualDockable firstTool = CreateModel("bridge-tool-first");
        EqualDockable secondTool = CreateModel("bridge-tool-second");
        EqualDockable newTool = CreateModel("bridge-tool-replacement");
        ResetCollection<IDockable> documents = [firstDocument, secondDocument];
        ResetCollection<IDockable> tools = [firstTool, secondTool];
        DocumentDock documentDock = new() { VisibleDockables = documents };
        ToolDock toolDock = new() { Alignment = DockAlignment.Left, VisibleDockables = tools };
        ToolDock rightDock = new()
        {
            Alignment = DockAlignment.Right,
            VisibleDockables = new ObservableCollection<IDockable> { CreateModel("bridge-tool-right") }
        };
        RootDock root = new() { VisibleDockables = new ObservableCollection<IDockable> { documentDock, toolDock, rightDock } };
        manager.DockLayout = root;
        ObservableCollection<object> bridgeDocuments = (ObservableCollection<object>)manager.DocumentsSource!;
        ObservableCollection<object> bridgeTools = (ObservableCollection<object>)manager.AnchorablesSource!;
        LayoutDocument retainedDocument = Find<LayoutDocument>(manager, secondDocument);
        LayoutAnchorable retainedTool = Find<LayoutAnchorable>(manager, secondTool);
        SampleChecks.Require(manager.Layout.Descendents().OfType<LayoutDocument>().Count() == 2
            && manager.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 3,
            "The bridge imports equal but distinct document and tool models separately.");

        documents[0] = newDocument;
        tools[0] = newTool;
        SampleChecks.Require(ReferenceEquals(Find<LayoutDocument>(manager, secondDocument), retainedDocument)
            && ReferenceEquals(Find<LayoutAnchorable>(manager, secondTool), retainedTool)
            && Find<LayoutAnchorable>(manager, newTool).FindParent<LayoutAnchorSide>()?.Side == AnchorSide.Left,
            "Bridge Replace retains equal sibling nodes and publishes tool alignment before source insertion.");
        documents.ResetTo(secondDocument, newDocument);
        tools.ResetTo(secondTool, newTool);
        SampleChecks.Require(ReferenceEquals(Find<LayoutDocument>(manager, secondDocument), retainedDocument)
            && ReferenceEquals(Find<LayoutAnchorable>(manager, secondTool), retainedTool),
            "Bridge Reset reconciles by reference without recreating retained document or tool nodes.");

        // A view model can replace its collections before the old bridge is detached.
        // Detachment must still unsubscribe the exact collections observed at attachment.
        documentDock.VisibleDockables = new ObservableCollection<IDockable>();
        toolDock.VisibleDockables = new ObservableCollection<IDockable>();
        manager.DockLayout = null;
        documents.Add(firstDocument);
        tools.Add(firstTool);
        SampleChecks.Require(!manager.Layout.Descendents().OfType<LayoutContent>().Any()
            && bridgeDocuments.Count == 0 && bridgeTools.Count == 0
            && manager.LayoutUpdateStrategy == null,
            "Detaching DockLayout releases the original collection subscriptions and its installed alignment strategy.");
    }

    private static T Find<T>(DockingManager manager, object model) where T : LayoutContent =>
        manager.Layout.Descendents().OfType<T>().Single(item => ReferenceEquals(item.Content, model));

    private static EqualDockable CreateModel(string id) => new() { Id = id, Title = id };

    private sealed class EqualDockable : DockableBase
    {
        public override bool Equals(object? obj) => obj is EqualDockable;
        public override int GetHashCode() => 0;
    }

    private sealed class ResetCollection<T> : ObservableCollection<T>
    {
        public void ResetTo(params T[] items)
        {
            Items.Clear();
            foreach (T item in items)
            {
                Items.Add(item);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
