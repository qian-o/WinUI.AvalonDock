// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/Extensions.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock.Controls;

/// <summary>
/// Provides helper members for extensions.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Executes the find Visual Children operation.
    /// </summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="depObj">The dep Obj.</param>
    /// <returns>The result of the operation.</returns>
    public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject? depObj)
        where T : DependencyObject
    {
        if (depObj == null)
        {
            yield break;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
            {
                yield return t;
            }

            foreach (T childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }



    /// <summary>
    /// Executes the find Visual Tree Root operation.
    /// </summary>
    /// <param name="initial">The initial.</param>
    /// <returns>The result of the operation.</returns>
    public static DependencyObject FindVisualTreeRoot(this DependencyObject initial)
    {
        DependencyObject? current = initial;
        DependencyObject result = initial;
        while (current != null)
        {
            result = current;
            if (current is UIElement)
            {
                current = VisualTreeHelper.GetParent(current);
            }
            else
            {
                // If we're in Logical Land then we must walk
                // up the logical tree until we find a
                // Visual/Visual3D to get us back to Visual Land.
                current = NativeLogicalTree.GetParent(current);
            }
        }

        return result;
    }

    /// <summary>
    /// Executes the find Visual Ancestor operation.
    /// </summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="dependencyObject">The dependency Object.</param>
    /// <returns>The result of the operation.</returns>
    public static T? FindVisualAncestor<T>(this DependencyObject dependencyObject)
        where T : class
    {
        DependencyObject? target = dependencyObject;
        do
        {
            target = VisualTreeHelper.GetParent(target);
        }
        while (target != null && !(target is T));
        return target as T;
    }



    /// <summary>Enumerates original logical descendants through native content ownership.</summary>
    public static IEnumerable<T> FindLogicalChildren<T>(this DependencyObject? depObj)
        where T : DependencyObject
    {
        if (depObj == null)
        {
            yield break;
        }

        foreach (DependencyObject child in NativeLogicalTree.GetChildren(depObj).OfType<DependencyObject>())
        {
            if (child is T t)
            {
                yield return t;
            }

            foreach (T childOfChild in FindLogicalChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    /// <summary>Finds an ancestor through original logical ownership before the visual-parent fallback.</summary>
    public static T? FindLogicalAncestor<T>(this DependencyObject dependencyObject)
        where T : class
    {
        DependencyObject? target = dependencyObject;
        do
        {
            DependencyObject current = target;
            target = NativeLogicalTree.GetParent(target) ?? VisualTreeHelper.GetParent(current);
        }
        while (target != null && !(target is T));
        return target as T;
    }
}
