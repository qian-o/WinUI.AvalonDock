// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutDocumentPane.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout document pane.
/// </summary>
[ContentProperty(Name = nameof(Children))]
[Serializable]
public class LayoutDocumentPane : LayoutPositionableGroup<LayoutContent>, ILayoutDocumentPane, ILayoutPositionableElement, ILayoutContentSelector, ILayoutPaneSerializable, Core.Serialization.ISerializableLayoutPane
{
    // WinUI XBF cannot resolve an implicit content member declared on a closed generic
    // base. Preserve the same inherited collection and type through a concrete metadata owner.
    public override System.Collections.ObjectModel.ObservableCollection<LayoutContent> Children => base.Children;

    private bool showHeader = true;
    private int selectedIndex = -1;
    private string? id;

    [XmlIgnore]
    private readonly bool autoFixSelectedContent = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutDocumentPane"/> class.
    /// </summary>
    public LayoutDocumentPane()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutDocumentPane"/> class.
    /// </summary>
    /// <param name="firstChild">The first child.</param>
    public LayoutDocumentPane(LayoutContent firstChild)
    {
        Children.Add(firstChild);
    }

    /// <summary>
    /// Gets or sets a value indicating whether show header is enabled.
    /// </summary>
    public bool ShowHeader
    {
        get => showHeader;
        set
        {
            if (value == showHeader)
            {
                return;
            }

            showHeader = value;
            RaisePropertyChanged(nameof(ShowHeader));
        }
    }

    /// <summary>
    /// Gets or sets the selected content index.
    /// </summary>
    public int SelectedContentIndex
    {
        get => selectedIndex;
        set
        {
            if (value < 0 || value >= Children.Count)
            {
                value = -1;
            }

            if (value == selectedIndex)
            {
                return;
            }

            RaisePropertyChanging(nameof(SelectedContentIndex));
            RaisePropertyChanging(nameof(SelectedContent));
            if (selectedIndex >= 0 && selectedIndex < Children.Count)
            {
                Children[selectedIndex].IsSelected = false;
            }

            selectedIndex = value;
            if (selectedIndex >= 0 && selectedIndex < Children.Count)
            {
                Children[selectedIndex].IsSelected = true;
            }

            RaisePropertyChanged(nameof(SelectedContentIndex));
            RaisePropertyChanged(nameof(SelectedContent));
        }
    }

    /// <summary>
    /// Gets the selected content.
    /// </summary>
    public LayoutContent? SelectedContent => selectedIndex == -1 ? null : Children[selectedIndex];

    /// <inheritdoc/>
    string? ILayoutPaneSerializable.Id
    {
        get => id;
        set => id = value;
    }

    /// <inheritdoc/>
    string? Core.Serialization.ISerializableLayoutPane.Id
    {
        get => id;
        set => id = value;
    }

    /// <summary>
    /// Gets a value indicating whether this instance is hosted in floating window.
    /// </summary>
    public bool IsHostedInFloatingWindow => this.FindParent<LayoutFloatingWindow>() != null;

    /// <summary>
    /// Gets the children sorted.
    /// </summary>
    public IEnumerable<LayoutContent> ChildrenSorted
    {
        get
        {
            List<LayoutContent> listSorted = Children.ToList();
            listSorted.Sort();
            return listSorted;
        }
    }

    /// <inheritdoc/>
    protected override bool GetVisibility()
    {
        if (Parent is LayoutDocumentPaneGroup)
        {
            return ChildrenCount > 0 && Children.Any(c => (c is LayoutDocument document && document.IsVisible) || c is LayoutAnchorable);
        }

        return true;
    }

    /// <inheritdoc/>
    protected override void ChildMoved(int oldIndex, int newIndex)
    {
        if (selectedIndex == oldIndex)
        {
            RaisePropertyChanging(nameof(SelectedContentIndex));
            selectedIndex = newIndex;
            RaisePropertyChanged(nameof(SelectedContentIndex));
        }

        base.ChildMoved(oldIndex, newIndex);
    }

