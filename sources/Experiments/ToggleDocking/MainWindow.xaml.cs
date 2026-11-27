using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Experiments.ToggleDocking;

public sealed partial class MainWindow : Window
{
    private readonly DockLayoutService layoutService;
    private int nextDocumentNumber = 2;

    public MainWindow()
    {
        InitializeComponent();
        Title = "ToggleDocking: Six-Zone Workspace";
        Closed += OnClosed;

        WorkspaceTool[] tools =
        [
            CreateTool("toggle-explorer", "Explorer", DockZone.LeftTop, "\uE838", "Project files"),
            CreateTool("toggle-outline", "Outline", DockZone.LeftBottom, "\uE8A5", "Document outline"),
            CreateTool("toggle-properties", "Properties", DockZone.RightTop, "\uE713", "Selection properties"),
            CreateTool("toggle-diagnostics", "Diagnostics", DockZone.RightBottom, "\uE814", "No issues"),
            CreateTool("toggle-output", "Output", DockZone.BottomLeft, "\uE756", "Build output"),
            CreateTool("toggle-terminal", "Terminal", DockZone.BottomRight, "\uE756", "Terminal output")
        ];
        layoutService = new DockLayoutService(tools);
        layoutService.OpenDocument(new WorkspaceDocument
        {
            Id = "toggle-editor",
            Title = "Workspace",
            Text = "Select a sidebar tool to show or hide it.",
            IsModified = false
        });
        layoutService.OpenDocument(new WorkspaceDocument
        {
            Id = "toggle-notes",
            Title = "Notes",
            Text = "Use a zone button to move the current tool.",
            IsModified = false
        });

        Manager.LayoutItemContainerStyleSelector = SampleStyles.CreateItemStyleSelector();
        Manager.DockLayout = layoutService.Layout;
        Manager.ActiveContentChanged += OnActiveContentChanged;
    }

    private static WorkspaceTool CreateTool(string id, string title, DockZone zone, string glyph, string text)
    {
        return new WorkspaceTool
        {
            Id = id,
            Title = title,
            Zone = zone,
            Icon = new FontIcon { Glyph = glyph, FontSize = 20 },
            IsOpenByDefault = true,
            Text = text
        };
    }

    private LayoutAnchorable? SelectedTool =>
        Manager.Layout?.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(item => ReferenceEquals(item.Content, Manager.ActiveContent))
        ?? Manager.Layout?.Descendents().OfType<LayoutAnchorable>().FirstOrDefault()
        ?? Manager.Layout?.Hidden.FirstOrDefault();

    private void AddDocument_Click(object sender, RoutedEventArgs e)
    {
        int number = nextDocumentNumber++;
        WorkspaceDocument document = new()
        {
            Id = $"toggle-document-{number}",
            Title = $"Document {number}",
        };
        layoutService.OpenDocument(document);
        Manager.ActiveContent = document;
        StatusText.Text = $"Added {document.Title}.";
    }

    private void ToggleTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = SelectedTool;
        if (tool is null)
        {
            StatusText.Text = "No tool is available to toggle.";
            return;
        }

        Manager.ToggleAnchorable(tool, GetToolZone(tool));
        StatusText.Text = $"Toggled {tool.Title}.";
    }

    private void ShowHiddenTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = Manager.Layout?.Hidden.FirstOrDefault();
        if (tool is null)
        {
            StatusText.Text = "No hidden tool is available.";
            return;
        }

        tool.Show();
        StatusText.Text = $"Shown {tool.Title}.";
    }

    private void DetachTool_Click(object sender, RoutedEventArgs e)
    {
        LayoutAnchorable? tool = SelectedTool;
        if (tool is null)
        {
            StatusText.Text = "No tool is available to detach.";
            return;
        }

        if (Manager.IsDetached(tool))
        {
            Manager.ReattachAnchorable(tool);
            StatusText.Text = $"Reattached {tool.Title}.";
        }
        else
        {
            Manager.DetachAnchorableToWindow(tool);
            StatusText.Text = $"Detached {tool.Title}.";
        }
    }

    private void MoveLeftTop_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.LeftTop);
    private void MoveLeftBottom_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.LeftBottom);
    private void MoveRightTop_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.RightTop);
    private void MoveRightBottom_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.RightBottom);
    private void MoveBottomLeft_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.BottomLeft);
    private void MoveBottomRight_Click(object sender, RoutedEventArgs e) => MoveSelectedTool(DockZone.BottomRight);

    private void MoveSelectedTool(DockZone zone)
    {
        LayoutAnchorable? tool = SelectedTool;
        if (tool is null)
        {
            StatusText.Text = "No tool is available to move.";
            return;
        }

        Manager.MoveAnchorableToZone(tool, zone);
        StatusText.Text = $"Moved {tool.Title} to {zone}.";
    }

    private void BottomFullWidth_Click(object sender, RoutedEventArgs e)
    {
        Manager.LayoutPriority = DockLayoutPriority.BottomFullWidth;
        StatusText.Text = "Layout priority: bottom spans full width.";
    }

    private void SidesFullHeight_Click(object sender, RoutedEventArgs e)
    {
        Manager.LayoutPriority = DockLayoutPriority.SidesFullHeight;
        StatusText.Text = "Layout priority: sidebars span full height.";
    }

    private void DefaultLayoutPriority_Click(object sender, RoutedEventArgs e)
    {
        Manager.LayoutPriority = DockLayoutPriority.Default;
        StatusText.Text = "Layout priority: default.";
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        PageRoot.RequestedTheme = PageRoot.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        StatusText.Text = PageRoot.RequestedTheme == ElementTheme.Dark ? "Dark theme." : "Light theme.";
    }

    private void OnActiveContentChanged(object? sender, EventArgs e)
    {
        if (Manager.ActiveContent is not WorkspaceTool model)
        {
            return;
        }

        StatusText.Text = $"Current tool: {model.Title}.";
    }

    private DockZone GetToolZone(LayoutAnchorable tool) => PageRoot.FindVisualChildren<ToggleDockButton>()
        .FirstOrDefault(button => ReferenceEquals(button.Anchorable, tool))?.Zone
        ?? (tool.Content as WorkspaceTool)?.Zone ?? DockZone.LeftTop;

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.ActiveContentChanged -= OnActiveContentChanged;
        Manager.Dispose();
    }
}
