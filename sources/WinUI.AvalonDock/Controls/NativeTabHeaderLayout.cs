using System.ComponentModel;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace AvalonDock.Controls;

/// <summary>
/// Applies the upstream header rules while retaining ItemsStackPanel. The native TabView separator
/// callback queries ContainerFromIndex(-1), which crashes the nonvirtualizing SDK container path.
/// </summary>
internal sealed class NativeTabHeaderLayout : IDisposable
{
    private readonly TabControlEx owner;
    private readonly ListView list;
    private readonly bool documents;
    private readonly Dictionary<TabViewItem, Entry> entries = [];
    private readonly List<(DependencyProperty Property, long Token)> managerTokens = [];
    private readonly DockingManager? manager;
    private bool queued;
    private bool updating;
    private bool disposed;

    internal NativeTabHeaderLayout(TabControlEx owner, ListView list)
    {
        this.owner = owner;
        this.list = list;
        documents = owner is LayoutDocumentPaneControl;
        manager = (owner as ILayoutControl)?.Model?.Root?.Manager;
        if (manager is not null)
        {
            foreach (DependencyProperty? property in new[] { DockingManager.DocumentHeaderTemplateProperty, DockingManager.DocumentHeaderTemplateSelectorProperty,
                DockingManager.AnchorableHeaderTemplateProperty, DockingManager.AnchorableHeaderTemplateSelectorProperty })
            {
                managerTokens.Add((property, manager.RegisterPropertyChangedCallback(property, (_, _) =>
                {
                    foreach (Entry entry in entries.Values)
                    {
                        entry.NaturalWidth = double.NaN;
                    }

                    QueueUpdate();
                })));
            }
        }
        owner.SizeChanged += OnSizeChanged;
        list.SizeChanged += OnSizeChanged;
        owner.TabItemsChanged += OnItemsChanged;
        owner.SelectionChanged += OnSelectionChanged;
        owner.Loaded += OnLoaded;
        QueueUpdate();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        owner.SizeChanged -= OnSizeChanged;
        list.SizeChanged -= OnSizeChanged;
        owner.TabItemsChanged -= OnItemsChanged;
        owner.SelectionChanged -= OnSelectionChanged;
        owner.Loaded -= OnLoaded;
        if (manager is not null)
        {
            foreach ((DependencyProperty? property, long token) in managerTokens)
            {
                manager.UnregisterPropertyChangedCallback(property, token);
            }
        }

        managerTokens.Clear();
        foreach (Entry entry in entries.Values)
        {
            Release(entry);
        }

        entries.Clear();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TabViewItem tab)
        {
            QueueUpdate();
            return;
        }

