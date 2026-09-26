// WinUI presentation adapter for the preserved public document/anchorable pane controls.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AvalonDock.Converters;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Controls;

internal sealed class LayoutPanePresenter : IDisposable
{
    private static readonly ConditionalWeakTable<ILayoutRoot, ConditionalWeakTable<ILayoutElement, WeakReference<LayoutContent?>>> NativeSelections = new();
    private readonly TabControlEx tabs;
    private readonly ILayoutElement pane;
    private readonly List<(LayoutContent Model, TabViewItem Tab, Control Header, Control Content)> items = [];
    private readonly List<(DependencyProperty Property, long Token)> managerSubscriptions = [];
    private DockingManager? manager;
    private IDisposable? dropRegistration;
    private bool synchronizing;
    private bool attached;
    private bool disposed;
    private readonly WeakReference<LayoutContent?> nativeSelectedContent;
    private readonly ResourceDictionary contentStyles = new() { Source = new Uri("ms-appx:///WinUI.AvalonDock/Themes/AnchorableContent.xaml") };
    private readonly ResourceDictionary tabStyles = new() { Source = new Uri("ms-appx:///WinUI.AvalonDock/Themes/TabItem.xaml") };

    internal LayoutPanePresenter(TabControlEx tabs, ILayoutElement pane)
    {
        this.tabs = tabs;
        this.pane = pane;
        nativeSelectedContent = pane.Root == null ? new WeakReference<LayoutContent?>(null)
            : NativeSelections.GetValue(pane.Root, _ => new()).GetValue(pane, _ => new(null));
        tabs.NativeSelectionChanged = OnSelectionChanged;
        tabs.HorizontalAlignment = HorizontalAlignment.Stretch;
        tabs.VerticalAlignment = VerticalAlignment.Stretch;
        tabs.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        tabs.VerticalContentAlignment = VerticalAlignment.Stretch;
        tabs.IsAddTabButtonVisible = false;
        tabs.CanDragTabs = false;
        tabs.CanReorderTabs = false;
        tabs.AllowDrop = false;
        tabs.TabWidthMode = TabViewWidthMode.SizeToContent;
        tabs.Loaded += OnLoaded;
        tabs.Unloaded += OnUnloaded;
        tabs.SizeChanged += OnSizeChanged;
    }

    internal void Attach()
    {
        if (attached || disposed)
        {
            return;
        }

        attached = true;
        pane.PropertyChanged += OnPanePropertyChanged;
        if (pane is ILayoutGroup group)
        {
            group.ChildrenCollectionChanged += OnChildrenChanged;
        }

        tabs.TabCloseRequested += OnTabCloseRequested;
        manager = pane.Root?.Manager;
        if (manager != null)
        {
            manager.LayoutItemCreated += OnLayoutItemCreated;
            if (pane is ILayoutGroup targetGroup)
            {
                dropRegistration = manager.RegisterDockTarget(targetGroup, tabs);
            }

            foreach (DependencyProperty? property in new[]
            {
                DockingManager.DocumentHeaderTemplateProperty, DockingManager.DocumentHeaderTemplateSelectorProperty,
                DockingManager.AnchorableHeaderTemplateProperty, DockingManager.AnchorableHeaderTemplateSelectorProperty,
                FrameworkElement.FlowDirectionProperty
            })
            {
                managerSubscriptions.Add((property, manager.RegisterPropertyChangedCallback(property, OnPresentationChanged)));
            }
            ApplyPanePresentation();
        }
        Rebuild();
    }

    private void Detach()
    {
        if (!attached)
        {
            return;
        }

        attached = false;
        pane.PropertyChanged -= OnPanePropertyChanged;
        if (pane is ILayoutGroup group)
        {
            group.ChildrenCollectionChanged -= OnChildrenChanged;
        }

        tabs.TabCloseRequested -= OnTabCloseRequested;
        if (manager != null)
        {
            manager.LayoutItemCreated -= OnLayoutItemCreated;
            foreach ((DependencyProperty? property, long token) in managerSubscriptions)
            {
                manager.UnregisterPropertyChangedCallback(property, token);
            }
        }
        managerSubscriptions.Clear();
        dropRegistration?.Dispose();
        dropRegistration = null;
        manager = null;
        ClearItems();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Detach();
        tabs.Loaded -= OnLoaded;
        tabs.Unloaded -= OnUnloaded;
        tabs.SizeChanged -= OnSizeChanged;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e) => Attach();
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (tabs.IsLoaded)
        {
            return;
        }

