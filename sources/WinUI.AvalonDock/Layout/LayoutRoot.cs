// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutRoot.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout root.
/// </summary>
[ContentProperty(Name = nameof(RootPanel))]
[Serializable]
public class LayoutRoot : LayoutElement, ILayoutContainer, ILayoutRoot, Core.Serialization.ISerializableLayoutRoot
{
    private LayoutPanel rootPanel;
    private LayoutAnchorSide? topSide;
    private LayoutAnchorSide? rightSide;
    private LayoutAnchorSide? leftSide;
    private LayoutAnchorSide? bottomSide;

    private ObservableCollection<LayoutFloatingWindow>? floatingWindows;
    private ObservableCollection<LayoutAnchorable>? hiddenAnchorables;
    private readonly HashSet<LayoutFloatingWindow> ownedFloatingWindows = new(ReferenceEqualityComparer.Default);
    private readonly HashSet<LayoutAnchorable> ownedHiddenAnchorables = new(ReferenceEqualityComparer.Default);

    [field: NonSerialized]
    private WeakReference? activeContentReference;

    private bool activeContentSet = false;

    [field: NonSerialized]
    private WeakReference? lastFocusedDocument;

    [NonSerialized]
    private DockingManager? dockingManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutRoot"/> class.
    /// </summary>
    public LayoutRoot()
    {
        RightSide = new LayoutAnchorSide();
        LeftSide = new LayoutAnchorSide();
        TopSide = new LayoutAnchorSide();
        BottomSide = new LayoutAnchorSide();
        rootPanel = new LayoutPanel(new LayoutDocumentPane());
        rootPanel.Parent = this;
    }

    /// <summary>
    /// Occurs when the updated event is raised.
    /// </summary>
    public event EventHandler? Updated;

    /// <summary>
    /// Occurs when the element added event is raised.
    /// </summary>
    public event EventHandler<LayoutElementEventArgs>? ElementAdded;

    /// <summary>
    /// Occurs when the element removed event is raised.
    /// </summary>
    public event EventHandler<LayoutElementEventArgs>? ElementRemoved;

    /// <summary>
    /// Gets or sets the root panel.
    /// </summary>
    [AllowNull]
    public LayoutPanel RootPanel
    {
        get => rootPanel;
        set
        {
            if (rootPanel == value)
            {
                return;
            }

            RaisePropertyChanging(nameof(RootPanel));
            LayoutContent? activeContent = ActiveContent;
            ILayoutRoot? activeRoot = activeContent?.Root;
            if (rootPanel != null && ReferenceEquals(rootPanel.Parent, this))
            {
                rootPanel.Parent = null;
            }

            rootPanel = value ?? new LayoutPanel(new LayoutDocumentPane());
            rootPanel.Parent = this;
            if (ActiveContent == null && ReferenceEquals(activeRoot, this) && activeContent != null)
            {
                ActiveContent = activeContent;
                if (ActiveContent != activeContent)
                {
                    ActiveContent = activeContent;
                }
            }

            RaisePropertyChanged(nameof(RootPanel));
        }
    }

    /// <summary>
    /// Gets or sets the top side.
    /// </summary>
    public LayoutAnchorSide? TopSide
    {
        get => topSide;
        set
        {
            if (topSide == value)
            {
                return;
            }

            RaisePropertyChanging(nameof(TopSide));
            if (topSide != null && ReferenceEquals(topSide.Parent, this))
            {
                topSide.Parent = null;
            }

            topSide = value;
            if (topSide != null)
            {
                topSide.Parent = this;
            }

            RaisePropertyChanged(nameof(TopSide));
        }
    }

    /// <summary>
    /// Gets or sets the right side.
    /// </summary>
    public LayoutAnchorSide? RightSide
    {
        get => rightSide;
        set
        {
            if (rightSide == value)
            {
                return;
            }

            RaisePropertyChanging(nameof(RightSide));
            if (rightSide != null && ReferenceEquals(rightSide.Parent, this))
            {
                rightSide.Parent = null;
            }

            rightSide = value;
            if (rightSide != null)
            {
                rightSide.Parent = this;
            }

            RaisePropertyChanged(nameof(RightSide));
        }
    }

