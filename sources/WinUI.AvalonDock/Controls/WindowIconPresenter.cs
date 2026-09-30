using System.Runtime.InteropServices.WindowsRuntime;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace AvalonDock.Controls;

/// <summary>Rasterizes the mapped WinUI ImageSource without exposing native icon handles.</summary>
internal sealed class WindowIconPresenter : IDisposable
{
    private readonly Grid root;
    private readonly Canvas canvas = new() { IsHitTestVisible = false };
    private readonly Image image = new() { Width = 32, Height = 32, Stretch = Stretch.Uniform };
    private readonly IWindowIconSurface surface;
    private bool rendering;
    private bool disposed;
    private long revision;
    internal WindowIconPresenter(Window window, Grid root)
    {
        this.root = root;
        surface = PlatformServices.CreateWindowIconSurface(window);
        Canvas.SetLeft(image, -10000);
        canvas.Children.Add(image);
        root.Children.Add(canvas);
        image.Loaded += OnImageReady;
        image.ImageOpened += OnImageReady;
    }
    internal ImageSource? Source
    {
        get => image.Source;
        set
        {
            if (ReferenceEquals(image.Source, value))
            {
                return;
            }

            image.Source = value;
            revision++;
            if (value == null)
            {
                surface.SetIcon(null);
            }
            else
            {
                QueueRender();
            }
        }
    }
    private void OnImageReady(object? sender, RoutedEventArgs args)
    {
        revision++;
        QueueRender();
    }
    private void QueueRender()
    {
        if (!disposed && !rendering && image.IsLoaded && image.Source != null)
        {
            image.DispatcherQueue.TryEnqueue(Render);
        }
    }
    private async void Render()
    {
        if (disposed || rendering || !image.IsLoaded || image.Source == null)
        {
            return;
        }

        rendering = true;
        long current = revision;
        try
        {
            RenderTargetBitmap bitmap = new();
            await bitmap.RenderAsync(image, 32, 32);
            IBuffer pixels = await bitmap.GetPixelsAsync();
            if (!disposed && current == revision && image.Source != null)
            {
                surface.SetIcon(pixels.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight);
            }
        }
        catch (Exception)
        {
            // Keep the current native icon when its bitmap cannot be rendered.
            return;
        }
        finally
        {
            rendering = false;
            if (!disposed && current != revision)
            {
                QueueRender();
            }
        }
    }
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        revision++;
        image.Loaded -= OnImageReady;
        image.ImageOpened -= OnImageReady;
        root.Children.Remove(canvas);
        image.Source = null;
        surface.Dispose();
    }
}
