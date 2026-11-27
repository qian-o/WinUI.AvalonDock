// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/IAdjustableSizeLayout.cs

using Windows.Foundation;

namespace AvalonDock.Layout;

public interface IAdjustableSizeLayout
{
    void AdjustFixedChildrenPanelSizes(Size? parentSize = null);
}
