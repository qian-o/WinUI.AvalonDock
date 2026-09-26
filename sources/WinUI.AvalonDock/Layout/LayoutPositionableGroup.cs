// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutPositionableGroup.cs

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Layout;

/// <summary>
/// Provides a base class for layout positionable group.
/// </summary>
/// <typeparam name="T">The type of the related layout element.</typeparam>
[Serializable]
public abstract class LayoutPositionableGroup<T> : LayoutGroup<T>, ILayoutPositionableElementWithActualSize
    where T : class, ILayoutElement
{
    // DockWidth fields
    private GridLength dockWidth = new(1.0, GridUnitType.Star);

    private double? resizableAbsoluteDockWidth;

    // DockHeight fields
    private GridLength dockHeight = new(1.0, GridUnitType.Star);

    private double? resizableAbsoluteDockHeight;

    private bool allowDuplicateContent = true;
    private bool canRepositionItems = true;

    private double dockMinWidth = 25.0;
    private double dockMinHeight = 25.0;
    private double floatingWidth = 0.0;
    private double floatingHeight = 0.0;
    private double floatingLeft = 0.0;
    private double floatingTop = 0.0;

    private bool isMaximized = false;

    [NonSerialized]
    private double actualWidth;

    [NonSerialized]
    private double actualHeight;

    /// <summary>
    /// Occurs when the floating properties updated event is raised.
    /// </summary>
    public event EventHandler? FloatingPropertiesUpdated;

    /// <summary>
    /// Gets or sets the dock width.
    /// </summary>
    public GridLength DockWidth
    {
        get => dockWidth.IsAbsolute && resizableAbsoluteDockWidth < dockWidth.Value && resizableAbsoluteDockWidth.HasValue ?
                    new GridLength(resizableAbsoluteDockWidth.Value) : dockWidth;
        set
        {
            if (value == dockWidth || !(value.Value > 0))
            {
                return;
            }

            if (value.IsAbsolute)
            {
                resizableAbsoluteDockWidth = value.Value;
            }

            RaisePropertyChanging(nameof(DockWidth));
            dockWidth = value;
            RaisePropertyChanged(nameof(DockWidth));
            OnDockWidthChanged();
        }
    }

    /// <summary>
    /// Gets the fixed dock width.
    /// </summary>
    public double FixedDockWidth => dockWidth.IsAbsolute && dockWidth.Value >= dockMinWidth ? dockWidth.Value : dockMinWidth;

    /// <summary>
    /// Gets or sets the resizable absolute dock width.
    /// </summary>
    public double ResizableAbsoluteDockWidth
    {
        get => resizableAbsoluteDockWidth ?? 0;
        set
        {
            if (!dockWidth.IsAbsolute)
            {
                return;
            }

            if (value <= dockWidth.Value && value > 0)
            {
                RaisePropertyChanging(nameof(DockWidth));
                resizableAbsoluteDockWidth = value;
                RaisePropertyChanged(nameof(DockWidth));
                OnDockWidthChanged();
            }
            else if (value > dockWidth.Value && resizableAbsoluteDockWidth < dockWidth.Value)
            {
                resizableAbsoluteDockWidth = dockWidth.Value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the dock height.
    /// </summary>
    public GridLength DockHeight
    {
        get => dockHeight.IsAbsolute && resizableAbsoluteDockHeight < dockHeight.Value && resizableAbsoluteDockHeight.HasValue ?
                    new GridLength(resizableAbsoluteDockHeight.Value) : dockHeight;
        set
        {
            if (dockHeight == value || !(value.Value > 0))
            {
                return;
            }

            if (value.IsAbsolute)
            {
                resizableAbsoluteDockHeight = value.Value;
            }

            RaisePropertyChanging(nameof(DockHeight));
            dockHeight = value;
            RaisePropertyChanged(nameof(DockHeight));
            OnDockHeightChanged();
        }
    }

    /// <summary>
    /// Gets the fixed dock height.
    /// </summary>
    public double FixedDockHeight => dockHeight.IsAbsolute && dockHeight.Value >= dockMinHeight ? dockHeight.Value : dockMinHeight;

    /// <summary>
    /// Gets or sets the resizable absolute dock height.
    /// </summary>
    public double ResizableAbsoluteDockHeight
    {
        get => resizableAbsoluteDockHeight ?? 0;
        set
        {
            if (!dockHeight.IsAbsolute)
            {
                return;
            }

            if (value < dockHeight.Value && value > 0)
            {
                RaisePropertyChanging(nameof(DockHeight));
                resizableAbsoluteDockHeight = value;
                RaisePropertyChanged(nameof(DockHeight));
                OnDockHeightChanged();
            }
            else if (value > dockHeight.Value && resizableAbsoluteDockHeight < dockHeight.Value)
            {
                resizableAbsoluteDockHeight = dockHeight.Value;
            }
            else if (value == 0)
            {
                resizableAbsoluteDockHeight = DockMinHeight;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether duplicate content is allowed.
    /// </summary>
    public bool AllowDuplicateContent
    {
        get => allowDuplicateContent;
        set
        {
            if (value == allowDuplicateContent)
            {
                return;
            }

            RaisePropertyChanging(nameof(AllowDuplicateContent));
            allowDuplicateContent = value;
            RaisePropertyChanged(nameof(AllowDuplicateContent));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance can reposition items.
    /// </summary>
    public bool CanRepositionItems
    {
        get => canRepositionItems;
        set
        {
            if (value == canRepositionItems)
            {
                return;
            }

            RaisePropertyChanging(nameof(CanRepositionItems));
            canRepositionItems = value;
            RaisePropertyChanged(nameof(CanRepositionItems));
        }
    }

    /// <summary>
    /// Executes the calculated dock min width operation.
    /// </summary>
    /// <returns>The resulting value.</returns>
    public double CalculatedDockMinWidth()
    {
        double childrenDockMinWidth = 0.0;
        List<ILayoutPositionableElement> visibleChildren = Children.OfType<ILayoutPositionableElement>().Where(child => child.IsVisible).ToList();
        if (this is ILayoutOrientableGroup orientableGroup && visibleChildren.Any())
        {
            childrenDockMinWidth = orientableGroup.Orientation == Orientation.Vertical ?
                visibleChildren.Max(child => child.CalculatedDockMinWidth())
              : visibleChildren.Sum(child => child.CalculatedDockMinWidth() + (Root?.Manager?.GridSplitterWidth ?? 0) * (visibleChildren.Count - 1));
        }

        return Math.Max(dockMinWidth, childrenDockMinWidth);
    }

    /// <summary>
    /// Gets or sets the dock min width.
    /// </summary>
    public double DockMinWidth
    {
        get => dockMinWidth;
        set
        {
            if (value == dockMinWidth)
            {
                return;
            }

            MathHelper.AssertIsPositiveOrZero(value);
            RaisePropertyChanging(nameof(DockMinWidth));
            dockMinWidth = value;
            RaisePropertyChanged(nameof(DockMinWidth));
        }
    }

    /// <summary>
    /// Executes the calculated dock min height operation.
    /// </summary>
    /// <returns>The resulting value.</returns>
    public double CalculatedDockMinHeight()
    {
        double childrenDockMinHeight = 0.0;
        List<ILayoutPositionableElement> visibleChildren = Children.OfType<ILayoutPositionableElement>().Where(child => child.IsVisible).ToList();
        if (this is ILayoutOrientableGroup orientableGroup && visibleChildren.Any())
        {
            childrenDockMinHeight = orientableGroup.Orientation == Orientation.Vertical ?
                visibleChildren.Sum(child => child.CalculatedDockMinHeight() + (Root?.Manager?.GridSplitterHeight ?? 0) * (visibleChildren.Count - 1))
              : visibleChildren.Max(child => child.CalculatedDockMinHeight());
        }

        return Math.Max(dockMinHeight, childrenDockMinHeight);
    }

    /// <summary>
    /// Gets or sets the dock min height.
    /// </summary>
    public double DockMinHeight
    {
        get => dockMinHeight;
        set
        {
            if (value == dockMinHeight)
            {
                return;
            }

            MathHelper.AssertIsPositiveOrZero(value);
            RaisePropertyChanging(nameof(DockMinHeight));
            dockMinHeight = value;
            RaisePropertyChanged(nameof(DockMinHeight));
        }
    }

    /// <summary>
    /// Gets or sets the floating width.
    /// </summary>
    public double FloatingWidth
    {
        get => floatingWidth;
        set
        {
            if (value == floatingWidth)
            {
                return;
            }

            RaisePropertyChanging(nameof(FloatingWidth));
            floatingWidth = value;
            RaisePropertyChanged(nameof(FloatingWidth));
        }
    }

    /// <summary>
    /// Gets or sets the floating height.
    /// </summary>
    public double FloatingHeight
    {
        get => floatingHeight;
        set
        {
            if (floatingHeight == value)
            {
                return;
            }

            RaisePropertyChanging(nameof(FloatingHeight));
            floatingHeight = value;
            RaisePropertyChanged(nameof(FloatingHeight));
        }
    }

    /// <summary>
    /// Gets or sets the floating left.
    /// </summary>
    public double FloatingLeft
    {
        get => floatingLeft;
        set
        {
            if (value == floatingLeft)
            {
                return;
            }

            RaisePropertyChanging(nameof(FloatingLeft));
            floatingLeft = value;
            RaisePropertyChanged(nameof(FloatingLeft));
        }
    }

    /// <summary>
    /// Gets or sets the floating top.
    /// </summary>
    public double FloatingTop
    {
        get => floatingTop;
        set
        {
            if (value == floatingTop)
            {
                return;
            }

            RaisePropertyChanging(nameof(FloatingTop));
            floatingTop = value;
            RaisePropertyChanged(nameof(FloatingTop));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is maximized.
    /// </summary>
    public bool IsMaximized
    {
        get => isMaximized;
        set
        {
            if (value == isMaximized)
            {
                return;
            }

            isMaximized = value;
            RaisePropertyChanged(nameof(IsMaximized));
        }
    }

    /// <inheritdoc/>
    double ILayoutPositionableElementWithActualSize.ActualWidth
    {
        get => actualWidth;
        set => actualWidth = value;
    }

    /// <inheritdoc/>
    double ILayoutPositionableElementWithActualSize.ActualHeight
    {
        get => actualHeight;
        set => actualHeight = value;
    }

    /// <inheritdoc/>
    void ILayoutElementForFloatingWindow.RaiseFloatingPropertiesUpdated() => FloatingPropertiesUpdated?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Executes the on dock width changed operation.
    /// </summary>
    protected virtual void OnDockWidthChanged()
    {
    }

    /// <summary>
    /// Executes the on dock height changed operation.
    /// </summary>
    protected virtual void OnDockHeightChanged()
    {
    }
}
