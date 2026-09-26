// Ported from Dirkster99/AvalonDock, commit 408dc2896e2f41f3bb79a15207f160edee8a6792.
// Source: source/Components/AvalonDock/AnchorableHiddenEventArgs.cs. Licensed under MS-PL.
using System;
using AvalonDock.Layout;

namespace AvalonDock;

/// <summary>
/// Provides data for the anchorable Hidden event.
/// </summary>
public class AnchorableHiddenEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnchorableHiddenEventArgs"/> class.
    /// </summary>
    /// <param name="anchorable">The anchorable.</param>
    public AnchorableHiddenEventArgs(LayoutAnchorable anchorable)
    {
        Anchorable = anchorable;
    }

    /// <summary>
    /// Gets the anchorable.
    /// </summary>
    public LayoutAnchorable Anchorable
    {
        get; private set;
    }
}