    /// <summary>
    /// Gets or sets the left side.
    /// </summary>
    public LayoutAnchorSide? LeftSide
    {
        get => leftSide;
        set
        {
            if (value == leftSide)
            {
                return;
            }

            RaisePropertyChanging(nameof(LeftSide));
            if (leftSide != null && ReferenceEquals(leftSide.Parent, this))
            {
                leftSide.Parent = null;
            }

            leftSide = value;
            if (leftSide != null)
            {
                leftSide.Parent = this;
            }

            RaisePropertyChanged(nameof(LeftSide));
        }
    }

    /// <summary>
    /// Gets or sets the bottom side.
    /// </summary>
    public LayoutAnchorSide? BottomSide
    {
        get => bottomSide;
        set
        {
            if (value == bottomSide)
            {
                return;
            }

            RaisePropertyChanging(nameof(BottomSide));
            if (bottomSide != null && ReferenceEquals(bottomSide.Parent, this))
            {
                bottomSide.Parent = null;
            }

            bottomSide = value;
            if (bottomSide != null)
            {
                bottomSide.Parent = this;
            }

            RaisePropertyChanged(nameof(BottomSide));
        }
    }

    /// <summary>
    /// Gets the floating windows.
    /// </summary>
    public ObservableCollection<LayoutFloatingWindow> FloatingWindows
    {
        get
        {
            if (floatingWindows == null)
            {
                floatingWindows = new ObservableCollection<LayoutFloatingWindow>();
                floatingWindows.CollectionChanged += OnFloatingWindowsCollectionChanged;
            }

            return floatingWindows;
        }
    }

    /// <summary>
    /// Gets the hidden.
    /// </summary>
    public ObservableCollection<LayoutAnchorable> Hidden
    {
        get
        {
            if (hiddenAnchorables == null)
            {
                hiddenAnchorables = new ObservableCollection<LayoutAnchorable>();
                hiddenAnchorables.CollectionChanged += OnHiddenAnchorablesCollectionChanged;
            }

            return hiddenAnchorables;
        }
    }

    /// <summary>
    /// Gets the children.
    /// </summary>
    public IEnumerable<ILayoutElement> Children
    {
        get
        {
            if (RootPanel != null)
            {
                yield return RootPanel;
            }

            if (floatingWindows != null)
            {
                foreach (LayoutFloatingWindow floatingWindow in floatingWindows)
                {
                    yield return floatingWindow;
                }
            }

            if (TopSide != null)
            {
                yield return TopSide;
            }

            if (RightSide != null)
            {
                yield return RightSide;
            }

            if (BottomSide != null)
            {
                yield return BottomSide;
            }

            if (LeftSide != null)
            {
                yield return LeftSide;
            }

            if (hiddenAnchorables != null)
            {
                foreach (LayoutAnchorable hiddenAnchorable in hiddenAnchorables)
                {
                    yield return hiddenAnchorable;
                }
            }
        }
    }

    /// <summary>
    /// Gets the children count.
    /// </summary>
    public int ChildrenCount => 5 + (floatingWindows?.Count ?? 0) + (hiddenAnchorables?.Count ?? 0);

    /// <summary>
    /// Gets or sets the active content.
    /// </summary>
    [XmlIgnore]
    public LayoutContent? ActiveContent
    {
        get
        {
            return activeContentReference?.Target as LayoutContent;
        }
        set
        {
            LayoutContent? currentValue = ActiveContent;
            if (currentValue != value)
            {
                InternalSetActiveContent(currentValue, value);
            }
        }
    }

    /// <summary>
    /// Gets the last focused document.
    /// </summary>
    [XmlIgnore]
    public LayoutContent? LastFocusedDocument
    {
        get => lastFocusedDocument?.Target as LayoutContent;
        private set
        {
            LayoutContent? currentValue = LastFocusedDocument;
            if (currentValue != value)
            {
                RaisePropertyChanging(nameof(LastFocusedDocument));
                if (currentValue != null)
                {
                    currentValue.IsLastFocusedDocument = false;
                }

                lastFocusedDocument = new WeakReference(value);
                currentValue = LastFocusedDocument;
                if (currentValue != null)
                {
                    currentValue.IsLastFocusedDocument = true;
                }

                RaisePropertyChanged(nameof(LastFocusedDocument));
            }
        }
    }

