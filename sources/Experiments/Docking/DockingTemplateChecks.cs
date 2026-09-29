using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUI.AvalonDock.Experiments.Shared;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace WinUI.AvalonDock.Experiments.Docking;

internal static class DockingTemplateChecks
{
    internal static async Task RunAsync(DockingManager manager, FrameworkElement mainRoot,
        WorkspaceDocument sourceDocument, WorkspaceTool sourceTool, WorkspaceTool secondTool,
        Action saveLayout, Action restoreLayout, SampleChecks checks)
    {
        DataTemplate documentContent = Template("SmokeDocumentContentTemplate");
        DataTemplate anchorableContent = Template("SmokeAnchorableContentTemplate");
        WorkspaceContentTemplateSelector selector = new(documentContent, anchorableContent);
        manager.DocumentHeaderTemplate = Template("SmokeDocumentHeaderTemplate");
        manager.AnchorableHeaderTemplate = Template("SmokeAnchorableHeaderTemplate");
        manager.LayoutItemTemplate = null;
        manager.LayoutItemTemplateSelector = selector;
        manager.DocumentTitleTemplate = Template("SmokeDocumentTitleTemplate");
        manager.AnchorableTitleTemplate = Template("SmokeAnchorableTitleTemplate");

        LayoutDocument document = DocumentFor(manager, sourceDocument);
        LayoutAnchorable tool = ToolFor(manager, sourceTool);
        LayoutAnchorable partner = ToolFor(manager, secondTool);
        document.IsSelected = true;
        tool.IsSelected = true;
        await WaitForMarksAsync(mainRoot, sourceDocument, sourceTool, "docked");
        VerifyContentSelector(manager, document, tool, selector, "docked");
        checks.Record("Classic custom document and tool tab templates and the content selector are visible in the main window.");

        document.Float();
        await DockingDragChecks.WaitUntilAsync(() => FloatingWindowFor(manager, document) is { IsLoaded: true },
            "The Classic template check document floating window did not load.");
        LayoutFloatingWindowControl documentWindow = FloatingWindowFor(manager, document)!;
        FrameworkElement documentWindowRoot = WindowRoot(documentWindow);
        FrameworkElement documentContentRoot = ContentRoot(documentWindow);
        await WaitForMarksAsync(() => HasMark(documentContentRoot, "SmokeDocumentHeaderMark", sourceDocument.Title)
            && HasMark(documentContentRoot, "SmokeDocumentContentMark", sourceDocument.Id)
            && HasMark(documentWindowRoot, "SmokeDocumentTitleMark", sourceDocument.Title),
            () => $"Title root: {DescribeMarks(documentWindowRoot)} Content root: {DescribeMarks(documentContentRoot)}",
            "The Classic document floating window did not show the custom tab, content, and title templates together.");

        LayoutAnchorableFloatingWindowControl toolWindow = manager.CreateFloatingWindow(tool, false)
            as LayoutAnchorableFloatingWindowControl
            ?? throw new InvalidOperationException("The Classic template check tool floating window was not created.");
        LayoutAnchorablePane toolPane = (toolWindow.Model as LayoutAnchorableFloatingWindow)?.SinglePane
            as LayoutAnchorablePane
            ?? throw new InvalidOperationException("The Classic template check tool floating window has no pane.");
        toolPane.Children.Add(partner);
        tool.IsSelected = true;
        toolWindow.Show();
        await DockingDragChecks.WaitUntilAsync(() => toolWindow.IsLoaded,
            "The Classic template check floating window with two tools did not load.");
        FrameworkElement toolWindowRoot = WindowRoot(toolWindow);
        FrameworkElement toolContentRoot = ContentRoot(toolWindow);
        await WaitForMarksAsync(() => HasMark(toolContentRoot, "SmokeAnchorableHeaderMark", sourceTool.Title)
            && HasMark(toolContentRoot, "SmokeAnchorableContentMark", sourceTool.Id)
            && HasMark(toolWindowRoot, "SmokeAnchorableTitleMark", sourceTool.Title),
            () => $"Title root: {DescribeMarks(toolWindowRoot)} Content root: {DescribeMarks(toolContentRoot)}",
            "The Classic floating window with two tools did not show the custom tab, content, and title templates together.");
        checks.Record("Classic custom tab, content, and title templates are visible in document and tool floating windows.");
        await VerifyFloatingBorderThemeAsync(mainRoot, document, documentWindow, toolWindow, checks);

        saveLayout();
        restoreLayout();
        await DockingDragChecks.WaitUntilAsync(() =>
        {
            LayoutDocument? restoredDocument = manager.Layout.Descendents().OfType<LayoutDocument>()
                .FirstOrDefault(item => ReferenceEquals(item.Content, sourceDocument));
            LayoutAnchorable? restoredTool = manager.Layout.Descendents().OfType<LayoutAnchorable>()
                .FirstOrDefault(item => ReferenceEquals(item.Content, sourceTool));
            return restoredDocument is { IsFloating: true } && restoredTool is { IsFloating: true }
                && FloatingWindowFor(manager, restoredDocument) is { IsLoaded: true }
                && FloatingWindowFor(manager, restoredTool) is { IsLoaded: true };
        }, "The Classic document or tool floating window did not reload after XML restoration.");

        LayoutDocument recoveredDocument = DocumentFor(manager, sourceDocument);
        LayoutAnchorable recoveredTool = ToolFor(manager, sourceTool);
        recoveredDocument.IsSelected = true;
        recoveredTool.IsSelected = true;
        LayoutFloatingWindowControl recoveredDocumentWindow = FloatingWindowFor(manager, recoveredDocument)!;
        LayoutFloatingWindowControl recoveredToolWindow = FloatingWindowFor(manager, recoveredTool)!;
        FrameworkElement recoveredDocumentWindowRoot = WindowRoot(recoveredDocumentWindow);
        FrameworkElement recoveredDocumentContentRoot = ContentRoot(recoveredDocumentWindow);
        FrameworkElement recoveredToolWindowRoot = WindowRoot(recoveredToolWindow);
        FrameworkElement recoveredToolContentRoot = ContentRoot(recoveredToolWindow);
        await WaitForMarksAsync(() => HasMark(recoveredDocumentContentRoot, "SmokeDocumentHeaderMark", sourceDocument.Title)
            && HasMark(recoveredDocumentContentRoot, "SmokeDocumentContentMark", sourceDocument.Id)
            && HasMark(recoveredDocumentWindowRoot, "SmokeDocumentTitleMark", sourceDocument.Title)
            && HasMark(recoveredToolContentRoot, "SmokeAnchorableHeaderMark", sourceTool.Title)
            && HasMark(recoveredToolContentRoot, "SmokeAnchorableContentMark", sourceTool.Id)
            && HasMark(recoveredToolWindowRoot, "SmokeAnchorableTitleMark", sourceTool.Title),
            () => $"Document title: {DescribeMarks(recoveredDocumentWindowRoot)} Document content: {DescribeMarks(recoveredDocumentContentRoot)} Tool title: {DescribeMarks(recoveredToolWindowRoot)} Tool content: {DescribeMarks(recoveredToolContentRoot)}",
            "Classic custom templates were not fully visible in both floating windows after XML restoration.");
        VerifyContentSelector(manager, recoveredDocument, recoveredTool, selector, "after XML restoration");
        checks.Record("Classic document and tool floating windows still show custom tabs, content selectors, and title templates after XML restoration.");
    }

