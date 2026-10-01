// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL).
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / Controls/WeakDictionary.cs
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the weak dictionary.
/// </summary>
/// <typeparam name="K">The type of k.</typeparam>
/// <typeparam name="V">The type of v.</typeparam>
internal class WeakDictionary<K, V>
    where K : class
    where V : class
{
    private List<WeakReference> keys = new();
    private List<V> values = new();

    /// <summary>
    /// Gets or sets the value associated with the specified index.
    /// </summary>
    /// <param name="key">The key.</param>
    public V this[K key]
    {
        get
        {
            if (!GetValue(key, out V? valueToReturn))
            {
                throw new ArgumentException();
            }

            return valueToReturn;
        }
        set
        {
            SetValue(key, value);
        }
    }

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void SetValue(K key, V value)
    {
        CollectGarbage();
        int vIndex = keys.FindIndex(k => k.GetValueOrDefault<K>() == key);
        if (vIndex > -1)
        {
            values[vIndex] = value;
        }
        else
        {
            values.Add(value);
            keys.Add(new WeakReference(key));
        }
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>true if the operation for get value succeeds; otherwise, false.</returns>
    public bool GetValue(K key, [NotNullWhen(true)] out V? value)
    {
        CollectGarbage();
        int vIndex = keys.FindIndex(k => k.GetValueOrDefault<K>() == key);
        value = null;
        if (vIndex == -1)
        {
            return false;
        }

        value = values[vIndex];
        return true;
    }

    public bool Remove(K key)
    {
        CollectGarbage();
        int index = keys.FindIndex(item => item.GetValueOrDefault<K>() == key);
        if (index < 0)
        {
            return false;
        }

        keys.RemoveAt(index);
        values.RemoveAt(index);
        return true;
    }

    /// <summary>Removes entries whose key has been garbage collected.</summary>
    private void CollectGarbage()
    {
        int vIndex = 0;

        do
        {
            vIndex = keys.FindIndex(vIndex, k => !k.IsAlive);
            if (vIndex >= 0)
            {
                keys.RemoveAt(vIndex);
                values.RemoveAt(vIndex);
            }
        }
        while (vIndex >= 0);
    }
}
