// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/ILayoutElement.cs

using System.ComponentModel;

namespace AvalonDock.Layout;

/// <summary>
/// Interface for layout elements that participate in the AvalonDock layout tree.
/// </summary>
public interface ILayoutElement : INotifyPropertyChanged, INotifyPropertyChanging
{
    /// <summary>Gets the parent <see cref="ILayoutContainer"/> for this layout element.</summary>
    ILayoutContainer? Parent
    {
        get;
    }

    /// <summary>Gets the <see cref="LayoutRoot"/> for this layout element.</summary>
    ILayoutRoot? Root
    {
        get;
    }
}
