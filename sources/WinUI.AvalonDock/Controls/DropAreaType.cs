// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Controls/DropArea.cs

namespace AvalonDock.Controls;

/// <summary>Identifies the kind of layout region that can receive docking content.</summary>
public enum DropAreaType
{
    /// <summary>The docking manager's outer region.</summary>
    DockingManager,

    /// <summary>A document pane.</summary>
    DocumentPane,

    /// <summary>A document pane group.</summary>
    DocumentPaneGroup,

    /// <summary>An anchorable pane.</summary>
    AnchorablePane,
}
