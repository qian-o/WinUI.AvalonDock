// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/LayoutDocumentFloatingWindowControl.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
namespace AvalonDock.Controls;

public partial class LayoutDocumentFloatingWindowControl
{
    public IEnumerable<IDropArea> GetDropAreas(LayoutFloatingWindowControl draggingWindow) =>
        GetFloatingDropAreas(draggingWindow, acceptsDocumentWindows: true);
}
