// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), logical child ordering and presentation inheritance.
using System.Collections;
using AvalonDock.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock;

public partial class DockingManager
{
    private readonly List<WeakReference> logicalChildren = new();
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, InheritedPresentation> logicalPresentation = new();

    /// <summary>Enumerates live owned elements, deepest visual descendants first with stable ties.</summary>
    protected virtual IEnumerator LogicalChildren => GetOrderedLogicalChildren().GetEnumerator();
    /// <summary>Gets the original external logical-children enumeration entry point.</summary>
    public IEnumerator LogicalChildrenPublic => LogicalChildren;

    private void InitializeLogicalPresentation()
    {
        foreach (DependencyProperty property in InheritedPresentation.Properties)
        {
            RegisterPropertyChangedCallback(property, (_, _) => RefreshInheritedPresentation());
        }

        RegisterPropertyChangedCallback(StyleProperty, (_, _) => RefreshInheritedPresentation());
    }

    private void RefreshInheritedPresentation()
    {
        foreach (WeakReference reference in logicalChildren.ToArray())
        {
            if (reference.GetValueOrDefault<object>() is FrameworkElement element && logicalPresentation.TryGetValue(element, out InheritedPresentation? presentation))
            {
                presentation.Refresh();
            }
        }

        foreach (LayoutFloatingWindowControl window in floatingControls.ToArray())
        {
            window.RefreshInheritedPresentation();
        }
    }

    internal void InternalAddLogicalChild(object? child)
    {
        if (child == null || logicalChildren.Select(ch => ch.GetValueOrDefault<object>()).Contains(child))
        {
            return;
        }

        logicalChildren.Add(new WeakReference(child));
        AddNativeLogicalChild(child);
    }
    internal void InternalRemoveLogicalChild(object? child)
    {
        WeakReference? wrToRemove = logicalChildren.FirstOrDefault(ch => ch.GetValueOrDefault<object>() == child);
        if (wrToRemove != null)
        {
            logicalChildren.Remove(wrToRemove);
        }

        RemoveNativeLogicalChild(child);
    }
    private void ClearLogicalChildrenList()
    {
        foreach (object? child in logicalChildren.Select(ch => ch.GetValueOrDefault<object>()).ToArray())
        {
            RemoveNativeLogicalChild(child);
        }

        logicalChildren.Clear();
    }
    private IReadOnlyList<object> GetOrderedLogicalChildren()
    {
        List<object> children = new(logicalChildren.Count);
        foreach (WeakReference weakReference in logicalChildren)
        {
            object? child = weakReference.GetValueOrDefault<object>();
            if (child != null)
            {
                children.Add(child);
            }
        }
        if (children.Count < 2)
        {
            return children;
        }
        // OrderByDescending is a stable sort, so children of equal depth keep their insertion order.
        return children.OrderByDescending(GetVisualTreeDepth).ToList();
    }
    private static int GetVisualTreeDepth(object element)
    {
        int depth = 0;
        DependencyObject? current = element as DependencyObject;
        while (current is UIElement)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current != null)
            {
                depth++;
            }
        }
        return depth;
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        areas = null;
        return base.ArrangeOverride(arrangeBounds);
    }

    private void AddNativeLogicalChild(object child)
    {
        NativeLogicalTree.Register(child, this);
        if (child is FrameworkElement element)
        {
            logicalPresentation.Add(element, new InheritedPresentation(this, element));
        }
    }

    private void RemoveNativeLogicalChild(object? child)
    {
        NativeLogicalTree.Unregister(child, this);
        if (child is FrameworkElement element && logicalPresentation.TryGetValue(element, out InheritedPresentation? presentation))
        {
            logicalPresentation.Remove(element);
            presentation.Dispose();
        }
    }
}
