using System.Collections.ObjectModel;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace WinUI.AvalonDock.Experiments.Docking;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<SampleDocument> classicDocuments = [];
    private readonly ObservableCollection<SampleTool> classicTools = [];
    private readonly DockLayoutService toggleService;
    private byte[]? classicSavedLayout;
    private byte[]? toggleSavedLayout;
    private bool showingToggle;
    private int nextDocument = 2;

    public MainWindow()
    {
        InitializeComponent();
        Title = "WinUI.AvalonDock 示例";
        Closed += OnClosed;

        StyleSelector itemStyles = CreateItemStyleSelector();
        ClassicManager.LayoutItemContainerStyleSelector = itemStyles;
        ClassicManager.DocumentsSource = classicDocuments;
        ClassicManager.AnchorablesSource = classicTools;
        classicDocuments.Add(new SampleDocument
        {
            Id = "classic-welcome",
            Title = "欢迎",
            Text = "在这里编辑文本，然后拖动标签进行停靠。"
        });
        classicDocuments.Add(new SampleDocument
        {
            Id = "classic-notes",
            Title = "笔记",
            Text = "这个文档来自 DocumentsSource。"
        });
        classicTools.Add(new SampleTool
        {
            Id = "classic-tool",
            Title = "工具",
            Zone = DockZone.LeftTop,
            Text = "这个工具来自 AnchorablesSource。"
        });

        SampleTool toggleTool = new()
        {
            Id = "toggle-tool",
            Title = "工具箱",
            Zone = DockZone.LeftTop,
            IsOpenByDefault = true,
            Text = "这是官方 MVVM 工具模型。"
        };
        toggleService = new DockLayoutService(new IToolbox[] { toggleTool });
        toggleService.OpenDocument(new SampleDocument
        {
            Id = "toggle-welcome",
            Title = "MVVM 文档",
            Text = "DockLayout 由官方 MVVM 服务提供。"
        });
        ToggleManager.LayoutItemContainerStyleSelector = itemStyles;
        ToggleManager.DockLayout = toggleService.Layout;
    }

    private DockingManager CurrentManager => showingToggle ? ToggleManager : ClassicManager;

    private static StyleSelector CreateItemStyleSelector() => new SampleItemStyleSelector
    {
        DocumentStyle = CreateItemStyle(typeof(LayoutDocumentItem)),
        ToolStyle = CreateItemStyle(typeof(LayoutAnchorableItem))
    };

    private static Style CreateItemStyle(Type targetType)
    {
        Style style = new(targetType);
        style.Setters.Add(new Setter(LayoutItem.TitleProperty,
            new Binding { Path = new PropertyPath("Model.Title"), Mode = BindingMode.TwoWay }));
        style.Setters.Add(new Setter(LayoutItem.ContentIdProperty,
            new Binding { Path = new PropertyPath("Model.Id") }));
        return style;
    }

    private void ShowClassic_Click(object sender, RoutedEventArgs e)
    {
        showingToggle = false;
        ClassicManager.Visibility = Visibility.Visible;
        ToggleManager.Visibility = Visibility.Collapsed;
        StatusText.Text = "Classic：XAML LayoutRoot、DocumentsSource 和 AnchorablesSource。";
    }

    private void ShowToggle_Click(object sender, RoutedEventArgs e)
    {
        showingToggle = true;
        ClassicManager.Visibility = Visibility.Collapsed;
        ToggleManager.Visibility = Visibility.Visible;
        StatusText.Text = "MVVM / Toggle：官方 DockLayoutService 与侧边工具栏。";
    }

    private void AddDocument_Click(object sender, RoutedEventArgs e)
    {
        int number = ++nextDocument;
        SampleDocument document = new()
        {
            Id = $"{(showingToggle ? "toggle" : "classic")}-document-{number}",
            Title = $"文档 {number}",
            Text = "新增文档的内容。"
        };
        if (showingToggle)
        {
            toggleService.OpenDocument(document);
        }
        else
        {
            classicDocuments.Add(document);
        }
        StatusText.Text = $"已新增 {document.Title}。";
    }

    private void FloatDocument_Click(object sender, RoutedEventArgs e)
    {
        LayoutDocument? document = CurrentManager.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault();
        document?.Float();
        StatusText.Text = document is null ? "当前没有文档。" : $"已浮动 {document.Title}。";
    }

    private void DockDocument_Click(object sender, RoutedEventArgs e)
    {
        LayoutDocument? document = CurrentManager.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault(item => item.IsFloating);
        document?.Dock();
        StatusText.Text = document is null ? "当前没有浮动文档。" : $"已重新停靠 {document.Title}。";
    }

    private void AutoHideTool_Click(object sender, RoutedEventArgs e)
    {
        DockingManager manager = CurrentManager;
        LayoutAnchorable? tool = manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(item => !manager.IsDetached(item));
        if (tool is null)
        {
            StatusText.Text = "当前没有工具。";
            return;
        }
        if (showingToggle)
        {
            ToggleManager.ToggleAnchorable(tool, DockZone.LeftTop);
        }
        else
        {
            tool.ToggleAutoHide();
        }
        StatusText.Text = $"已切换 {tool.Title} 的自动隐藏状态。";
    }

    private void ShowHiddenTool_Click(object sender, RoutedEventArgs e)
    {
        DockingManager manager = CurrentManager;
        LayoutAnchorable? tool = manager.Layout.Hidden.FirstOrDefault(item => !manager.IsDetached(item));
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
        DockingManager manager = CurrentManager;
        LayoutAnchorable? tool = manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "当前没有工具。";
            return;
        }
        if (manager.IsDetached(tool))
        {
            manager.ReattachAllDetachedAnchorables();
            StatusText.Text = $"已附回 {tool.Title}。";
        }
        else
        {
            manager.DetachAnchorableToWindow(tool);
            StatusText.Text = $"已拆分 {tool.Title}。";
        }
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        using MemoryStream output = new();
        new XmlLayoutSerializer(CurrentManager).Serialize(output);
        if (showingToggle)
        {
            toggleSavedLayout = output.ToArray();
        }
        else
        {
            classicSavedLayout = output.ToArray();
        }
        StatusText.Text = "当前布局已保存在内存中。";
    }

    private void RestoreLayout_Click(object sender, RoutedEventArgs e)
    {
        byte[]? saved = showingToggle ? toggleSavedLayout : classicSavedLayout;
        if (saved is null)
        {
            StatusText.Text = "请先保存当前模式的布局。";
            return;
        }
        DockingManager manager = CurrentManager;
        Dictionary<string, object> contents = [];
        foreach (LayoutContent item in manager.Layout.Descendents().OfType<LayoutContent>())
        {
            if (item.ContentId is { Length: > 0 } contentId && item.Content is { } content)
            {
                contents.Add(contentId, content);
            }
        }
        XmlLayoutSerializer serializer = new(manager);
        serializer.LayoutSerializationCallback += (_, args) =>
        {
            if (args.Model.ContentId is { } contentId && contents.TryGetValue(contentId, out object? content))
            {
                args.Content = content;
            }
        };
        using MemoryStream input = new(saved);
        serializer.Deserialize(input);
        StatusText.Text = "布局已恢复，文档和工具内容按 ContentId 重新连接。";
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        PageRoot.RequestedTheme = PageRoot.RequestedTheme == ElementTheme.Dark
            ? ElementTheme.Light : ElementTheme.Dark;
        StatusText.Text = PageRoot.RequestedTheme == ElementTheme.Dark ? "深色主题。" : "浅色主题。";
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        ClassicManager.Dispose();
        ToggleManager.Dispose();
    }
}

internal sealed class SampleItemStyleSelector : StyleSelector
{
    public required Style DocumentStyle
    {
        get; init;
    }

    public required Style ToolStyle
    {
        get; init;
    }

    protected override Style SelectStyleCore(object item, DependencyObject container)
        => container is LayoutAnchorableItem ? ToolStyle : DocumentStyle;
}