    /// <summary>
    /// Gets the manager.
    /// </summary>
    [XmlIgnore]
    public DockingManager? Manager
    {
        get => dockingManager;
        internal set
        {
            if (value == dockingManager)
            {
                return;
            }

            RaisePropertyChanging(nameof(Manager));
            dockingManager = value;
            RaisePropertyChanged(nameof(Manager));
        }
    }

    /// <summary>
    /// Removes the child.
    /// </summary>
    /// <param name="element">The layout element.</param>
    public void RemoveChild(ILayoutElement element)
    {
        if (ReferenceEquals(element, RootPanel))
        {
            RootPanel = null;
        }
        else if (element is LayoutFloatingWindow floatingWindow && floatingWindows != null && floatingWindows.Contains(floatingWindow))
        {
            floatingWindows.Remove(floatingWindow);
        }
        else if (element is LayoutAnchorable anchorable && hiddenAnchorables != null && hiddenAnchorables.Contains(anchorable))
        {
            hiddenAnchorables.Remove(anchorable);
        }
        else if (ReferenceEquals(element, TopSide))
        {
            TopSide = null;
        }
        else if (ReferenceEquals(element, RightSide))
        {
            RightSide = null;
        }
        else if (ReferenceEquals(element, BottomSide))
        {
            BottomSide = null;
        }
        else if (ReferenceEquals(element, LeftSide))
        {
            LeftSide = null;
        }
    }

    /// <summary>
    /// Replaces the child.
    /// </summary>
    /// <param name="oldElement">The existing layout element.</param>
    /// <param name="newElement">The replacement layout element.</param>
    public void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    {
        if (ReferenceEquals(oldElement, RootPanel))
        {
            RootPanel = (LayoutPanel)newElement;
        }
        else if (oldElement is LayoutFloatingWindow floatingWindow && floatingWindows != null && floatingWindows.Contains(floatingWindow))
        {
            LayoutFloatingWindow replacement = (LayoutFloatingWindow)newElement;
            int index = floatingWindows.IndexOf(floatingWindow);
            floatingWindows.Remove(floatingWindow);
            floatingWindows.Insert(index, replacement);
        }
        else if (oldElement is LayoutAnchorable anchorable && hiddenAnchorables != null && hiddenAnchorables.Contains(anchorable))
        {
            LayoutAnchorable replacement = (LayoutAnchorable)newElement;
            int index = hiddenAnchorables.IndexOf(anchorable);
            hiddenAnchorables.Remove(anchorable);
            hiddenAnchorables.Insert(index, replacement);
        }
        else if (ReferenceEquals(oldElement, TopSide))
        {
            TopSide = (LayoutAnchorSide)newElement;
        }
        else if (ReferenceEquals(oldElement, RightSide))
        {
            RightSide = (LayoutAnchorSide)newElement;
        }
        else if (ReferenceEquals(oldElement, BottomSide))
        {
            BottomSide = (LayoutAnchorSide)newElement;
        }
        else if (ReferenceEquals(oldElement, LeftSide))
        {
            LeftSide = (LayoutAnchorSide)newElement;
        }
    }

