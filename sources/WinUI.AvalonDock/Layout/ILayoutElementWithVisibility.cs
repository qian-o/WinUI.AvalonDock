// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutElementWithVisibility.cs

namespace AvalonDock.Layout;

/// <summary>Interface definition for a layout element that can update its visibility (IsVisible) property.</summary>
public interface ILayoutElementWithVisibility
{
    /// <summary>Invoke this to update the visibility (IsVisible) property of this layout element.</summary>
    void ComputeVisibility();
}
