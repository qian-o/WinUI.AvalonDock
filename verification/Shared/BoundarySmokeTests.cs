using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using DropDownButton = AvalonDock.Controls.DropDownButton;

namespace WinUI.AvalonDock.Verification;

/// <summary>Runs opt-in boundary checks on the real WinUI dispatcher and native window roots.</summary>
internal static class BoundarySmokeTests
{
    internal static void StartIfRequested(Window window, DockingManager sample)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        if (OverlayLifetimeSmokeTests.StartIfRequested(window, sample, arguments))
        {
            return;
        }
        int option = Array.IndexOf(arguments, "--boundary-smoke");
        if (option < 0)
        {
            return;
        }

        string output = option + 1 < arguments.Length ? arguments[option + 1]
            : Path.Combine(AppContext.BaseDirectory, "boundary-smoke.log");
        sample.Loaded += Run;
        async void Run(object sender, RoutedEventArgs args)
        {
            sample.Loaded -= Run;
            List<string> checks = [];
            DockingManager? manager = null;
            try
            {
                await Settle();
                LayoutDocument initial = sample.Layout.Descendents().OfType<LayoutDocument>().First();
                object? initialContent = initial.Content;
                Check(sample.GetLayoutItemFromModel(initial) != null, "sample creates document containers");
                void Cancel(object? _, DocumentClosingEventArgs e) => e.Cancel = true;
                sample.DocumentClosing += Cancel;
                initial.Close();
                Check(ReferenceEquals(initial.Root, sample.Layout), "canceled document close retains layout");
                sample.DocumentClosing -= Cancel;
                initial.Close();
                await Settle();
                Check(initial.Root == null, "clean document close removes layout node");
                if (sample.DocumentsSource is { } source)
                {
                    Check(!source.Cast<object>().Any(item => ReferenceEquals(item, initialContent)),
                        "clean close synchronizes sample source collection");
                }

                sample.Dispose();
                manager = sample is ToggleDockingManager ? new ToggleDockingManager() : new DockingManager();
                ContextSource context = new() { Value = new object() };
                manager.DataContext = new object();
                manager.FontSize = 19;
                UserControl inherited = new() { Content = new TextBlock { Text = "Inherited editor" } };
                UserControl bound = new() { Content = new TextBox { Text = "Bound editor" } };
                Binding binding = new() { Source = context, Path = new PropertyPath(nameof(ContextSource.Value)) };
                bound.SetBinding(FrameworkElement.DataContextProperty, binding);
                Style userStyle = new(typeof(UserControl));
                userStyle.Setters.Add(new Setter(Control.FontSizeProperty, 25d));
                bound.Style = userStyle;
                ResourceDictionary userResources = new();
                userResources["ConsumerMarker"] = new object();
                bound.Resources.MergedDictionaries.Add(userResources);
                LayoutDocument first = new() { Title = "Inherited", Content = inherited, ContentId = "inherited" };
                LayoutDocument second = new() { Title = "Bound", Content = bound, ContentId = "bound" };
                UserControl toolContent = new();
                toolContent.SetBinding(FrameworkElement.DataContextProperty, binding);
                LayoutAnchorable tool = new() { Title = "Tool", Content = toolContent };
                LayoutDocumentPane documents = new();
                documents.Children.Add(first);
                documents.Children.Add(second);
                LayoutAnchorablePane tools = new();
                tools.Children.Add(tool);
                LayoutPanel panel = new();
                panel.Children.Add(tools);
                panel.Children.Add(documents);
                manager.Layout = new LayoutRoot { RootPanel = panel };
                window.Content = manager;
                await Settle();
                AssertContent("initial docking");
                Check(ReferenceEquals(manager.GetLayoutItemFromModel(first)!.View.DataContext, manager.DataContext),
                    "manager context reaches owned presenter");
                LayoutDocumentPaneControl dockedPane = FindChild<LayoutDocumentPaneControl>(manager)!;
                Check(HeadersAlign(dockedPane), "selected outline, inactive hover surface, labels and close buttons align");
                if (manager is not ToggleDockingManager)
                {
                    LayoutAnchorable otherTool = new() { Title = "Another tool" };
                    tools.Children.Add(otherTool);
                    await Settle();
                    Check(HeadersAlign(FindChild<LayoutAnchorablePaneControl>(manager)!), "bottom tool tabs align with their hover surfaces and content edge");
                    otherTool.Close();
                }
                second.Float();
                await Settle();
                Check(second.IsFloating, "document floats into a native window");
                AssertContent("floating");
                LayoutDocumentFloatingWindowControl floating = manager.FloatingWindows.OfType<LayoutDocumentFloatingWindowControl>().Single();
                LayoutDocumentPaneControl floatingPane = FindAncestor<LayoutDocumentPaneControl>(bound)!;
                LayoutDocumentPane floatingModel = (LayoutDocumentPane)floatingPane.Model;
                FrameworkElement windowRoot = (FrameworkElement)((Window)floating).Content;
                double topInset = windowRoot.XamlRoot.CoordinateConverter.ConvertLocalToScreen(new Point()).Y - floating.AppWindow.Position.Y;
                Check(topInset >= 0 && topInset <= Math.Ceiling(windowRoot.XamlRoot.RasterizationScale),
                    $"custom caption meets the restored window edge (top inset {topInset}px)");
                nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(floating);
                var top = floating.AppWindow.Position;
                int center = top.X + floating.AppWindow.Size.Width / 2;
                Check(HitTest(hwnd, center, top.Y + 1) == 12 && HitTest(hwnd, center, top.Y + (int)(18 * windowRoot.XamlRoot.RasterizationScale)) == 2,
                    "the restored top edge resizes and the caption below it drags");
                Check(HeaderMatches(floatingPane, false) && floatingModel.ShowHeader,
                    "a single floating document uses only its caption and preserves its saved header preference");
                Check(ReferenceEquals(floating.SingleContentLayoutItem, manager.GetLayoutItemFromModel(second)),
                    "floating caption commands target the visible document");
                DropDownButton menuButton = FindChild<DropDownButton>(windowRoot, "PART_CaptionMenu")!;
                Check(menuButton.Visibility == Visibility.Visible && ReferenceEquals(menuButton.DropDownContextMenuDataContext, floating.SingleContentLayoutItem),
                    "the single caption exposes the document menu to keyboard and pointer input");
                ControlTemplate defaultCaption = floating.Template!;
                floating.Template = (ControlTemplate)XamlReader.Load("""
                    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ContentControl">
                        <ContentPresenter Content="{TemplateBinding Content}" />
                    </ControlTemplate>
                    """);
                floating.ApplyTemplate();
                await Settle();
                floatingPane = FindAncestor<LayoutDocumentPaneControl>(bound)!;
                Check(HeaderMatches(floatingPane, true), "a window template without document commands keeps the tab accessible");
                floating.Template = defaultCaption;
                floating.ApplyTemplate();
                await Settle();
                floatingPane = FindAncestor<LayoutDocumentPaneControl>(bound)!;
                Check(HeaderMatches(floatingPane, false), "restoring the default caption removes the redundant tab again");
                LayoutDocument extra = new() { Title = "Second floating tab" };
                floatingModel.Children.Add(extra);
                extra.IsSelected = true;
                await Settle();
                Check(HeaderMatches(floatingPane, true) && ReferenceEquals(floating.SingleContentLayoutItem, manager.GetLayoutItemFromModel(extra)),
                    "adding and selecting a floating document restores its strip and updates caption commands");
                floatingModel.ShowHeader = false;
                await Settle();
                Check(HeaderMatches(floatingPane, false), "a hidden header stays hidden with multiple floating documents");
                floatingModel.ShowHeader = true;
                extra.Close();
                await Settle();
                Check(HeaderMatches(floatingPane, false), "removing the second floating tab collapses the redundant strip");
                LayoutDocument split = new() { Title = "Split document" };
                ((LayoutDocumentFloatingWindow)floating.Model).RootPanel!.Children.Add(new LayoutDocumentPane(split));
                await Settle();
                Check(HeaderMatches(floatingPane, true) && floating.SingleContentLayoutItem == null,
                    "split floating panes keep their headers and clear the single-pane command target");
                split.Close();
                second.Dock();
                await Settle();
                AssertContent("redocking");
                Check(HeaderMatches(FindAncestor<LayoutDocumentPaneControl>(bound)!, true),
                    "redocking restores the document strip");
                tool.Hide();
                await Settle();
                tool.Show();
                await Settle();
                Check(ReferenceEquals(toolContent.GetBindingExpression(FrameworkElement.DataContextProperty)?.ParentBinding, binding),
                    "tool hide and show preserve content binding");
                manager.DetachAnchorableToWindow(tool);
                await Settle();
                Check(manager.IsDetached(tool), "tool detaches into an independent native window");
                Check(toolContent.ReadLocalValue(Control.FontSizeProperty) == DependencyProperty.UnsetValue,
                    "detached tool has no injected local font size");
                Check(toolContent.FontSize == manager.FontSize, "detached tool inherits manager font through owned presenter");
                manager.FontSize = 23;
                await Settle();
                Check(toolContent.FontSize == manager.FontSize, "detached tool follows manager font changes");
                manager.ReattachAnchorable(tool);
                await Settle();
                Check(ReferenceEquals(toolContent.GetBindingExpression(FrameworkElement.DataContextProperty)?.ParentBinding, binding),
                    "tool reattachment preserves content binding");
                object nextContext = new();
                context.Value = nextContext;
                manager.DataContext = new object();
                await Settle();
                Check(ReferenceEquals(bound.DataContext, nextContext), "consumer binding remains live after transitions");

                GenericTheme theme = new();
                ResourceDictionary consumerTheme = new() { Source = theme.GetResourceUri() };
                manager.Resources.MergedDictionaries.Add(consumerTheme);
                manager.Theme = theme;
                ResourceDictionary ownedTheme = manager.Resources.MergedDictionaries.Last();
                manager.Theme = null;
                Check(!manager.Resources.MergedDictionaries.Contains(consumerTheme), "URI theme removal uses upstream first-match rule");
                Check(manager.Resources.MergedDictionaries.Contains(ownedTheme), "duplicate URI dictionary ordering matches upstream behavior");

                Style menuStyle = new(typeof(MenuItemEx));
                menuStyle.Setters.Add(new Setter(MenuFlyoutItem.TextProperty, "Consumer menu"));
                MenuItemEx suppliedItem = new() { Style = menuStyle };
                RefreshableMenu suppliedMenu = new() { ItemsSource = new[] { suppliedItem } };
                suppliedMenu.Refresh();
                suppliedMenu.ItemContainerStyle = new Style(typeof(MenuItemEx));
                Check(ReferenceEquals(suppliedItem.Style, menuStyle), "menu keeps consumer-owned item style");

                Type menuContext = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls.MenuFlyoutContext", true)!;
                MenuFlyout menu = new();
                MenuFlyoutItem policyItem = new() { Text = "Policy", Tag = "AvalonDock.Menu.Close", Visibility = Visibility.Collapsed };
                menu.Items.Add(policyItem);
                menuContext.GetMethod("SetContext", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [menu, null]);
                Check(policyItem.Visibility == Visibility.Collapsed, "menu policy keeps consumer visibility");
                Style policyStyle = new(typeof(MenuFlyoutItem));
                policyStyle.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
                MenuFlyoutItem styledPolicy = new() { Text = "Styled default policy", Tag = "AvalonDock.Menu.Close", Style = policyStyle };
                menu.Items.Add(styledPolicy);
                menuContext.GetMethod("SetContext", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [menu, null]);
                Check(styledPolicy.Visibility == Visibility.Visible && ReferenceEquals(styledPolicy.Style, policyStyle),
                    "default menu visibility policy has upstream local-binding priority over a style setter");

                ContextSource visibilitySource = new() { Value = Visibility.Collapsed };
                Binding visibilityBinding = new() { Source = visibilitySource, Path = new PropertyPath(nameof(ContextSource.Value)) };
                policyItem.SetBinding(UIElement.VisibilityProperty, visibilityBinding);
                ContextSource styleSource = new() { Value = new Style(typeof(MenuFlyoutPresenter)) };
                Binding presenterBinding = new() { Source = styleSource, Path = new PropertyPath(nameof(ContextSource.Value)) };
                BindingOperations.SetBinding(menu, MenuFlyout.MenuFlyoutPresenterStyleProperty, presenterBinding);
                menu.Items.Add(new MenuFlyoutItem { Text = "Visible item" });
                menuContext.GetMethod("AttachResources", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [menu, manager.Resources]);
                menu.ShowAt(manager);
                await Settle();
                Check(menu.IsOpen, "resource-scoped menu opens on a real XAML root");
                menu.Hide();
                await Settle();
                Check(ReferenceEquals((menu.ReadLocalValue(MenuFlyout.MenuFlyoutPresenterStyleProperty) as BindingExpression)?.ParentBinding, presenterBinding),
                    "menu opening and closing preserve presenter style binding");
                Check(ReferenceEquals(policyItem.GetBindingExpression(UIElement.VisibilityProperty)?.ParentBinding, visibilityBinding),
                    "menu opening and closing preserve item visibility binding");
                visibilitySource.Value = Visibility.Visible;
                Style nextPresenterStyle = new(typeof(MenuFlyoutPresenter));
                styleSource.Value = nextPresenterStyle;
                await Settle();
                Check(policyItem.Visibility == Visibility.Visible && ReferenceEquals(menu.MenuFlyoutPresenterStyle, nextPresenterStyle),
                    "menu consumer bindings remain live after close");

                ContextSource templateModel = new() { Name = "Content model" };
                manager.DataContext = new ContextSource { Name = "Manager context" };
                LayoutDocument templated = new() { Title = "Template", Content = templateModel };
                documents.Children.Add(templated);
                manager.LayoutItemTemplate = (DataTemplate)XamlReader.Load("""
                    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                    """);
                templated.IsSelected = true;
                await Settle();
                TextBlock? templateText = FindChild<TextBlock>(manager.GetLayoutItemFromModel(templated)!.View);
                Check(templateText?.Text == templateModel.Name, "content template binds its model rather than manager context");
                templated.Float();
                await Settle();
                Check(FindChild<TextBlock>(manager.GetLayoutItemFromModel(templated)!.View)?.Text == templateModel.Name,
                    "floating content template retains model context");
                templated.Dock();
                await Settle();
                Check(FindChild<TextBlock>(manager.GetLayoutItemFromModel(templated)!.View)?.Text == templateModel.Name,
                    "redocked content template retains model context");
                manager.LayoutItemTemplate = null;
                first.IsSelected = true;
                await Settle();
                ContentPresenter firstView = manager.GetLayoutItemFromModel(first)!.View;
                LayoutDocumentControl templateOwner = FindAncestor<LayoutDocumentControl>(firstView)
                    ?? throw new InvalidOperationException("Document template owner missing");
                ControlTemplate originalTemplate = templateOwner.Template;
                templateOwner.Template = (ControlTemplate)XamlReader.Load("""
                    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:dock="using:AvalonDock.Controls" TargetType="dock:LayoutDocumentControl">
                        <ContentPresenter x:Name="PART_ContentHost"
                            Content="{Binding LayoutItem.View, RelativeSource={RelativeSource TemplatedParent}}" />
                    </ControlTemplate>
                    """);
                templateOwner.ApplyTemplate();
                await Settle();
                ContentPresenter? customHost = FindChild<ContentPresenter>(templateOwner);
                Check(customHost?.GetBindingExpression(ContentPresenter.ContentProperty) != null
                    && ReferenceEquals(customHost.Content, firstView), "live custom template keeps bound content host");
                templateOwner.Template = originalTemplate;
                templateOwner.ApplyTemplate();
                await Settle();
                Check(firstView.IsLoaded && inherited.IsLoaded, "retired template releases view to restored template");
                first.Float();
                await Settle();
                first.Dock();
                await Settle();
                Check(firstView.IsLoaded && inherited.IsLoaded, "view still moves after custom template replacement");

                Type inheritance = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls.InheritedPresentation", true)!;
                ContentPresenter presenter = new();
                using (IDisposable mirror = (IDisposable)Activator.CreateInstance(inheritance,
                    BindingFlags.Instance | BindingFlags.NonPublic, null, [manager, presenter, true], null)!)
                {
                    presenter.SetBinding(FrameworkElement.DataContextProperty, binding);
                    manager.DataContext = nextContext;
                    inheritance.GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(mirror, null);
                }
                Check(ReferenceEquals(presenter.GetBindingExpression(FrameworkElement.DataContextProperty)?.ParentBinding, binding),
                    "inheritance refresh and disposal keep consumer binding");

                Type contentHostType = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls.LayoutContentViewHost", true)!;
                LayoutDocumentControl owner = new() { Model = first };
                object contentHost = Activator.CreateInstance(contentHostType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, [owner, LayoutDocumentControl.LayoutItemProperty], null)!;
                ContentPresenter boundHost = new();
                Binding hostBinding = new() { Source = context, Path = new PropertyPath("Missing") };
                boundHost.SetBinding(ContentPresenter.ContentProperty, hostBinding);
                contentHostType.GetMethod("Attach", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(contentHost, [boundHost]);
                Check(ReferenceEquals(boundHost.GetBindingExpression(ContentPresenter.ContentProperty)?.ParentBinding, hostBinding),
                    "template content-host binding survives attachment");
                ContentPresenter freeHost = new();
                contentHostType.GetMethod("Attach", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(contentHost, [freeHost]);
                object sentinel = new();
                freeHost.Content = sentinel;
                contentHostType.GetMethod("Release", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(contentHost, [false]);
                Check(ReferenceEquals(freeHost.Content, sentinel), "host release keeps replacement consumer content");

                manager.Dispose();
                AssertContent("manager disposal");
                using (DockingManager syncManager = new())
                {
                    window.Content = syncManager;
                    await Settle();
                    checks.AddRange(await LayoutSyncSmokeTests.RunAsync(syncManager));
                }
                window.Content = new Grid();
                await Settle();
                checks.AddRange(await ContainerStyleCompatibilitySmokeTests.RunAsync());
                checks.AddRange(await WorkspaceLifecycleSmokeTests.RunAsync(window));
                checks.AddRange(await ModalCompatibilitySmokeTests.RunAsync(window));
                File.WriteAllLines(output, checks.Append($"PASS: {checks.Count(check => check.StartsWith("PASS:", StringComparison.Ordinal))} checks"));

                void AssertContent(string stage)
                {
                    Check(inherited.ReadLocalValue(FrameworkElement.DataContextProperty) == DependencyProperty.UnsetValue,
                        $"{stage}: consumer root has no injected context");
                    Check(inherited.ReadLocalValue(Control.FontSizeProperty) == DependencyProperty.UnsetValue,
                        $"{stage}: consumer root has no injected font size");
                    Check(ReferenceEquals(bound.GetBindingExpression(FrameworkElement.DataContextProperty)?.ParentBinding, binding),
                        $"{stage}: consumer context binding preserved");
                    Check(ReferenceEquals(bound.Style, userStyle) && bound.Resources.MergedDictionaries.Contains(userResources),
                        $"{stage}: consumer style and resources preserved");
                }
                void Check(bool condition, string name)
                {
                    if (!condition)
                    {
                        throw new InvalidOperationException(name);
                    }
                    checks.Add($"PASS: {name}");
                }
            }
            catch (Exception exception)
            {
                File.WriteAllLines(output, checks.Append($"FAIL: {exception}"));
                Environment.ExitCode = 1;
            }
            finally
            {
                manager?.Dispose();
                window.Close();
                Application.Current.Exit();
            }
        }
    }

    private static async Task Settle() => await Task.Delay(150);

    private static nint HitTest(nint hwnd, int x, int y) => SendMessageW(hwnd, 0x0084, 0, (y << 16) | (x & 0xffff));
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint SendMessageW(nint hwnd, uint message, nint wParam, nint lParam);

    private static bool HeaderMatches(TabControlEx pane, bool visible)
    {
        FrameworkElement strip = FindChild<FrameworkElement>(pane, "TabContainerGrid")!;
        FrameworkElement body = FindChild<FrameworkElement>(pane, "ContentPanel")!;
        double bodyTop = body.TransformToVisual(pane).TransformPoint(new Point()).Y;
        return visible ? strip.Visibility == Visibility.Visible && bodyTop > 0 && Math.Abs(bodyTop - strip.ActualHeight) < 0.1
            : strip.Visibility == Visibility.Collapsed && Math.Abs(bodyTop) < 0.1;
    }

    private static bool HeadersAlign(TabControlEx pane)
    {
        TabViewItem selected = (TabViewItem)pane.SelectedItem;
        TabViewItem other = pane.TabItems.Cast<TabViewItem>().First(tab => tab != selected);
        VisualStateManager.GoToState(other, "PointerOver", false);
        pane.UpdateLayout();
        Rect Bounds(FrameworkElement element) => element.TransformToVisual(pane).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        Rect active = Bounds(selected);
        Rect hover = Bounds(FindChild<Border>(other, "Surface")!);
        Rect label = Bounds((FrameworkElement)selected.Header);
        Rect otherLabel = Bounds((FrameworkElement)other.Header);
        Rect body = Bounds(FindChild<FrameworkElement>(pane, "ContentPanel")!);
        VisualStateManager.GoToState(other, "Normal", false);
        if (Math.Abs(active.Top - hover.Top) >= 0.1 || Math.Abs(active.Bottom - hover.Bottom) >= 0.1 || Math.Abs(label.Top - otherLabel.Top) >= 0.1)
        {
            return false;
        }
        if (pane is LayoutAnchorablePaneControl)
        {
            return body.Bottom <= active.Top + 0.1;
        }
        Rect close = Bounds(FindChild<Button>(selected, "CloseButton")!);
        Rect otherClose = Bounds(FindChild<Button>(other, "CloseButton")!);
        return Math.Abs(body.Top - active.Bottom) < 0.1 && Math.Abs(close.Top - otherClose.Top) < 0.1;
    }

    private static T? FindChild<T>(DependencyObject owner, string? name = null) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(owner); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(owner, i);
            if (child is T result && (name == null || child is FrameworkElement element && element.Name == name))
            {
                return result;
            }
            T? descendant = FindChild<T>(child, name);
            if (descendant != null)
            {
                return descendant;
            }
        }
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (DependencyObject? parent = VisualTreeHelper.GetParent(child); parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is T result)
            {
                return result;
            }
        }
        return null;
    }

    private sealed class ContextSource : INotifyPropertyChanged
    {
        private object? value;
        public event PropertyChangedEventHandler? PropertyChanged;
        public string? Name { get; set; }
        public object? Value
        {
            get => value;
            set
            {
                this.value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class RefreshableMenu : ContextMenuEx
    {
        internal void Refresh() => OnOpened(new RoutedEventArgs());
    }
}