    private static DataTemplate Template(string key) => Application.Current.Resources[key] as DataTemplate
        ?? throw new InvalidOperationException($"The Classic template check resource {key} was not found.");

    private static LayoutDocument DocumentFor(DockingManager manager, WorkspaceDocument content)
        => manager.Layout.Descendents().OfType<LayoutDocument>()
            .Single(item => ReferenceEquals(item.Content, content));

    private static LayoutAnchorable ToolFor(DockingManager manager, WorkspaceTool content)
        => manager.Layout.Descendents().OfType<LayoutAnchorable>()
            .Single(item => ReferenceEquals(item.Content, content));

    private static LayoutFloatingWindowControl? FloatingWindowFor(DockingManager manager, LayoutContent content)
        => manager.FloatingWindows.FirstOrDefault(window => window.Model.Descendents()
            .OfType<LayoutContent>().Any(item => ReferenceEquals(item, content)));

    private static FrameworkElement WindowRoot(LayoutFloatingWindowControl window)
        => ((Window)window).Content as FrameworkElement
            ?? throw new InvalidOperationException("The Classic template check floating window has no visual root.");

    private static FrameworkElement ContentRoot(LayoutFloatingWindowControl window)
        => window.Content?.GetType().GetProperty("RootVisual")?.GetValue(window.Content) as FrameworkElement
            ?? throw new InvalidOperationException("The Classic template check floating window's XAML root is not attached.");

