using AvalonDock.Compatibility;
using Microsoft.UI.Xaml;

namespace AvalonDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private readonly Dictionary<DependencyPropertyKey, object?> keyBaseValues = [];
    private bool initializingKeyMetadata;

    private void InitializeKeyMetadata()
    {
        initializingKeyMetadata = true;
        try
        {
            foreach (DependencyPropertyKey? key in new[] { IsDraggingPropertyKey, TotalMarginPropertyKey, ContentMinWidthPropertyKey, ContentMinHeightPropertyKey })
            {
                state.SetValue(key.DependencyProperty, key.DefaultValue);
            }
        }
        finally { initializingKeyMetadata = false; }
    }

    private static DependencyPropertyKey? FindKey(DependencyProperty property)
    {
        if (property == IsDraggingProperty)
        {
            return IsDraggingPropertyKey;
        }

        if (property == TotalMarginProperty)
        {
            return TotalMarginPropertyKey;
        }

        if (property == ContentMinWidthProperty)
        {
            return ContentMinWidthPropertyKey;
        }

        if (property == ContentMinHeightProperty)
        {
            return ContentMinHeightPropertyKey;
        }

        return null;
    }

    private void SetKeyValue(DependencyPropertyKey key, object? value, bool local)
    {
        if (!ReferenceEquals(FindKey(key.DependencyProperty), key))
        {
            throw new ArgumentException("The key does not belong to this window.", nameof(key));
        }

        key.Validate(value);
        if (local)
        {
            keyBaseValues[key] = value;
        }
        else
        {
            keyBaseValues.Remove(key);
        }

        state.SetValue(key.DependencyProperty, value);
    }
}
