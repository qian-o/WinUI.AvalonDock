// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/LayoutDocumentPaneGroupControl.cs
using AvalonDock.Layout;

namespace AvalonDock.Controls;

public class LayoutDocumentPaneGroupControl : LayoutGridControl<ILayoutDocumentPane>
{
    internal LayoutDocumentPaneGroupControl(LayoutDocumentPaneGroup model) : base(model) { }

    protected override void OnFixChildrenDockLengths() => EnsureStarLengths();
}