    private static async Task VerifyFloatingBorderThemeAsync(FrameworkElement mainRoot,
        LayoutDocument document, LayoutFloatingWindowControl documentWindow,
        LayoutFloatingWindowControl toolWindow, SampleChecks checks)
    {
        LayoutFloatingWindowControl[] windows = [documentWindow, toolWindow];
        ElementTheme originalTheme = mainRoot.RequestedTheme;
        try
        {
            foreach (ElementTheme theme in new[] { ElementTheme.Dark, ElementTheme.Light })
            {
                mainRoot.RequestedTheme = theme;
                await DockingDragChecks.WaitUntilAsync(() => windows.All(window => WindowRoot(window).ActualTheme == theme
                    && ContentRoot(window).ActualTheme == theme),
                    $"Classic floating windows did not switch to the {(theme == ElementTheme.Dark ? "dark" : "light")} theme.");
                if (theme == ElementTheme.Dark)
                {
                    document.IsSelected = true;
                    document.IsActive = true;
                    await DockingDragChecks.WaitUntilAsync(() => document.IsActive && windows.All(window =>
                        WindowRoot(window).FindVisualChildren<Border>().Any(border => border.Name == "WindowBorder"
                            && border.IsLoaded && border.Visibility == Visibility.Visible
                            && border.ActualWidth > 0 && border.ActualHeight > 0))
                        && ContentRoot(documentWindow).FindVisualChildren<LayoutDocumentPaneControl>()
                            .Any(control => control.Model is LayoutDocumentPane { IsDirectlyHostedInFloatingWindow: true }
                                && control.SelectedItem is TabViewItem { Tag: LayoutDocument { IsActive: true } })
                        && DocumentPaneOutline(ContentRoot(documentWindow)) is
                        {
                            IsLoaded: true, Data: not null,
                            ActualWidth: > 0, ActualHeight: > 0
                        },
                        "The Classic dark floating window border or active document outline did not load.");

                    foreach (LayoutFloatingWindowControl window in windows)
                    {
                        Border border = WindowRoot(window).FindVisualChildren<Border>()
                            .Single(element => element.Name == "WindowBorder");
                        SampleChecks.Require(IsLowContrastDarkStroke(border.BorderBrush, border.Background),
                            "The Classic dark floating window XAML border is still bright.");
                    }

                    LayoutDocumentPaneControl pane = ContentRoot(documentWindow)
                        .FindVisualChildren<LayoutDocumentPaneControl>()
                        .Single(control => control.Model is LayoutDocumentPane { IsDirectlyHostedInFloatingWindow: true });
                    Path outline = DocumentPaneOutline(ContentRoot(documentWindow))!;
                    SampleChecks.Require(IsLowContrastDarkStroke(outline.Stroke, pane.Background, outline.Opacity),
                        "The active document pane in the Classic dark floating window still has a bright, high contrast outline.");
                }
            }
            checks.Record("Classic document and tool floating windows switch between light and dark themes; dark XAML borders and active document outlines have low contrast.");
        }
        finally
        {
            mainRoot.RequestedTheme = originalTheme;
            await SampleChecks.SettleAsync();
        }
    }

