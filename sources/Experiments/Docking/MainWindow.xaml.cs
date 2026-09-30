using System.Collections.ObjectModel;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.Docking;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<WorkspaceDocument> documents = [];
    private readonly ObservableCollection<WorkspaceTool> tools = [];
    private byte[]? savedLayout;
    private int nextDocumentNumber = 3;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Docking Classic Workspace";
        Closed += OnClosed;

        Manager.LayoutItemContainerStyleSelector = SampleStyles.CreateItemStyleSelector();
        Manager.DocumentsSource = documents;
        Manager.AnchorablesSource = tools;
        Manager.DocumentClosing += OnDocumentClosing;
        Manager.DocumentClosed += OnDocumentClosed;
        Manager.AnchorableHiding += OnAnchorableHiding;
        Manager.ContentFloated += OnContentFloated;
        Manager.ContentDocked += OnContentDocked;

        documents.Add(new WorkspaceDocument
        {
            Id = "classic-editor",
            Title = "Editor",
            Text = "This document was imported through DocumentsSource. Mark it as modified from Edit to try cancelable closing.",
            IsModified = false
        });
        documents.Add(new WorkspaceDocument
        {
            Id = "classic-readme",
            Title = "Notes",
            Text = "Drag the tab onto a docking guide, or use the document menu to create a new tab group.",
            IsModified = false
        });
        tools.Add(new WorkspaceTool
        {
            Id = "classic-solution",
            Title = "Solution Explorer",
            Zone = DockZone.LeftTop,
            Text = "Tool panes support hiding, auto hide, floating, and detached windows."
        });
        tools.Add(new WorkspaceTool
        {
            Id = "classic-properties",
            Title = "Properties",
            Zone = DockZone.LeftBottom,
            Text = "A single AnchorablesSource can provide multiple tool models."
        });
        Manager.Loaded += InitializeSampleAutoHideSizes;
    }

    private void InitializeSampleAutoHideSizes(object sender, RoutedEventArgs e)
    {
        Manager.Loaded -= InitializeSampleAutoHideSizes;
        foreach (LayoutAnchorable tool in Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Where(item => item.Content is WorkspaceTool))
        {
            tool.AutoHideWidth = 280;
            tool.AutoHideHeight = 180;
        }
    }

    private LayoutContent? ActiveLayoutContent =>
        Manager.Layout.Descendents().OfType<LayoutContent>()
            .FirstOrDefault(item => ReferenceEquals(item.Content, Manager.ActiveContent))
        ?? Manager.Layout.Descendents().OfType<LayoutContent>().FirstOrDefault(item => item.IsSelected);

    private void AddDocument_Click(object sender, RoutedEventArgs e)
    {
        int number = nextDocumentNumber++;
        WorkspaceDocument document = new()
        {
            Id = $"classic-document-{number}",
            Title = $"Document {number}",
            Text = "A document added at runtime immediately appears in DocumentsSource and the layout tree.",
            IsModified = false
        };
        documents.Add(document);
        Manager.ActiveContent = document;
        StatusText.Text = $"Added {document.Title}.";
    }

    private void ReplaceDocument_Click(object sender, RoutedEventArgs e)
    {
        if (documents.Count == 0)
        {
            AddDocument_Click(sender, e);
            return;
        }

        WorkspaceDocument replacement = new()
        {
            Id = $"classic-document-{nextDocumentNumber++}",
            Title = "Replacement Document",
            Text = "This document entered the layout source through ObservableCollection.Replace.",
            IsModified = false
        };
        documents[0] = replacement;
        Manager.ActiveContent = replacement;
        StatusText.Text = "Replaced the first document in DocumentsSource.";
    }

    private void ResetDocuments_Click(object sender, RoutedEventArgs e)
    {
        documents.Clear();
        WorkspaceDocument resetDocument = new()
        {
            Id = "classic-reset-document",
            Title = "Reset Document",
            Text = "This document was reimported through DocumentsSource.Reset.",
            IsModified = false
        };
        documents.Add(resetDocument);
        Manager.ActiveContent = resetDocument;
        StatusText.Text = "Reset DocumentsSource and reimported the document.";
    }

    private void CloseActive_Click(object sender, RoutedEventArgs e)
    {
        LayoutContent? content = ActiveLayoutContent;
        content?.Close();
        StatusText.Text = content is null ? "No active item." : $"Requested closing {content.Title}.";
    }

    private void MarkActiveDocumentModified_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveLayoutContent?.Content is not WorkspaceDocument document)
        {
            StatusText.Text = "No active document.";
            return;
        }

        document.IsModified = true;
        StatusText.Text = $"Marked {document.Title} as modified.";
    }

    private void FloatActive_Click(object sender, RoutedEventArgs e)
    {
        LayoutContent? content = ActiveLayoutContent;
        content?.Float();
        StatusText.Text = content is null ? "No active item." : $"Requested floating {content.Title}.";
    }

    private void DockActive_Click(object sender, RoutedEventArgs e)
    {
        LayoutContent? content = ActiveLayoutContent;
        content?.Dock();
        StatusText.Text = content is null ? "No active item." : $"Docked {content.Title}.";
    }

    private void AutoHideTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "No tool pane is available.";
            return;
        }

        tool.ToggleAutoHide();
        StatusText.Text = $"Toggled auto hide for {tool.Title}.";
    }

    private void ShowHiddenTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = Manager.Layout.Hidden.FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "No hidden tools are available.";
            return;
        }

        tool.Show();
        StatusText.Text = $"Showed {tool.Title}.";
    }

    private void ToggleToolFloating_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable[] availableTools = Manager.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
        LayoutAnchorable? tool = availableTools.FirstOrDefault(item => item.IsActive)
            ?? availableTools.FirstOrDefault(item => item.IsFloating && item.IsSelected)
            ?? availableTools.FirstOrDefault(item => item.IsSelected)
            ?? availableTools.FirstOrDefault(item => item.IsFloating)
            ?? availableTools.FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "No tool pane is available.";
            return;
        }

        if (tool.IsFloating)
        {
            tool.Dock();
            StatusText.Text = $"Docked {tool.Title}.";
        }
        else
        {
            tool.Float();
            StatusText.Text = $"Floated {tool.Title}.";
        }
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        using MemoryStream output = new();
        new XmlLayoutSerializer(Manager).Serialize(output);
        savedLayout = output.ToArray();
        StatusText.Text = "Current layout saved in memory.";
    }

    private void RestoreLayout_Click(object sender, RoutedEventArgs e)
    {
        if (savedLayout is null)
        {
            StatusText.Text = "Save the layout first.";
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
        StatusText.Text = "Layout restored and content reconnected by ContentId.";
    }

    private void AllowCloseModified_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = AllowCloseModifiedMenuItem.IsChecked
            ? "Closing modified documents is allowed."
            : "Closing modified documents will be canceled.";
    }

    private void ToggleWindowPolicy_Click(object sender, RoutedEventArgs e)
    {
        Manager.AllowFloatingWindows = !Manager.AllowFloatingWindows;
        Manager.AllowDetachedWindows = Manager.AllowFloatingWindows;
        StatusText.Text = Manager.AllowFloatingWindows ? "Floating and detached tool windows are enabled." : "New floating and detached tool windows are disabled.";
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        PageRoot.RequestedTheme = PageRoot.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        StatusText.Text = PageRoot.RequestedTheme == ElementTheme.Dark ? "Dark theme." : "Light theme.";
    }

    private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        if (e.Document.Content is WorkspaceDocument document && document.IsModified && !AllowCloseModifiedMenuItem.IsChecked)
        {
            e.Cancel = true;
            StatusText.Text = $"Closing modified {document.Title} was canceled. Enable Allow Closing Modified Items under Edit to retry.";
        }
    }

    private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        if (e.Document.Content is WorkspaceDocument document)
        {
            documents.Remove(document);
        }
    }

    private void OnAnchorableHiding(object? sender, AnchorableHidingEventArgs e)
    {
        StatusText.Text = $"Hiding {e.Anchorable.Title}.";
    }

    private void OnContentFloated(object? sender, ContentFloatedEventArgs e)
    {
        StatusText.Text = $"{e.Content.Title} floated.";
    }

    private void OnContentDocked(object? sender, ContentDockedEventArgs e)
    {
        StatusText.Text = $"{e.Content.Title} docked.";
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.Dispose();
    }
}