        Detach();
        tabs.DispatcherQueue.TryEnqueue(() => { if (tabs.IsLoaded && !disposed && !attached) { Attach(); } });
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (pane is ILayoutPositionableElementWithActualSize size)
        {
            size.ActualWidth = e.NewSize.Width;
            size.ActualHeight = e.NewSize.Height;
        }
    }

    private void Rebuild()
    {
        synchronizing = true;
        tabs.BeginItemsUpdate();
        try
        {
            LayoutContent[] contents = ((ILayoutContainer)pane).Children.OfType<LayoutContent>().ToArray();
            foreach ((LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry in items.ToArray())
            {
                if (contents.Any(model => ReferenceEquals(model, entry.Model)))
                {
                    continue;
                }

                ReleaseItem(entry);
                tabs.TabItems.Remove(entry.Tab);
                items.Remove(entry);
            }
            for (int index = 0; index < contents.Length; index++)
            {
                LayoutContent model = contents[index];
                LayoutItem? item = model.Root?.Manager?.GetLayoutItemFromModel(model);
                if (item == null)
                {
                    continue;
                }

                (LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry = items.FirstOrDefault(entry => ReferenceEquals(entry.Model, model));
                if (entry.Model == null)
                {
                    Control header = pane is LayoutAnchorablePane
                        ? new LayoutAnchorableTabItem { Model = model }
                        : new LayoutDocumentTabItem { Model = model };
                    ApplyHeaderTemplate(header, model);
                    Control content = pane is LayoutAnchorablePane && model is LayoutAnchorable tool
                        ? new LayoutAnchorableControl { Model = tool }
                        : new LayoutDocumentControl { Model = model };
                    if (content is LayoutAnchorableControl && manager is ToggleDockingManager)
                    {
                        content.Style = (Style)contentStyles["DockToggleAnchorableContentStyle"];
                    }

                    TabViewItem tab = new()
                    {
                        Header = header,
                        Content = content,
                        Tag = model,
                        IsClosable = pane is LayoutDocumentPane && (model.CanClose || model is LayoutAnchorable { CanHide: true })
                    };
                    tab.SetBinding(Control.IsEnabledProperty, new Binding
                    {
                        Source = model,
                        Path = new PropertyPath(nameof(LayoutContent.IsEnabled)),
                        Mode = BindingMode.OneWay
                    });
                    tab.SetBinding(AutomationProperties.NameProperty, new Binding
                    {
                        Source = model,
                        Path = new PropertyPath(nameof(LayoutContent.Title)),
                        Mode = BindingMode.OneWay
                    });
                    if (pane is LayoutDocumentPane && model is LayoutDocument)
                    {
                        tab.SetBinding(UIElement.VisibilityProperty, new Binding
                        {
                            Source = model,
                            Path = new PropertyPath(nameof(LayoutDocument.IsVisible)),
                            Converter = new BoolToVisibilityConverter(),
                            Mode = BindingMode.OneWay
                        });
                    }
                    // Original ItemContainerStyle belongs to the pane. Keep that style identity
                    // while native list reordering briefly removes the container from its resource scope.
                    tab.Style = (Style)tabStyles[pane is LayoutAnchorablePane ? "AvalonDockReferenceToolTabItemStyle" : "AvalonDockReferenceTabItemStyle"];
                    tab.Loaded += OnTabLoaded;
                    model.PropertyChanged += OnContentPropertyChanged;
                    entry = (model, tab, header, content);
                    items.Add(entry);
                }
                int currentIndex = tabs.TabItems.IndexOf(entry.Tab);
                if (currentIndex == index)
                {
                    continue;
                }

                if (currentIndex >= 0)
                {
                    tabs.TabItems.RemoveAt(currentIndex);
                }
                // WinUI can materialize a later model before the earlier tab container.
                // The source order is restored on the next collection reconciliation.
                int insertionIndex = Math.Min(index, tabs.TabItems.Count);
                if (insertionIndex == tabs.TabItems.Count)
                {
                    tabs.TabItems.Add(entry.Tab);
                }
                else
                {
                    tabs.TabItems.Insert(insertionIndex, entry.Tab);
                }
            }
            SynchronizeSelection();
        }
        finally
        {
            tabs.EndItemsUpdate();
            synchronizing = false;
        }
    }

    private static void ApplyHeaderTemplate(Control headerControl, LayoutContent model)
    {
        if (headerControl is LayoutAnchorableTabItem toolHeader)
        {
            toolHeader.RefreshHeaderPresentation();
            return;
        }
        LayoutDocumentTabItem header = (LayoutDocumentTabItem)headerControl;
        DockingManager? manager = model.Root?.Manager;
        bool anchorable = model is LayoutAnchorable;
        DataTemplate? template = anchorable ? manager?.AnchorableHeaderTemplate : manager?.DocumentHeaderTemplate;
        DataTemplateSelector? selector = anchorable ? manager?.AnchorableHeaderTemplateSelector : manager?.DocumentHeaderTemplateSelector;
        header.ClearValue(ContentControl.ContentProperty);
        header.ContentTemplate = template;
        header.ContentTemplateSelector = selector;
        if (template != null || selector != null)
        {
            header.Content = model;
        }
        else
        {
            header.SetBinding(ContentControl.ContentProperty, new Binding
            {
                Source = model,
                Path = new PropertyPath(nameof(LayoutContent.Title)),
                Mode = BindingMode.OneWay
            });
        }
    }

    private void ApplyPanePresentation()
    {
        tabs.FlowDirection = manager?.FlowDirection ?? FlowDirection.LeftToRight;
    }

    private void OnPresentationChanged(DependencyObject sender, DependencyProperty property)
    {
        if (property == FrameworkElement.FlowDirectionProperty)
        {
            ApplyPanePresentation();
        }
        foreach ((LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry in items)
        {
            ApplyHeaderTemplate(entry.Header, entry.Model);
        }
    }

    private void OnChildrenChanged(object? sender, EventArgs e) => Rebuild();

    private void OnLayoutItemCreated(LayoutContent content)
    {
        // Source imports add the model before creating its LayoutItem. The earlier
        // collection notification cannot yet create a native tab for that model.
        if (ReferenceEquals(content.Parent, pane))
        {
            Rebuild();
        }
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SelectedContent" or "SelectedContentIndex")
        {
            SynchronizeSelection();
        }

        if (e.PropertyName is nameof(LayoutAnchorablePane.IsDirectlyHostedInFloatingWindow) or nameof(LayoutElement.Parent) or nameof(LayoutElement.Root))
        {
            tabs.UpdatePaneActiveState();
        }
    }

    private void OnContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LayoutContent model)
        {
            return;
        }

        if (e.PropertyName == nameof(LayoutContent.IsSelected))
        {
            SynchronizeSelection();
        }

        if (e.PropertyName == nameof(LayoutContent.IsActive))
        {
            tabs.UpdatePaneActiveState();
        }

        if (e.PropertyName == nameof(LayoutContent.IsEnabled))
        {
            foreach ((LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry in items.Where(entry => ReferenceEquals(entry.Model, model)))
            {
                UpdateHeaderEnabledState(entry);
            }
        }

        if (e.PropertyName is nameof(LayoutContent.CanClose) or nameof(LayoutAnchorable.CanHide))
        {
            foreach ((LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry in items.Where(entry => ReferenceEquals(entry.Model, model)))
            {
                entry.Tab.IsClosable = pane is LayoutDocumentPane && (model.CanClose || model is LayoutAnchorable { CanHide: true });
                UpdateTabClosePolicy(entry.Tab);
            }
        }
    }

    private void SynchronizeSelection()
    {
        bool previous = synchronizing;
        synchronizing = true;
        try
        {
            nativeSelectedContent.TryGetTarget(out LayoutContent? previousContent);
            TabViewItem previousTab = items.FirstOrDefault(entry => ReferenceEquals(entry.Model, previousContent)).Tab;
            LayoutContent? selected = (pane as ILayoutContentSelector)?.SelectedContent;
            TabViewItem selectedTab = items.FirstOrDefault(entry => ReferenceEquals(entry.Model, selected)).Tab;
            if (!ReferenceEquals(tabs.SelectedItem, selectedTab))
            {
                tabs.SelectedItem = selectedTab;
            }

            tabs.SynchronizeNativeSelection(previousTab);
            if (pane is LayoutAnchorablePane)
            {
                foreach ((LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry in items)
                {
                    UpdateToolTabPlacement(entry.Tab, ReferenceEquals(entry.Model, selected));
                }
            }

            tabs.UpdatePaneActiveState();
        }
        finally { synchronizing = previous; }
    }

    private bool OnSelectionChanged()
    {
        if (!attached)
        {
            return false;
        }

        if (!synchronizing && tabs.SelectedItem is TabViewItem { Tag: LayoutContent { IsSelected: false } model })
        {
            model.IsSelected = true;
        }
        // Native view construction is deferred and may replace the control for the
        // same pane. Preserve its selection notification history across that replacement
        // without retaining a released pane, root or content. Its first selection still
        // reaches the original hook; reconstructing the same selection does not.
        LayoutContent? selected = (pane as ILayoutContentSelector)?.SelectedContent;
        nativeSelectedContent.TryGetTarget(out LayoutContent? previous);
        if (ReferenceEquals(previous, selected))
        {
            return false;
        }

        nativeSelectedContent.SetTarget(selected);
        return true;
    }

    private static void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is not LayoutContent model)
        {
            return;
        }

        LayoutItem? item = model.Root?.Manager?.GetLayoutItemFromModel(model);
        // Original LayoutDocumentTabItem template selects HideCommand for a noncloseable
        // anchorable that can hide; native TabView raises this event for its close button.
        ICommand? command = model is LayoutAnchorable { CanClose: false, CanHide: true } && item is LayoutAnchorableItem tool ? tool.HideCommand : item?.CloseCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
    private void OnTabLoaded(object? sender, RoutedEventArgs args)
    {
        if (sender is not TabViewItem tab)
        {
            return;
        }
        (LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry = items.FirstOrDefault(item => ReferenceEquals(item.Tab, tab));
        if (entry.Model != null)
        {
            UpdateHeaderEnabledState(entry);
        }

        if (pane is LayoutAnchorablePane)
        {
            UpdateToolTabPlacement(tab, ReferenceEquals(tab.Tag, (pane as ILayoutContentSelector)?.SelectedContent));
        }
        else
        {
            VisualStateManager.GoToState(tab, "DocumentTab", false);
        }

        UpdateTabClosePolicy(tab);
    }

    private static void UpdateToolTabPlacement(TabViewItem tab, bool selected) =>
        VisualStateManager.GoToState(tab, selected ? "ToolTabSelected" : "ToolTab", false);

    private static void UpdateHeaderEnabledState((LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry)
    {
        // WPF's disabled TabItem Foreground reaches its generated header through inheritance.
        // WinUI TabViewItem's disabled presenter state does not cross its nested header Control.
        bool disabled = !entry.Model.IsEnabled && entry.Header.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue;
        VisualStateManager.GoToState(entry.Header, disabled ? "Disabled" : "Normal", false);
    }

    private static void UpdateTabClosePolicy(TabViewItem tab)
    {
        string action = tab.Tag is LayoutAnchorable { CanClose: false, CanHide: true }
            ? global::AvalonDock.Properties.Resources.Anchorable_Hide
            : global::AvalonDock.Properties.Resources.Document_Close;
        // Native TabViewItem replaces its template close button's tooltip during realization.
        foreach (Button? button in tab.FindVisualChildren<Button>().Where(button => button.Name == "CloseButton"))
        {
            ToolTipService.SetToolTip(button, action);
            AutomationProperties.SetName(button, action);
        }
    }

    private void ClearItems()
    {
        bool previous = synchronizing;
        synchronizing = true;
        foreach ((LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry in items)
        {
            ReleaseItem(entry);
        }

        tabs.TabItems.Clear();
        items.Clear();
        synchronizing = previous;
    }

    private void ReleaseItem((LayoutContent Model, TabViewItem Tab, Control Header, Control Content) entry)
    {
        entry.Tab.Loaded -= OnTabLoaded;
        entry.Model.PropertyChanged -= OnContentPropertyChanged;
        if (entry.Header is LayoutDocumentTabItem documentHeader)
        {
            documentHeader.Model = null;
            documentHeader.ClearValue(ContentControl.ContentProperty);
        }
        if (entry.Header is LayoutAnchorableTabItem toolHeader)
        {
            toolHeader.Model = null;
        }

        if (entry.Content is LayoutDocumentControl document)
        {
            document.Model = null;
        }

        if (entry.Content is LayoutAnchorableControl tool)
        {
            tool.Model = null;
        }

        entry.Tab.ClearValue(Control.IsEnabledProperty);
        entry.Tab.ClearValue(AutomationProperties.NameProperty);
        if (pane is LayoutDocumentPane && entry.Model is LayoutDocument)
        {
            entry.Tab.ClearValue(UIElement.VisibilityProperty);
        }

        entry.Tab.Content = null;
        entry.Tab.Header = null;
        entry.Tab.Tag = null;
    }
}
