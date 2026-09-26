// Ported from Dirkster99/AvalonDock, commit 408dc2896e2f41f3bb79a15207f160edee8a6792.
// Source: source/Components/AvalonDock/DocumentClosingEventArgs.cs. Licensed under MS-PL.
using System.ComponentModel;
using AvalonDock.Layout;

namespace AvalonDock;

/// <summary>
/// Provides data for the document Closing event.
/// </summary>
public class DocumentClosingEventArgs : CancelEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentClosingEventArgs"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public DocumentClosingEventArgs(LayoutDocument document)
    {
        Document = document;
    }

    /// <summary>
    /// Gets the document.
    /// </summary>
    public LayoutDocument Document
    {
        get; private set;
    }
}
