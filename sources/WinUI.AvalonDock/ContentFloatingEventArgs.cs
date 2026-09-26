// Ported from Dirkster99/AvalonDock, commit 408dc2896e2f41f3bb79a15207f160edee8a6792.
// Source: source/Components/AvalonDock/ContentFloatingEventArgs.cs. Licensed under MS-PL.
using System.ComponentModel;
using AvalonDock.Layout;

namespace AvalonDock;

/// <summary>
/// Provides data for the content Floating event.
/// </summary>
public class ContentFloatingEventArgs : CancelEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentFloatingEventArgs"/> class.
    /// </summary>
    /// <param name="content">The content.</param>
    public ContentFloatingEventArgs(LayoutContent content)
    {
        Content = content;
    }

    /// <summary>
    /// Gets the content.
    /// </summary>
    public LayoutContent Content
    {
        get;
    }
}

/// <summary>
/// Provides data for the content Floated event.
/// </summary>
public class ContentFloatedEventArgs : System.EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentFloatedEventArgs"/> class.
    /// </summary>
    /// <param name="content">The content.</param>
    public ContentFloatedEventArgs(LayoutContent content)
    {
        Content = content;
    }

    /// <summary>
    /// Gets the content.
    /// </summary>
    public LayoutContent Content
    {
        get;
    }
}
