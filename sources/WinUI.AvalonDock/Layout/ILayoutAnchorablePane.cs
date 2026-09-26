// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutAnchorablePane.cs

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout elements that behave like a <see cref="LayoutAnchorablePane"/>
/// or an equivalent pane container such as <see cref="LayoutAnchorablePaneGroup"/>.
/// </summary>
public interface ILayoutAnchorablePane : ILayoutPanelElement, ILayoutPane
{
}
