using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AvalonDock.Controls;

public abstract partial class LayoutItem
{
    private readonly Dictionary<DependencyProperty, object?> modelValues = [];
    private readonly Dictionary<DependencyProperty, Binding> styleBindings = [];
    private bool changingStyle;
    private Style? containerStyle;
    private Style? consumerStyle;
    private Style? managerStyle;

    // Native WinUI cannot apply Binding-valued setters as style values. Keep the
    // inherited CLR/DP entry points while the base element owns a literal-only style.
    public new Style? Style
    {
        get => containerStyle ?? base.Style;
        set => SetConsumerStyle(value);
    }
    public new object? GetValue(DependencyProperty dp) => dp == StyleProperty && containerStyle != null ? containerStyle : base.GetValue(dp);
    public new object? ReadLocalValue(DependencyProperty dp) => dp == StyleProperty && containerStyle != null ? containerStyle : base.ReadLocalValue(dp);
    public new void SetValue(DependencyProperty dp, object? value)
    {
        if (dp == StyleProperty)
        {
            SetConsumerStyle((Style?)value);
        }
        else
        {
            modelValues.Remove(dp);
            base.SetValue(dp, value);
        }
    }
    public new void ClearValue(DependencyProperty dp)
    {
        if (dp != StyleProperty)
        {
            modelValues.Remove(dp);
            base.ClearValue(dp);
            return;
        }

        SetConsumerStyle(null);
    }

    private void SetConsumerStyle(Style? style)
    {
        consumerStyle = style;
        ApplyStyleCore(managerStyle ?? consumerStyle);
    }

    internal void ApplyManagerStyle(Style? style)
    {
        if (ReferenceEquals(managerStyle, style))
        {
            return;
        }

        if (managerStyle == null && consumerStyle == null && base.Style != null)
        {
            consumerStyle = base.Style;
        }

        managerStyle = style;
        ApplyStyleCore(managerStyle ?? consumerStyle);
    }

    private void ApplyStyleCore(Style? style)
    {
        if (ReferenceEquals(containerStyle, style))
        {
            return;
        }

        changingStyle = true;
        try
        {
            Style? previousStyle = containerStyle;
            ReleaseStyleBindings();
            Setter[] previousSetters = Setters(previousStyle).GroupBy(setter => setter.Property).Select(group => group.Last()).ToArray();
            Setter[] setters = Setters(style).GroupBy(setter => setter.Property).Select(group => group.Last()).ToArray();
            containerStyle = style;
            foreach (Setter? setter in previousSetters)
            {
                if (modelValues.Remove(setter.Property, out object? value) && Equals(ReadLocalValue(setter.Property), value))
                {
                    ClearValue(setter.Property);
                }
            }

            foreach (Setter? setter in setters)
            {
                if (modelValues.Remove(setter.Property, out object? value) && Equals(ReadLocalValue(setter.Property), value))
                {
                    // Release a model seed so the style setter can take precedence.
                    ClearValue(setter.Property);
                }
            }

            if (style is { } appliedStyle && setters.Any(setter => SetterBinding(setter) != null))
            {
                // Native style application can unbox a Binding as the target value type
                // and crash. Keep bindings out of the native style value table entirely.
                Style nativeStyle = new(appliedStyle.TargetType);
                foreach (Setter? setter in setters.Where(setter => SetterBinding(setter) == null))
                {
                    nativeStyle.Setters.Add(new Setter(setter.Property, setter.Value));
                }

                base.Style = nativeStyle;
            }
            else
            {
                base.Style = style;
            }
            // Bind the original dependency properties rather than a parallel model.
            foreach (Setter? setter in setters)
            {
                if (SetterBinding(setter) is { } binding && ReadLocalValue(setter.Property) == DependencyProperty.UnsetValue)
                {
                    SetBinding(setter.Property, binding);
                    styleBindings[setter.Property] = binding;
                }
            }
        }
        finally { changingStyle = false; }
    }

    private static Binding? SetterBinding(Setter setter) => setter.Value as Binding
        ?? (setter.ReadLocalValue(Setter.ValueProperty) as BindingExpression)?.ParentBinding;

    internal void SetModelValue(DependencyProperty property, object? value)
    {
        if (changingStyle || GetBindingExpression(property) != null || Setters(containerStyle ?? Style).Any(setter => setter.Property == property))
        {
            return;
        }
        // An item setter first updates the model, which immediately notifies the item
        // with the same value. Do not misclassify that caller-local value as our seed.
        // A later independent model change still has to update the unstyled item.
        if (Equals(GetValue(property), value))
        {
            return;
        }

        base.SetValue(property, value);
        modelValues[property] = value;
    }
    private void ReleaseStyleBindings()
    {
        foreach (KeyValuePair<DependencyProperty, Binding> pair in styleBindings)
        {
            if (ReferenceEquals(GetBindingExpression(pair.Key)?.ParentBinding, pair.Value))
            {
                ClearValue(pair.Key);
            }
        }

        styleBindings.Clear();
    }
    private static IEnumerable<Setter> Setters(Style? style)
    {
        if (style == null)
        {
            yield break;
        }

        foreach (Setter setter in Setters(style.BasedOn))
        {
            yield return setter;
        }

        foreach (Setter setter in style.Setters.OfType<Setter>())
        {
            yield return setter;
        }
    }
}
