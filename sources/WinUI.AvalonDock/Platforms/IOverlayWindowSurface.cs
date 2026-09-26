using Windows.Foundation;

namespace AvalonDock.Platforms;

/// <summary>A nonactivating, input-transparent window surface. Bounds use physical desktop pixels on Windows.</summary>
internal interface IOverlayWindowSurface : IDisposable
{
    bool IsVisible
    {
        get;
    }
    bool IsClosed
    {
        get;
    }
    void Show(Rect screenBounds);
    void Present(byte[] premultipliedBgra, int pixelWidth, int pixelHeight);
    void Hide();
}
