// Ported from Dirkster99/AvalonDock, commit 408dc2896e2f41f3bb79a15207f160edee8a6792.
// Source: source/Components/AvalonDock/ContentFloatingEventArgs.cs. Licensed under MS-PL.
using System.ComponentModel;
using AvalonDock.Layout;

namespace AvalonDock;

public class ContentFloatingEventArgs : CancelEventArgs
{
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

public class ContentFloatedEventArgs : System.EventArgs
{
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