    /// <inheritdoc/>
    protected override void OnChildrenCollectionChanged()
    {
        AutoFixSelectedContent();
        for (int i = 0; i < Children.Count; i++)
        {
            if (!Children[i].IsSelected)
            {
                continue;
            }

            SelectedContentIndex = i;
            break;
        }

        RaisePropertyChanged(nameof(IsDirectlyHostedInFloatingWindow));
        base.OnChildrenCollectionChanged();
        RaisePropertyChanged(nameof(ChildrenSorted));
    }

    /// <summary>
    /// Executes the auto fix selected content operation.
    /// </summary>
    private void AutoFixSelectedContent()
    {
        if (!autoFixSelectedContent)
        {
            return;
        }

        if (SelectedContentIndex >= ChildrenCount)
        {
            SelectedContentIndex = Children.Count - 1;
        }

        if (SelectedContentIndex == -1 && ChildrenCount > 0)
        {
            SetLastActivatedIndex();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is directly hosted in floating window.
    /// </summary>
    public bool IsDirectlyHostedInFloatingWindow
    {
        get
        {
            LayoutDocumentFloatingWindow? parentFloatingWindow = this.FindParent<LayoutDocumentFloatingWindow>();
            return parentFloatingWindow != null && parentFloatingWindow.IsSinglePane;
            // return Parent != null && Parent.ChildrenCount == 1 && Parent.Parent is LayoutFloatingWindow;
        }
    }

#if TRACE
    /// <inheritdoc/>
    public override void ConsoleDump(int tab)
    {
        System.Diagnostics.Trace.Write(new string(' ', tab * 4));
        System.Diagnostics.Trace.WriteLine("DocumentPane()");

        foreach (LayoutElement child in Children)
        {
            child.ConsoleDump(tab + 1);
        }
    }
#endif

    /// <summary>
    /// Executes the index of operation.
    /// </summary>
    /// <param name="content">The layout content.</param>
    /// <returns>The resulting value.</returns>
    public int IndexOf(LayoutContent content)
    {
        return !(content is LayoutDocument documentChild) ? -1 : Children.IndexOf(documentChild);
    }

    /// <summary>
    /// Sets the next selected index.
    /// </summary>
    internal void SetNextSelectedIndex()
    {
        SelectedContentIndex = -1;
        for (int i = 0; i < Children.Count; ++i)
        {
            if (!Children[i].IsEnabled)
            {
                continue;
            }

            SelectedContentIndex = i;
            return;
        }
    }

    /// <summary>
    /// Sets the last activated index.
    /// </summary>
    private void SetLastActivatedIndex()
    {
        LayoutContent? lastActivatedDocument = Children.Where(c => c.IsEnabled).OrderByDescending(c => c.LastActivationTimeStamp.GetValueOrDefault()).FirstOrDefault();
        SelectedContentIndex = lastActivatedDocument == null ? -1 : Children.IndexOf(lastActivatedDocument);
    }

    /// <summary>
    /// Updates the is directly hosted in floating window.
    /// </summary>
    internal void UpdateIsDirectlyHostedInFloatingWindow() => RaisePropertyChanged(nameof(IsDirectlyHostedInFloatingWindow));

    /// <inheritdoc/>
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
        if (oldValue is ILayoutGroup oldGroup)
        {
            oldGroup.ChildrenCollectionChanged -= OnParentChildrenCollectionChanged;
        }

        RaisePropertyChanged(nameof(IsDirectlyHostedInFloatingWindow));
        if (newValue is ILayoutGroup newGroup)
        {
            newGroup.ChildrenCollectionChanged += OnParentChildrenCollectionChanged;
        }

        base.OnParentChanged(oldValue, newValue);
    }

    /// <summary>
    /// Executes the on parent children collection changed operation.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnParentChildrenCollectionChanged(object? sender, EventArgs e) => RaisePropertyChanged(nameof(IsDirectlyHostedInFloatingWindow));
}
