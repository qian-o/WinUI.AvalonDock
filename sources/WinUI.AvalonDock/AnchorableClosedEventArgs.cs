// Ported from Dirkster99/AvalonDock, commit 408dc2896e2f41f3bb79a15207f160edee8a6792.
// Source: source/Components/AvalonDock/AnchorableClosedEventArgs.cs. Licensed under MS-PL.
using System;
using AvalonDock.Layout;

namespace AvalonDock;

public class AnchorableClosedEventArgs : EventArgs
{
    public AnchorableClosedEventArgs(LayoutAnchorable anchorable)
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