    /// <summary>
    /// Collects the garbage.
    /// </summary>
    public void CollectGarbage()
    {
        bool exitFlag = true;

        do
        {
            exitFlag = true;

            // for each content that references via PreviousContainer a disconnected Pane set the property to null
            foreach (ILayoutPreviousContainer? content in this.Descendents().OfType<ILayoutPreviousContainer>().Where(c => c.PreviousContainer != null &&
                (c.PreviousContainer.Parent == null || !ReferenceEquals(c.PreviousContainer.Parent.Root, this))))
            {
                content.PreviousContainer = null;
            }

            // for each pane that is empty
            foreach (ILayoutPane? emptyPane in this.Descendents().OfType<ILayoutPane>().Where(p => p.ChildrenCount == 0))
            {
                // ...set null any reference coming from contents not yet hosted in a floating window
                foreach (LayoutContent? contentReferencingEmptyPane in this.Descendents().OfType<LayoutContent>()
                    .Where(c => ((ILayoutPreviousContainer)c).PreviousContainer == emptyPane))
                {
                    if (contentReferencingEmptyPane is LayoutAnchorable anchorable &&
                        !anchorable.IsVisible)
                    {
                        continue;
                    }

                    ((ILayoutPreviousContainer)contentReferencingEmptyPane).PreviousContainer = null;
                    contentReferencingEmptyPane.PreviousContainerIndex = -1;
                }

                // ...if this pane is the only documentpane present in the layout of the main window (not floating) then skip it
                if (emptyPane is LayoutDocumentPane &&
                     emptyPane.FindParent<LayoutDocumentFloatingWindow>() == null &&
                     this.Descendents().OfType<LayoutDocumentPane>().Count(c => !ReferenceEquals(c, emptyPane) && c.FindParent<LayoutDocumentFloatingWindow>() == null) == 0)
                {
                    continue;
                }

                // ...if this empty pane is not referenced by anyone, then remove it from its parent container
                if (!this.Descendents().OfType<ILayoutPreviousContainer>().Any(c => c.PreviousContainer == emptyPane))
                {
                    if (emptyPane.Parent is not { } parentGroup)
                    {
                        continue;
                    }

                    parentGroup.RemoveChild(emptyPane);
                    exitFlag = false;
                    break;
                }
            }

            if (!exitFlag)
            {
                // removes any empty anchorable pane group
                foreach (LayoutAnchorablePaneGroup? emptyLayoutAnchorablePaneGroup in this.Descendents().OfType<LayoutAnchorablePaneGroup>().Where(p => p.ChildrenCount == 0))
                {
                    if (emptyLayoutAnchorablePaneGroup.Parent is not { } parentGroup)
                    {
                        continue;
                    }

                    parentGroup.RemoveChild(emptyLayoutAnchorablePaneGroup);
                    exitFlag = false;
                    break;
                }
            }

            if (!exitFlag)
            {
                // removes any empty layout panel
                foreach (LayoutPanel? emptyLayoutPanel in this.Descendents().OfType<LayoutPanel>().Where(p => p.ChildrenCount == 0))
                {
                    if (emptyLayoutPanel.Parent is not { } parentGroup)
                    {
                        continue;
                    }

                    parentGroup.RemoveChild(emptyLayoutPanel);
                    exitFlag = false;
                    break;
                }

                foreach (LayoutDocumentPane? emptyLayoutDocumentPane in this.Descendents().OfType<LayoutDocumentPane>().Where(p => p.ChildrenCount == 0))
                {
                    if (emptyLayoutDocumentPane.Parent is not { } parentGroup || parentGroup.Parent is not LayoutDocumentFloatingWindow)
                    {
                        continue;
                    }

                    int index = RootPanel.IndexOfChild(this.Descendents().OfType<LayoutDocumentPaneGroup>().First());
                    parentGroup.RemoveChild(emptyLayoutDocumentPane);
                    if (!this.Descendents().OfType<LayoutDocumentPane>().Any())
                    {
                        // Now the last Pane container is deleted, at least one is required for documents to be added.
                        // We did not want to keep an empty window floating, but add a new one to the main window
                        RootPanel.Children.Insert(index < 0 ? 0 : index, emptyLayoutDocumentPane);
                    }

                    exitFlag = false;
                    break;
                }
            }

            if (!exitFlag)
            {
                // removes any empty floating window
                foreach (LayoutFloatingWindow? emptyLayoutFloatingWindow in this.Descendents().OfType<LayoutFloatingWindow>().Where(p => p.ChildrenCount == 0))
                {
                    if (emptyLayoutFloatingWindow.Parent is not { } parentGroup)
                    {
                        continue;
                    }

                    parentGroup.RemoveChild(emptyLayoutFloatingWindow);
                    exitFlag = false;
                    break;
                }
            }

            if (!exitFlag)
            {
                // removes any empty anchor group
                foreach (LayoutAnchorGroup? emptyLayoutAnchorGroup in this.Descendents().OfType<LayoutAnchorGroup>().Where(p => p.ChildrenCount == 0))
                {
                    if (!this.Descendents().OfType<ILayoutPreviousContainer>().Any(c => ReferenceEquals(c.PreviousContainer, emptyLayoutAnchorGroup)))
                    {
                        if (emptyLayoutAnchorGroup.Parent is not { } parentGroup)
                        {
                            continue;
                        }

                        parentGroup.RemoveChild(emptyLayoutAnchorGroup);
                        exitFlag = false;
                        break;
                    }
                }
            }
        }
        while (!exitFlag);

        do
        {
            exitFlag = true;
            // for each pane that is empty
            foreach (LayoutAnchorablePaneGroup? paneGroupToCollapse in this.Descendents().OfType<LayoutAnchorablePaneGroup>().Where(p => p.ChildrenCount == 1 && p.Children[0] is LayoutAnchorablePaneGroup).ToArray())
            {
                LayoutAnchorablePaneGroup singleChild = (LayoutAnchorablePaneGroup)paneGroupToCollapse.Children[0];
                paneGroupToCollapse.Orientation = singleChild.Orientation;
                while (singleChild.ChildrenCount > 0)
                {
                    paneGroupToCollapse.InsertChildAt(paneGroupToCollapse.ChildrenCount, singleChild.Children[0]);
                }

                paneGroupToCollapse.RemoveChild(singleChild);
                exitFlag = false;
                break;
            }
        }
        while (!exitFlag);

        do
        {
            exitFlag = true;
            // for each pane that is empty
            foreach (LayoutDocumentPaneGroup? paneGroupToCollapse in this.Descendents().OfType<LayoutDocumentPaneGroup>().Where(p => p.ChildrenCount == 1 && p.Children[0] is LayoutDocumentPaneGroup).ToArray())
            {
                LayoutDocumentPaneGroup singleChild = (LayoutDocumentPaneGroup)paneGroupToCollapse.Children[0];
                paneGroupToCollapse.Orientation = singleChild.Orientation;
                while (singleChild.ChildrenCount > 0)
                {
                    paneGroupToCollapse.InsertChildAt(paneGroupToCollapse.ChildrenCount, singleChild.Children[0]);
                }

                paneGroupToCollapse.RemoveChild(singleChild);
                exitFlag = false;
                break;
            }
        }
        while (!exitFlag);

        // Update ActiveContent and LastFocusedDocument properties
        UpdateActiveContentProperty();

    }

