// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutUpdateStrategy.cs

namespace AvalonDock.Layout;

public interface ILayoutUpdateStrategy
{
    bool BeforeInsertAnchorable(
        LayoutRoot layout,
        LayoutAnchorable anchorableToShow,
        ILayoutContainer? destinationContainer);

    void AfterInsertAnchorable(
        LayoutRoot layout,
        LayoutAnchorable anchorableShown);

    bool BeforeInsertDocument(
        LayoutRoot layout,
        LayoutDocument anchorableToShow,
        ILayoutContainer? destinationContainer);

    void AfterInsertDocument(
        LayoutRoot layout,
        LayoutDocument anchorableShown);
}
