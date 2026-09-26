// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutDocumentPaneGroupControl.cs

using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the layout Document Pane Group Control.
/// </summary>
public class LayoutDocumentPaneGroupControl : LayoutGridControl<ILayoutDocumentPane>, ILayoutControl
{
    private readonly LayoutDocumentPaneGroup model;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutDocumentPaneGroupControl"/> class.
    /// </summary>
    /// <param name="model">The model.</param>
    internal LayoutDocumentPaneGroupControl(LayoutDocumentPaneGroup model)
        : base(model, model.Orientation)
    {
        this.model = model;
    }

    /// <inheritdoc/>
    protected override void OnFixChildrenDockLengths()
    {
        if (model.Orientation == Orientation.Horizontal)
        {
            for (int i = 0; i < model.Children.Count; i++)
            {
                ILayoutPositionableElement childModel = (ILayoutPositionableElement)model.Children[i];
                if (!childModel.DockWidth.IsStar)
                {
                    childModel.DockWidth = new GridLength(1.0, GridUnitType.Star);
                }
            }
        }
        else
        {
            for (int i = 0; i < model.Children.Count; i++)
            {
                ILayoutPositionableElement childModel = (ILayoutPositionableElement)model.Children[i];
                if (!childModel.DockHeight.IsStar)
                {
                    childModel.DockHeight = new GridLength(1.0, GridUnitType.Star);
                }
            }
        }
    }
}
