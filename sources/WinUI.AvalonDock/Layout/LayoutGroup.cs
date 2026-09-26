// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Layout/LayoutGroup.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace AvalonDock.Layout;

/// <summary>
/// Provides a base class for layout group.
/// </summary>
/// <typeparam name="T">The type of the related layout element.</typeparam>
[Serializable]
public abstract class LayoutGroup<T> : LayoutGroupBase, ILayoutGroup
    where T : class, ILayoutElement
{
    private readonly ObservableCollection<T> groupChildren = new();
    private bool isVisible = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutGroup{T}"/> class.
    /// </summary>
    internal LayoutGroup()
    {
        groupChildren.CollectionChanged += OnGroupChildrenCollectionChanged;
    }

    /// <summary>
    /// Gets the children.
    /// </summary>
    // Concrete XBF metadata owners override this getter; virtual dispatch keeps one reflected property.
    public virtual ObservableCollection<T> Children => groupChildren;

    /// <summary>
    /// Gets the children count.
    /// </summary>
    public int ChildrenCount => groupChildren.Count;

    /// <inheritdoc/>
    IEnumerable<ILayoutElement> ILayoutContainer.Children => groupChildren.Cast<ILayoutElement>();

    /// <summary>
    /// Gets or sets a value indicating whether this instance is visible.
    /// </summary>
    public bool IsVisible
    {
        get => isVisible;
        protected set
        {
            if (value == isVisible)
            {
                return;
            }

            RaisePropertyChanging(nameof(IsVisible));
            isVisible = value;
            OnIsVisibleChanged();
            RaisePropertyChanged(nameof(IsVisible));
        }
    }

    /// <summary>
    /// Executes the compute visibility operation.
    /// </summary>
    public void ComputeVisibility() => IsVisible = GetVisibility();

    /// <summary>
    /// Executes the move child operation.
    /// </summary>
    /// <param name="oldIndex">The old index.</param>
    /// <param name="newIndex">The new index.</param>
    public void MoveChild(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
        {
            return;
        }

        groupChildren.Move(oldIndex, newIndex);
        ChildMoved(oldIndex, newIndex);
    }

    /// <summary>
    /// Removes the child at.
    /// </summary>
    /// <param name="childIndex">The child index.</param>
    public void RemoveChildAt(int childIndex)
    {
        groupChildren.RemoveAt(childIndex);
    }

    /// <summary>
    /// Executes the index of child operation.
    /// </summary>
    /// <param name="element">The layout element.</param>
    /// <returns>The resulting value.</returns>
    public int IndexOfChild(ILayoutElement element)
    {
        return groupChildren.Cast<ILayoutElement>().ToList().IndexOf(element);
    }

    /// <summary>
    /// Inserts the child at.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <param name="element">The layout element.</param>
    public void InsertChildAt(int index, ILayoutElement element)
    {
        if (element is T t)
        {
            groupChildren.Insert(index, t);
        }
    }

    /// <summary>
    /// Removes the child.
    /// </summary>
    /// <param name="element">The layout element.</param>
    public void RemoveChild(ILayoutElement element)
    {
        if (element is T t)
        {
            groupChildren.Remove(t);
        }
    }

    /// <summary>
    /// Replaces the child.
    /// </summary>
    /// <param name="oldElement">The existing layout element.</param>
    /// <param name="newElement">The replacement layout element.</param>
    public void ReplaceChild(ILayoutElement oldElement, ILayoutElement newElement)
    {
        if (oldElement is T oldT && newElement is T newT)
        {
            int index = groupChildren.IndexOf(oldT);
            groupChildren.Insert(index, newT);
            groupChildren.RemoveAt(index + 1);
        }
    }

    /// <summary>
    /// Replaces the child at.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <param name="element">The layout element.</param>
    public void ReplaceChildAt(int index, ILayoutElement element)
    {
        groupChildren[index] = (T)element;
    }

    /// <summary>
    /// Executes the on is visible changed operation.
    /// </summary>
    protected virtual void OnIsVisibleChanged()
    {
        UpdateParentVisibility();
    }

    /// <summary>
    /// Gets the visibility.
    /// </summary>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    protected abstract bool GetVisibility();

    /// <summary>
    /// Executes the child moved operation.
    /// </summary>
    /// <param name="oldIndex">The old index.</param>
    /// <param name="newIndex">The new index.</param>
    protected virtual void ChildMoved(int oldIndex, int newIndex)
    {
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(ILayoutContainer? oldValue, ILayoutContainer? newValue)
    {
        base.OnParentChanged(oldValue, newValue);
        ComputeVisibility();
    }

    /// <summary>
    /// Executes the children collection changed operation.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnGroupChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace)
        {
            if (e.OldItems != null)
            {
                foreach (LayoutElement element in e.OldItems)
                {
                    if (ReferenceEquals(element.Parent, this) || e.Action == NotifyCollectionChangedAction.Remove)
                    {
                        element.Parent = null;
                    }
                }
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
        {
            if (e.NewItems != null)
            {
                foreach (LayoutElement element in e.NewItems)
                {
                    if (ReferenceEquals(element.Parent, this))
                    {
                        continue;
                    }

                    element.Parent?.RemoveChild(element);
                    element.Parent = this;
                }
            }
        }

        ComputeVisibility();
        OnChildrenCollectionChanged();

        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // #81 - Make parents update their children up the tree. Otherwise, they will not be redrawn.
            RaiseChildrenTreeChanged();
        }
        else
        {
            NotifyChildrenTreeChanged(ChildrenTreeChange.DirectChildrenChanged);
        }

        RaisePropertyChanged(nameof(ChildrenCount));
    }

    /// <summary>
    /// Updates the parent visibility.
    /// </summary>
    private void UpdateParentVisibility()
    {
        if (Parent is ILayoutElementWithVisibility parentPane)
        {
            parentPane.ComputeVisibility();
        }
    }
}
