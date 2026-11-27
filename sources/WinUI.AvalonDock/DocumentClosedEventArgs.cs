// Ported from Dirkster99/AvalonDock, commit 408dc2896e2f41f3bb79a15207f160edee8a6792.
// Source: source/Components/AvalonDock/DocumentClosedEventArgs.cs. Licensed under MS-PL.
using System;
using AvalonDock.Layout;

namespace AvalonDock;

public class DocumentClosedEventArgs : EventArgs
{
    public DocumentClosedEventArgs(LayoutDocument document)
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
