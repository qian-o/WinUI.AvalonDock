// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/IAdjustableSizeLayout.cs

using Windows.Foundation;

namespace AvalonDock.Layout;

/// <summary>
/// Interface for adjustable size layout.
/// </summary>
public interface IAdjustableSizeLayout
{
    /// <summary>
    /// Executes the adjust fixed children panel sizes operation.
    /// </summary>
    /// <param name="parentSize">The parent size.</param>
    void AdjustFixedChildrenPanelSizes(Size? parentSize = null);
}
