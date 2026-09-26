using Microsoft.UI.Xaml;

namespace AvalonDock.Compatibility;

/// <summary>Authorizes writes to a read-only property exposed by an AvalonDock WinUI window.</summary>
public sealed class DependencyPropertyKey
{
    private readonly Type propertyType;

    internal DependencyPropertyKey(DependencyProperty dependencyProperty, Type ownerType, Type propertyType)
    {
        DependencyProperty = dependencyProperty;
        this.propertyType = propertyType;
        DefaultValue = dependencyProperty.GetMetadata(ownerType).DefaultValue;
    }

    /// <summary>Gets the WinUI dependency property that can be read without the key.</summary>
    public DependencyProperty DependencyProperty
    {
        get;
    }
    internal object? DefaultValue
    {
        get;
    }
    internal void Validate(object? value)
    {
        if (ReferenceEquals(value, DependencyProperty.UnsetValue) || value == null && propertyType.IsValueType
            || value != null && !propertyType.IsInstanceOfType(value))
        {
            throw new ArgumentException("The value does not match the dependency property's type.", nameof(value));
        }
    }
}
