// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), internal ToggleDockDragOverlay.
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AvalonDock;

public partial class ToggleDockingManager
{
    private IDragInputSession? zoneInput;
    private ToggleDockDragOverlay? zoneOverlay;
    private LayoutAnchorable? zoneSource;
    private FrameworkElement? zoneOrigin;
    internal void BeginZoneDrag(LayoutAnchorable tool, FrameworkElement origin, bool waitForLeave = false,
        Point? pressPosition = null)
    {
        if (IsDisposed || zoneInput != null || !tool.CanMove || tool.Root?.Manager != this || IsDetached(tool) || !IsLoaded)
        {
            return;
        }

        zoneSource = tool;
        zoneOrigin = origin;
        zoneInput = PlatformServices.CreateDragInputService(origin).TryBegin(OnZoneInput, pressPosition);
        if (zoneInput == null)
        {
            zoneSource = null;
            return;
        }
        LayoutUpdated += OnZoneLayoutUpdated;
        try
        {
            if (!waitForLeave)
            {
                zoneOverlay = new ToggleDockDragOverlay(this, tool);
                zoneOverlay.Update(new Point(zoneInput.Position.X, zoneInput.Position.Y));
            }
        }
        catch { StopZoneDrag(); throw; }
    }
    private void OnZoneInput(DragInputUpdate update)
    {
        if (update.Kind == DragInputUpdateKind.Cancelled || !IsZoneSourceValid())
        {
            StopZoneDrag();
            return;
        }
        Point point = new(update.Position.X, update.Position.Y);
        if (zoneOverlay == null)
        {
            if (zoneOrigin == null || PlatformServices.Coordinates.TryGetScreenBounds(zoneOrigin, out Rect area) && area.Contains(point))
            {
                if (update.Kind == DragInputUpdateKind.Released)
                {
                    StopZoneDrag();
                }
                return;
            }

            zoneOverlay = new ToggleDockDragOverlay(this, zoneSource!);
        }
        zoneOverlay?.Update(point, refreshGeometry: update.Kind == DragInputUpdateKind.Released);
        if (update.Kind != DragInputUpdateKind.Released)
        {
            return;
        }

        LayoutAnchorable? tool = zoneSource;
        DockZone? zone = zoneOverlay?.Hit(point);
        StopZoneDrag();
        if (tool?.Root?.Manager == this && zone.HasValue)
        {
            MoveAnchorableToZone(tool, zone.Value);
        }
    }
    private bool IsZoneSourceValid() => !IsDisposed && IsLoaded && zoneSource != null
        && ReferenceEquals(zoneSource.Root, Layout) && !IsDetached(zoneSource) && !zoneSource.IsHidden;
    private void OnZoneLayoutUpdated(object? sender, object args)
    {
        if (zoneInput == null)
        {
            return;
        }

        if (!IsZoneSourceValid())
        {
            StopZoneDrag();
            return;
        }
        zoneOverlay?.Update(new Point(zoneInput.Position.X, zoneInput.Position.Y), refreshGeometry: true);
    }
    private void StopZoneDrag()
    {
        LayoutUpdated -= OnZoneLayoutUpdated;
        IDragInputSession? input = zoneInput;
        zoneInput = null;
        zoneSource = null;
        zoneOrigin = null;
        input?.Dispose();
        ToggleDockDragOverlay? overlay = zoneOverlay;
        zoneOverlay = null;
        overlay?.Dispose();
    }
}
