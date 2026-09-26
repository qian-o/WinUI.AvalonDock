// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/TabControlEx.cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;

namespace AvalonDock.Controls;

/// <summary>Retains visited tab content when virtualization is disabled.</summary>
[TemplatePart(Name = "PART_ItemsHolder", Type = typeof(Panel))]
public class TabControlEx : TabView
{
    internal void MeasureHeader(TabViewItem tab)
    {
        // A virtualized container has no visual parent. A secondary XAML island requires
        // a rooted native template for measurement. Temporarily attach it invisibly without
        // changing the owning TabItems collection, then restore its original parentlessness.
        Panel? holder = !tab.IsLoaded && Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(tab) == null ? itemsHolder : null;
        double opacity = tab.Opacity;
        try
        {
            if (holder is not null)
            {
                tab.Opacity = 0;
                holder.Children.Add(tab);
            }
            tab.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        finally
        {
            if (holder is not null)
            {
                holder.Children.Remove(tab);
                tab.Opacity = opacity;
            }
        }
    }
    private readonly List<object> previousItems = [];
    private readonly Dictionary<TabViewItem, ContentPresenter> retainedContent = [];
    private readonly Dictionary<TabViewItem, List<(DependencyProperty Property, long Token)>> contentTokens = [];
    private Panel? itemsHolder;
    private bool updatingContent;
    private int itemsUpdateDepth;
    private ControlTemplate? appliedTemplate;
    private ListView? nativeList;
    private bool changingTemplate;
    private object? templateSelection;
    private NativeTabHeaderLayout? headerLayout;
    private DocumentPaneMenu? documentMenu;
    internal Func<bool>? NativeSelectionChanged
    {
        get; set;
    }

    internal void SynchronizeNativeSelection(object? previousItem)
    {
        if (NativeSelectionChanged?.Invoke() == true)
        {
            OnSelectionChanged(new SelectionChangedEventArgs(
                previousItem == null ? Array.Empty<object>() : new[] { previousItem },
                SelectedItem == null ? Array.Empty<object>() : new[] { SelectedItem }));
        }
    }

    /// <summary>Initializes the tab content lifetime and keyboard policies.</summary>
    public TabControlEx(bool isVirtualizing, bool ignoreTabControlKeyBindingBindings) : this()
    {
        IsVirtualiting = isVirtualizing;
        IgnoreTabControlKeyBindings = ignoreTabControlKeyBindingBindings;
    }

    /// <summary>Initializes a tab control using the platform's selected-content lifetime.</summary>
    protected TabControlEx()
    {
        IsVirtualiting = true;
        DefaultStyleKey = typeof(TabControlEx);
        DefaultStyleResourceUri = new Uri("ms-appx:///WinUI.AvalonDock/Themes/Generic.xaml");
        TabItemsChanged += OnTabItemsChanged;
        SelectionChanged += (_, args) =>
        {
            if (changingTemplate)
            {
                return;
            }

            if (NativeSelectionChanged?.Invoke() != false)
            {
                OnSelectionChanged(args);
            }
            else
            {
                UpdateSelectedItem();
            }
        };
        Loaded += (_, _) => UpdateSelectedItem();
        PreviewKeyDown += OnPreviewKeyDown;
        Unloaded += (_, _) =>
        {
            if (IsLoaded)
            {
                return;
            }

            documentMenu?.Dispose();
            documentMenu = null;
            headerLayout?.Dispose();
            headerLayout = null;
            DispatcherQueue.TryEnqueue(() => { if (IsLoaded) { AttachHeaderLayout(); AttachDocumentMenu(); UpdateSelectedItem(); } });
        };
        Loaded += (_, _) => { AttachHeaderLayout(); AttachDocumentMenu(); };
        RegisterPropertyChangedCallback(TemplateProperty, (_, _) =>
        {
            if (!ReferenceEquals(appliedTemplate, Template))
            {
                ReleaseRetainedContent();
                PrepareTemplateChange();
            }
        });
    }

    /// <summary>Gets whether inactive tab content is virtualized.</summary>
    [Bindable(false)]
    [Description("Gets whether the control and its inheriting classes are virtualizing their items or not.")]
    [Category("Other")]
    public bool IsVirtualiting
    {
        get;
    }

    /// <summary>Gets whether the platform tab-control key bindings are ignored.</summary>
    [Bindable(false)]
    [Description("Gets whether the TabControl keybindings are ignored or not.")]
    [Category("Document")]
    public bool IgnoreTabControlKeyBindings
    {
        get;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        documentMenu?.Dispose();
        documentMenu = null;
        ReleaseRetainedContent();
        itemsHolder = null;
        appliedTemplate = Template;
        headerLayout?.Dispose();
        headerLayout = null;
        if (nativeList is not null)
        {
            nativeList.ContainerContentChanging -= OnContainerContentChanging;
        }
        base.OnApplyTemplate();
        if (GetTemplateChild("PaneBorder") is DockPaneSurface surface)
        {
            surface.Attach(this, GetTemplateChild("ContentPanel") as FrameworkElement);
        }

        nativeList = GetTemplateChild("TabListView") as ListView;
        if (nativeList is not null)
        {
            nativeList.ContainerContentChanging += OnContainerContentChanging;
            if (changingTemplate)
            {
                nativeList.Loaded += RestoreTemplateSelection;
            }
        }
        itemsHolder = GetTemplateChild("PART_ItemsHolder") as Panel;
        AttachHeaderLayout();
        UpdateSelectedItem();
        AttachDocumentMenu();
        UpdatePaneActiveState();
    }
    internal void UpdatePaneActiveState()
    {
        LayoutAnchorablePane? tool = (this as LayoutAnchorablePaneControl)?.Model as LayoutAnchorablePane;
        VisualStateManager.GoToState(this, SelectedItem is TabViewItem { Tag: LayoutContent { IsActive: true } }
            && tool?.IsDirectlyHostedInFloatingWindow != true ? "PaneActive" : "PaneInactive", false);
        VisualStateManager.GoToState(this, tool?.ChildrenCount == 1 ? "SingleTool" : "MultipleTools", false);
        VisualStateManager.GoToState(this, tool?.IsDirectlyHostedInFloatingWindow == true ? "FloatingTool" : "DockedTool", false);
    }
    internal bool IsTabStripCollapsed => GetTemplateChild("TabContainerGrid") is FrameworkElement { Visibility: Visibility.Collapsed };
    private void AttachDocumentMenu()
    {
        if (documentMenu == null && this is LayoutDocumentPaneControl { Model: LayoutDocumentPane pane }
            && pane.Root?.Manager != null && GetTemplateChild("MenuDropDownButton") is DropDownButton button)
        {
            documentMenu = new DocumentPaneMenu(pane, button);
        }
    }

    /// <summary>Processes changes to the platform tab item collection.</summary>
    protected virtual void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        if (itemsUpdateDepth > 0)
        {
            return;
        }

        ReconcileRetainedContent();
        UpdateSelectedItem();
    }