    /// <summary>
    /// Executes the fire layout updated operation.
    /// </summary>
    internal void FireLayoutUpdated() => Updated?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Executes the on layout element added operation.
    /// </summary>
    /// <param name="element">The layout element.</param>
    internal void OnLayoutElementAdded(LayoutElement element) => ElementAdded?.Invoke(this, new LayoutElementEventArgs(element));

    /// <summary>
    /// Executes the on layout element removed operation.
    /// </summary>
    /// <param name="element">The layout element.</param>
    internal void OnLayoutElementRemoved(LayoutElement element)
    {
        if (element.Descendents().OfType<LayoutContent>().Any(c => c == LastFocusedDocument))
        {
            LastFocusedDocument = null;
        }

        if (element.Descendents().OfType<LayoutContent>().Any(c => c == ActiveContent))
        {
            ActiveContent = null;
        }

        ElementRemoved?.Invoke(this, new LayoutElementEventArgs(element));
    }

    /// <summary>
    /// Executes the floating windows collection changed operation.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnFloatingWindowsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        bool bNotifyChildren = false;
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            LayoutFloatingWindow[] removedWindows = ownedFloatingWindows.ToArray();
            ownedFloatingWindows.Clear();
            foreach (LayoutFloatingWindow element in removedWindows)
            {
                if (ReferenceEquals(element.Parent, this))
                {
                    element.Parent = null;
                }
            }

