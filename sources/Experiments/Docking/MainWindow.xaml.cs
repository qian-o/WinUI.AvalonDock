using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WinUI.AvalonDock.Experiments.Shared;
using Windows.Foundation;

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
        SampleChecks.RunWhenLoaded(this, RunScenarioChecksAsync);
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

    public async Task RunScenarioChecksAsync(SampleChecks checks)
    {
        DockingStructureChecks.Run(checks);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents.Count == 2, "Classic initial document source was created.");
        SampleChecks.Require(tools.Count == 2, "Classic initial tool source was created.");
        CheckSourceContent(checks, documents, "Classic initial documents");
        CheckSourceContent(checks, tools, "Classic initial tools");

        TabViewItem[] initialToolTabs = PageRoot.FindVisualChildren<LayoutAnchorablePaneControl>()
            .SelectMany(pane => pane.TabItems.OfType<TabViewItem>())
            .ToArray();
        SampleChecks.Require(initialToolTabs.Length == tools.Count, "Classic initial load creates every tool tab.");
        SampleChecks.Require(initialToolTabs.All(tab => tab.Header is LayoutAnchorableTabItem { ActualWidth: > 0 } header
            && header.FindVisualChildren<TextBlock>().Any(text => text.Visibility == Visibility.Visible && text.ActualWidth > 0)),
            "Classic tool tab headers are measured and visible on initial load.");
        SampleChecks.Require(Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Where(tool => tool.Content is WorkspaceTool)
            .All(tool => tool.AutoHideWidth == 280 && tool.AutoHideHeight == 180),
            "Classic initial load sets suitable auto-hide popup sizes for source tools.");
        checks.Record("Classic initial tool tab presentation");

        LayoutAnchorable styleTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().First();
        LayoutAnchorableItem styleItem = Manager.GetLayoutItemFromModel(styleTool) as LayoutAnchorableItem
            ?? throw new InvalidOperationException("Classic tool layout item was not created.");
        SampleChecks.Require(!styleTool.CanClose, "Classic tool cannot be closed by default.");
        Style closeStyle = new(typeof(LayoutItem));
        closeStyle.Setters.Add(new Setter(LayoutItem.CanCloseProperty, true));
        Manager.LayoutItemContainerStyle = closeStyle;
        SampleChecks.Require(styleItem.CanClose && styleTool.CanClose,
            "Classic layout item style CanClose setting overrides the model's initial value.");
        styleItem.CanClose = false;
        Style nextCloseStyle = new(typeof(LayoutItem));
        nextCloseStyle.Setters.Add(new Setter(LayoutItem.CanCloseProperty, true));
        Manager.LayoutItemContainerStyle = nextCloseStyle;
        SampleChecks.Require(!styleItem.CanClose && !styleTool.CanClose,
            "Classic explicitly set local value overrides a later style.");
        styleItem.ClearValue(LayoutItem.CanCloseProperty);
        SampleChecks.Require(styleItem.CanClose && styleTool.CanClose,
            "Classic clearing the local value reapplies the style's close policy.");
        Manager.LayoutItemContainerStyle = null;
        styleItem.CanClose = false;
        SampleChecks.Require(!styleItem.CanClose && !styleTool.CanClose, "Classic removing the style restores the tool's close policy.");
        checks.Record("Classic layout item style overrides initial model value and restores it");

        await CheckDocumentTabLayoutAsync(checks);
        await CheckDocumentChromeAsync(checks);
        await CheckDocumentDropAreaAfterFloatAsync(checks);
        await CheckContentOperationEventsAsync(checks);
        await DockingTargetChecks.RunAsync(Manager, PageRoot, documents[1], tools[1], checks,
            new Point(AppWindow.Position.X, AppWindow.Position.Y));

        LayoutAnchorable floatingTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().First();
        floatingTool.IsActive = true;
        ToggleToolFloating_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(floatingTool.IsFloating && !Manager.IsDetached(floatingTool), "Tool menu uses standard floating layout rather than detached-window mode.");
        SampleChecks.Require(Manager.FloatingWindows.OfType<LayoutAnchorableFloatingWindowControl>()
            .Any(window => window.Model.Descendents().OfType<LayoutAnchorable>().Any(tool => ReferenceEquals(tool, floatingTool))),
            "Tool menu creates a standard floating window that participates in docking drag and drop.");
        ToggleToolFloating_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!floatingTool.IsFloating && !Manager.IsDetached(floatingTool), "Tool menu docks a standard floating tool back into the original layout.");
        checks.Record("Classic tool menu standard float and dock");

        LayoutAnchorable[] pairedTools = Manager.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
        SampleChecks.Require(pairedTools.Length == 2 && ReferenceEquals(pairedTools[0].Parent, pairedTools[1].Parent),
            "Classic initial tools share a pane.");
        LayoutAnchorablePane homePane = (LayoutAnchorablePane)pairedTools[0].Parent!;
        LayoutAnchorableFloatingWindowControl pairedFloating = Manager.CreateFloatingWindow(pairedTools[0], false)
            as LayoutAnchorableFloatingWindowControl
            ?? throw new InvalidOperationException("Classic tool floating window was not created.");
        LayoutAnchorablePane floatingPane = (pairedFloating.Model as LayoutAnchorableFloatingWindow)?.SinglePane as LayoutAnchorablePane
            ?? throw new InvalidOperationException("Classic tool floating window has no pane.");
        floatingPane.Children.Add(pairedTools[1]);
        pairedFloating.Show();
        SampleChecks.Require((pairedFloating.Model as LayoutAnchorableFloatingWindow)?.SinglePane is LayoutAnchorablePane { ChildrenCount: 2 },
            "Classic both tools share the same floating pane.");
        await SampleChecks.SettleAsync();
        FrameworkElement? floatingRoot = pairedFloating.Content?.GetType().GetProperty("Content")?
            .GetValue(pairedFloating.Content) as FrameworkElement;
        LayoutAnchorablePaneControl? floatingTabs = floatingRoot
            .FindVisualChildren<LayoutAnchorablePaneControl>().FirstOrDefault();
        FrameworkElement? paneFill = floatingTabs?.FindVisualChildren<FrameworkElement>()
            .FirstOrDefault(surface => surface.Name == "PaneFill");
        FrameworkElement? paneOutline = floatingTabs?.FindVisualChildren<FrameworkElement>()
            .FirstOrDefault(surface => surface.Name == "PaneBorder");
        FrameworkElement? tabStrip = floatingTabs?.FindVisualChildren<FrameworkElement>()
            .FirstOrDefault(element => element.Name == "TabContainerGrid");
        global::Microsoft.UI.Xaml.Shapes.Path? fillPath = paneFill?.FindVisualChildren<global::Microsoft.UI.Xaml.Shapes.Path>().FirstOrDefault();
        global::Microsoft.UI.Xaml.Shapes.Path? outlinePath = paneOutline?.FindVisualChildren<global::Microsoft.UI.Xaml.Shapes.Path>().FirstOrDefault();
        SampleChecks.Require(floatingTabs is not null && floatingTabs.TabItems.Count == 2
            && floatingTabs.Model is LayoutAnchorablePane { IsDirectlyHostedInFloatingWindow: true, ChildrenCount: 2 }
            && tabStrip?.Visibility == Visibility.Visible && paneFill is not null && paneOutline is not null
            && paneFill is Grid { CornerRadius: { TopLeft: > 0 } }
            && paneOutline is Grid { CornerRadius: { TopLeft: > 0 } }
            && fillPath?.Fill is not null && fillPath.Stroke is null && outlinePath?.Fill is null
            && outlinePath?.Stroke is not null
            && Canvas.GetZIndex(paneOutline) > Canvas.GetZIndex(paneFill),
            "Classic two-tool floating pane keeps both tabs, rounded border, and upper outline.");
        pairedTools[0].IsActive = true;
        await SampleChecks.SettleAsync();
        SampleChecks.Require(outlinePath!.Stroke is SolidColorBrush activeStroke && activeStroke.Color.A > 0
            && floatingTabs!.BorderBrush is SolidColorBrush inactiveStroke && activeStroke.Color != inactiveStroke.Color,
            "Classic active border is visible in the two-tool floating pane.");
        checks.Record("Classic two-tool shared floating pane appearance");
        homePane.Children.Add(pairedTools[1]);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(floatingPane.ChildrenCount == 1 && floatingTabs!.TabItems.Count == 1
            && tabStrip!.Visibility == Visibility.Collapsed
            && paneOutline is Grid { CornerRadius: { TopLeft: 0 } }
            && paneFill is Grid { CornerRadius: { TopLeft: 0 } }
            && outlinePath.Stroke is SolidColorBrush singleStroke && singleStroke.Color.A == 0,
            "Classic floating pane hides tabs and outline when reduced to one tool.");
        pairedTools[0].Dock();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(pairedTools.All(tool => !tool.IsFloating) && Manager.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 2,
            "Classic tools can dock back into the main layout after floating together.");

        AddDocument_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents.Count == 3, "DocumentsSource Add took effect.");
        CheckSourceContent(checks, documents, "Documents after Add");

        ReplaceDocument_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents[0].Title == "Replacement Document", "DocumentsSource Replace took effect.");
        CheckSourceContent(checks, documents, "Documents after Replace");

        ResetDocuments_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents.Count == 1 && documents[0].Id == "classic-reset-document", "DocumentsSource Reset took effect.");
        CheckSourceContent(checks, documents, "Documents after Reset");

        WorkspaceDocument dirtyDocument = documents[0];
        Manager.ActiveContent = dirtyDocument;
        MarkActiveDocumentModified_Click(this, new RoutedEventArgs());
        SampleChecks.Require(dirtyDocument.IsModified, "Edit menu marks the active document as modified.");
        LayoutDocument? layoutDocument = Manager.Layout.Descendents().OfType<LayoutDocument>()
            .FirstOrDefault(item => ReferenceEquals(item.Content, dirtyDocument));
        SampleChecks.Require(layoutDocument is not null, "Modified document is connected to a layout item.");
        layoutDocument!.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(documents.Contains(dirtyDocument), "Close request for a modified document was canceled.");
        AllowCloseModifiedMenuItem.IsChecked = true;
        layoutDocument.Close();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!documents.Contains(dirtyDocument), "Document closes when closing modified items is allowed.");
        checks.Record("Classic cancelable closing and source collection synchronization");

        WorkspaceDocument restored = new()
        {
            Id = "classic-smoke-document",
            Title = "Smoke Document",
            Text = "ContentId restoration check.",
            IsModified = false
        };
        documents.Add(restored);
        Manager.ActiveContent = restored;
        SaveLayout_Click(this, new RoutedEventArgs());
        RestoreLayout_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        CheckSourceContent(checks, documents, "Documents after restore");
        SampleChecks.Require(Manager.Layout.Descendents().OfType<LayoutContent>().Any(item => ReferenceEquals(item.Content, restored)), "ContentId reconnects content after restore.");
        checks.Record("Classic XML ContentId restoration");

        bool originalFloating = Manager.AllowFloatingWindows;
        ToggleWindowPolicy_Click(this, new RoutedEventArgs());
        SampleChecks.Require(Manager.AllowFloatingWindows != originalFloating, "Classic floating policy can be toggled.");
        ToggleWindowPolicy_Click(this, new RoutedEventArgs());
        SampleChecks.Require(Manager.AllowFloatingWindows == originalFloating, "Classic floating policy can be restored.");
        checks.Record("Classic window policy");

        await CheckRightAutoHidePopupAsync(checks);
        await DockingTemplateChecks.RunAsync(Manager, PageRoot, documents.Single(), tools[0], tools[1],
            () => SaveLayout_Click(this, new RoutedEventArgs()),
            () => RestoreLayout_Click(this, new RoutedEventArgs()), checks);
    }

    private async Task CheckRightAutoHidePopupAsync(SampleChecks checks)
    {
        LayoutAnchorable tool = Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Single(item => item.Content is WorkspaceTool { Id: "classic-properties" });
        LayoutAnchorablePane rightPane = new()
        {
            DockWidth = new GridLength(280)
        };
        rightPane.Children.Add(tool);
        Manager.LayoutEngine.InsertPane(Manager.Layout, rightPane, AnchorSide.Right);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ReferenceEquals(tool.Parent, rightPane) && rightPane.GetSide() == AnchorSide.Right,
            "Classic Properties tool enters the real right pane.");

        tool.ToggleAutoHide();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(tool.IsAutoHidden && tool.FindParent<LayoutAnchorSide>()?.Side == AnchorSide.Right,
            "Classic right-side tool moves to the auto-hide sidebar.");
        LayoutAnchorControl anchor = PageRoot.FindVisualChildren<LayoutAnchorControl>()
            .Single(item => ReferenceEquals(item.Model, tool));
        SampleChecks.Require(anchor.IsLoaded, "Classic right auto-hide tab is loaded in the real main window.");
        tool.IsSelected = false;
        tool.IsSelected = true;
        await SampleChecks.SettleAsync();
        LayoutAutoHideWindowControl popup = Manager.AutoHideWindow
            ?? throw new InvalidOperationException("Classic auto-hide popup was not created.");
        SampleChecks.Require(ReferenceEquals(popup.Model, tool) && popup.Visibility == Visibility.Visible
            && Math.Abs(popup.ActualWidth - (tool.AutoHideWidth + Manager.GridSplitterWidth)) < 3,
            $"Classic real right auto-hide popup uses a 280-pixel content width; actual width: {popup.ActualWidth}.");

        LayoutAnchorableItem item = Manager.GetLayoutItemFromModel(tool) as LayoutAnchorableItem
            ?? throw new InvalidOperationException("Classic Properties tool layout item was not created.");
        SampleChecks.Require(item.AutoHideCommand?.CanExecute(null) == true,
            "Classic pin command is available for the right auto-hide tool.");
        item.AutoHideCommand!.Execute(null);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!tool.IsAutoHidden && ReferenceEquals(tool.Parent, rightPane)
            && rightPane.GetSide() == AnchorSide.Right,
            "Classic Properties tool returns to its original right pane after pinning.");
        checks.Record("Classic source tool auto-hide width, right popup size, and pin return");
    }

    private async Task CheckContentOperationEventsAsync(SampleChecks checks)
    {
        foreach (LayoutContent source in new LayoutContent[]
        {
            Manager.Layout.Descendents().OfType<LayoutDocument>().First(),
            Manager.Layout.Descendents().OfType<LayoutAnchorable>().First(),
        })
        {
            string label = source is LayoutDocument ? "document" : "tool";
            ILayoutContainer? originalParent = source.Parent;
            int floating = 0;
            int floated = 0;
            int docking = 0;
            int docked = 0;
            bool cancelFloat = true;
            bool cancelDock = true;
            EventHandler<ContentFloatingEventArgs> onFloating = (_, args) =>
            {
                if (ReferenceEquals(args.Content, source))
                {
                    floating++;
                    args.Cancel = cancelFloat;
                }
            };
            EventHandler<ContentFloatedEventArgs> onFloated = (_, args) =>
            {
                if (ReferenceEquals(args.Content, source))
                {
                    floated++;
                }
            };
            EventHandler<ContentDockingEventArgs> onDocking = (_, args) =>
            {
                if (ReferenceEquals(args.Content, source))
                {
                    docking++;
                    args.Cancel = cancelDock;
                }
            };
            EventHandler<ContentDockedEventArgs> onDocked = (_, args) =>
            {
                if (ReferenceEquals(args.Content, source))
                {
                    docked++;
                }
            };
            Manager.ContentFloating += onFloating;
            Manager.ContentFloated += onFloated;
            Manager.ContentDocking += onDocking;
            Manager.ContentDocked += onDocked;
            try
            {
                LayoutItem item = Manager.GetLayoutItemFromModel(source)
                    ?? throw new InvalidOperationException("Layout item for the event check was not created.");
                SampleChecks.Require(item.FloatCommand?.CanExecute(null) == true, $"Classic {label} can float normally.");
                item.FloatCommand!.Execute(null);
                await SampleChecks.SettleAsync();
                SampleChecks.Require(floating == 1 && floated == 0 && !source.IsFloating
                    && ReferenceEquals(source.Parent, originalParent), $"Classic {label} canceled float leaves the layout unchanged and sends no completion event.");
                cancelFloat = false;
                item.FloatCommand.Execute(null);
                await SampleChecks.SettleAsync();
                SampleChecks.Require(floating == 2 && floated == 1 && source.IsFloating,
                    $"Classic {label} successful float sends one completion event.");
                LayoutFloatingWindowControl window = Manager.FloatingWindows.Single(host => host.Model.Descendents().Contains(source));
                window.Width = 160;
                window.Height = 120;
                window.Left = AppWindow.Position.X;
                window.Top = AppWindow.Position.Y;
                await SampleChecks.SettleAsync();
                FrameworkElement pane = PageRoot.FindVisualChildren<FrameworkElement>().Single(element =>
                    element is ILayoutControl control && ReferenceEquals(control.Model, originalParent)
                    && element is LayoutDocumentPaneControl or LayoutAnchorablePaneControl);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    using DockingDragChecks session = new(window);
                    session.Update(DockingDragChecks.Center(DockingDragChecks.ScreenBounds(pane)));
                    string glyphName = source is LayoutDocument ? "PART_DocumentPaneDropTargetInto" : "PART_AnchorablePaneDropTargetInto";
                    await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(target =>
                        ReferenceEquals(DockingDragChecks.TargetArea(target), pane)), "Target for the event check did not appear.");
                    Point release = DockingDragChecks.Center(session.GlyphBounds(glyphName));
                    session.Update(release);
                    session.Drop(release);
                    await SampleChecks.SettleAsync();
                    if (attempt == 0)
                    {
                        SampleChecks.Require(docking == 1 && docked == 0 && source.IsFloating,
                            $"Classic {label} canceled dock keeps the floating window and sends no completion event.");
                        cancelDock = false;
                    }
                    else
                    {
                        SampleChecks.Require(docking == 2 && docked == 1 && !source.IsFloating
                            && ReferenceEquals(source.Parent, originalParent), $"Classic {label} successful dock sends one completion event and returns to the target pane.");
                    }
                }
                checks.Record($"Classic {label} float/dock cancellation preserves layout and successful completion notifies once");
            }
            finally
            {
                Manager.ContentFloating -= onFloating;
                Manager.ContentFloated -= onFloated;
                Manager.ContentDocking -= onDocking;
                Manager.ContentDocked -= onDocked;
            }
        }
    }

    private async Task CheckDocumentTabLayoutAsync(SampleChecks checks)
    {
        LayoutDocumentPaneControl pane = PageRoot.FindVisualChildren<LayoutDocumentPaneControl>().First();
        VerifyTabs("initial load");

        LayoutDocument document = Manager.Layout.Descendents().OfType<LayoutDocument>().First();
        document.Float();
        await SampleChecks.SettleAsync();
        document.Dock();
        await SampleChecks.SettleAsync();
        VerifyTabs("after floating and redocking");

        WorkspaceDocument longDocument = new()
        {
            Id = "classic-wide-tab-check",
            Title = "This Tab Has a Much Longer Title Than an Ordinary Document Tab",
            Text = "Check the content-based width of the document tab."
        };
        documents.Add(longDocument);
        await SampleChecks.SettleAsync();
        TabViewItem longTab = pane.TabItems.OfType<TabViewItem>().First(tab => ReferenceEquals(((LayoutContent)tab.Tag).Content, longDocument));
        SampleChecks.Require(longTab.ActualWidth > 100 && longTab.ActualWidth <= 240,
            "Classic long document tab grows with its content and stays within the 240-pixel cap.");
        documents.Remove(longDocument);
        await SampleChecks.SettleAsync();
        checks.Record("Classic document tab width and non-button blank-space hit testing remain consistent on load and after redocking");

        void VerifyTabs(string stage)
        {
            TabViewItem[] tabs = pane.TabItems.OfType<TabViewItem>().ToArray();
            SampleChecks.Require(tabs.Length == 2, $"Classic {stage} keeps two document tabs.");
            foreach (TabViewItem tab in tabs)
            {
                LayoutDocumentTabItem header = (LayoutDocumentTabItem)tab.Header;
                Button? closeButton = tab.FindVisualChildren<Button>().FirstOrDefault(button => button.Name == "CloseButton");
                double headerRight = header.TransformToVisual(tab).TransformBounds(new Rect(0, 0, header.ActualWidth, header.ActualHeight)).Right;
                double closeLeft = closeButton?.TransformToVisual(tab).TransformPoint(new Point(0, 0)).X ?? -1;
                SampleChecks.Require(Math.Abs(tab.ActualWidth - 100) < 1 && Math.Abs(tab.MinWidth - 100) < 0.1,
                    $"Classic {stage} keeps a 100-pixel minimum for short document tabs.");
                SampleChecks.Require(closeButton is not null && closeLeft - headerRight >= 3,
                    $"Classic {stage} leaves testable space between title and close button.");
                Point blank = tab.TransformToVisual(PageRoot).TransformPoint(new Point((headerRight + closeLeft) / 2, 14));
                IReadOnlyList<UIElement> hits = VisualTreeHelper.FindElementsInHostCoordinates(blank, PageRoot).ToArray();
                SampleChecks.Require(hits.Contains(tab) && !hits.Contains(closeButton!),
                    $"Classic {stage} hits the document tab between title and close button.");
            }
        }
    }

    private async Task CheckDocumentDropAreaAfterFloatAsync(SampleChecks checks)
    {
        foreach (Orientation orientation in new[] { Orientation.Vertical, Orientation.Horizontal })
        {
            string direction = orientation == Orientation.Vertical ? "top/bottom" : "left/right";
            LayoutDocument[] layoutDocuments = Manager.Layout.Descendents().OfType<LayoutDocument>().ToArray();
            SampleChecks.Require(layoutDocuments.Length == 2, $"Classic {direction} grouping keeps two initial documents before splitting.");
            LayoutDocument remainingDocument = layoutDocuments.Single(document => ReferenceEquals(document.Content, documents[0]));
            LayoutDocument floatedDocument = layoutDocuments.Single(document => ReferenceEquals(document.Content, documents[1]));
            LayoutDocumentItem item = Manager.GetLayoutItemFromModel(floatedDocument) as LayoutDocumentItem
                ?? throw new InvalidOperationException("Classic grouped document layout item was not created.");
            ICommand splitCommand = (orientation == Orientation.Vertical ? item.NewHorizontalTabGroupCommand : item.NewVerticalTabGroupCommand)
                ?? throw new InvalidOperationException("Classic document grouping command was not created.");
            SampleChecks.Require(splitCommand.CanExecute(null), $"Classic can create a {direction} tab group.");
            splitCommand.Execute(null);
            await SampleChecks.SettleAsync();
            LayoutDocumentPane remainingModel = remainingDocument.Parent as LayoutDocumentPane
                ?? throw new InvalidOperationException("Classic remaining document pane is missing.");
            SampleChecks.Require(remainingModel.Parent is LayoutDocumentPaneGroup group && group.Orientation == orientation
                && !ReferenceEquals(remainingDocument.Parent, floatedDocument.Parent), $"Classic {direction} group orientation is correct.");

            WorkspaceDocument dragModel = new()
            {
                Id = "classic-active-drag-check",
                Title = "Session Check Document",
                Text = "Change the layout in the same drag session while docking indicators are visible."
            };
            documents.Add(dragModel);
            await SampleChecks.SettleAsync();
            LayoutDocument dragDocument = Manager.Layout.Descendents().OfType<LayoutDocument>()
                .Single(document => ReferenceEquals(document.Content, dragModel));
            try
            {
                dragDocument.Float();
                await SampleChecks.SettleAsync();
                LayoutFloatingWindowControl draggingWindow = WindowFor(dragDocument);
                ParkWindow(draggingWindow);
                LayoutDocumentPaneControl pane = PaneFor(remainingModel);
                Rect splitBounds = DockingDragChecks.ScreenBounds(pane);
                using DockingDragChecks session = new(draggingWindow);
                Point originalCenter = DockingDragChecks.Center(splitBounds);
                session.Update(originalCenter);
                await DockingDragChecks.WaitUntilAsync(() => session.Targets().Any(target => IsCenter(target, pane)),
                    "The real session did not create a center target for the main document pane.");
                DropArea<LayoutDocumentPaneControl> capturedArea = session.HostAreas(Manager)
                    .Single(area => ReferenceEquals(area.AreaElement, pane));
                SampleChecks.Require(session.ContainsArea(capturedArea)
                    && session.HostAreas(Manager).Any(area => ReferenceEquals(area, capturedArea)),
                    "Classic initial target comes from the real host cache and enters the drag session.");
                session.Update(DockingDragChecks.Center(session.GlyphBounds("PART_DocumentPaneDropTargetInto")));
                await session.WaitForFrameAsync(0, "Initial center indicator and preview were not rendered.");
                SampleChecks.Require(DockingDragChecks.Centered(session.GlyphBounds("PART_DocumentPaneDropTargetInto"), splitBounds)
                    && DockingDragChecks.Near(session.PreviewBounds(), splitBounds),
                    $"Classic {direction} group's real indicator is centered and its preview covers the current pane.");
                OverlayWindow originalOverlay = session.Overlay;
                int splitFrames = session.Frames;

                floatedDocument.Float();
                ParkWindow(WindowFor(floatedDocument));
                await DockingDragChecks.WaitUntilAsync(() =>
                {
                    LayoutDocumentPaneControl? currentPane = PageRoot.FindVisualChildren<LayoutDocumentPaneControl>()
                        .FirstOrDefault(current => ReferenceEquals(current.Model, remainingModel));
                    if (currentPane is not { IsLoaded: true })
                    {
                        return false;
                    }
                    Rect currentBounds = DockingDragChecks.ScreenBounds(currentPane);
                    return orientation == Orientation.Vertical ? currentBounds.Height > splitBounds.Height + 30
                        : currentBounds.Width > splitBounds.Width + 30;
                }, "The remaining pane did not expand after floating the other document.");
                pane = PaneFor(remainingModel);
                Rect expandedBounds = DockingDragChecks.ScreenBounds(pane);
                session.Update(originalCenter);
                await session.WaitForFrameAsync(splitFrames, "The expanded indicator was not rendered in the same drag session.");
                SampleChecks.Require(ReferenceEquals(session.Overlay, originalOverlay), "Classic continues using the same real overlay session after layout change.");
                DropArea<LayoutDocumentPaneControl> currentArea = session.HostAreas(Manager)
                    .Single(area => ReferenceEquals(area.AreaElement, pane));
                Point formerOtherHalf = orientation == Orientation.Vertical
                    ? new Point(expandedBounds.X + expandedBounds.Width / 2, (splitBounds.Bottom + expandedBounds.Bottom) / 2)
                    : new Point((splitBounds.Right + expandedBounds.Right) / 2, expandedBounds.Y + expandedBounds.Height / 2);
                session.Update(formerOtherHalf);
                SampleChecks.Require(!splitBounds.Contains(formerOtherHalf) && session.ContainsArea(currentArea)
                    && currentArea.DetectionRect.Contains(currentArea.TransformToDeviceDPI(formerOtherHalf)),
                    $"Classic {direction} group's former other half hits the expanded pane in the real session.");
                object centerTarget = session.Targets().Single(target => IsCenter(target, pane));
                Rect glyphBounds = session.GlyphBounds("PART_DocumentPaneDropTargetInto");
                SampleChecks.Require(DockingDragChecks.Centered(glyphBounds, expandedBounds)
                    && DockingDragChecks.Near(DockingDragChecks.TargetBounds(centerTarget), glyphBounds),
                    $"Classic {direction} group's real indicator and dock target centers differ by no more than two physical pixels after floating.");
                int expandedFrames = session.Frames;
                session.Update(DockingDragChecks.Center(glyphBounds));
                await session.WaitForFrameAsync(expandedFrames, "Center-target preview of the expanded pane was not rendered.");
                SampleChecks.Require(session.ActiveTarget is { } active && IsCenter(active, pane)
                    && DockingDragChecks.Near(session.PreviewBounds(), expandedBounds),
                    "Classic center hit region and real preview both cover the expanded pane.");

                Thickness originalMargin = pane.Margin;
                object? originalTarget = session.ActiveTarget;
                try
                {
                    int marginFrames = session.Frames;
                    pane.Margin = new Thickness(originalMargin.Left + 12, originalMargin.Top + 12,
                        originalMargin.Right + 12, originalMargin.Bottom + 12);
                    PageRoot.UpdateLayout();
                    await DockingDragChecks.WaitUntilAsync(() => DockingDragChecks.ScreenBounds(pane).Width < expandedBounds.Width - 16,
                        "Real pane bounds did not change after symmetric inset.");
                    Rect insetBounds = DockingDragChecks.ScreenBounds(pane);
                    session.Update(DockingDragChecks.Center(glyphBounds));
                    await session.WaitForFrameAsync(marginFrames, "Preview did not follow pane bounds when the center-target position stayed fixed.");
                    SampleChecks.Require(ReferenceEquals(session.ActiveTarget, originalTarget)
                        && DockingDragChecks.Centered(session.GlyphBounds("PART_DocumentPaneDropTargetInto"), glyphBounds)
                        && DockingDragChecks.Near(session.PreviewBounds(), insetBounds),
                        "Classic real preview follows pane bounds even when the same center target keeps its position.");
                }
                finally
                {
                    int restoreFrames = session.Frames;
                    pane.Margin = originalMargin;
                    PageRoot.UpdateLayout();
                    session.Update(DockingDragChecks.Center(glyphBounds));
                    await session.WaitForFrameAsync(restoreFrames, "Preview was not redrawn after restoring pane bounds.");
                }
                SampleChecks.Require(DockingDragChecks.Near(session.PreviewBounds(), DockingDragChecks.ScreenBounds(pane)),
                    "Classic restored real center preview covers the current pane.");
                SampleChecks.Require(session.Drop(DockingDragChecks.Center(session.GlyphBounds("PART_DocumentPaneDropTargetInto"))),
                    "Classic real drag session commits the center dock target.");
                await DockingDragChecks.WaitUntilAsync(() => !dragDocument.IsFloating
                    && ReferenceEquals(dragDocument.Parent, remainingDocument.Parent), "The document did not merge into the real target pane after center release.");
                SampleChecks.Require(remainingModel.Children.Contains(dragDocument)
                    && PageRoot.FindVisualChildren<LayoutDocumentPaneControl>().Any(current => ReferenceEquals(current.Model, remainingModel)
                        && current.TabItems.OfType<TabViewItem>().Any(tab => tab.Tag is LayoutContent content && ReferenceEquals(content.Content, dragModel))),
                    "Classic layout tree and visible tabs contain the same document content after center release.");
                checks.Record($"Classic {direction} dynamic group change in real DragService, targets, indicators, preview, rendered frames, and center release");
            }
            finally
            {
                if (floatedDocument.IsFloating)
                {
                    remainingModel.Children.Add(floatedDocument);
                    Manager.Layout.CollectGarbage();
                }
                documents.Remove(dragModel);
                await SampleChecks.SettleAsync();
            }
            SampleChecks.Require(documents.Count == 2 && !floatedDocument.IsFloating
                && ReferenceEquals(remainingDocument.Parent, floatedDocument.Parent), "Classic session check restores two documents docked in the same group.");
        }

        LayoutDocumentPaneControl PaneFor(LayoutDocumentPane model) => PageRoot.FindVisualChildren<LayoutDocumentPaneControl>()
            .Single(pane => ReferenceEquals(pane.Model, model));
        LayoutFloatingWindowControl WindowFor(LayoutDocument document) => Manager.FloatingWindows
            .Single(window => window.Model.Descendents().OfType<LayoutDocument>().Any(current => ReferenceEquals(current, document)));
        static bool IsCenter(object target, LayoutDocumentPaneControl pane) => DockingDragChecks.TargetType(target) == DropTargetType.DocumentPaneDockInside
            && DockingDragChecks.TabIndex(target) == -1 && ReferenceEquals(DockingDragChecks.TargetArea(target), pane);
        void ParkWindow(LayoutFloatingWindowControl window)
        {
            window.Width = 160;
            window.Height = 120;
            window.Left = AppWindow.Position.X;
            window.Top = AppWindow.Position.Y;
        }
    }

    private async Task CheckDocumentChromeAsync(SampleChecks checks)
    {
        LayoutDocumentPaneControl? pane = PageRoot.FindVisualChildren<LayoutDocumentPaneControl>().FirstOrDefault();
        SampleChecks.Require(pane is not null, "Classic document pane is loaded.");
        TabViewItem[] tabs = pane!.TabItems.OfType<TabViewItem>().ToArray();
        SampleChecks.Require(tabs.Length == documents.Count, "Classic initial document tabs are loaded.");
        foreach (TabViewItem tab in tabs)
        {
            Button? closeButton = tab.FindVisualChildren<Button>().FirstOrDefault(button => button.Name == "CloseButton");
            SampleChecks.Require(closeButton is not null && Math.Abs(closeButton.ActualWidth - 20) < 1
                && Math.Abs(closeButton.ActualHeight - 20) < 1 && Math.Abs(tab.Padding.Right - 4) < 0.1,
                "Classic document tab close button and right padding match WPFUI dimensions.");
            SampleChecks.Require(closeButton!.Content is Viewbox { Width: 12, Height: 12, Child: PathIcon { Width: 12, Height: 12, Data: not null } },
                "Classic document tab close glyph is 12 pixels.");
        }
        checks.Record("Classic document tab close button size and glyph");

        global::AvalonDock.Controls.DropDownButton? selectorButton = pane.FindVisualChildren<global::AvalonDock.Controls.DropDownButton>()
            .FirstOrDefault(button => button.Name == "MenuDropDownButton");
        SampleChecks.Require(selectorButton?.DropDownContextMenu is ContextMenuEx, "Classic document selector is connected to a menu.");
        ContextMenuEx menu = (ContextMenuEx)selectorButton!.DropDownContextMenu!;
        ElementTheme originalTheme = PageRoot.RequestedTheme;
        try
        {
            foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                PageRoot.RequestedTheme = theme;
                await SampleChecks.SettleAsync();
                SampleChecks.Require(pane.Background is SolidColorBrush surface
                    && surface.Color.A == 255
                    && surface.Color.R == (theme == ElementTheme.Light ? 0xF9 : 0x28)
                    && surface.Color.G == (theme == ElementTheme.Light ? 0xF9 : 0x28)
                    && surface.Color.B == (theme == ElementTheme.Light ? 0xF9 : 0x28),
                    $"Classic document surface matches the {(theme == ElementTheme.Light ? "light" : "dark")} theme colors.");
                menu.ShowAt(selectorButton);
                selectorButton.IsChecked = true;
                await SampleChecks.SettleAsync();

                Popup? popup = VisualTreeHelper.GetOpenPopupsForXamlRoot(PageRoot.XamlRoot)
                    .FirstOrDefault(candidate => candidate.Child is MenuFlyoutPresenter
                        || candidate.Child.FindVisualChildren<MenuFlyoutPresenter>().Any());
                MenuFlyoutPresenter? flyoutPresenter = popup?.Child as MenuFlyoutPresenter
                    ?? popup?.Child.FindVisualChildren<MenuFlyoutPresenter>().FirstOrDefault();
                SampleChecks.Require(flyoutPresenter is not null && flyoutPresenter.ActualTheme == theme,
                    $"Classic document selector popup follows the {(theme == ElementTheme.Light ? "light" : "dark")} theme.");

                if (theme == ElementTheme.Light)
                {
                    MenuItemEx[] items = menu.Items.OfType<MenuItemEx>().ToArray();
                    SampleChecks.Require(items.Length == documents.Count, "Classic document selector contains initial documents.");
                    double? firstLeft = null;
                    foreach (WorkspaceDocument document in documents)
                    {
                        MenuItemEx? item = items.FirstOrDefault(candidate => candidate.DataContext is LayoutContent model
                            && ReferenceEquals(model.Content, document));
                        ContentPresenter? titlePresenter = item?.FindVisualChildren<ContentPresenter>()
                            .FirstOrDefault(presenter => presenter.Name == "TextBlock");
                        TextBlock? title = titlePresenter?.FindVisualChildren<TextBlock>()
                            .FirstOrDefault(text => text.Text == document.Title);
                        Grid? itemRoot = item?.FindVisualChildren<Grid>().FirstOrDefault(grid => grid.Name == "LayoutRoot");
                        SampleChecks.Require(item is not null && item.Text == document.Title
                            && titlePresenter is not null && title is not null && itemRoot is not null
                            && item.HorizontalContentAlignment == HorizontalAlignment.Left
                            && titlePresenter.HorizontalContentAlignment == HorizontalAlignment.Left,
                            "Classic document selector title and left alignment are correct.");
                        double titleLeft = title!.TransformToVisual(popup!.Child).TransformPoint(new Point(0, 0)).X;
                        double presenterLeft = titlePresenter!.TransformToVisual(itemRoot).TransformPoint(new Point(0, 0)).X;
                        SampleChecks.Require(item!.Icon is Image { Source: null }
                            && ((MenuFlyoutItem)item).Icon is null
                            && titlePresenter.Margin.Left < 1
                            && titleLeft - itemRoot!.TransformToVisual(popup.Child).TransformPoint(new Point(0, 0)).X < 20,
                            "Classic iconless document menu title starts at the left without reserving an empty icon column.");
                        SampleChecks.Require(Math.Abs(presenterLeft - itemRoot!.Padding.Left - titlePresenter.Margin.Left) < 1,
                            "Classic document selector title has no duplicate icon placeholder.");
                        SampleChecks.Require(firstLeft is null || Math.Abs(titleLeft - firstLeft.Value) < 1,
                            "Classic long and short document titles share the same left edge.");
                        firstLeft ??= titleLeft;

                        AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(item)
                            ?? FrameworkElementAutomationPeer.CreatePeerForElement(item);
                        SampleChecks.Require(peer?.GetName() == document.Title, "Classic document selector automation name is readable.");
                    }
                    checks.Record("Classic document selector text, left alignment, and automation name");

                    LayoutContent iconDocument = (LayoutContent)items[0].DataContext;
                    iconDocument.IconSource = new Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap(1, 1);
                    await SampleChecks.SettleAsync();
                    menu.Hide();
                    await SampleChecks.SettleAsync();
                    menu.ShowAt(selectorButton);
                    await SampleChecks.SettleAsync();
                    ContentPresenter? iconTitle = items[0].FindVisualChildren<ContentPresenter>()
                        .FirstOrDefault(presenter => presenter.Name == "TextBlock");
                    Viewbox? iconRoot = items[0].FindVisualChildren<Viewbox>()
                        .FirstOrDefault(viewbox => viewbox.Name == "IconRoot");
                    SampleChecks.Require(items[0].Icon is Image { Source: not null }
                        && ((MenuFlyoutItem)items[0]).Icon is not null
                        && iconRoot?.Visibility == Visibility.Visible && iconTitle?.Margin.Left >= 20,
                        "Classic document selector reserves an icon column after icon loading.");
                    checks.Record("Classic menu spacing after document icon loads");
                }

                menu.Hide();
                selectorButton.IsChecked = false;
                await SampleChecks.SettleAsync();
            }
            checks.Record("Classic document selector popup theme in light and dark modes");
        }
        finally
        {
            menu.Hide();
            selectorButton.IsChecked = false;
            PageRoot.RequestedTheme = originalTheme;
        }

        global::Windows.Graphics.SizeInt32 originalSize = AppWindow.Size;
        WorkspaceDocument[] initialDocuments = documents.ToArray();
        try
        {
            for (int index = 0; index < 4; index++)
            {
                AddDocument_Click(this, new RoutedEventArgs());
            }

            AppWindow.Resize(new global::Windows.Graphics.SizeInt32(720, originalSize.Height));
            await SampleChecks.SettleAsync();
            TabViewItem[] overflowTabs = pane.TabItems.OfType<TabViewItem>().ToArray();
            SampleChecks.Require(overflowTabs.Length == 6 && overflowTabs.Any(tab => tab.Opacity == 0),
                "Classic document selector hides overflowing tabs in a narrow pane.");
            Rect selectorBounds = selectorButton.TransformToVisual(pane).TransformBounds(
                new Rect(0, 0, selectorButton.ActualWidth, selectorButton.ActualHeight));
            SampleChecks.Require(selectorBounds.Right <= pane.ActualWidth + 1 && selectorBounds.Left >= 0,
                "Classic document selector button stays within a narrow pane.");
            TabViewItem hiddenTab = overflowTabs.First(tab => tab.Opacity == 0);
            LayoutContent hiddenModel = (LayoutContent)hiddenTab.Tag;
            menu.ShowAt(selectorButton);
            await SampleChecks.SettleAsync();
            MenuItemEx? hiddenItem = menu.Items.OfType<MenuItemEx>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, hiddenModel));
            SampleChecks.Require(hiddenItem?.Command?.CanExecute(null) == true,
                "Classic document selector provides an activation command for an overflow document.");
            hiddenItem!.Command!.Execute(null);
            menu.Hide();
            await SampleChecks.SettleAsync();
            SampleChecks.Require(hiddenModel.IsSelected && hiddenTab.Opacity > 0,
                "Classic overflow document activated from the selector reappears in the tab strip.");
            checks.Record("Classic narrow-pane document overflow, selector button, and hidden-document activation");
        }
        finally
        {
            menu.Hide();
            AppWindow.Resize(originalSize);
            foreach (WorkspaceDocument extra in documents.Where(document => !initialDocuments.Contains(document)).ToArray())
            {
                documents.Remove(extra);
            }

            await SampleChecks.SettleAsync();
        }
    }

    private void CheckSourceContent<T>(SampleChecks checks, IEnumerable<T> models, string label)
    {
        object[] expected = models.Cast<object>().ToArray();
        object[] actual = Manager.Layout.Descendents().OfType<LayoutContent>()
            .Select(item => item.Content)
            .Where(content => content is T)
            .Cast<object>()
            .ToArray();
        SampleChecks.Require(actual.Length == expected.Length
            && actual.Distinct(System.Collections.Generic.ReferenceEqualityComparer.Instance).Count() == actual.Length
            && expected.Distinct(System.Collections.Generic.ReferenceEqualityComparer.Instance).Count() == expected.Length
            && expected.All(model => actual.Any(content => ReferenceEquals(content, model))),
            $"{label}: content identity set is exact, with no stale items or duplicate models.");
        checks.Record($"{label}: exact reference set and duplicate check ({expected.Length})");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Manager.Dispose();
    }
}
