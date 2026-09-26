using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms;

/// <summary>Owns a hosted XAML root and its connection to a measured parent control.</summary>
internal interface IChildWindowHost : IDisposable
{
    UIElement? RootVisual
    {
        get; set;
    }
    void Connect();
    void UpdateBounds();
    void Disconnect();
}
