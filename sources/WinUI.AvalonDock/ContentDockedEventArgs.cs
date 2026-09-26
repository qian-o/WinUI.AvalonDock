// Ported from Dirkster99/AvalonDock, commit 408dc2896e2f41f3bb79a15207f160edee8a6792.
// Source: source/Components/AvalonDock/ContentDockedEventArgs.cs. Licensed under MS-PL.
using System.ComponentModel;
using AvalonDock.Layout;

namespace AvalonDock;

/// <summary>
/// Provides data for the content Docking event.
/// </summary>
public class ContentDockingEventArgs : CancelEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDockingEventArgs"/> class.
    /// </summary>
    /// <param name="content">The content.</param>
    public ContentDockingEventArgs(LayoutContent content)
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
/// Provides data for the content Docked event.
/// </summary>
public class ContentDockedEventArgs : System.EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDockedEventArgs"/> class.
    /// </summary>
    /// <param name="content">The content.</param>
    public ContentDockedEventArgs(LayoutContent content)
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
