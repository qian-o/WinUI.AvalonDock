using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.Mvvm;

public sealed partial class MainWindow : Window
{
    private byte[]? savedLayout;

    public WorkspaceViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "MVVM Data-Driven Workspace";
        Closed += OnClosed;
        Manager.LayoutItemContainerStyleSelector = SampleStyles.CreateItemStyleSelector();
        Manager.DocumentClosing += OnDocumentClosing;
        Manager.DocumentClosed += OnDocumentClosed;
        SampleChecks.RunWhenLoaded(this, RunScenarioChecksAsync);
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        using MemoryStream output = new();
        new XmlLayoutSerializer(Manager).Serialize(output);
        savedLayout = output.ToArray();
        ViewModel.SetStatus("MVVM layout saved in memory.");
    }

    private void RestoreLayout_Click(object sender, RoutedEventArgs e)
    {
        if (savedLayout is null)
        {
            ViewModel.SetStatus("Save the layout first.");
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
        ViewModel.SetStatus("MVVM layout restored and model content reconnected by ContentId.");
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        PageRoot.RequestedTheme = PageRoot.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        ViewModel.SetStatus(PageRoot.RequestedTheme == ElementTheme.Dark ? "Dark theme." : "Light theme.");
    }

    private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        if (e.Document.Content is WorkspaceDocument document && document.IsModified)
        {
            e.Cancel = true;
            ViewModel.SetStatus($"Closing modified {document.Title} was canceled. Save it first.");
        }
    }

    private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        if (e.Document.Content is WorkspaceDocument document)
        {
            ViewModel.OnDocumentClosed(document);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.Dispose();
    }

    public async Task RunScenarioChecksAsync(SampleChecks checks)
    {
        SampleChecks.Require(ReferenceEquals(Manager.DockLayout, ViewModel.Layout), "DockLayout binds to ViewModel.Layout.");
        SampleChecks.Require(ViewModel.Documents.Count == 2, "Initial document models were created.");
        SampleChecks.Require(ViewModel.Tools.Count == 4, "Initial tool models were created.");
        await SampleChecks.SettleAsync();
        CheckContentCount(checks, ViewModel.Documents, "Initial documents");
        CheckContentCount(checks, ViewModel.Tools, "Initial tools");

        WorkspaceDocument opened = ViewModel.OpenDocument("Scenario Document", "Used to verify opening and active-item synchronization.");
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ReferenceEquals(ViewModel.Layout.ActiveDockable, opened), "Opening a document updates the active model item.");
        SampleChecks.Require(HasContent(opened), "The opened document appears in the manager layout.");
        checks.Record("Document opening and active-item synchronization");

        ViewModel.MarkModifiedCommand.Execute(null);
        SampleChecks.Require(opened.IsModified, "Edit command marks the active document as modified.");
        ViewModel.CloseActiveCommand.Execute(null);
        SampleChecks.Require(ViewModel.Documents.Contains(opened), "The command does not close a modified document.");
        ViewModel.SaveCommand.Execute(null);
        ViewModel.CloseActiveCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!ViewModel.Documents.Contains(opened), "The command closes the document after it is saved.");
        checks.Record("IsModified save and close commands");

        WorkspaceDocument closedFromTab = ViewModel.Documents.First();
        LayoutDocument tabDocument = Manager.Layout.Descendents().OfType<LayoutDocument>()
            .First(item => ReferenceEquals(item.Content, closedFromTab));
        Manager.GetLayoutItemFromModel(tabDocument)?.CloseCommand?.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!ViewModel.Documents.Contains(closedFromTab) && !HasContent(closedFromTab),
            "Closing a document tab removes the same model from the MVVM list and layout tree.");
        ViewModel.NextCommand.Execute(null);
        SampleChecks.Require(ViewModel.ActiveDocument is { } active && ViewModel.Documents.Contains(active) && HasContent(active),
            "Next Document still selects a document in the layout after tab closing.");
        checks.Record("Document tab closing and Next Document model synchronization");

        ViewModel.NextCommand.Execute(null);
        WorkspaceDocument? beforeReplace = ViewModel.ActiveDocument;
        ViewModel.ReplaceCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(beforeReplace is null || !ViewModel.Documents.Contains(beforeReplace), "Replace removes the old document.");
        SampleChecks.Require(ViewModel.ActiveDocument is not null && HasContent(ViewModel.ActiveDocument), "Replace adds the new document.");
        checks.Record("Document Replace command");

        ViewModel.ResetCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ViewModel.Documents.Count == 1, "Reset keeps one initial document.");
        CheckContentCount(checks, ViewModel.Documents, "Documents after Reset");

        ViewModel.AddToolCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ViewModel.Tools.Count == 5, "Add Tool adds a model.");
        CheckContentCount(checks, ViewModel.Tools, "Tools after Add");
        ViewModel.ReplaceToolCommand.Execute(null);
        await SampleChecks.SettleAsync();
        CheckContentCount(checks, ViewModel.Tools, "Tools after Replace");
        ViewModel.ResetToolsCommand.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ViewModel.Tools.Count == 1, "Reset Tools keeps one tool.");
        CheckContentCount(checks, ViewModel.Tools, "Tools after Reset");

        bool originalFloating = ViewModel.Layout.AllowFloatingWindows;
        ViewModel.ToggleFloatingCommand.Execute(null);
        SampleChecks.Require(ViewModel.Layout.AllowFloatingWindows != originalFloating, "The command toggles the floating-window policy.");
        ViewModel.ToggleFloatingCommand.Execute(null);
        SampleChecks.Require(ViewModel.Layout.AllowFloatingWindows == originalFloating, "The floating-window policy can be restored.");
        checks.Record("Tool collection and window policy synchronization");

        SaveLayout_Click(this, new RoutedEventArgs());
        RestoreLayout_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        CheckContentCount(checks, ViewModel.Documents, "Documents after restore");
        checks.Record("MVVM XML layout restore");

        await WorkspacePersistenceChecks.RunAsync(Manager, ViewModel, checks);
    }

    private bool HasContent(object content)
    {
        return Manager.Layout.Descendents().OfType<LayoutContent>().Any(item => ReferenceEquals(item.Content, content));
    }

    private void CheckContentCount<T>(SampleChecks checks, IEnumerable<T> models, string label)
    {
        object[] expected = models.Cast<object>().ToArray();
        IEnumerable<LayoutContent> contents = typeof(T) == typeof(WorkspaceDocument)
            ? Manager.Layout.Descendents().OfType<LayoutDocument>()
            : Manager.Layout.Descendents().OfType<LayoutAnchorable>();
        object[] actual = contents
            .Select(item => item.Content)
            .Where(content => content is not null)
            .Cast<object>()
            .ToArray();
        SampleChecks.Require(actual.Length == expected.Length, $"{label}: layout node count matches the model collection without extra or duplicate items.");
        foreach (object model in expected)
        {
            SampleChecks.Require(actual.Any(content => ReferenceEquals(content, model)), $"{label}: model is present in the layout.");
        }
        checks.Record($"{label}: content references synchronized ({expected.Length})");
    }
}
