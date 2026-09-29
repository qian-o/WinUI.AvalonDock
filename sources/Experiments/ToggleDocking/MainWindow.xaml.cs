using System.Collections;
using System.Reflection;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
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
            CreateTool("toggle-explorer", "Explorer", DockZone.LeftTop, "\uE838", "Upper-left tool zone for project navigation."),
            CreateTool("toggle-outline", "Outline", DockZone.LeftBottom, "\uE8A5", "Lower-left tool zone for document structure."),
            CreateTool("toggle-properties", "Properties", DockZone.RightTop, "\uE713", "Upper-right tool zone for inspecting the selection."),
            CreateTool("toggle-diagnostics", "Diagnostics", DockZone.RightBottom, "\uE814", "Lower-right tool zone for errors, warnings, and logs."),
            CreateTool("toggle-output", "Output", DockZone.BottomLeft, "\uE756", "Bottom-left tool zone for build and debug output."),
            CreateTool("toggle-terminal", "Terminal", DockZone.BottomRight, "\uE756", "Bottom-right tool zone for commands and tasks.")
        ];
        layoutService = new DockLayoutService(tools);
        layoutService.OpenDocument(new WorkspaceDocument
        {
            Id = "toggle-editor",
            Title = "Workspace",
            Text = "ToggleDocking manages all six tool zones in one layout tree.",
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
        PageRoot.LayoutUpdated += OnRootLayoutUpdated;
        SampleChecks.RunWhenLoaded(this, RunScenarioChecksAsync);
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
            Text = "DockLayoutService.OpenDocument adds dynamic documents to the MVVM layout.",
            IsModified = false
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
        MoveToolToZone(SelectedTool, zone);
    }

    private void MoveToolToZone(LayoutAnchorable? tool, DockZone zone)
    {
        if (tool is null)
        {
            StatusText.Text = "No tool is available to move.";
            return;
        }

        Manager.MoveAnchorableToZone(tool, zone);
        if (tool.Content is WorkspaceTool model)
        {
            model.Zone = zone;
        }
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

        StatusText.Text = GetActiveToolStatus(model);
    }

    private void OnRootLayoutUpdated(object? sender, object args)
    {
        if (StatusText.Text.StartsWith("Current tool: ", StringComparison.Ordinal)
            && Manager.ActiveContent is WorkspaceTool model)
        {
            string status = GetActiveToolStatus(model);
            if (StatusText.Text != status)
            {
                StatusText.Text = status;
            }
        }
    }

    private string GetActiveToolStatus(WorkspaceTool model)
    {
        LayoutAnchorable? tool = Manager.Layout?.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(item => ReferenceEquals(item.Content, model));
        DockZone zone = tool is null ? model.Zone : GetToolZone(tool);
        return $"Current tool: {model.Title} ({zone}).";
    }

    private DockZone GetToolZone(LayoutAnchorable tool) => PageRoot.FindVisualChildren<ToggleDockButton>()
        .FirstOrDefault(button => ReferenceEquals(button.Anchorable, tool))?.Zone
        ?? (tool.Content as WorkspaceTool)?.Zone ?? DockZone.LeftTop;

    public async Task RunScenarioChecksAsync(SampleChecks checks)
    {
        await SampleChecks.SettleAsync();
        SampleChecks.Require(layoutService.Anchorables.Count() == 6, "Toggle created six initial tool models.");
        SampleChecks.Require(layoutService.Documents.Count() == 2, "Toggle created the initial document models.");
        foreach (WorkspaceTool tool in layoutService.Anchorables.OfType<WorkspaceTool>())
        {
            SampleChecks.Require(Manager.Layout.Descendents().OfType<LayoutAnchorable>().Any(item => ReferenceEquals(item.Content, tool)), $"Toggle synchronized tool {tool.Title} with the layout.");
        }
        checks.Record("Toggle synchronized all six tool-zone models.");

        ToggleDockButton[] buttons = PageRoot.FindVisualChildren<ToggleDockButton>().ToArray();
        SampleChecks.Require(buttons.Length == 6, "Toggle created sidebar buttons for all six tools.");
        SampleChecks.Require(buttons.All(button => Math.Abs(button.ActualWidth - Manager.ButtonSize) < 0.5
            && button.FindVisualChildren<Border>().Any(border => border.Name == "Indicator")),
            "Toggle sidebar buttons use WPFUI dimensions and selection-indicator templates.");
        SampleChecks.Require(PageRoot.FindVisualChildren<Grid>().Any(grid => grid.Name == "PART_ToggleNavigationGrid")
            && PageRoot.FindVisualChildren<Border>().Any(border => border.Name == "PART_LeftNavigationFrame")
            && PageRoot.FindVisualChildren<Border>().Any(border => border.Name == "PART_RightNavigationFrame"),
            "Toggle loaded the left and right navigation-frame templates.");
        LayoutAnchorablePaneControl[] panes = PageRoot.FindVisualChildren<LayoutAnchorablePaneControl>().ToArray();
        SampleChecks.Require(panes.Length > 0 && panes.All(pane => pane.FindVisualChildren<Border>()
            .Any(border => border.Name == "PaneFrame" && border.CornerRadius.TopLeft == 6)),
            "Toggle tool panes use rounded-border templates.");
        SampleChecks.Require(panes.Where(pane => ((LayoutAnchorablePane)pane.Model).ChildrenCount == 1)
            .All(pane => pane.FindVisualChildren<Border>().Any(border => border.Name == "TabStrip" && border.Visibility == Visibility.Collapsed)),
            "Toggle single-tool panes hide the bottom tab strip.");
        SampleChecks.Require(PageRoot.FindVisualChildren<ToggleAnchorablePaneTitle>()
            .All(title => Math.Abs(title.ActualHeight - 32) < 0.5),
            "Toggle pane titles use a height of 32 pixels.");
        Border navigationFrame = PageRoot.FindVisualChildren<Border>()
            .First(border => border.Name == "PART_LeftNavigationFrame");
        ElementTheme originalTheme = PageRoot.RequestedTheme;
        PageRoot.RequestedTheme = ElementTheme.Light;
        await SampleChecks.SettleAsync();
        global::Windows.UI.Color lightSurface = ((SolidColorBrush)navigationFrame.Background).Color;
        PageRoot.RequestedTheme = ElementTheme.Dark;
        await SampleChecks.SettleAsync();
        global::Windows.UI.Color darkSurface = ((SolidColorBrush)navigationFrame.Background).Color;
        PageRoot.RequestedTheme = originalTheme;
        SampleChecks.Require(lightSurface != darkSurface, "Toggle navigation frames respond to Light/Dark theme changes.");
        checks.Record("Toggle WPFUI manager, sidebar, and pane templates passed.");

        await CheckZonePreviewGeometryAsync(checks);

        LayoutAnchorable selectedTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault()
            ?? throw new InvalidOperationException("Toggle has no available tool.");
        WorkspaceTool model = selectedTool.Content as WorkspaceTool
            ?? throw new InvalidOperationException("Toggle tool content is not a WorkspaceTool.");
        DockZone originalZone = model.Zone;
        MoveToolToZone(selectedTool, DockZone.RightBottom);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(ReferenceEquals(selectedTool.Content, model), "Toggle zone moves preserve content references.");
        SampleChecks.Require(model.Zone == DockZone.RightBottom, "Toggle updated the tool model to the target zone.");
        ToggleDockButton movedButton = PageRoot.FindVisualChildren<ToggleDockButton>()
            .Single(button => ReferenceEquals(button.Anchorable, selectedTool));
        Border movedIndicator = movedButton.FindVisualChildren<Border>().Single(border => border.Name == "Indicator");
        Rect indicatorBounds = movedIndicator.TransformToVisual(movedButton)
            .TransformBounds(new Rect(0, 0, movedIndicator.ActualWidth, movedIndicator.ActualHeight));
        SampleChecks.Require(movedButton.Zone == DockZone.RightBottom
            && movedIndicator.HorizontalAlignment == HorizontalAlignment.Right
            && Math.Abs(indicatorBounds.Right - movedButton.ActualWidth) < 0.5
            && Math.Abs((indicatorBounds.Top + indicatorBounds.Bottom) / 2 - movedButton.ActualHeight / 2) < 0.5,
            "After moving a tool to the right zone, its indicator remains right-aligned and vertically centered.");
        checks.Record($"Toggle tool zone move passed ({originalZone} -> {model.Zone}).");

        StatusText.Text = GetActiveToolStatus(model);
        Manager.MoveAnchorableToZone(selectedTool, DockZone.BottomRight);
        await SampleChecks.SettleAsync();
        SampleChecks.Require(model.Zone == DockZone.RightBottom
            && StatusText.Text == $"Current tool: {model.Title} (BottomRight).",
            "After a drag changes the actual zone, the status uses the current sidebar-button zone.");
        ToggleTool_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(selectedTool.IsAutoHidden && selectedTool.FindParent<LayoutAnchorSide>()?.Side == AnchorSide.Bottom,
            "After dragging, the menu collapses the tool in its actual bottom zone.");
        ToggleTool_Click(this, new RoutedEventArgs());
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!selectedTool.IsAutoHidden && GetToolZone(selectedTool) == DockZone.BottomRight,
            "After dragging, the menu expands the tool in its actual zone.");
        Manager.MoveAnchorableToZone(selectedTool, DockZone.RightBottom);
        await SampleChecks.SettleAsync();
        checks.Record("Toggle status follows actual zone changes.");

        selectedTool.Hide();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(Manager.Layout.Hidden.Contains(selectedTool), "Toggle tool can be hidden.");
        selectedTool.Show();
        await SampleChecks.SettleAsync();
        SampleChecks.Require(!Manager.Layout.Hidden.Contains(selectedTool), "Toggle hidden tool can be restored.");
        checks.Record("Toggle hide and restore passed.");

        MethodInfo hideFromMenu = typeof(ToggleDockingManager).GetMethod("HideAnchorableFromMenu", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Toggle hide-menu entry was not found.");
        int canceledHides = 0;
        EventHandler<AnchorableHidingEventArgs> cancelHide = (_, args) =>
        {
            canceledHides++;
            args.Cancel = true;
        };
        Manager.AnchorableHiding += cancelHide;
        try
        {
            hideFromMenu.Invoke(Manager, [selectedTool]);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(canceledHides == 1 && !selectedTool.IsHidden
                && PageRoot.FindVisualChildren<ToggleDockButton>().Any(button => ReferenceEquals(button.Anchorable, selectedTool)),
                "A canceled hide preserves the Toggle sidebar button.");

            Manager.DetachAnchorableToWindow(selectedTool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(Manager.IsDetached(selectedTool), "Toggle tool entered a detached window.");
            hideFromMenu.Invoke(Manager, [selectedTool]);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(canceledHides == 2 && Manager.IsDetached(selectedTool) && !selectedTool.IsHidden,
                "A canceled hide preserves the Toggle detached window.");

            MethodInfo floatFromMenu = typeof(ToggleDockingManager).GetMethod("FloatAnchorableFromMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Toggle float-menu entry was not found.");
            int canceledFloats = 0;
            EventHandler<ContentFloatingEventArgs> cancelFloat = (_, args) =>
            {
                canceledFloats++;
                args.Cancel = true;
            };
            Manager.ContentFloating += cancelFloat;
            try
            {
                floatFromMenu.Invoke(Manager, [selectedTool]);
                await SampleChecks.SettleAsync();
                SampleChecks.Require(canceledFloats == 1 && Manager.IsDetached(selectedTool) && !selectedTool.IsFloating,
                    "A canceled float preserves the Toggle detached window.");
            }
            finally
            {
                Manager.ContentFloating -= cancelFloat;
            }
        }
        finally
        {
            Manager.AnchorableHiding -= cancelHide;
            Manager.ReattachAnchorable(selectedTool);
        }
        await SampleChecks.SettleAsync();
        checks.Record("Toggle preserves sidebar buttons and detached windows when menu hide or float is canceled.");

        LayoutAnchorable secondTool = Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .First(tool => !ReferenceEquals(tool, selectedTool));
        try
        {
            Manager.DetachAnchorableToWindow(selectedTool);
            Manager.DetachAnchorableToWindow(secondTool);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(Manager.IsDetached(selectedTool) && Manager.IsDetached(secondTool),
                "Toggle allows two tools in detached windows at the same time.");
            selectedTool.IsActive = true;
            DetachTool_Click(this, new RoutedEventArgs());
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!Manager.IsDetached(selectedTool) && Manager.IsDetached(secondTool),
                "Reattaching the current Toggle tool leaves other detached windows alone.");
        }
        finally
        {
            Manager.ReattachAnchorable(selectedTool);
            Manager.ReattachAnchorable(secondTool);
        }
        checks.Record("Reattaching the current Toggle tool affects only the selected tool.");

        bool originalFloating = Manager.AllowDetachedWindows;
        Manager.AllowDetachedWindows = false;
        Manager.AllowFloatingWindows = false;
        SampleChecks.Require(!Manager.AllowDetachedWindows && !Manager.AllowFloatingWindows, "Toggle window policies can be disabled.");
        Manager.AllowDetachedWindows = originalFloating;
        Manager.AllowFloatingWindows = originalFloating;
        checks.Record("Toggle window policy passed.");

        await CheckLayoutPrioritiesAsync(checks);
    }

    private async Task CheckZonePreviewGeometryAsync(SampleChecks checks)
    {
        Dictionary<DockZone, LayoutAnchorable> tools = Manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Where(tool => tool.Content is WorkspaceTool)
            .ToDictionary(tool => ((WorkspaceTool)tool.Content!).Zone);
        Type overlayType = typeof(ToggleDockingManager).Assembly.GetType("AvalonDock.Controls.ToggleDockDragOverlay")
            ?? throw new InvalidOperationException("Toggle drag-indicator overlay was not found.");
        ConstructorInfo constructor = overlayType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        using IDisposable overlay = (IDisposable)constructor.Invoke([Manager, tools[DockZone.LeftTop]]);
        MethodInfo update = overlayType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Toggle indicator update entry was not found.");
        MethodInfo hit = overlayType.GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Toggle indicator hit-test entry was not found.");
        FieldInfo zones = overlayType.GetField("zones", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Toggle indicator zones were not found.");
        PropertyInfo failure = overlayType.GetProperty("Failure", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Toggle indicator failure state was not found.");
        double? leftSplitGap = null;

        (DockZone First, DockZone Second)[] pairs =
        [
            (DockZone.LeftTop, DockZone.LeftBottom),
            (DockZone.RightTop, DockZone.RightBottom),
            (DockZone.BottomLeft, DockZone.BottomRight)
        ];
        foreach ((DockZone first, DockZone second) in pairs)
        {
            Manager.ActiveContent = tools[second].Content;
            await SampleChecks.SettleAsync();
            VerifyIndicators(first, second);
            VerifyPair(first, second, splitEvenly: false);

            Manager.ToggleAnchorable(tools[first], first);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(tools[first].IsAutoHidden && !tools[second].IsAutoHidden,
                $"After collapsing {first}, only {second} is expanded.");
            VerifyIndicators(first, second);
            VerifyPair(first, second, splitEvenly: true);

            Manager.ToggleAnchorable(tools[first], first);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!tools[first].IsAutoHidden && !tools[second].IsAutoHidden,
                $"After expanding {first} again, both tools are expanded.");
            VerifyIndicators(first, second);
            Manager.ActiveContent = tools[second].Content;
            await SampleChecks.SettleAsync();
            VerifyIndicators(first, second);
            Manager.ToggleAnchorable(tools[second], second);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(!tools[first].IsAutoHidden && tools[second].IsAutoHidden,
                $"After collapsing {second}, only {first} is expanded.");
            VerifyIndicators(first, second);
            VerifyPair(first, second, splitEvenly: true);

            Manager.ToggleAnchorable(tools[first], first);
            await SampleChecks.SettleAsync();
            SampleChecks.Require(tools[first].IsAutoHidden && tools[second].IsAutoHidden,
                $"Both {first} and {second} are collapsed.");
            VerifyIndicators(first, second);
            VerifyPair(first, second, splitEvenly: true);

            Manager.ToggleAnchorable(tools[first], first);
            Manager.ToggleAnchorable(tools[second], second);
            await SampleChecks.SettleAsync();
            VerifyIndicators(first, second);
        }

        SampleChecks.Require(failure.GetValue(overlay) == null, "Toggle drag indicators render correctly.");
        checks.Record("Toggle six-zone indicators partition and hit-test correctly with two panes, one pane, or an empty group.");
        checks.Record("Toggle left and right split indicators preserve the same spacing when the sidebar navigation is present.");
        checks.Record("Toggle indicators remain visible for expanded tools and hidden for collapsed tools after repeated same-side toggles.");

        void VerifyIndicators(DockZone first, DockZone second)
        {
            foreach (DockZone zone in new[] { first, second })
            {
                LayoutAnchorable tool = tools[zone];
                ToggleDockButton button = PageRoot.FindVisualChildren<ToggleDockButton>()
                    .Single(candidate => ReferenceEquals(candidate.Anchorable, tool));
                Border indicator = button.FindVisualChildren<Border>().Single(border => border.Name == "Indicator");
                bool expanded = !tool.IsAutoHidden;
                SampleChecks.Require(button.IsChecked == expanded
                    && indicator.Visibility == (expanded ? Visibility.Visible : Visibility.Collapsed)
                    && (!expanded || indicator.ActualWidth > 0 && indicator.ActualHeight > 0),
                    $"Toggle {zone} button indicator matches the tool expansion state.");
            }
        }

        void VerifyPair(DockZone first, DockZone second, bool splitEvenly)
        {
            update.Invoke(overlay, [new Point(0, 0), true]);
            IEnumerable entries = (IEnumerable)(zones.GetValue(overlay)
                ?? throw new InvalidOperationException("Toggle indicator zones have not been generated."));
            Dictionary<DockZone, Rect> bounds = [];
            foreach (object entry in entries)
            {
                Type zoneType = entry.GetType();
                if (zoneType.GetProperty("Label")?.GetValue(entry) == null)
                {
                    continue;
                }

                DockZone target = (DockZone)(zoneType.GetProperty("Target")?.GetValue(entry)
                    ?? throw new InvalidOperationException("Toggle indicator zone has no target."));
                Rect rectangle = (Rect)(zoneType.GetProperty("Bounds")?.GetValue(entry)
                    ?? throw new InvalidOperationException("Toggle indicator zone has no bounds."));
                bounds.Add(target, rectangle);
            }

            Rect firstBounds = bounds[first];
            Rect secondBounds = bounds[second];
            SampleChecks.Require(firstBounds.Width > 0 && firstBounds.Height > 0
                && secondBounds.Width > 0 && secondBounds.Height > 0,
                $"Toggle {first}/{second} indicator zones are nonempty.");
            Rect overlap = firstBounds;
            overlap.Intersect(secondBounds);
            SampleChecks.Require(overlap.IsEmpty || overlap.Width <= 0.5 || overlap.Height <= 0.5,
                $"Toggle {first}/{second} indicator zones do not overlap.");

            if (first is DockZone.LeftTop or DockZone.RightTop)
            {
                double splitGap = secondBounds.Top - firstBounds.Bottom;
                if (first == DockZone.LeftTop)
                {
                    leftSplitGap = splitGap;
                }
                else
                {
                    double baseline = leftSplitGap ?? throw new InvalidOperationException(
                        "Toggle right-side split indicators have a left-side spacing baseline.");
                    SampleChecks.Require(Math.Abs(splitGap - baseline) <= 1.5,
                        $"Toggle left and right split indicators keep equal spacing ({baseline:F1} vs {splitGap:F1} pixels).");
                }
            }

            if (splitEvenly)
            {
                if (first is DockZone.BottomLeft)
                {
                    double expectedGap = Manager.GridSplitterWidth * Manager.XamlRoot.RasterizationScale;
                    SampleChecks.Require(Math.Abs(firstBounds.Width - secondBounds.Width) <= 1.5
                        && Math.Abs(secondBounds.Left - firstBounds.Right - expectedGap) <= 1.5
                        && Math.Abs(firstBounds.Top - secondBounds.Top) <= 1.5
                        && Math.Abs(firstBounds.Height - secondBounds.Height) <= 1.5,
                        $"Toggle empty bottom group splits evenly with a {expectedGap:F1}-pixel splitter gap along the horizontal axis.");
                }
                else
                {
                    double expectedGap = Manager.GridSplitterHeight * Manager.XamlRoot.RasterizationScale;
                    SampleChecks.Require(Math.Abs(firstBounds.Height - secondBounds.Height) <= 1.5
                        && Math.Abs(secondBounds.Top - firstBounds.Bottom - expectedGap) <= 1.5
                        && Math.Abs(firstBounds.Left - secondBounds.Left) <= 1.5
                        && Math.Abs(firstBounds.Width - secondBounds.Width) <= 1.5,
                        $"Toggle {first}/{second} empty group splits evenly with a {expectedGap:F1}-pixel splitter gap along the vertical axis.");
                }
            }

            Point firstCenter = new(firstBounds.X + firstBounds.Width / 2, firstBounds.Y + firstBounds.Height / 2);
            Point secondCenter = new(secondBounds.X + secondBounds.Width / 2, secondBounds.Y + secondBounds.Height / 2);
            SampleChecks.Require((DockZone?)hit.Invoke(overlay, [firstCenter]) == first,
                $"Toggle {first} indicator-zone center hits its target.");
            SampleChecks.Require((DockZone?)hit.Invoke(overlay, [secondCenter]) == second,
                $"Toggle {second} indicator-zone center hits its target.");
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        PageRoot.LayoutUpdated -= OnRootLayoutUpdated;
        Manager.Dispose();
    }
}
