// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutAnchorGroup.cs

using System;
using System.Xml.Serialization;
using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Layout;

/// <summary>
/// Represents a layout anchor group.
/// </summary>
[ContentProperty(Name = nameof(Children))]
[Serializable]
public class LayoutAnchorGroup : LayoutGroup<LayoutAnchorable>, ILayoutPreviousContainer, ILayoutPaneSerializable,
    Core.Serialization.ISerializableLayoutPane, Core.Serialization.ISerializablePreviousContainer
{
    // WinUI XBF requires a concrete owner for implicit content declared on a generic base.
    // This exposes the original collection instance without changing its API type or state.
    public override System.Collections.ObjectModel.ObservableCollection<LayoutAnchorable> Children => base.Children;

    /// <inheritdoc/>
    protected override bool GetVisibility() => Children.Count > 0;

    [field: NonSerialized]
    private ILayoutContainer? previousContainer;

    /// <inheritdoc/>
    [XmlIgnore]
    ILayoutContainer? ILayoutPreviousContainer.PreviousContainer
    {
        get => previousContainer;
        set
        {
            if (value == previousContainer)
            {
                return;
            }

            previousContainer = value;
            RaisePropertyChanged(nameof(ILayoutPreviousContainer.PreviousContainer));
            if (previousContainer is ILayoutPaneSerializable paneSerializable && paneSerializable.Id == null)
            {
                paneSerializable.Id = Guid.NewGuid().ToString();
            }
        }
    }

    /// <inheritdoc/>
    string? ILayoutPreviousContainer.PreviousContainerId
    {
        get; set;
    }

    private string? id;

    /// <inheritdoc/>
    string? ILayoutPaneSerializable.Id
    {
        get => id; set => id = value;
    }

    /// <inheritdoc/>
    string? Core.Serialization.ISerializableLayoutPane.Id
    {
        get => id; set => id = value;
    }

    /// <inheritdoc/>
    Core.Serialization.ISerializableLayoutContainer? Core.Serialization.ISerializablePreviousContainer.PreviousContainer
    {
        get => previousContainer as Core.Serialization.ISerializableLayoutContainer;
        set => ((ILayoutPreviousContainer)this).PreviousContainer = value as ILayoutContainer;
    }

    /// <inheritdoc/>
    string? Core.Serialization.ISerializablePreviousContainer.PreviousContainerId
    {
        get => ((ILayoutPreviousContainer)this).PreviousContainerId;
        set => ((ILayoutPreviousContainer)this).PreviousContainerId = value;
    }
}
