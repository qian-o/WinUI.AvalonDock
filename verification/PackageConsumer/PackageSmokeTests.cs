using System.Collections.ObjectModel;
using System.Diagnostics;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Mvvm;
using AvalonDock.Serializer.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using WinUI.AvalonDock.Experiments.Shared;

namespace WinUI.AvalonDock.Verification.PackageConsumer;

internal static class PackageSmokeTests
{
    internal static async Task RunAsync(DockingManager manager)
    {
        WorkspaceDocument editor = new() { Id = "editor", Title = "Editor" };
        WorkspaceDocument notes = new() { Id = "notes", Title = "Notes" };
        DocumentDock documents = new() { VisibleDockables = new ObservableCollection<IDockable> { editor, notes } };
        manager.LayoutItemContainerStyleSelector = SampleStyles.CreateItemStyleSelector();
        manager.LayoutItemTemplate = (DataTemplate)Application.Current.Resources["ConsumerContentTemplate"];
        manager.DockLayout = new RootDock { VisibleDockables = new ObservableCollection<IDockable> { documents }, ActiveDockable = notes };

        LayoutDocument? FindNotes() => manager.Layout.Descendents().OfType<LayoutDocument>().SingleOrDefault(doc => doc.Content == notes);
        LayoutDocumentPaneControl? Pane(LayoutDocument doc) => manager.GetLayoutItemFromModel(doc)?.View.FindVisualAncestor<LayoutDocumentPaneControl>();

        await Until(() => FindNotes() is { } doc && Pane(doc)?.TabItems.Count == 2, "The package did not render both MVVM documents.");
        LayoutDocument document = FindNotes()!;
        notes.Title = "Updated notes";
        await Until(() => document.Title == notes.Title && document.ContentId == notes.Id, "Document title or identifier binding failed.");

        LayoutDocumentPaneControl pane = Pane(document)!;
        TabViewItem active = (TabViewItem)pane.SelectedItem;
        TabViewItem other = pane.TabItems.Cast<TabViewItem>().Single(tab => tab != active);
        Require(VisualStateManager.GoToState(other, "PointerOver", false), "The inactive tab has no hover state.");
        pane.UpdateLayout();
        Grid tabRoot = other.FindVisualChildren<Grid>().First(grid => grid.Name == "LayoutRoot");
        Border surface = tabRoot.Children.OfType<Border>().Single(border => border.Name == "Surface");
        Rect Bounds(FrameworkElement element) => element.TransformToVisual(pane).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        Rect selected = Bounds(active);
        Rect hover = Bounds(surface);
        Require(selected.Height > 0 && Math.Abs(selected.Top - hover.Top) < 0.1 && Math.Abs(selected.Bottom - hover.Bottom) < 0.1,
            "The inactive tab surface is misaligned.");
        VisualStateManager.GoToState(other, "Normal", false);

        document.Float();
        await Until(() => document.IsFloating && Pane(document) is { IsLoaded: true } floatingPane && floatingPane != pane && BodyTop(floatingPane) == 0,
            "The floating document has an extra header row.");
        LayoutDocumentFloatingWindowControl window = manager.FloatingWindows.OfType<LayoutDocumentFloatingWindowControl>().Single();
        XamlRoot root = ((FrameworkElement)((Window)window).Content).XamlRoot;
        int inset = root.CoordinateConverter.ConvertLocalToScreen(new Point()).Y - window.AppWindow.Position.Y;
        Require(inset >= 0 && inset <= Math.Ceiling(root.RasterizationScale) && window.SingleContentLayoutItem == manager.GetLayoutItemFromModel(document),
            "The floating caption is inset or targets the wrong document.");

        using MemoryStream layout = new();
        XmlLayoutSerializer serializer = new(manager);
        serializer.Serialize(layout);
        document.Dock();
        await Until(() => !manager.FloatingWindows.Any() && Pane(document) is { } docked && BodyTop(docked) > 0,
            "Redocking did not release the window and restore the tab strip.");
        layout.Position = 0;
        serializer.Deserialize(layout);
        await Until(() => FindNotes() is { IsFloating: true, ContentId: "notes" } restored && Pane(restored)?.IsLoaded == true,
            "XML restore did not reconnect the floating document to its original model.");
    }

    private static double? BodyTop(TabControlEx pane) => pane.FindVisualChildren<FrameworkElement>().FirstOrDefault(element => element.Name == "ContentPanel")
        ?.TransformToVisual(pane).TransformPoint(new Point()).Y;

    private static async Task Until(Func<bool> condition, string failure)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            Require(elapsed.Elapsed < TimeSpan.FromSeconds(5), failure);
            await Task.Delay(20);
        }
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
