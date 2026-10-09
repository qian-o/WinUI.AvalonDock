using System.Collections.ObjectModel;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Verification.PackageConsumer;

public sealed class MainWindow : Window
{
    private readonly Grid root = new();
    private readonly TextBlock status = new() { Margin = new Thickness(12, 4, 12, 8) };
    private readonly ObservableCollection<WorkspaceDocument> documents = [];
    private readonly ObservableCollection<WorkspaceTool> tools = [];
    private byte[]? savedLayout;
    internal DockingManager Manager { get; } = new();

    public MainWindow()
    {
        Title = "WinUI.AvalonDock package consumer";
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        StackPanel actions = new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12) };
        AddAction("Float document", () => FirstDocument()?.Float());
        AddAction("Dock document", () => FirstDocument()?.Dock());
        AddAction("Save layout", () =>
        {
            savedLayout = SaveLayout();
            status.Text = "XML layout saved in memory.";
        });
        AddAction("Restore layout", () =>
        {
            if (savedLayout != null)
            {
                RestoreLayout(savedLayout);
                status.Text = "XML layout restored using stable ContentId values.";
            }
        });
        AddAction("Theme", () => root.RequestedTheme = root.RequestedTheme == ElementTheme.Dark
            ? ElementTheme.Light : ElementTheme.Dark);
        root.Children.Add(actions);
        Grid.SetRow(Manager, 1);
        root.Children.Add(Manager);
        Grid.SetRow(status, 2);
        root.Children.Add(status);
        Content = root;

        Manager.LayoutItemTemplate = (DataTemplate)Application.Current.Resources["ConsumerContentTemplate"];
        Manager.LayoutItemContainerStyleSelector = CreateItemStyleSelector();
        Manager.DocumentsSource = documents;
        Manager.AnchorablesSource = tools;
        // As in the upstream collection-based sample, the consumer owns collection removal.
        Manager.DocumentClosed += (_, args) =>
        {
            if (args.Document.Content is WorkspaceDocument document) { documents.Remove(document); }
        };
        documents.Add(new WorkspaceDocument
        {
            Id = "consumer-editor", Title = "Editor", Text = "This host references the local NuGet package, with no library ProjectReference.", IsModified = false
        });
        documents.Add(new WorkspaceDocument
        {
            Id = "consumer-notes", Title = "Notes", Text = "Drag, float, dock, edit, save, and restore the layout.", IsModified = false
        });
        tools.Add(new WorkspaceTool { Id = "consumer-tool", Title = "Explorer", Text = "Consumer-owned tool model.", Zone = DockZone.LeftTop });
        status.Text = $"Package consumer · {typeof(DockingManager).Assembly.GetName().Name}";
        Closed += (_, _) => Manager.Dispose();
        BoundarySmokeTests.StartIfRequested(this, Manager);
        PackageSmokeTests.StartIfRequested(this);

        void AddAction(string label, Action action)
        {
            Button button = new() { Content = label };
            button.Click += (_, _) => action();
            actions.Children.Add(button);
        }
    }

    internal WorkspaceDocument FirstModel => documents[0];
    internal WorkspaceDocument SecondModel => documents[1];
    internal WorkspaceTool ToolModel => tools[0];

    internal static StyleSelector CreateItemStyleSelector() => new ConsumerItemStyleSelector();

    internal LayoutDocument? FirstDocument() => Manager.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault();

    internal void ReplaceFirst(WorkspaceDocument replacement) => documents[0] = replacement;

    internal byte[] SaveLayout()
    {
        using MemoryStream stream = new();
        new XmlLayoutSerializer(Manager).Serialize(stream);
        return stream.ToArray();
    }

    internal void RestoreLayout(byte[] layout)
    {
        Dictionary<string, object> content = documents.ToDictionary(model => model.Id, model => (object)model);
        foreach (WorkspaceTool model in tools)
        {
            content[model.Id] = model;
        }
        XmlLayoutSerializer serializer = new(Manager);
        serializer.LayoutSerializationCallback += (_, args) =>
        {
            if (args.Model.ContentId is { } id && content.TryGetValue(id, out object? model))
            {
                args.Content = model;
            }
        };
        using MemoryStream stream = new(layout);
        serializer.Deserialize(stream);
    }

    private sealed class ConsumerItemStyleSelector : StyleSelector
    {
        private readonly Style document = CreateStyle(typeof(LayoutDocumentItem));
        private readonly Style tool = CreateStyle(typeof(LayoutAnchorableItem));

        protected override Style SelectStyleCore(object item, DependencyObject container)
            => container is LayoutAnchorableItem ? tool : document;

        private static Style CreateStyle(Type type)
        {
            Style style = new(type);
            style.Setters.Add(new Setter(LayoutItem.TitleProperty, new Binding { Path = new PropertyPath("Model.Title") }));
            style.Setters.Add(new Setter(LayoutItem.ContentIdProperty, new Binding { Path = new PropertyPath("Model.Id") }));
            return style;
        }
    }
}