            bNotifyChildren = true;
        }

        if (e.OldItems != null && (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove || e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace))
        {
            foreach (LayoutFloatingWindow element in e.OldItems)
            {
                ownedFloatingWindows.Remove(element);
                if (!ReferenceEquals(element.Parent, this))
                {
                    continue;
                }

                element.Parent = null;
                bNotifyChildren = true;
            }
        }

        if (e.NewItems != null && (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add || e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace))
        {
            foreach (LayoutFloatingWindow element in e.NewItems)
            {
                ownedFloatingWindows.Add(element);
                element.Parent = this;
                bNotifyChildren = true;
            }
        }

        // descendants of LayoutElement notify when their Children and ChildrenCount properties change
        // https://github.com/xceedsoftware/wpftoolkit/issues/1313
        if (!bNotifyChildren)
        {
            return;
        }

        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                RaisePropertyChanged(nameof(Children));
                RaisePropertyChanged(nameof(ChildrenCount));
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                RaisePropertyChanged(nameof(Children));
                break;
        }
    }

    /// <summary>
    /// Executes the hidden anchorables collection changed operation.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnHiddenAnchorablesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        bool bNotifyChildren = false;
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            LayoutAnchorable[] removedAnchorables = ownedHiddenAnchorables.ToArray();
            ownedHiddenAnchorables.Clear();
            foreach (LayoutAnchorable element in removedAnchorables)
            {
                if (ReferenceEquals(element.Parent, this))
                {
                    element.Parent = null;
                }
            }

            bNotifyChildren = true;
        }

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove || e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
        {
            if (e.OldItems != null)
            {
                foreach (LayoutAnchorable element in e.OldItems)
                {
                    ownedHiddenAnchorables.Remove(element);
                    if (!ReferenceEquals(element.Parent, this))
                    {
                        continue;
                    }

                    element.Parent = null;
                    bNotifyChildren = true;
                }
            }
        }

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add || e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
        {
            if (e.NewItems != null)
            {
                foreach (LayoutAnchorable element in e.NewItems)
                {
                    ownedHiddenAnchorables.Add(element);
                    if (ReferenceEquals(element.Parent, this))
                    {
                        continue;
                    }

                    element.Parent?.RemoveChild(element);
                    element.Parent = this;
                    bNotifyChildren = true;
                }
            }
        }

        // descendants of LayoutElement notify when their Children and ChildrenCount properties change
        // https://github.com/xceedsoftware/wpftoolkit/issues/1313
        if (!bNotifyChildren)
        {
            return;
        }

        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                RaisePropertyChanged(nameof(Children));
                RaisePropertyChanged(nameof(ChildrenCount));
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                RaisePropertyChanged(nameof(Children));
                break;
        }
    }

    /// <summary>
    /// Executes the internal set active content operation.
    /// </summary>
    /// <param name="currentValue">The current value.</param>
    /// <param name="newActiveContent">The new active content.</param>
    private void InternalSetActiveContent(LayoutContent? currentValue, LayoutContent? newActiveContent)
    {
        RaisePropertyChanging(nameof(ActiveContent));
        if (currentValue != null && currentValue.IsActive)
        {
            currentValue.IsActive = false;
        }

        activeContentReference = new WeakReference(newActiveContent);
        currentValue = ActiveContent;
        if (currentValue != null && !currentValue.IsActive)
        {
            currentValue.IsActive = true;
        }

        RaisePropertyChanged(nameof(ActiveContent));
        activeContentSet = currentValue != null;
        if (currentValue != null)
        {
            if (currentValue.Parent is LayoutDocumentPane || currentValue is LayoutDocument)
            {
                LastFocusedDocument = currentValue;
            }
        }
        else
        {
            LastFocusedDocument = null;
        }
    }

    /// <summary>
    /// Updates the active content property.
    /// </summary>
    private void UpdateActiveContentProperty()
    {
        LayoutContent? activeContent = ActiveContent;
        if (activeContentSet && (activeContent == null || !ReferenceEquals(activeContent.Root, this)))
        {
            activeContentSet = false;
            InternalSetActiveContent(activeContent, null);
        }
    }

    /// <inheritdoc/>
    IEnumerable<Core.Serialization.ISerializableLayoutElement> Core.Serialization.ISerializableLayoutRoot.Descendents()
        => ((ILayoutElement)this).Descendents().OfType<Core.Serialization.ISerializableLayoutElement>();
}
