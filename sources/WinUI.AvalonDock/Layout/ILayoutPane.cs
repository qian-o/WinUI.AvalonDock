// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutPane.cs

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout pane.
/// </summary>
public interface ILayoutPane : ILayoutContainer, ILayoutElementWithVisibility
{
    /// <summary>
    /// Executes the move child operation.
    /// </summary>
    /// <param name="oldIndex">The old index.</param>
    /// <param name="newIndex">The new index.</param>
    void MoveChild(int oldIndex, int newIndex);

    /// <summary>
    /// Removes the child at.
    /// </summary>
    /// <param name="childIndex">The child index.</param>
    void RemoveChildAt(int childIndex);
}
