// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutGroupBase.cs

using System;
using System.Xml.Serialization;

namespace AvalonDock.Layout;

/// <summary>
/// Provides a base class for layout group base.
/// </summary>
[Serializable]
public abstract class LayoutGroupBase : LayoutElement, Core.Serialization.ISerializableLayoutContainer
{
    /// <summary>
    /// Occurs when the children collection changed event is raised.
    /// </summary>
    [field: NonSerialized]
    [field: XmlIgnore]
    public event EventHandler? ChildrenCollectionChanged;

    /// <summary>
    /// Occurs when the children tree changed event is raised.
    /// </summary>
    [field: NonSerialized]
    [field: XmlIgnore]
    public event EventHandler<ChildrenTreeChangedEventArgs>? ChildrenTreeChanged;

    /// <summary>
    /// Raises the children tree changed.
    /// </summary>
    internal void RaiseChildrenTreeChanged()
    {
        OnChildrenTreeChanged(ChildrenTreeChange.DirectChildrenChanged);
        LayoutGroupBase? parentGroup = Parent as LayoutGroupBase;
        if (parentGroup != null)
        {
            parentGroup.RaiseChildrenTreeChanged();
        }
    }

    protected virtual void OnChildrenCollectionChanged()
    {
        if (ChildrenCollectionChanged != null)
        {
            ChildrenCollectionChanged(this, EventArgs.Empty);
        }
    }

    protected virtual void OnChildrenTreeChanged(ChildrenTreeChange change)
    {
        if (ChildrenTreeChanged != null)
        {
            ChildrenTreeChanged(this, new ChildrenTreeChangedEventArgs(change));
        }
    }

    protected void NotifyChildrenTreeChanged(ChildrenTreeChange change)
    {
        OnChildrenTreeChanged(change);
        LayoutGroupBase? parentGroup = Parent as LayoutGroupBase;
        if (parentGroup != null)
        {
            parentGroup.NotifyChildrenTreeChanged(ChildrenTreeChange.TreeChanged);
        }
    }
}
