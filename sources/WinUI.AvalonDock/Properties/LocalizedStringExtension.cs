using Microsoft.UI.Xaml.Markup;

namespace AvalonDock.Properties;

/// <summary>Resolves the original resx strings where WinUI has no WPF x:Static extension.</summary>
internal sealed class LocalizedStringExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue() => Resources.ResourceManager.GetString(Key, Resources.Culture)
        ?? throw new InvalidOperationException($"Missing AvalonDock resource: {Key}");
}
