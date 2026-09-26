// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutContainer.cs

using System.Collections.Generic;

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout container.
/// </summary>
public interface ILayoutContainer : ILayoutElement
{
    /// <summary>
    /// Gets the children.
    /// </summary>
    IEnumerable<ILayoutElement> Children
    {
        get;
    }

    /// <summary>
    /// Gets the children count.
    /// </summary>
    int ChildrenCount
    {
        get;
    }

    /// <summary>
    /// Removes the child.
    /// </summary>
    /// <param name="element">The layout element.</param>
    void RemoveChild(ILayoutElement element);

    /// <summary>
    /// Replaces the child.
    /// </summary>
    /// <param name="oldElement">The existing layout element.</param>
    /// <param name="newElement">The replacement layout element.</param>
    void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement);
}
