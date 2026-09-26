// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutControl.cs

namespace AvalonDock.Layout;

/// <summary>Defines a control class that hosts a <see cref="ILayoutElement"/> as its model</summary>
public interface ILayoutControl
{
    /// <summary>Gets the <see cref="ILayoutElement"/> model for this control.</summary>
    ILayoutElement? Model
    {
        get;
    }
}
