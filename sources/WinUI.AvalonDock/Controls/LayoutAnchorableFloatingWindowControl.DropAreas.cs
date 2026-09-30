// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/LayoutAnchorableFloatingWindowControl.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
namespace AvalonDock.Controls;

public partial class LayoutAnchorableFloatingWindowControl
{
    IEnumerable<IDropArea> IOverlayWindowHost.GetDropAreas(LayoutFloatingWindowControl draggingWindow) =>
        GetFloatingDropAreas(draggingWindow, acceptsDocumentWindows: false);
}
