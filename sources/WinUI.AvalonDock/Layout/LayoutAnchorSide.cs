// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutAnchorSide.cs

using System;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Implements the viewmodel for a a side element (left, right, top, bottom) in AvalonDock's
/// visual root of the <see cref="DockingManager"/>.
/// </summary>
[ContentProperty(Name = nameof(Children))]
[Serializable]
public class LayoutAnchorSide : LayoutGroup<LayoutAnchorGroup>
{
    // WinUI XBF requires a concrete owner for implicit content declared on a generic base.
    // This exposes the original collection instance without changing its API type or state.
    public override System.Collections.ObjectModel.ObservableCollection<LayoutAnchorGroup> Children => base.Children;

    private AnchorSide anchorSide;

    /// <summary>Gets the side (top, bottom, left, right) that this layout is anchored in the layout.</summary>
    public AnchorSide Side
    {
        get => anchorSide;
        private set
        {
            if (value == anchorSide)
            {
                return;
            }

            RaisePropertyChanging(nameof(Side));
            anchorSide = value;
            RaisePropertyChanged(nameof(Side));
        }
    }

    /// <inheritdoc />
    protected override bool GetVisibility() => Children.Count > 0;

    /// <inheritdoc />
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
        base.OnParentChanged(oldValue, newValue);
        UpdateSide();
    }

    private void UpdateSide()
    {
        ILayoutRoot? root = Root;
        if (root == null)
        {
            return;
        }

        if (this == root.LeftSide)
        {
            Side = AnchorSide.Left;
        }
        else if (this == root.TopSide)
        {
            Side = AnchorSide.Top;
        }
        else if (this == root.RightSide)
        {
            Side = AnchorSide.Right;
        }
        else if (this == root.BottomSide)
        {
            Side = AnchorSide.Bottom;
        }
    }
}
