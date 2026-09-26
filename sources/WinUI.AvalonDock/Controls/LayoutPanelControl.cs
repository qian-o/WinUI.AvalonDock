// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutPanelControl.cs

using System;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the layout Panel Control.
/// </summary>
public class LayoutPanelControl : LayoutGridControl<ILayoutPanelElement>, ILayoutControl
{
    private readonly LayoutPanel model;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutPanelControl"/> class.
    /// </summary>
    /// <param name="model">The model.</param>
    internal LayoutPanelControl(LayoutPanel model)
        : base(model, model.Orientation)
    {
        this.model = model;
    }

    /// <inheritdoc/>
    protected override void OnFixChildrenDockLengths()
    {
        if (ActualWidth == 0.0 || ActualHeight == 0.0)
        {
            return;
        }

        if (model.Orientation == Orientation.Horizontal)
        {
            if (model.ContainsChildOfType<LayoutDocumentPane, LayoutDocumentPaneGroup>())
            {
                for (int i = 0; i < model.Children.Count; i++)
                {
                    if (model.Children[i] is ILayoutContainer &&
                        (((ILayoutContainer)model.Children[i]).IsOfType<LayoutDocumentPane, LayoutDocumentPaneGroup>() ||
                         ((ILayoutContainer)model.Children[i]).ContainsChildOfType<LayoutDocumentPane, LayoutDocumentPaneGroup>()))
                    {
                        // Keep set values (from XML for instance)
                        if (!((ILayoutPositionableElement)model.Children[i]).DockWidth.IsStar)
                        {
                            ((ILayoutPositionableElement)model.Children[i]).DockWidth = new GridLength(1.0, GridUnitType.Star);
                        }
                    }
                    else if (model.Children[i] is ILayoutPositionableElement && ((ILayoutPositionableElement)model.Children[i]).DockWidth.IsStar)
                    {
                        ILayoutPositionableElementWithActualSize childPositionableModelWidthActualSize = (ILayoutPositionableElementWithActualSize)model.Children[i];
                        double childDockMinWidth = ((ILayoutPositionableElement)model.Children[i]).CalculatedDockMinWidth();
                        double widthToSet = Math.Max(childPositionableModelWidthActualSize.ActualWidth, childDockMinWidth);

                        widthToSet = Math.Min(widthToSet, ActualWidth / 2.0);
                        widthToSet = Math.Max(widthToSet, childDockMinWidth);
                        ((ILayoutPositionableElement)model.Children[i]).DockWidth = new GridLength(widthToSet, GridUnitType.Pixel);
                    }
                }
            }
            else
            {
                for (int i = 0; i < model.Children.Count; i++)
                {
                    ILayoutPositionableElement childPositionableModel = (ILayoutPositionableElement)model.Children[i];
                    if (!childPositionableModel.DockWidth.IsStar)
                    {
                        // Keep set values (from XML for instance)
                        if (!childPositionableModel.DockWidth.IsStar)
                        {
                            childPositionableModel.DockWidth = new GridLength(1.0, GridUnitType.Star);
                        }
                    }
                }
            }
        }
        else // Vertical
        {
            if (model.ContainsChildOfType<LayoutDocumentPane, LayoutDocumentPaneGroup>())
            {
                for (int i = 0; i < model.Children.Count; i++)
                {
                    if (model.Children[i] is ILayoutContainer childContainerModel &&
                        (childContainerModel.IsOfType<LayoutDocumentPane, LayoutDocumentPaneGroup>() ||
                         childContainerModel.ContainsChildOfType<LayoutDocumentPane, LayoutDocumentPaneGroup>()))
                    {
                        // Keep set values (from XML for instance)
                        if (!((ILayoutPositionableElement)model.Children[i]).DockHeight.IsStar)
                        {
                            ((ILayoutPositionableElement)model.Children[i]).DockHeight = new GridLength(1.0, GridUnitType.Star);
                        }
                    }
                    else if (model.Children[i] is ILayoutPositionableElement && ((ILayoutPositionableElement)model.Children[i]).DockHeight.IsStar)
                    {
                        ILayoutPositionableElementWithActualSize childPositionableModelWidthActualSize = (ILayoutPositionableElementWithActualSize)model.Children[i];
                        double childDockMinHeight = ((ILayoutPositionableElement)model.Children[i]).CalculatedDockMinHeight();
                        double heightToSet = Math.Max(childPositionableModelWidthActualSize.ActualHeight, childDockMinHeight);
                        heightToSet = Math.Min(heightToSet, ActualHeight / 2.0);
                        heightToSet = Math.Max(heightToSet, childDockMinHeight);
                        ((ILayoutPositionableElement)model.Children[i]).DockHeight = new GridLength(heightToSet, GridUnitType.Pixel);
                    }
                }
            }
            else
            {
                for (int i = 0; i < model.Children.Count; i++)
                {
                    ILayoutPositionableElement childPositionableModel = (ILayoutPositionableElement)model.Children[i];
                    if (!childPositionableModel.DockHeight.IsStar)
                    {
                        // Keep set values (from XML for instance)
                        if (!childPositionableModel.DockHeight.IsStar)
                        {
                            childPositionableModel.DockHeight = new GridLength(1.0, GridUnitType.Star);
                        }
                    }
                }
            }
        }
    }
}
