using System.Collections;
using System.Reflection;
using AvalonDock;
using AvalonDock.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUI.AvalonDock.Experiments.Shared;
using Windows.Foundation;

namespace WinUI.AvalonDock.Experiments.Docking;

internal sealed class DockingDragChecks : IDisposable
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type SessionType = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls.DragService")
        ?? throw new InvalidOperationException("The docking drag session type was not found.");
    private static readonly Type HostType = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls.IOverlayWindowHost")
        ?? throw new InvalidOperationException("The docking overlay host interface was not found.");
    private static readonly Type OverlayType = typeof(DockingManager).Assembly.GetType("AvalonDock.Controls.IOverlayWindow")
        ?? throw new InvalidOperationException("The docking overlay interface was not found.");
    private readonly LayoutFloatingWindowControl source;
    private readonly object session;

    internal DockingDragChecks(LayoutFloatingWindowControl source)
    {
        this.source = source;
        session = Activator.CreateInstance(SessionType, Members, null, [source], null)
            ?? throw new InvalidOperationException("Could not create a docking drag session.");
    }

    internal OverlayWindow Overlay => Value(session, "currentWindow") as OverlayWindow
        ?? throw new InvalidOperationException("The drag session has not entered a visible overlay host.");
    internal object? ActiveTarget => Value(session, "currentDropTarget");
    internal int Frames => (int)(Value(Overlay, "PresentedFrames") ?? 0);
    internal bool FrameIsCurrent => Equals(Value(Overlay, "revision"), Value(Overlay, "renderedRevision"));

    internal void Update(Point screenPoint) => Method(SessionType, "UpdateMouseLocation").Invoke(session, [screenPoint]);

    internal bool Drop(Point screenPoint)
    {
        object?[] arguments = [screenPoint, false];
        Method(SessionType, "Drop").Invoke(session, arguments);
        return arguments[1] is true;
    }

    internal DropArea<LayoutDocumentPaneControl>[] HostAreas(DockingManager manager)
    {
        SampleChecks.Require(ReferenceEquals(Value(session, "currentHost"), manager), "The active drag session uses the main docking manager.");
        IEnumerable areas = (IEnumerable)(Method(HostType, "GetDropAreas").Invoke(manager, [source])
            ?? throw new InvalidOperationException("The host did not provide any docking areas."));
        return areas.OfType<DropArea<LayoutDocumentPaneControl>>().ToArray();
    }

    internal bool ContainsArea(object area) => Value(session, "currentWindowAreas") is IEnumerable areas
        && areas.Cast<object>().Any(item => ReferenceEquals(item, area));

    internal object[] Targets() => ((IEnumerable)(Method(OverlayType, "GetTargets").Invoke(Overlay, null)
        ?? throw new InvalidOperationException("The overlay did not create docking targets."))).Cast<object>().ToArray();

    internal static DropTargetType TargetType(object target) => (DropTargetType)(Value(target, "Type")
        ?? throw new InvalidOperationException("The docking target has no type."));
    internal static int TabIndex(object target) => (int)(Value(target, "TabIndex") ?? -1);
    internal static FrameworkElement TargetArea(object target) => (FrameworkElement)(Value(Value(target, "Target")
        ?? throw new InvalidOperationException("The docking target is not initialized."), "Area")
        ?? throw new InvalidOperationException("The docking target has no native pane."));
    internal static Rect TargetBounds(object target) => (Rect)(Value(Value(target, "Target")
        ?? throw new InvalidOperationException("The docking target is not initialized."), "ScreenBounds")
        ?? throw new InvalidOperationException("The docking target has no screen bounds."));

    internal Rect GlyphBounds(string name)
    {
        Canvas canvas = Value(Overlay, "TargetCanvas") as Canvas
            ?? throw new InvalidOperationException("The overlay target canvas was not loaded.");
        FrameworkElement glyph = canvas.FindVisualChildren<FrameworkElement>().Single(element => element.Name == name);
        SampleChecks.Require(glyph.Visibility == Visibility.Visible && glyph.ActualWidth > 0 && glyph.ActualHeight > 0,
            "The central docking indicator is loaded and visible.");
        Rect local = glyph.TransformToVisual(canvas).TransformBounds(new Rect(0, 0, glyph.ActualWidth, glyph.ActualHeight));
        return OverlayToScreen(local);
    }

    internal Rect PreviewBounds()
    {
        Microsoft.UI.Xaml.Shapes.Path preview = Value(Overlay, "preview") as Microsoft.UI.Xaml.Shapes.Path
            ?? throw new InvalidOperationException("The overlay docking preview was not loaded.");
        SampleChecks.Require(preview.Visibility == Visibility.Visible && preview.Data is RectangleGeometry,
            "The central target shows a rectangular docking preview.");
        return OverlayToScreen(((RectangleGeometry)preview.Data!).Rect);
    }

    internal async Task WaitForFrameAsync(int previousFrames, string message)
    {
        await WaitUntilAsync(() =>
        {
            if (Value(Overlay, "RenderFailure") is Exception failure)
            {
                throw new InvalidOperationException("The docking overlay failed to render.", failure);
            }
            return Frames > previousFrames && FrameIsCurrent;
        }, message);
    }

    internal static async Task WaitUntilAsync(Func<bool> ready, string message)
    {
        for (int attempt = 0; attempt < 150; attempt++)
        {
            if (ready())
            {
                return;
            }
            await Task.Delay(30);
        }
        throw new InvalidOperationException(message);
    }

    internal static Rect ScreenBounds(FrameworkElement element)
    {
        SampleChecks.Require(element.IsLoaded && element.XamlRoot is not null, "The check pane is attached to a real window.");
        Rect local = element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        global::Windows.Graphics.RectInt32 screen = element.XamlRoot!.CoordinateConverter.ConvertLocalToScreen(local);
        return new Rect(screen.X, screen.Y, screen.Width, screen.Height);
    }

    internal static Point Center(Rect bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    internal static bool Near(Rect actual, Rect expected) => Math.Abs(actual.Left - expected.Left) <= 2
        && Math.Abs(actual.Top - expected.Top) <= 2 && Math.Abs(actual.Right - expected.Right) <= 2
        && Math.Abs(actual.Bottom - expected.Bottom) <= 2;
    internal static bool Centered(Rect glyph, Rect area) => Math.Abs(Center(glyph).X - Center(area).X) <= 2
        && Math.Abs(Center(glyph).Y - Center(area).Y) <= 2;

    private Rect OverlayToScreen(Rect local)
    {
        Rect bounds = (Rect)(Value(Overlay, "bounds") ?? throw new InvalidOperationException("The overlay has no screen bounds."));
        FrameworkElement destination = (FrameworkElement)(Value(Overlay, "destination")
            ?? throw new InvalidOperationException("The overlay has no destination window."));
        double scale = destination.XamlRoot.RasterizationScale;
        return new Rect(bounds.X + local.X * scale, bounds.Y + local.Y * scale, local.Width * scale, local.Height * scale);
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Members)
        ?? throw new InvalidOperationException($"The check entry point {type.Name}.{name} was not found.");

    private static object? Value(object owner, string name)
    {
        for (Type? type = owner.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(name, Members | BindingFlags.DeclaredOnly) is PropertyInfo property)
            {
                return property.GetValue(owner);
            }
            if (type.GetField(name, Members | BindingFlags.DeclaredOnly) is FieldInfo field)
            {
                return field.GetValue(owner);
            }
        }
        throw new InvalidOperationException($"The check observation point {owner.GetType().Name}.{name} was not found.");
    }

    public void Dispose() => Method(SessionType, "Abort").Invoke(session, null);
}
