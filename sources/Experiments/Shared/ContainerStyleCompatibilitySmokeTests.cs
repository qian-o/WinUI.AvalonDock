using System.Collections.ObjectModel;
using System.ComponentModel;
using AvalonDock.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace WinUI.AvalonDock.Experiments.Shared;

/// <summary>Checks the WPF generator's local-style precedence on native menu containers.</summary>
internal static class ContainerStyleCompatibilitySmokeTests
{
    internal static async Task<IReadOnlyList<string>> RunAsync()
    {
        List<string> checks = [];
        Style first = FontStyle(21);
        Style second = FontStyle(23);
        Style consumer = FontStyle(29);
        MenuItemEx direct = new() { Header = "Unstyled direct item" };
        MenuItemEx local = new() { Header = "Consumer styled item", Style = consumer };
        MenuItemEx explicitNull = new() { Header = "Consumer explicitly empty style" };
        explicitNull.SetValue(FrameworkElement.StyleProperty, null);
        object generatedModel = new();
        ObservableCollection<object> models = [direct, local, explicitNull, generatedModel];
        ContextMenuEx menu = new() { ItemsSource = models };
        MenuItemEx generated = (MenuItemEx)menu.Items[3];
        Check(direct.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue,
            "empty container style does not set a local style on a supplied item");
        Check(generated.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue,
            "empty container style does not set a local style on a generated item");

        menu.ItemContainerStyle = first;
        Check(ReferenceEquals(direct.Style, first) && direct.FontSize == 21,
            "unstyled supplied item receives the container style");
        Check(ReferenceEquals(generated.Style, first) && generated.FontSize == 21,
            "generated item receives the container style");
        Check(ReferenceEquals(local.Style, consumer) && local.FontSize == 29,
            "consumer local style takes precedence over the container style");
        Check(explicitNull.ReadLocalValue(FrameworkElement.StyleProperty) is null,
            "consumer explicit null style takes precedence over the container style");

        menu.ItemContainerStyle = second;
        Check(ReferenceEquals(direct.Style, second) && ReferenceEquals(generated.Style, second),
            "changing container style updates only generator-owned styles");
        Check(ReferenceEquals(local.Style, consumer), "changing container style preserves the consumer style");

        generated.Style = consumer;
        menu.ItemContainerStyle = null;
        Check(direct.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue,
            "clearing container style clears only its generated local value");
        Check(ReferenceEquals(generated.Style, consumer),
            "clearing container style preserves a consumer replacement");

        menu.ItemContainerStyle = first;
        models.Remove(direct);
        Check(direct.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue,
            "removing a supplied container releases its generator-owned style");
        models.Remove(generatedModel);
        Check(ReferenceEquals(generated.Style, consumer),
            "removing a generated container preserves its consumer replacement");

        MenuItemEx bound = new() { Header = "Bound style item" };
        StyleSource source = new() { Value = first };
        Binding binding = new() { Source = source, Path = new PropertyPath(nameof(StyleSource.Value)) };
        bound.SetBinding(FrameworkElement.StyleProperty, binding);
        models.Add(bound);
        menu.ItemContainerStyle = second;
        await Task.Delay(50);
        Check(ReferenceEquals(bound.GetBindingExpression(FrameworkElement.StyleProperty)?.ParentBinding, binding),
            "container style preserves the consumer style binding");
        source.Value = consumer;
        await Task.Delay(50);
        Check(ReferenceEquals(bound.Style, consumer) && bound.FontSize == 29,
            "preserved style binding remains live after container style changes");
        menu.ItemsSource = null;
        Check(ReferenceEquals(bound.GetBindingExpression(FrameworkElement.StyleProperty)?.ParentBinding, binding),
            "removing a supplied container preserves its style binding");
        return checks;

        void Check(bool condition, string description)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Menu container upstream behavior: {description}");
            }

            checks.Add($"PASS: {description}");
        }
    }

    private static Style FontStyle(double fontSize)
    {
        Style style = new(typeof(MenuItemEx));
        style.Setters.Add(new Setter(Control.FontSizeProperty, fontSize));
        return style;
    }

    private sealed class StyleSource : INotifyPropertyChanged
    {
        private Style? value;
        public Style? Value
        {
            get => value;
            set
            {
                this.value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
