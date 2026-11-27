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
}
