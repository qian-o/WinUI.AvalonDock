using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms;

/// <summary>Semantic owner contract used by platform child-window hosts.</summary>
internal interface IChildWindowHostOwner
{
    FrameworkElement Element
    {
        get;
    }
    UIElement? RootVisual
    {
        get; set;
    }
    void BuildHostContent();
    void ReleaseHostContent();
    void NotifyDisconnected();
}
