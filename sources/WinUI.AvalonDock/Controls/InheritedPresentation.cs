using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Controls;

/// <summary>Mirrors supported inherited values across logical ownership and native XAML roots.</summary>
internal sealed class InheritedPresentation : IDisposable
{
    internal static readonly DependencyProperty[] Properties =
    [
        FrameworkElement.DataContextProperty, FrameworkElement.FlowDirectionProperty, FrameworkElement.LanguageProperty,
        Control.FontFamilyProperty, Control.FontSizeProperty, Control.FontWeightProperty, Control.FontStyleProperty,
        Control.FontStretchProperty, Control.CharacterSpacingProperty, Control.ForegroundProperty
    ];
    private readonly WeakReference<DockingManager> owner;
    private readonly WeakReference<FrameworkElement> element;
    private readonly bool includeContext;
    private readonly Dictionary<DependencyProperty, object?> mirrored = [];
    private readonly List<(DependencyProperty Property, long Token)> tokens = [];
    private bool applying;
    private bool disposed;

    internal InheritedPresentation(DockingManager manager, FrameworkElement target, bool includeContext = true)
    {
        owner = new(manager);
        element = new(target);
        this.includeContext = includeContext;
        foreach (DependencyProperty property in Properties)
        {
            if (!includeContext && property == FrameworkElement.DataContextProperty)
            {
                continue;
            }

            DependencyProperty? mapped = TargetProperty(target, property);
            if (mapped == null)
            {
                continue;
            }

            tokens.Add((mapped, target.RegisterPropertyChangedCallback(mapped, (_, changed) =>
            {
                if (applying || disposed)
                {
                    return;
                }

                mirrored.Remove(changed);
                if (element.TryGetTarget(out FrameworkElement? current) && current.ReadLocalValue(changed) == DependencyProperty.UnsetValue)
                {
                    Refresh();
                }
            })));
        }
        tokens.Add((FrameworkElement.StyleProperty, target.RegisterPropertyChangedCallback(FrameworkElement.StyleProperty, (_, _) => Refresh())));
        Refresh();
    }

    internal void Refresh()
    {
        if (disposed || applying || !owner.TryGetTarget(out DockingManager? manager) || !element.TryGetTarget(out FrameworkElement? target))
        {
            return;
        }

        applying = true;
        try
        {
            foreach (DependencyProperty property in Properties)
            {
                if (!includeContext && property == FrameworkElement.DataContextProperty)
                {
                    continue;
                }

                DependencyProperty? mapped = TargetProperty(target, property);
                if (mapped == null)
                {
                    continue;
                }

                object local = target.ReadLocalValue(mapped);
                bool owned = mirrored.TryGetValue(mapped, out object? old) && Equals(local, old);
                if (!owned && local != DependencyProperty.UnsetValue)
                {
                    continue;
                }

                object value = manager.GetValue(property);
                bool isDefault = manager.ReadLocalValue(property) == DependencyProperty.UnsetValue && !Styled(manager.Style, property)
                    && Equals(value, property.GetMetadata(manager.GetType()).DefaultValue);
                if (Styled(target.Style, mapped) || isDefault)
                {
                    if (owned)
                    {
                        target.ClearValue(mapped);
                    }

                    mirrored.Remove(mapped);
                    continue;
                }
                if (!Equals(target.GetValue(mapped), value) || !owned)
                {
                    target.SetValue(mapped, value);
                    mirrored[mapped] = value;
                }
            }
        }
        finally { applying = false; }
    }

    internal static bool Styled(Style? style, DependencyProperty property)
    {
        for (; style != null; style = style.BasedOn)
        {
            if (style.Setters.OfType<Setter>().Any(setter => setter.Property == property))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyProperty? TargetProperty(FrameworkElement target, DependencyProperty property)
    {
        if (property == FrameworkElement.DataContextProperty || property == FrameworkElement.FlowDirectionProperty || property == FrameworkElement.LanguageProperty)
        {
            return property;
        }

        if (target is Control)
        {
            return property;
        }

        if (target is ContentPresenter)
        {
            if (property == Control.FontSizeProperty)
            {
                return ContentPresenter.FontSizeProperty;
            }

            if (property == Control.FontFamilyProperty)
            {
                return ContentPresenter.FontFamilyProperty;
            }

            if (property == Control.FontWeightProperty)
            {
                return ContentPresenter.FontWeightProperty;
            }

            if (property == Control.FontStyleProperty)
            {
                return ContentPresenter.FontStyleProperty;
            }

            if (property == Control.FontStretchProperty)
            {
                return ContentPresenter.FontStretchProperty;
            }

            if (property == Control.CharacterSpacingProperty)
            {
                return ContentPresenter.CharacterSpacingProperty;
            }

            if (property == Control.ForegroundProperty)
            {
                return ContentPresenter.ForegroundProperty;
            }
        }
        if (target is TextBlock)
        {
            if (property == Control.FontSizeProperty)
            {
                return TextBlock.FontSizeProperty;
            }

            if (property == Control.FontFamilyProperty)
            {
                return TextBlock.FontFamilyProperty;
            }

            if (property == Control.FontWeightProperty)
            {
                return TextBlock.FontWeightProperty;
            }

            if (property == Control.FontStyleProperty)
            {
                return TextBlock.FontStyleProperty;
            }

            if (property == Control.FontStretchProperty)
            {
                return TextBlock.FontStretchProperty;
            }

            if (property == Control.CharacterSpacingProperty)
            {
                return TextBlock.CharacterSpacingProperty;
            }

            if (property == Control.ForegroundProperty)
            {
                return TextBlock.ForegroundProperty;
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (!element.TryGetTarget(out FrameworkElement? target))
        {
            return;
        }

        foreach ((DependencyProperty? property, long token) in tokens)
        {
            target.UnregisterPropertyChangedCallback(property, token);
        }

        tokens.Clear();
        foreach (KeyValuePair<DependencyProperty, object?> pair in mirrored)
        {
            if (Equals(target.ReadLocalValue(pair.Key), pair.Value))
            {
                target.ClearValue(pair.Key);
            }
        }

        mirrored.Clear();
    }
}
