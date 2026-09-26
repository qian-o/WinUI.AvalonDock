using Windows.Foundation;

namespace AvalonDock.Platforms;

/// <summary>Shows a modal UI window while preserving the calling thread's enabled window state.</summary>
internal interface IModalWindowHost : IDisposable
{
    void Run(Size logicalSize);
}