    /// <summary>Updates the active content without unloading visited tabs.</summary>
    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e) => UpdateSelectedItem();

    /// <summary>Gets the selected native tab item, resolving a generated container when necessary.</summary>
    protected TabViewItem? GetSelectedTabItem() => SelectedItem as TabViewItem
        ?? (SelectedIndex >= 0 ? ContainerFromIndex(SelectedIndex) as TabViewItem : null);

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        if (!IgnoreTabControlKeyBindings)
        {
            base.OnKeyDown(e);
        }
    }

    internal void BeginItemsUpdate() => itemsUpdateDepth++;

    internal void EndItemsUpdate()
    {
        if (itemsUpdateDepth == 0)
        {
            throw new InvalidOperationException("No tab item update is active.");
        }

        if (--itemsUpdateDepth != 0)
        {
            return;
        }

        ReconcileRetainedContent();
        UpdateSelectedItem();
    }

    private void OnTabItemsChanged(TabView sender, IVectorChangedEventArgs args)
    {
        if (changingTemplate)
        {
            return;
        }

        object[] current = TabItemsSource is System.Collections.IEnumerable source ? source.Cast<object>().ToArray() : TabItems.ToArray();
        int index = (int)args.Index;
        NotifyCollectionChangedEventArgs change;
        switch (args.CollectionChange)
        {
            case CollectionChange.ItemInserted when index < current.Length:
                change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, current[index], index);
                break;
            case CollectionChange.ItemRemoved when index < previousItems.Count:
                change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, previousItems[index], index);
                break;
            case CollectionChange.ItemChanged when index < current.Length && index < previousItems.Count:
                change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, current[index], previousItems[index], index);
                break;
            default:
                change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                break;
        }
        previousItems.Clear();
        previousItems.AddRange(current);
        OnItemsChanged(change);
    }

    private void UpdateSelectedItem()
    {
        if (itemsHolder is null || updatingContent || itemsUpdateDepth > 0 || changingTemplate)
        {
            return;
        }

        updatingContent = true;
        try
        {
            TabViewItem? selected = GetSelectedTabItem();
            // WPF TabControl keeps keyboard focus within its selected tab. Transfer
            // retiring native content's focus to a pane header before WinUI's unload
            // fallback can choose an unrelated tool pane during temporary null selection.
            if (XamlRoot != null
                && FocusManager.GetFocusedElement(XamlRoot) is DependencyObject focused)
            {
                for (DependencyObject ancestor = focused; ancestor != null && !ReferenceEquals(ancestor, this); ancestor = VisualTreeHelper.GetParent(ancestor))
                {
                    if (!retainedContent.Any(entry => !ReferenceEquals(entry.Key, selected) && ReferenceEquals(entry.Value, ancestor)))
                    {
                        continue;
                    }

                    TabViewItem oldTab = retainedContent.First(entry => ReferenceEquals(entry.Value, ancestor)).Key;
                    if (selected?.Focus(FocusState.Programmatic) != true)
                    {
                        oldTab.Focus(FocusState.Programmatic);
                    }

                    break;
                }
            }
            if (IsVirtualiting)
            {
                foreach (TabViewItem? tab in retainedContent.Keys.Where(tab => !ReferenceEquals(tab, selected)).ToArray())
                {
                    ReleaseContent(tab);
                }
            }
            if (selected is not null && !retainedContent.ContainsKey(selected))
            {
                ContentPresenter presenter = new()
                {
                    Content = selected.Content,
                    ContentTemplate = selected.ContentTemplate,
                    ContentTemplateSelector = selected.ContentTemplateSelector,
                    DataContext = ResolveDataContext(selected),
                    HorizontalContentAlignment = HorizontalContentAlignment,
                    VerticalContentAlignment = VerticalContentAlignment,
                    Visibility = Visibility.Collapsed,
                };
                retainedContent.Add(selected, presenter);
                List<(DependencyProperty, long)> tokens = new();
                foreach (DependencyProperty? property in new[] { ContentControl.ContentProperty, ContentControl.ContentTemplateProperty, ContentControl.ContentTemplateSelectorProperty, DataContextProperty })
                {
                    tokens.Add((property, selected.RegisterPropertyChangedCallback(property, (_, _) => UpdateContent(selected, presenter))));
                }
                contentTokens.Add(selected, tokens);
                itemsHolder.Children.Add(presenter);
            }
            foreach ((TabViewItem? tab, ContentPresenter? presenter) in retainedContent)
            {
                if (ReferenceEquals(tab, selected) && presenter.Content is null)
                {
                    presenter.Content = selected.Content;
                }

                Visibility visibility = ReferenceEquals(tab, selected) ? Visibility.Visible : Visibility.Collapsed;
                if (presenter.Visibility != visibility)
                {
                    presenter.Visibility = visibility;
                }
            }
        }
        finally
        {
            updatingContent = false;
        }
    }

    private void ReleaseContent(TabViewItem tab)
    {
        if (!retainedContent.Remove(tab, out ContentPresenter? presenter))
        {
            return;
        }

        if (contentTokens.Remove(tab, out List<(DependencyProperty Property, long Token)>? tokens))
        {
            foreach ((DependencyProperty? property, long token) in tokens)
            {
                tab.UnregisterPropertyChangedCallback(property, token);
            }
        }

        presenter.Content = null;
        itemsHolder?.Children.Remove(presenter);
    }

    private void ReleaseRetainedContent()
    {
        foreach (TabViewItem? tab in retainedContent.Keys.ToArray())
        {
            ReleaseContent(tab);
        }
    }

    private void ReconcileRetainedContent()
    {
        object[] current = TabItemsSource is System.Collections.IEnumerable source ? source.Cast<object>().ToArray() : TabItems.ToArray();
        HashSet<TabViewItem?> containers = current.Select(item => item as TabViewItem ?? ContainerFromItem(item) as TabViewItem).ToHashSet();
        foreach (TabViewItem? tab in retainedContent.Keys.Where(tab => !containers.Contains(tab)).ToArray())
        {
            ReleaseContent(tab);
        }
    }

    private void UpdateContent(TabViewItem tab, ContentPresenter presenter)
    {
        if (!ReferenceEquals(presenter.Content, tab.Content))
        {
            presenter.Content = tab.Content;
        }

        if (!ReferenceEquals(presenter.ContentTemplate, tab.ContentTemplate))
        {
            presenter.ContentTemplate = tab.ContentTemplate;
        }

        if (!ReferenceEquals(presenter.ContentTemplateSelector, tab.ContentTemplateSelector))
        {
            presenter.ContentTemplateSelector = tab.ContentTemplateSelector;
        }

        object? context = ResolveDataContext(tab);
        if (!ReferenceEquals(presenter.DataContext, context))
        {
            presenter.DataContext = context;
        }
    }

    private object? ResolveDataContext(TabViewItem tab)
    {
        if (TabItemsSource is System.Collections.IEnumerable source)
        {
            foreach (object? item in source)
            {
                if (item is not TabViewItem && ReferenceEquals(ContainerFromItem(item), tab))
                {
                    return item;
                }
            }
        }

        return tab.DataContext;
    }

    private void OnPreviewKeyDown(object? sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (this is ILayoutControl layout)
        {
            layout.Model?.Root?.Manager?.HandleNavigatorKey(e);
        }

        if (e.Handled)
        {
            return;
        }

        if (e.Key != VirtualKey.Tab || !PlatformServices.Keyboard.IsKeyDown(VirtualKey.Control))
        {
            return;
        }

        if (IgnoreTabControlKeyBindings)
        {
            e.Handled = true;
            return;
        }
        if (this is not LayoutDocumentPaneControl { Model: LayoutDocumentPane pane } || pane.ChildrenCount == 0)
        {
            return;
        }

        int direction = PlatformServices.Keyboard.IsKeyDown(VirtualKey.Shift) ? -1 : 1;
        int start = pane.SelectedContentIndex;
        for (int offset = 1; offset <= pane.ChildrenCount; offset++)
        {
            int index = (start + direction * offset + pane.ChildrenCount * 2) % pane.ChildrenCount;
            LayoutContent candidate = pane.Children[index];
            if (!candidate.IsEnabled)
            {
                continue;
            }

            candidate.IsSelected = true;
            candidate.IsActive = true;
            e.Handled = true;
            break;
        }
    }

    private void PrepareTemplateChange()
    {
        if (nativeList is null || changingTemplate)
        {
            return;
        }

        changingTemplate = true;
        templateSelection = SelectedItem;
        if (TabItemsSource is null)
        {
            // Native TabView copies its old list's items into the new template list. Move that
            // snapshot to an unparented vector before releasing the old item containers.
            ObservableCollection<object> snapshot = new(TabItems);
            SetValue(TabItemsProperty, snapshot);
            nativeList.Items.Clear();
        }
        else
        {
            nativeList.ItemsSource = null;
        }
    }

    private void RestoreTemplateSelection(object? sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= RestoreTemplateSelection;
        SelectedItem = templateSelection;
        templateSelection = null;
        changingTemplate = false;
        previousItems.Clear();
        previousItems.AddRange(TabItemsSource is System.Collections.IEnumerable source ? source.Cast<object>() : TabItems);
        UpdateSelectedItem();
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue)
        {
            // A generated tab may not exist when SelectedItem first changes. Its next realization
            // phase supplies the container and content without polling dispatcher turns.
            args.RegisterUpdateCallback((_, _) => UpdateSelectedItem());
        }
    }

    private void AttachHeaderLayout()
    {
        if (headerLayout is null && nativeList is not null && this is LayoutDocumentPaneControl or LayoutAnchorablePaneControl)
        {
            headerLayout = new NativeTabHeaderLayout(this, nativeList);
        }
    }
}