        // An unrealized header can only measure its outer template. Defer until its
        // data template has entered the visual tree, then replace the provisional width.
        if (!owner.DispatcherQueue.TryEnqueue(() =>
        {
            if (!disposed && entries.TryGetValue(tab, out Entry? entry))
            {
                entry.NaturalWidth = double.NaN;
                QueueUpdate();
            }
        }))
        {
            if (entries.TryGetValue(tab, out Entry? entry))
            {
                entry.NaturalWidth = double.NaN;
            }

            QueueUpdate();
        }
    }
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => QueueUpdate();
    private void OnItemsChanged(TabView sender, IVectorChangedEventArgs e) => QueueUpdate();
    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => QueueUpdate();

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutContent.Title) or nameof(LayoutContent.CanClose) or nameof(LayoutAnchorable.CanHide))
        {
            foreach (Entry? entry in entries.Values.Where(entry => ReferenceEquals(entry.Model, sender)))
            {
                entry.NaturalWidth = double.NaN;
            }

            QueueUpdate();
        }
        else if (e.PropertyName == nameof(LayoutContent.IsSelected))
        {
            QueueUpdate();
        }
    }

    private void QueueUpdate()
    {
        if (queued || disposed || updating)
        {
            return;
        }

        queued = true;
        if (!owner.DispatcherQueue.TryEnqueue(() =>
        {
            queued = false;
            if (!disposed)
            {
                Update();
            }
        }))
        {
            queued = false;
        }
    }

    private void Update()
    {
        if (!owner.IsLoaded || owner.ActualWidth <= 0 || updating)
        {
            return;
        }

        updating = true;
        try
        {
            TabViewItem[] tabs = owner.TabItems.OfType<TabViewItem>().ToArray();
            foreach (TabViewItem? tab in entries.Keys.Where(tab => !tabs.Contains(tab)).ToArray())
            {
                Release(entries[tab]);
                entries.Remove(tab);
            }
            foreach (TabViewItem? tab in tabs)
            {
                if (!entries.TryGetValue(tab, out Entry? entry))
                {
                    entry = new Entry(tab, tab.Tag as LayoutContent);
                    entries.Add(tab, entry);
                    tab.Loaded += OnLoaded;
                    if (entry.Model is not null)
                    {
                        entry.Model.PropertyChanged += OnModelChanged;
                    }

                    foreach (DependencyProperty? property in new[] { TabViewItem.HeaderProperty, TabViewItem.HeaderTemplateProperty, UIElement.VisibilityProperty, FrameworkElement.WidthProperty })
                    {
                        entry.Tokens.Add((property, tab.RegisterPropertyChangedCallback(property, (_, changed) =>
                        {
                            if (updating || disposed)
                            {
                                return;
                            }

                            if (changed == FrameworkElement.WidthProperty && double.IsNaN(tab.Width)
                                && double.IsFinite(entry.AllocatedWidth))
                            {
                                tab.Width = entry.AllocatedWidth;
                                return;
                            }
                            if (changed == TabViewItem.HeaderProperty)
                            {
                                AttachHeader(entry);
                            }

                            if (changed == TabViewItem.HeaderProperty || changed == TabViewItem.HeaderTemplateProperty)
                            {
                                entry.NaturalWidth = double.NaN;
                            }

                            QueueUpdate();
                        })));
                    }
                    AttachHeader(entry);
                }
                if (double.IsNaN(entry.NaturalWidth))
                {
                    tab.MaxWidth = entry.OriginalMaxWidth;
                    tab.Width = double.NaN;
                    tab.Margin = entry.OriginalMargin;
                    owner.MeasureHeader(tab);
                    entry.NaturalWidth = tab.DesiredSize.Width;
                    entry.NaturalHeaderWidth = entry.Header?.DesiredSize.Width ?? 0;
                    entry.NaturalHeaderHeight = entry.Header?.DesiredSize.Height ?? 0;
                }
            }
            TabViewItem[] visible = tabs.Where(tab => tab.Visibility != Visibility.Collapsed).ToArray();
            // The document selector occupies its own 28-pixel column and 4-pixel
            // margins on both sides; reserve it before choosing visible tabs.
            double available = Math.Max(0, Math.Min(owner.ActualWidth, list.MaxWidth) - list.Padding.Left - list.Padding.Right - 8
                - (documents ? 36 : 0));
            double[] widths = visible.Select(tab => entries[tab].NaturalWidth).ToArray();
            int count = documents ? TabHeaderLayoutRules.VisibleDocumentCount(widths, available) : visible.Length;
            double[] toolWidths = documents ? [] : TabHeaderLayoutRules.ToolWidths(widths, available);
            for (int index = 0; index < visible.Length; index++)
            {
                TabViewItem tab = visible[index];
                Entry entry = entries[tab];
                // Upstream panels arrange DesiredSize, which includes the margins.
                // Native Width is the content box; otherwise the item margin is added twice.
                double allocated = documents ? entry.NaturalWidth : toolWidths[index];
                double width = Math.Max(0, allocated - entry.OriginalMargin.Left - entry.OriginalMargin.Right);
                if (tab.MinWidth > width)
                {
                    tab.MinWidth = 0;
                }

                if (tab.MaxWidth != width)
                {
                    tab.MaxWidth = width;
                }

                if (index < count)
                {
                    entry.AllocatedWidth = width;
                    if (tab.Width != width)
                    {
                        tab.Width = width;
                    }
                    Restore(entry);
                }
                else
                {
                    Hide(entry);
                }
            }
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            ScrollViewer.SetHorizontalScrollMode(list, ScrollMode.Disabled);
            if (VisualDescendants<ScrollViewer>(list).FirstOrDefault() is { } scroller && scroller.HorizontalOffset != 0)
            {
                scroller.ChangeView(0, null, null, true);
            }

            if (documents && owner.SelectedItem is TabViewItem selected && entries.TryGetValue(selected, out Entry? selectedEntry)
                && selectedEntry.Hidden && selectedEntry.Model?.Parent is ILayoutPane pane
                && selectedEntry.Model.Parent is ILayoutContentSelector selector)
            {
                int index = selector.IndexOf(selectedEntry.Model);
                if (index > 0)
                {
                    Restore(selectedEntry);
                    pane.MoveChild(index, 0);
                    selector.SelectedContentIndex = 0;
                    owner.DispatcherQueue.TryEnqueue(QueueUpdate);
                }
            }
        }
        finally { updating = false; }
    }

    private static void Hide(Entry entry)
    {
        if (!entry.Hidden)
        {
            entry.Hidden = true;
            entry.Opacity = entry.Tab.Opacity;
            entry.HitTest = entry.Tab.IsHitTestVisible;
            entry.TabStop = entry.Tab.IsTabStop;
            entry.Accessibility = AutomationProperties.GetAccessibilityView(entry.Tab);
        }
        entry.Tab.Opacity = 0;
        entry.Tab.IsHitTestVisible = false;
        entry.Tab.IsTabStop = false;
        entry.AllocatedWidth = 0;
        entry.Tab.Width = 0;
        entry.Tab.MinWidth = 0;
        entry.Tab.MaxWidth = 0;
        entry.Tab.Margin = new Thickness(0);
        AutomationProperties.SetAccessibilityView(entry.Tab, AccessibilityView.Raw);
    }

    private static void Restore(Entry entry)
    {
        if (!entry.Hidden)
        {
            return;
        }

        entry.Hidden = false;
        entry.Tab.Opacity = entry.Opacity;
        entry.Tab.IsHitTestVisible = entry.HitTest;
        entry.Tab.IsTabStop = entry.TabStop;
        entry.Tab.Margin = entry.OriginalMargin;
        AutomationProperties.SetAccessibilityView(entry.Tab, entry.Accessibility);
    }

    private void Release(Entry entry)
    {
        entry.Tab.Loaded -= OnLoaded;
        if (entry.Model is not null)
        {
            entry.Model.PropertyChanged -= OnModelChanged;
        }

        foreach ((DependencyProperty? property, long token) in entry.Tokens)
        {
            entry.Tab.UnregisterPropertyChangedCallback(property, token);
        }

        DetachHeader(entry);
        Restore(entry);
        entry.Tab.Width = entry.OriginalWidth;
        entry.Tab.MinWidth = entry.OriginalMinWidth;
        entry.Tab.MaxWidth = entry.OriginalMaxWidth;
        entry.Tab.Margin = entry.OriginalMargin;
    }

    private void AttachHeader(Entry entry)
    {
        DetachHeader(entry);
        if (entry.Tab.Header is not FrameworkElement header)
        {
            return;
        }

        entry.Header = header;
        List<DependencyProperty> properties = new()
        {
            FrameworkElement.WidthProperty, FrameworkElement.HeightProperty,
            FrameworkElement.MinWidthProperty, FrameworkElement.MaxWidthProperty,
            FrameworkElement.MarginProperty, FrameworkElement.StyleProperty,
            FrameworkElement.DataContextProperty,
        };
        if (header is Control)
        {
            properties.AddRange([Control.FontSizeProperty, Control.FontFamilyProperty, Control.FontWeightProperty,
                Control.PaddingProperty, Control.TemplateProperty]);
        }

        if (header is ContentControl)
        {
            properties.AddRange([ContentControl.ContentProperty, ContentControl.ContentTemplateProperty, ContentControl.ContentTemplateSelectorProperty]);
        }

        if (header is TextBlock)
        {
            properties.AddRange([TextBlock.TextProperty, TextBlock.FontSizeProperty, TextBlock.FontFamilyProperty, TextBlock.FontWeightProperty]);
        }

        foreach (DependencyProperty property in properties)
        {
            entry.HeaderTokens.Add((property, header.RegisterPropertyChangedCallback(property, (_, _) =>
            {
                if (updating || disposed)
                {
                    return;
                }

                entry.NaturalWidth = double.NaN;
                QueueUpdate();
            })));
        }

        entry.HeaderSizeChanged = (_, args) =>
        {
            if (updating || disposed || double.IsNaN(entry.NaturalWidth))
            {
                return;
            }

            double tolerance = 1 / (owner.XamlRoot?.RasterizationScale ?? 1);
            // Compare the measured content, not the arranged stretch slot. The reference's
            // minimum tab width can enlarge that slot without changing the header's desired size.
            // A compressed slot is likewise an output of our rule, not a new intrinsic width.
            // During intrinsic measurement MaxWidth is temporarily restored, but the
            // realized tab can still be arranged at its compressed width. Compare
            // the actual slot so a stale compressed DesiredSize does not invalidate
            // the natural width in a perpetual measure/update loop.
            bool compressed = entry.Tab.ActualWidth + tolerance < entry.NaturalWidth;
            if (Math.Abs(args.NewSize.Height - entry.NaturalHeaderHeight) <= tolerance
                && (compressed || Math.Abs(header.DesiredSize.Width - entry.NaturalHeaderWidth) <= tolerance))
            {
                return;
            }

            entry.NaturalWidth = double.NaN;
            QueueUpdate();
        };
        header.SizeChanged += entry.HeaderSizeChanged;
    }

    private static void DetachHeader(Entry entry)
    {
        if (entry.Header is not null)
        {
            if (entry.HeaderSizeChanged is not null)
            {
                entry.Header.SizeChanged -= entry.HeaderSizeChanged;
            }

            foreach ((DependencyProperty? property, long token) in entry.HeaderTokens)
            {
                entry.Header.UnregisterPropertyChangedCallback(property, token);
            }
        }
        entry.HeaderTokens.Clear();
        entry.Header = null;
        entry.HeaderSizeChanged = null;
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T value)
            {
                yield return value;
            }

            foreach (T nested in VisualDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private sealed class Entry(TabViewItem tab, LayoutContent? model)
    {
        internal TabViewItem Tab { get; } = tab;
        internal LayoutContent? Model { get; } = model;
        internal double OriginalWidth { get; } = tab.Width;
        internal double OriginalMinWidth { get; } = tab.MinWidth;
        internal double OriginalMaxWidth { get; } = tab.MaxWidth;
        internal Thickness OriginalMargin { get; } = tab.Margin;
        internal double NaturalWidth { get; set; } = double.NaN;
        internal double AllocatedWidth { get; set; } = double.NaN;
        internal double NaturalHeaderWidth
        {
            get; set;
        }
        internal double NaturalHeaderHeight
        {
            get; set;
        }
        internal List<(DependencyProperty Property, long Token)> Tokens { get; } = [];
        internal List<(DependencyProperty Property, long Token)> HeaderTokens { get; } = [];
        internal FrameworkElement? Header
        {
            get; set;
        }
        internal SizeChangedEventHandler? HeaderSizeChanged
        {
            get; set;
        }
        internal bool Hidden
        {
            get; set;
        }
        internal double Opacity
        {
            get; set;
        }
        internal bool HitTest
        {
            get; set;
        }
        internal bool TabStop
        {
            get; set;
        }
        internal AccessibilityView Accessibility
        {
            get; set;
        }
    }
}