    private static Path? DocumentPaneOutline(FrameworkElement root)
        => root.FindVisualChildren<LayoutDocumentPaneControl>()
            .FirstOrDefault(control => control.Model is LayoutDocumentPane { IsDirectlyHostedInFloatingWindow: true })?
            .FindVisualChildren<Grid>().FirstOrDefault(grid => grid.Name == "PaneBorder")?
            .FindVisualChildren<Path>().FirstOrDefault();

    private static bool IsLowContrastDarkStroke(Brush? stroke, Brush? background, double elementOpacity = 1)
    {
        if (stroke is not SolidColorBrush line || background is not SolidColorBrush surface)
        {
            return false;
        }

        if (surface.Color.A != 255 || surface.Opacity < 0.99)
        {
            return false;
        }

        double alpha = line.Color.A / 255d * line.Opacity * elementOpacity;
        double red = surface.Color.R + (line.Color.R - surface.Color.R) * alpha;
        double green = surface.Color.G + (line.Color.G - surface.Color.G) * alpha;
        double blue = surface.Color.B + (line.Color.B - surface.Color.B) * alpha;
        return Math.Max(red, Math.Max(green, blue)) < 112
            && Math.Max(Math.Abs(red - surface.Color.R), Math.Max(Math.Abs(green - surface.Color.G),
                Math.Abs(blue - surface.Color.B))) < 72;
    }

    private static async Task WaitForMarksAsync(FrameworkElement root, WorkspaceDocument document,
        WorkspaceTool tool, string stage)
    {
        await DockingDragChecks.WaitUntilAsync(() => HasMark(root, "SmokeDocumentHeaderMark", document.Title)
            && HasMark(root, "SmokeDocumentContentMark", document.Id)
            && HasMark(root, "SmokeAnchorableHeaderMark", tool.Title)
            && HasMark(root, "SmokeAnchorableContentMark", tool.Id),
            $"Classic {stage}: custom document and tool tab and content templates were not shown.");
    }

    private static bool HasMark(FrameworkElement root, string name, string? expectedTag)
        => root.FindVisualChildren<TextBlock>().Any(mark => mark.Name == name
            && Equals(mark.Tag, expectedTag)
            && mark.IsLoaded && mark.XamlRoot is not null
            && mark.Visibility == Visibility.Visible && mark.ActualWidth > 0 && mark.ActualHeight > 0);

    private static string DescribeMarks(FrameworkElement root)
        => string.Join("; ", root.FindVisualChildren<TextBlock>()
            .Where(mark => mark.Name.StartsWith("Smoke", StringComparison.Ordinal))
            .Select(mark => $"{mark.Name}:Tag={mark.Tag},Loaded={mark.IsLoaded},Visible={mark.Visibility},Size={mark.ActualWidth:F1}x{mark.ActualHeight:F1}"));

    private static async Task WaitForMarksAsync(Func<bool> ready, Func<string> describe, string message)
    {
        try
        {
            await DockingDragChecks.WaitUntilAsync(ready, message);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{message} {describe()}", exception);
        }
    }

    private static void VerifyContentSelector(DockingManager manager, LayoutDocument document,
        LayoutAnchorable tool, DataTemplateSelector selector, string stage)
    {
        LayoutItem documentItem = manager.GetLayoutItemFromModel(document)
            ?? throw new InvalidOperationException($"Classic {stage}: the document layout item was not created.");
        LayoutItem toolItem = manager.GetLayoutItemFromModel(tool)
            ?? throw new InvalidOperationException($"Classic {stage}: the tool layout item was not created.");
        SampleChecks.Require(ReferenceEquals(documentItem.View.ContentTemplateSelector, selector)
            && ReferenceEquals(toolItem.View.ContentTemplateSelector, selector)
            && documentItem.View.ContentTemplate is null && toolItem.View.ContentTemplate is null,
            $"Classic {stage}: the content presenter did not use the consumer DataTemplateSelector.");
    }

    private sealed class WorkspaceContentTemplateSelector(DataTemplate document, DataTemplate tool) : DataTemplateSelector
    {
        protected override DataTemplate SelectTemplateCore(object item)
            => item is WorkspaceTool ? tool : document;

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
            => SelectTemplateCore(item);
    }
}
