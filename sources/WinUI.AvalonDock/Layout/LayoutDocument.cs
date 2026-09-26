// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutDocument.cs

using System;
using System.Linq;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout document.
/// </summary>
[Serializable]
public class LayoutDocument : LayoutContent, Core.Serialization.ISerializableLayoutDocument
{
    private bool canMove = true;
    private bool isVisible = true;
    private string? documentDescription;

    /// <summary>
    /// Gets or sets a value indicating whether this instance can move.
    /// </summary>
    public bool CanMove
    {
        get => canMove;
        set
        {
            if (value == canMove)
            {
                return;
            }

            canMove = value;
            RaisePropertyChanged(nameof(CanMove));
        }
    }

    /// <summary>
    /// Gets a value indicating whether this instance can hide.
    /// </summary>
    public bool CanHide => false;

    /// <summary>
    /// Gets a value indicating whether this instance is visible.
    /// </summary>
    public bool IsVisible
    {
        get => isVisible;
        internal set => isVisible = value;
    }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description
    {
        get => documentDescription;
        set
        {
            if (documentDescription == value)
            {
                return;
            }

            documentDescription = value;
            RaisePropertyChanged(nameof(Description));
        }
    }

    /// <summary>
    /// Executes the close document operation.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    internal bool CloseDocument()
    {
        if (!TestCanClose())
        {
            return false;
        }

        CloseInternal();
        return true;
    }

    /// <inheritdoc/>
    public override void Close()
    {
        if (Root?.Manager is { } dockingManager)
        {
            dockingManager.ExecuteCloseCommand(this);
        }
        else
        {
            CloseDocument();
        }
    }
#if TRACE
    /// <inheritdoc/>
    public override void ConsoleDump(int tab)
    {
        System.Diagnostics.Trace.Write(new string(' ', tab * 4));
        System.Diagnostics.Trace.WriteLine("Document()");
    }
#endif

    /// <inheritdoc/>
    protected override void InternalDock()
    {
        if (Root is not LayoutRoot root)
        {
            throw new InvalidOperationException();
        }

        LayoutDocumentPane? documentPane = null;
        if (root.LastFocusedDocument is { } lastDocument && lastDocument != this)
        {
            documentPane = lastDocument.Parent as LayoutDocumentPane;
        }

        if (documentPane == null)
        {
            documentPane = root.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        }

        bool added = false;
        ILayoutUpdateStrategy? strategy = root.Manager?.LayoutUpdateStrategy;
        if (strategy != null)
        {
            added = strategy.BeforeInsertDocument(root, this, documentPane);
        }

        if (!added)
        {
            if (documentPane == null)
            {
                throw new InvalidOperationException("Layout must contains at least one LayoutDocumentPane in order to host documents");
            }

            documentPane.Children.Add(this);
        }

        strategy?.AfterInsertDocument(root, this);
        base.InternalDock();
    }
}
