// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutElement.cs

using System;
using System.ComponentModel;
using System.Xml.Serialization;
using Microsoft.UI.Xaml;

namespace AvalonDock.Layout;

/// <summary>
/// Provides a base class for layout element.
/// </summary>
[Serializable]
public abstract class LayoutElement : DependencyObject, ILayoutElement, Core.Serialization.ISerializableLayoutElement
{
    [NonSerialized]
    private ILayoutContainer? layoutParent;

    [NonSerialized]
    private ILayoutRoot? layoutRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutElement"/> class.
    /// </summary>
    internal LayoutElement()
    {
    }

    /// <summary>
    /// Occurs when the property changed event is raised.
    /// </summary>
    [field: NonSerialized]
    [field: XmlIgnore]
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when the property changing event is raised.
    /// </summary>
    [field: NonSerialized]
    [field: XmlIgnore]
    public event PropertyChangingEventHandler? PropertyChanging;

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    [XmlIgnore]
    public ILayoutContainer? Parent
    {
        get => layoutParent;
        set
        {
            if (layoutParent == value)
            {
                return;
            }

            ILayoutContainer? oldValue = layoutParent;
            ILayoutRoot? oldRoot = layoutRoot;
            RaisePropertyChanging(nameof(Parent));
            OnParentChanging(oldValue, value);
            layoutParent = value;
            OnParentChanged(oldValue, value);

            layoutRoot = Root;
            if (oldRoot != layoutRoot)
            {
                OnRootChanged(oldRoot, layoutRoot);
            }

            RaisePropertyChanged(nameof(Parent));
            if (Root is LayoutRoot root)
            {
                root.FireLayoutUpdated();
            }
        }
    }

    /// <summary>
    /// Gets the root.
    /// </summary>
    public ILayoutRoot? Root
    {
        get
        {
            ILayoutContainer? parent = Parent;
            while (parent != null && (!(parent is ILayoutRoot)))
            {
                parent = parent.Parent;
            }

            return parent as ILayoutRoot;
        }
    }

    /// <summary>
    /// Executes the fix cached root on deserialize operation.
    /// </summary>
    public void FixCachedRootOnDeserialize()
    {
        if (layoutRoot == null)
        {
            layoutRoot = Root;
        }
    }

    /// <summary>
    /// Executes the on parent changing operation.
    /// </summary>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    protected virtual void OnParentChanging(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
    }

    /// <summary>
    /// Executes the on parent changed operation.
    /// </summary>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    protected virtual void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
    }

    /// <summary>
    /// Executes the on root changed operation.
    /// </summary>
    /// <param name="oldRoot">The old root.</param>
    /// <param name="newRoot">The new root.</param>
    protected virtual void OnRootChanged(ILayoutRoot? oldRoot, ILayoutRoot? newRoot)
    {
        (oldRoot as LayoutRoot)?.OnLayoutElementRemoved(this);
        (newRoot as LayoutRoot)?.OnLayoutElementAdded(this);
    }

    /// <summary>
    /// Raises the property changed.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    protected virtual void RaisePropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Raises the property changing.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    protected virtual void RaisePropertyChanging(string propertyName) => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
}
