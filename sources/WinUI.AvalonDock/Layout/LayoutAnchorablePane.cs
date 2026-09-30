// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutAnchorablePane.cs

using System;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout anchorable pane.
/// </summary>
[ContentProperty(Name = nameof(Children))]
[Serializable]
public class LayoutAnchorablePane : LayoutPositionableGroup<LayoutAnchorable>, ILayoutAnchorablePane, ILayoutPositionableElement, ILayoutContentSelector, ILayoutPaneSerializable, Core.Serialization.ISerializableLayoutPane
{
    // WinUI XBF cannot resolve an implicit content member declared on a closed generic
    // base. Preserve the same inherited collection and type through a concrete metadata owner.
    public override System.Collections.ObjectModel.ObservableCollection<LayoutAnchorable> Children => base.Children;

    private int selectedIndex = -1;

    [XmlIgnore]
    private bool autoFixSelectedContent = true;

    private string? paneName;
    private string? id;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutAnchorablePane"/> class.
    /// </summary>
    public LayoutAnchorablePane()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutAnchorablePane"/> class.
    /// </summary>
    /// <param name="anchorable">The anchorable.</param>
    public LayoutAnchorablePane(LayoutAnchorable anchorable)
    {
        Children.Add(anchorable);
    }

    /// <summary>
    /// Gets a value indicating whether this instance can hide.
    /// </summary>
    public bool CanHide => Children.All(a => a.CanHide);

    /// <summary>
    /// Gets a value indicating whether this instance can close.
    /// </summary>
    public bool CanClose => Children.All(a => a.CanClose);

    /// <summary>
    /// Gets a value indicating whether this instance is hosted in floating window.
    /// </summary>
    public bool IsHostedInFloatingWindow => this.FindParent<LayoutFloatingWindow>() != null;

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string? Name
    {
        get => paneName;
        set
        {
            if (value == paneName)
            {
                return;
            }

            paneName = value;
            RaisePropertyChanged(nameof(Name));
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

    /// <inheritdoc/>
    protected override bool GetVisibility() => Children.Count > 0 && Children.Any(c => c.IsVisible);

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

        RaisePropertyChanged(nameof(CanClose));
        RaisePropertyChanged(nameof(CanHide));
        RaisePropertyChanged(nameof(IsDirectlyHostedInFloatingWindow));
        base.OnChildrenCollectionChanged();
    }

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
    /// Executes the index of operation.
    /// </summary>
    /// <param name="content">The layout content.</param>
    /// <returns>The resulting value.</returns>
    public int IndexOf(LayoutContent content)
    {
        if (!(content is LayoutAnchorable anchorableChild))
        {
            return -1;
        }

        return Children.IndexOf(anchorableChild);
    }

    /// <summary>
    /// Gets a value indicating whether this instance is directly hosted in floating window.
    /// </summary>
    public bool IsDirectlyHostedInFloatingWindow
    {
        get
        {
            LayoutAnchorableFloatingWindow? parentFloatingWindow = this.FindParent<LayoutAnchorableFloatingWindow>();
            return parentFloatingWindow != null && parentFloatingWindow.IsSinglePane;
        }
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
    /// Updates the is directly hosted in floating window.
    /// </summary>
    internal void UpdateIsDirectlyHostedInFloatingWindow() => RaisePropertyChanged(nameof(IsDirectlyHostedInFloatingWindow));

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
    /// Sets the last activated index.
    /// </summary>
    private void SetLastActivatedIndex()
    {
        LayoutAnchorable? lastActivatedDocument = Children.Where(c => c.IsEnabled).OrderByDescending(c => c.LastActivationTimeStamp.GetValueOrDefault()).FirstOrDefault();
        SelectedContentIndex = lastActivatedDocument == null ? -1 : Children.IndexOf(lastActivatedDocument);
    }

    /// <summary>
    /// Executes the on parent children collection changed operation.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnParentChildrenCollectionChanged(object? sender, EventArgs e) => RaisePropertyChanged(nameof(IsDirectlyHostedInFloatingWindow));
}
