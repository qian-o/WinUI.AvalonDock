// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutPanelControl.cs
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Controls;

public class LayoutPanelControl : LayoutGridControl<ILayoutPanelElement>
{
    internal LayoutPanelControl(LayoutPanel model) : base(model) { }

    protected override void OnFixChildrenDockLengths()
    {
        if (ActualWidth == 0 || ActualHeight == 0)
        {
            return;
        }

        LayoutPanel panel = (LayoutPanel)Model;
        if (!panel.ContainsChildOfType<LayoutDocumentPane, LayoutDocumentPaneGroup>())
        {
            EnsureStarLengths();
            return;
        }

        bool horizontal = Orientation == Orientation.Horizontal;
        double halfPanel = (horizontal ? ActualWidth : ActualHeight) / 2;
        foreach (ILayoutPanelElement element in panel.Children)
        {
            if (element is not ILayoutPositionableElement child)
            {
                continue;
            }

            bool holdsDocuments = element is LayoutDocumentPane or LayoutDocumentPaneGroup
                || element is ILayoutContainer group && group.ContainsChildOfType<LayoutDocumentPane, LayoutDocumentPaneGroup>();
            GridLength length = horizontal ? child.DockWidth : child.DockHeight;
            if (length.IsStar == holdsDocuments)
            {
                continue;
            }

            // Document groups share remaining space. Tools keep their measured size up to half
            // the panel, unless their minimum requires more room.
            GridLength desired = new(1, GridUnitType.Star);
            if (!holdsDocuments)
            {
                ILayoutPositionableElementWithActualSize size = (ILayoutPositionableElementWithActualSize)child;
                double actual = horizontal ? size.ActualWidth : size.ActualHeight;
                double minimum = horizontal ? child.CalculatedDockMinWidth() : child.CalculatedDockMinHeight();
                desired = new GridLength(Math.Max(minimum, Math.Min(actual, halfPanel)), GridUnitType.Pixel);
            }
            if (horizontal)
            {
                child.DockWidth = desired;
            }
            else
            {
                child.DockHeight = desired;
            }
        }
    }
}
