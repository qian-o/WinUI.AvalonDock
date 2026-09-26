namespace AvalonDock.Platforms;

/// <summary>Owns a native window icon built from premultiplied BGRA pixels.</summary>
internal interface IWindowIconSurface : IDisposable
{
    void SetIcon(byte[]? pixels, int width = 0, int height = 0);
}
