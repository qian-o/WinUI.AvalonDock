using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using NativeMetadata = Microsoft.UI.Xaml.PropertyMetadata;

namespace AvalonDock.Compatibility;

/// <summary>Restricts internal Writes while retaining native WinUI DP identity for read-only control state.</summary>
internal static class ReadOnlyPropertyGuard
{
    private static readonly ConditionalWeakTable<DependencyObject, HashSet<DependencyProperty>> Writes = new();
    private static readonly ConditionalWeakTable<DependencyObject, HashSet<DependencyProperty>> Restoring = new();

    internal static DependencyProperty Register(string name, Type valueType, Type ownerType, object? defaultValue,
        PropertyChangedCallback? changed = null) => DependencyProperty.Register(name, valueType, ownerType, new NativeMetadata(defaultValue, (owner, args) =>
        {
            if (Writes.TryGetValue(owner, out HashSet<DependencyProperty>? allowed) && allowed.Contains(args.Property))
            {
                if (!IsRestoring(owner, args.Property))
                {
                    changed?.Invoke(owner, args);
                }

                return;
            }
            // Native WinUI has no RegisterReadOnly. Restore the old value before reporting
            // an attempted write so native base casts cannot change the protected state.
            Set(owner, args.Property, args.OldValue, restore: true);
            throw new InvalidOperationException("The dependency property is read-only.");
        }));

    private static bool IsRestoring(DependencyObject owner, DependencyProperty property) => Restoring.TryGetValue(owner, out HashSet<DependencyProperty>? active) && active.Contains(property);
    internal static void VerifyWritable(DependencyProperty property, params DependencyProperty[] readOnly)
    {
        if (readOnly.Contains(property))
        {
            throw new InvalidOperationException("The dependency property is read-only.");
        }
    }
    internal static void Set(DependencyObject owner, DependencyProperty property, object? value, bool restore = false)
    {
        HashSet<DependencyProperty> allowed = Writes.GetOrCreateValue(owner);
        bool added = allowed.Add(property);
        if (restore)
        {
            Restoring.GetOrCreateValue(owner).Add(property);
        }

        try
        {
            owner.SetValue(property, value);
        }
        finally
        {
            if (restore)
            {
                Restoring.GetOrCreateValue(owner).Remove(property);
            }

            if (added)
            {
                allowed.Remove(property);
            }
        }
    }
}
