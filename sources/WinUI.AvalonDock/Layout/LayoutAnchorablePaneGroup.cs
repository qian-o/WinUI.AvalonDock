// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutAnchorablePaneGroup.cs

using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout anchorable pane group.
/// </summary>
[ContentProperty(Name = nameof(Children))]
[Serializable]
public class LayoutAnchorablePaneGroup : LayoutPositionableGroup<ILayoutAnchorablePane>, ILayoutAnchorablePane, ILayoutOrientableGroup
{
    // WinUI XBF requires a concrete owner for implicit content declared on a generic base.
    // This exposes the original collection instance without changing its API type or state.
    public override System.Collections.ObjectModel.ObservableCollection<ILayoutAnchorablePane> Children => base.Children;

    // WPF Horizontal is zero; WinUI Vertical is zero. Preserve the upstream default explicitly.
    private Orientation paneOrientation = Orientation.Horizontal;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutAnchorablePaneGroup"/> class.
    /// </summary>
    public LayoutAnchorablePaneGroup()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutAnchorablePaneGroup"/> class.
    /// </summary>
    /// <param name="firstChild">The first child.</param>
    public LayoutAnchorablePaneGroup(LayoutAnchorablePane firstChild)
    {
        Children.Add(firstChild);
    }

    /// <summary>
    /// Gets or sets the orientation.
    /// </summary>
    public Orientation Orientation
    {
        get => paneOrientation;
        set
        {
            if (value == paneOrientation)
            {
                return;
            }

            RaisePropertyChanging(nameof(Orientation));
            paneOrientation = value;
            RaisePropertyChanged(nameof(Orientation));
        }
    }

    /// <inheritdoc/>
    protected override bool GetVisibility() => Children.Count > 0 && Children.Any(c => c.IsVisible);

    /// <inheritdoc/>
    protected override void OnIsVisibleChanged()
    {
        UpdateParentVisibility();
        base.OnIsVisibleChanged();
    }

    /// <inheritdoc/>
    protected override void OnDockWidthChanged()
    {
        if (DockWidth.IsAbsolute && ChildrenCount == 1)
        {
            ((ILayoutPositionableElement)Children[0]).DockWidth = DockWidth;
        }

        base.OnDockWidthChanged();
    }

    /// <inheritdoc/>
    protected override void OnDockHeightChanged()
    {
        if (DockHeight.IsAbsolute && ChildrenCount == 1)
        {
            ((ILayoutPositionableElement)Children[0]).DockHeight = DockHeight;
        }

        base.OnDockHeightChanged();
    }

    /// <inheritdoc/>
    protected override void OnChildrenCollectionChanged()
    {
        if (DockWidth.IsAbsolute && ChildrenCount == 1)
        {
            ((ILayoutPositionableElement)Children[0]).DockWidth = DockWidth;
        }

        if (DockHeight.IsAbsolute && ChildrenCount == 1)
        {
            ((ILayoutPositionableElement)Children[0]).DockHeight = DockHeight;
        }

        base.OnChildrenCollectionChanged();
    }

#if TRACE
    /// <inheritdoc />
    public override void ConsoleDump(int tab)
    {
        System.Diagnostics.Trace.Write(new string(' ', tab * 4));
        System.Diagnostics.Trace.WriteLine(string.Format("AnchorablePaneGroup({0})", Orientation));

        foreach (LayoutElement child in Children)
        {
            child.ConsoleDump(tab + 1);
        }
    }
#endif

    /// <summary>
    /// Updates the parent visibility.
    /// </summary>
    private void UpdateParentVisibility()
    {
        if (Parent is ILayoutElementWithVisibility parentPane)
        {
            parentPane.ComputeVisibility();
        }
    }
}
