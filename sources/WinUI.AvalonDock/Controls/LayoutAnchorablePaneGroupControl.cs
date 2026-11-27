// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutAnchorablePaneGroupControl.cs
using AvalonDock.Layout;

namespace AvalonDock.Controls;

public class LayoutAnchorablePaneGroupControl : LayoutGridControl<ILayoutAnchorablePane>
{
    internal LayoutAnchorablePaneGroupControl(LayoutAnchorablePaneGroup model) : base(model) { }

    protected override void OnFixChildrenDockLengths() => EnsureStarLengths();
}
