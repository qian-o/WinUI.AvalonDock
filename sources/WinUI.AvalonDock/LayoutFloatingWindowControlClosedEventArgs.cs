// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/LayoutFloatingWindowControlClosedEventArgs.cs
using System;
using AvalonDock.Controls;

namespace AvalonDock;

/// <summary>
/// Provides data for the layout Floating Window Control Closed event.
/// </summary>
public sealed class LayoutFloatingWindowControlClosedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutFloatingWindowControlClosedEventArgs"/> class.
    /// </summary>
    /// <param name="layoutFloatingWindowControl">The layout Floating Window Control.</param>
    public LayoutFloatingWindowControlClosedEventArgs(LayoutFloatingWindowControl layoutFloatingWindowControl)
    {
        LayoutFloatingWindowControl = layoutFloatingWindowControl;
    }

    /// <summary>
    /// Gets the layout Floating Window Control.
    /// </summary>
    public LayoutFloatingWindowControl LayoutFloatingWindowControl
    {
        get;
    }
}
