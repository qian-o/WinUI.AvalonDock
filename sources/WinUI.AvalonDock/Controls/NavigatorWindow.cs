// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/NavigatorWindow.cs.
using System.ComponentModel;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using AvalonDock.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using ReadOnlyPropertyGuard = AvalonDock.Compatibility.ReadOnlyPropertyGuard;

namespace AvalonDock.Controls;

[Microsoft.UI.Xaml.Data.Bindable]
[TemplatePart(Name = "PART_AnchorableListBox", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_DocumentListBox", Type = typeof(ListBox))]
public class NavigatorWindow : Window
{
    private readonly DockingManager manager;
    private readonly TemplateView view;
    private readonly Style defaultStyle;
    private ResourceDictionary? themeResources;
    private Selector? documentList;
    private Selector? anchorableList;
    private bool internalSelection;
    private bool internalSetSelectedDocument;
    private bool internalSetSelectedAnchorable;
    private bool selectingDocument;
    private bool running;
    private bool closed;
    private bool closing;
    private bool activated;
    private bool automaticStyle = true;
    private LayoutItem? pendingActivation;

    internal NavigatorWindow(DockingManager manager)
    {
        this.manager = manager;
        view = new TemplateView(this) { IsTabStop = false };
        view.DataContext = this;
        ResourceDictionary resources = new()
        {
            Source = new Uri("ms-appx:///WinUI.AvalonDock/Themes/NavigatorWindow.xaml")
        };
        view.Resources.MergedDictionaries.Add(resources);
        defaultStyle = (Style)resources["AvalonDockNavigatorWindowStyle"];
        view.Style = defaultStyle;
        Content = view;
        internalSetSelectedDocument = true;
        SetAnchorables(manager.Layout.Descendents().OfType<LayoutAnchorable>().Where(item => item.IsVisible)
            .OrderByDescending(item => item.LastActivationTimeStamp.GetValueOrDefault()).Select(item => manager.GetLayoutItemFromModel(item)).OfType<LayoutAnchorableItem>().ToArray());
        SetDocuments(manager.Layout.Descendents().OfType<LayoutDocument>().OrderByDescending(item => item.LastActivationTimeStamp.GetValueOrDefault())
            .Select(item => manager.GetLayoutItemFromModel(item)).OfType<LayoutDocumentItem>().ToArray());
        internalSetSelectedDocument = false;

        if (Documents.Length > 1)
        {
            InternalSetSelectedDocument(Documents[1]);
            selectingDocument = true;
        }
        else if (Documents.Length == 1)
        {
            InternalSetSelectedDocument(Documents[0]);
            selectingDocument = true;
        }
        else
        {
            LayoutAnchorableItem? anchorable = Anchorables.FirstOrDefault();
            if (anchorable != null)
            {
                InternalSetSelectedAnchorable(anchorable);
                selectingDocument = false;
            }
        }
        UpdateThemeResources();
        view.PreviewKeyDown += (_, args) => OnKeyDown(args);
        view.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler((_, args) => OnKeyUp(args)), true);
        view.Loaded += (_, _) => FocusSelection();
        Activated += OnActivated;
        Closed += OnClosed;
        manager.ActualThemeChanged += OnManagerThemeChanged;
    }

    public static readonly DependencyProperty DocumentsProperty = ReadOnlyPropertyGuard.Register(nameof(Documents), typeof(IEnumerable<LayoutDocumentItem>), typeof(NavigatorWindow), null,
        (state, _) => ((TemplateView)state).Notify(nameof(Documents)));
    [System.ComponentModel.Bindable(true)]
    [Description("Gets the list of documents managed in this framework.")]
    [Category("Document")]
    public LayoutDocumentItem[] Documents => GetValue(DocumentsProperty) as LayoutDocumentItem[] ?? [];
    protected void SetDocuments(LayoutDocumentItem[] value) => ReadOnlyPropertyGuard.Set(view, DocumentsProperty, value);
    public static readonly DependencyProperty AnchorablesProperty = ReadOnlyPropertyGuard.Register(nameof(Anchorables), typeof(IEnumerable<LayoutAnchorableItem>), typeof(NavigatorWindow), null,
        (state, _) => ((TemplateView)state).Notify(nameof(Anchorables)));
    [System.ComponentModel.Bindable(true)]
    [Description("Gets the list of anchorables managed in the framework.")]
    [Category("Anchorable")]
    public IEnumerable<LayoutAnchorableItem> Anchorables => GetValue(AnchorablesProperty) as IEnumerable<LayoutAnchorableItem> ?? [];
    protected void SetAnchorables(IEnumerable<LayoutAnchorableItem> value) => ReadOnlyPropertyGuard.Set(view, AnchorablesProperty, value);
    public static readonly DependencyProperty AnchorablesLabelProperty = Register(nameof(AnchorablesLabel), typeof(string), Properties.Resources.Active_ToolWindows);
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the label displayed above the anchorables list in the navigator.")]
    [Category("Navigator")]
    public string? AnchorablesLabel
    {
        get => (string?)GetValue(AnchorablesLabelProperty); set => SetValue(AnchorablesLabelProperty, value);
    }
    public static readonly DependencyProperty DocumentsLabelProperty = Register(nameof(DocumentsLabel), typeof(string), Properties.Resources.Active_Files);
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the label displayed above the documents list in the navigator.")]
    [Category("Navigator")]
    public string? DocumentsLabel
    {
        get => (string?)GetValue(DocumentsLabelProperty); set => SetValue(DocumentsLabelProperty, value);
    }
    public static readonly DependencyProperty SelectedDocumentProperty = Register(nameof(SelectedDocument), typeof(LayoutDocumentItem), null, (owner, args) => owner.OnSelectedDocumentChanged(args));
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the currently selected document.")]
    [Category("Document")]
    public LayoutDocumentItem? SelectedDocument
    {
        get => (LayoutDocumentItem?)GetValue(SelectedDocumentProperty); set => SetValue(SelectedDocumentProperty, value);
    }
    protected virtual void OnSelectedDocumentChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!internalSetSelectedDocument && SelectedDocument?.ActivateCommand?.CanExecute(null) == true)
        {
            SelectedAnchorable = null;
            CloseAndActivateSelected();
        }
    }
    public static readonly DependencyProperty SelectedAnchorableProperty = Register(nameof(SelectedAnchorable), typeof(LayoutAnchorableItem), null, (owner, args) => owner.OnSelectedAnchorableChanged(args));
    [System.ComponentModel.Bindable(true)]
    [Description("Gets/sets the currently selected anchorable.")]
    [Category("Anchorable")]
    public LayoutAnchorableItem? SelectedAnchorable
    {
        get => (LayoutAnchorableItem?)GetValue(SelectedAnchorableProperty); set => SetValue(SelectedAnchorableProperty, value);
    }
    protected virtual void OnSelectedAnchorableChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!internalSetSelectedAnchorable && SelectedAnchorable?.ActivateCommand?.CanExecute(null) == true)
        {
            SelectedDocument = null;
            CloseAndActivateSelected();
        }
    }

    public static readonly DependencyProperty TemplateProperty = Control.TemplateProperty;
    public ControlTemplate? Template
    {
        get => view.Template; set => view.Template = value;
    }
    public static readonly DependencyProperty StyleProperty = FrameworkElement.StyleProperty;
    public Style? Style
    {
        get => view.Style; set
        {
            automaticStyle = false;
            view.Style = value;
        }
    }
    public ResourceDictionary Resources
    {
        get => view.Resources; set
        {
            view.Resources = value;
            UpdateThemeResources();
        }
    }
    public object? DataContext
    {
        get => view.DataContext; set => view.DataContext = value;
    }
    public object? GetValue(DependencyProperty dp) => view.GetValue(dp);
    public void SetValue(DependencyProperty dp, object? value)
    {
        VerifyWritable(dp);
        if (dp == StyleProperty)
        {
            automaticStyle = false;
        }

        view.SetValue(dp, value);
    }
    public void ClearValue(DependencyProperty dp)
    {
        VerifyWritable(dp);
        view.ClearValue(dp);
        if (dp == StyleProperty)
        {
            automaticStyle = true;
            ApplyStyle();
        }
    }
    public object ReadLocalValue(DependencyProperty dp) => view.ReadLocalValue(dp);
    public void SetBinding(DependencyProperty dp, BindingBase binding)
    {
        VerifyWritable(dp);
        if (dp == StyleProperty)
        {
            automaticStyle = false;
        }

        BindingOperations.SetBinding(view, dp, binding);
    }
    public bool ApplyTemplate() => view.ApplyTemplate();
    protected DependencyObject? GetTemplateChild(string childName) => view.Part(childName);

    public virtual void OnApplyTemplate()
    {
        DetachLists();
        anchorableList = GetTemplateChild("PART_AnchorableListBox") as Selector;
        documentList = GetTemplateChild("PART_DocumentListBox") as Selector;
        foreach (Selector list in new[] { documentList, anchorableList }.OfType<Selector>())
        {
            list.SelectionChanged += OnListSelectionChanged;
            list.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnListPressed), true);
            if (list is ListView native)
            {
                native.ContainerContentChanging += OnContainerContentChanging;
            }
        }
        UpdateLists();
        FocusSelection();
    }
    protected virtual void OnKeyDown(KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            // Press Tab to switch Selected LayoutContent.
            case VirtualKey.Tab:
                SetNextLayoutContent(true);
                e.Handled = true;
                break;
            case VirtualKey.Left:
            case VirtualKey.Right:
                if (selectingDocument)
                {
                    LayoutAnchorableItem? anchorable = Anchorables.ElementAtOrDefault(Array.IndexOf(Documents, SelectedDocument))
                        ?? Anchorables.LastOrDefault();
                    if (anchorable != null)
                    {
                        selectingDocument = false;
                        InternalSetSelectedDocument(null);
                        InternalSetSelectedAnchorable(anchorable);
                    }
                }
                else
                {
                    int index = anchorableList?.SelectedIndex
                        ?? Array.IndexOf(Anchorables.ToArray(), SelectedAnchorable);
                    LayoutDocumentItem? document = Documents.ElementAtOrDefault(index)
                        ?? Documents.LastOrDefault();
                    if (document != null)
                    {
                        selectingDocument = true;
                        InternalSetSelectedAnchorable(null);
                        InternalSetSelectedDocument(document);
                    }
                }

                e.Handled = true;
                break;
            case VirtualKey.Up:
                SetNextLayoutContent(false);
                e.Handled = true;
                break;
            case VirtualKey.Down:
                SetNextLayoutContent(true);
                e.Handled = true;
                break;
        }

        // WinUI Window has no OnKeyDown base hook; unhandled routed input continues natively.

        void SetNextLayoutContent(bool next)
        {
            // Selecting LayoutDocuments
            if (selectingDocument)
            {
                if (SelectedDocument != null)
                {
                    // Jump to previous/next LayoutDocument
                    if (next)
                    {
                        SelectNextDocument();
                    }
                    else
                    {
                        SelectPreviousDocument();
                    }
                }

                // There is no SelectedDocument, select the first one.
                else if (Documents.Length > 0)
                {
                    InternalSetSelectedDocument(Documents[0]);
                }
            }

            // Selecting LayoutAnchorables
            else
            {
                if (SelectedAnchorable != null)
                {
                    // Jump to previous/next LayoutAnchorable
                    if (next)
                    {
                        SelectNextAnchorable();
                    }
                    else
                    {
                        SelectPreviousAnchorable();
                    }
                }

                // There is no SelectedAnchorable, select the first one.
                else
                {
                    LayoutAnchorableItem? anchorable = Anchorables.FirstOrDefault();
                    if (anchorable != null)
                    {
                        InternalSetSelectedAnchorable(anchorable);
                    }
                }
            }
        }
    }

    protected virtual void OnKeyUp(KeyRoutedEventArgs e)
    {
        if (e.Key is not (VirtualKey.Tab or VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down))
        {
            CloseAndActivateSelected();
            e.Handled = true;
        }
    }
    public bool? ShowDialog()
    {
        if (closed)
        {
            throw new InvalidOperationException("A closed navigator cannot be shown again.");
        }

        if (running)
        {
            throw new InvalidOperationException("The navigator is already modal.");
        }

        running = true;
        try
        {
            ApplyStyle();
            ApplyTemplate();
            view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            using IModalWindowHost modal = PlatformServices.CreateModalWindowHost(this, manager);
            // The modal host remeasures after the WinUI lists join a XamlRoot.
            modal.Run(view.DesiredSize);
        }
        catch { Abort(); throw; }
        finally { running = false; }
        ActivatePending();
        return false;
    }
    internal void Abort()
    {
        pendingActivation = null;
        closing = true;
        if (!closed)
        {
            base.Close();
        }
    }
    internal bool IsClosed => closed;
    internal void UpdateThemeResources(Theme? oldTheme = null)
    {
        // WinUI loads Source immediately and requires a separate dictionary owner.
        ResourceDictionary? candidate = ThemeResourceFactory.Create(manager.Theme);

        if (oldTheme != null)
        {
            if (oldTheme is DictionaryTheme)
            {
                if (themeResources != null)
                {
                    Resources.MergedDictionaries.Remove(themeResources);
                    themeResources = null;
                }
            }
            else
            {
                ResourceDictionary? resourceDictionaryToRemove = Resources.MergedDictionaries.FirstOrDefault(r => r.Source == oldTheme.GetResourceUri());
                if (resourceDictionaryToRemove != null)
                {
                    Resources.MergedDictionaries.Remove(resourceDictionaryToRemove);
                }
            }
        }

        if (manager.Theme is DictionaryTheme && candidate is not null)
        {
            themeResources = candidate;
            Resources.MergedDictionaries.Add(candidate);
        }
        else if (candidate != null)
        {
            Resources.MergedDictionaries.Add(candidate);
        }

        view.RequestedTheme = manager.ActualTheme;
        ApplyStyle();
    }
    private void ApplyStyle()
    {
        if (automaticStyle)
        {
            view.Style = Resources.TryGetValue(typeof(NavigatorWindow), out object? item) && item is Style own ? own
            : Application.Current.Resources.TryGetValue(typeof(NavigatorWindow), out item) && item is Style application ? application : defaultStyle;
        }
    }
    internal void SelectNextDocument()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        int docIndex = Array.IndexOf(Documents, SelectedDocument);
        docIndex++;
        if (docIndex == Documents.Length)
        {
            docIndex = 0;
        }

        InternalSetSelectedDocument(Documents[docIndex]);
    }

    /// <summary>
    /// Select next anchorable.
    /// </summary>
    internal void SelectNextAnchorable()
    {
        if (SelectedAnchorable == null)
        {
            return;
        }

        LayoutAnchorableItem[] anchorablesArray = Anchorables.ToArray();
        int anchorableIndex = Array.IndexOf(anchorablesArray, SelectedAnchorable);
        anchorableIndex++;
        if (anchorableIndex == anchorablesArray.Length)
        {
            anchorableIndex = 0;
        }

        InternalSetSelectedAnchorable(anchorablesArray[anchorableIndex]);
    }

    /// <summary>
    /// Select previous document.
    /// </summary>
    internal void SelectPreviousDocument()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        int docIndex = Array.IndexOf(Documents, SelectedDocument);
        docIndex--;
        if (docIndex < 0)
        {
            docIndex = Documents.Length - 1;
        }

        InternalSetSelectedDocument(Documents[docIndex]);
    }

    /// <summary>
    /// Select previous anchorable.
    /// </summary>
    internal void SelectPreviousAnchorable()
    {
        if (SelectedAnchorable == null)
        {
            return;
        }

        LayoutAnchorableItem[] anchorablesArray = Anchorables.ToArray();
        int anchorableIndex = Array.IndexOf(anchorablesArray, SelectedAnchorable);
        anchorableIndex--;
        if (anchorableIndex < 0)
        {
            anchorableIndex = anchorablesArray.Length - 1;
        }

        InternalSetSelectedAnchorable(anchorablesArray[anchorableIndex]);
    }

    private void InternalSetSelectedAnchorable(LayoutAnchorableItem? value)
    {
        bool previous = internalSelection;
        bool previousAnchorable = internalSetSelectedAnchorable;
        internalSelection = true;
        internalSetSelectedAnchorable = true;
        try
        {
            SelectedAnchorable = value;
            UpdateLists();
        }
        finally { internalSetSelectedAnchorable = previousAnchorable; internalSelection = previous; }
        FocusSelectedItem(anchorableList);
    }
    private void InternalSetSelectedDocument(LayoutDocumentItem? value)
    {
        bool previous = internalSelection;
        bool previousDocument = internalSetSelectedDocument;
        internalSelection = true;
        internalSetSelectedDocument = true;
        try
        {
            SelectedDocument = value;
            UpdateLists();
        }
        finally { internalSetSelectedDocument = previousDocument; internalSelection = previous; }
        FocusSelectedItem(documentList);
    }
    private void UpdateLists()
    {
        bool previous = internalSelection;
        internalSelection = true;
        try
        {
            if (documentList != null)
            {
                documentList.ItemsSource = Documents;
                documentList.SelectedItem = SelectedDocument;
            }
            if (anchorableList != null)
            {
                anchorableList.ItemsSource = Anchorables;
                anchorableList.SelectedItem = SelectedAnchorable;
            }
        }
        finally { internalSelection = previous; }
    }
    private void FocusSelection()
    {
        if (SelectedDocument != null)
        {
            FocusSelectedItem(documentList);
        }
        else if (SelectedAnchorable != null)
        {
            FocusSelectedItem(anchorableList);
        }
    }
    private void FocusSelectedItem(Selector? list)
    {
        if (!view.IsLoaded || closed)
        {
            return;
        }

        if (list?.SelectedItem == null)
        {
            return;
        }

        if (list is ListBox legacy)
        {
            legacy.ScrollIntoView(list.SelectedItem);
        }
        else if (list is ListView native)
        {
            native.ScrollIntoView(list.SelectedItem);
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (closed || list.SelectedItem == null)
            {
                return;
            }

            if (list.ContainerFromItem(list.SelectedItem) is Control item)
            {
                item.Focus(FocusState.Keyboard);
            }
        });
    }
    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (internalSelection || closed || args.AddedItems.Count == 0)
        {
            return;
        }

        if (ReferenceEquals(sender, documentList))
        {
            SelectedDocument = (LayoutDocumentItem)args.AddedItems[0];
        }
        else
        {
            SelectedAnchorable = (LayoutAnchorableItem)args.AddedItems[0];
        }
    }
    private static void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not FrameworkElement container)
        {
            return;
        }

        container.DataContext = args.Item;
        if (args.Item is LayoutItem item)
        {
            container.SetBinding(AutomationProperties.NameProperty, new Binding
            {
                Source = item,
                Path = new PropertyPath(nameof(LayoutItem.LayoutElement) + "." + nameof(LayoutContent.Title)),
                Mode = BindingMode.OneWay
            });
        }
        else
        {
            container.ClearValue(AutomationProperties.NameProperty);
        }
    }
    private void OnListPressed(object? sender, PointerRoutedEventArgs args)
    {
        if (!args.GetCurrentPoint(view).Properties.IsLeftButtonPressed || closed)
        {
            return;
        }

        if (sender is not Selector list)
        {
            return;
        }

        for (DependencyObject? element = args.OriginalSource as DependencyObject; element != null && !ReferenceEquals(element, list); element = VisualTreeHelper.GetParent(element))
        {
            if (element is SelectorItem container && list.ItemFromContainer(container) is LayoutItem item)
            {
                if (item is LayoutDocumentItem document)
                {
                    SelectedDocument = document;
                }
                else
                {
                    SelectedAnchorable = (LayoutAnchorableItem)item;
                }

                args.Handled = true;
                return;
            }
        }
    }
    private void CloseAndActivateSelected()
    {
        if (closing || closed)
        {
            return;
        }

        closing = true;
        pendingActivation = (LayoutItem?)SelectedDocument ?? SelectedAnchorable;
        base.Close();
        if (!running)
        {
            ActivatePending();
        }
    }
    private void ActivatePending()
    {
        LayoutItem? item = pendingActivation;
        pendingActivation = null;
        if (item?.LayoutElement?.Root?.Manager != manager || item.ActivateCommand?.CanExecute(null) != true)
        {
            return;
        }

        item.ActivateCommand.Execute(null);
        if (item.LayoutElement.IsActive)
        {
            manager.FocusNavigatorContent(item.LayoutElement);
        }
    }
    private void OnActivated(object? sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            activated = true;
        }
        else if (activated && running)
        {
            CloseAndActivateSelected();
        }
    }
    private void OnManagerThemeChanged(FrameworkElement sender, object args) => view.RequestedTheme = manager.ActualTheme;
    private void OnClosed(object? sender, WindowEventArgs args)
    {
        closed = true;
        DetachLists();
        Activated -= OnActivated;
        Closed -= OnClosed;
        manager.ActualThemeChanged -= OnManagerThemeChanged;
    }
    private void DetachLists()
    {
        foreach (Selector list in new[] { documentList, anchorableList }.OfType<Selector>())
        {
            list.SelectionChanged -= OnListSelectionChanged;
            list.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnListPressed));
            if (list is ListView native)
            {
                native.ContainerContentChanging -= OnContainerContentChanging;
            }
        }
        documentList = null;
        anchorableList = null;
    }
    private static void VerifyWritable(DependencyProperty dp)
    {
        if (dp == DocumentsProperty || dp == AnchorablesProperty)
        {
            throw new InvalidOperationException("The navigator list is read-only.");
        }
    }
    private static DependencyProperty Register(string name, Type type, object? defaultValue, Action<NavigatorWindow, DependencyPropertyChangedEventArgs>? changed = null) =>
        DependencyProperty.Register(name, type, typeof(NavigatorWindow), new PropertyMetadata(defaultValue, (state, args) =>
        {
            TemplateView template = (TemplateView)state;
            changed?.Invoke(template.Owner, args);
            template.Notify(name);
        }));
    [Microsoft.UI.Xaml.Data.Bindable]
    private sealed class TemplateView(NavigatorWindow owner) : Control, INotifyPropertyChanged
    {
        internal NavigatorWindow Owner => owner;
        public LayoutDocumentItem[] Documents => owner.Documents;
        public IEnumerable<LayoutAnchorableItem> Anchorables => owner.Anchorables;
        public LayoutDocumentItem? SelectedDocument => owner.SelectedDocument;
        public LayoutAnchorableItem? SelectedAnchorable => owner.SelectedAnchorable;
        public LayoutItem? SelectedItem => (LayoutItem?)owner.SelectedDocument ?? owner.SelectedAnchorable;
        public string? DocumentsLabel => owner.DocumentsLabel;
        public string? AnchorablesLabel => owner.AnchorablesLabel;
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Notify(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            if (name is nameof(SelectedDocument) or nameof(SelectedAnchorable))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
            }
        }
        internal DependencyObject? Part(string name) => GetTemplateChild(name);
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            owner.OnApplyTemplate();
        }
    }
}
