// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutUpdateStrategy.cs

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout update strategy.
/// </summary>
public interface ILayoutUpdateStrategy
{
    /// <summary>
    /// Executes the before insert anchorable operation.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="anchorableToShow">The anchorable to show.</param>
    /// <param name="destinationContainer">The destination container.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    bool BeforeInsertAnchorable(
        LayoutRoot layout,
        LayoutAnchorable anchorableToShow,
        ILayoutContainer? destinationContainer);

    /// <summary>
    /// Executes the after insert anchorable operation.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="anchorableShown">The anchorable shown.</param>
    void AfterInsertAnchorable(
        LayoutRoot layout,
        LayoutAnchorable anchorableShown);

    /// <summary>
    /// Executes the before insert document operation.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="anchorableToShow">The anchorable to show.</param>
    /// <param name="destinationContainer">The destination container.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    bool BeforeInsertDocument(
        LayoutRoot layout,
        LayoutDocument anchorableToShow,
        ILayoutContainer? destinationContainer);

    /// <summary>
    /// Executes the after insert document operation.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="anchorableShown">The anchorable shown.</param>
    void AfterInsertDocument(
        LayoutRoot layout,
        LayoutDocument anchorableShown);
}
