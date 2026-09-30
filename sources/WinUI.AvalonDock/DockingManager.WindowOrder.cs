// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using AvalonDock.Controls;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;

namespace AvalonDock;

public partial class DockingManager
{
    internal void HideAllOverlayWindows()
    {
        ((IOverlayWindowHost)this).HideOverlayWindow();
        foreach (IOverlayWindowHost? host in floatingControls.OfType<IOverlayWindowHost>().ToArray())
        {
            host.HideOverlayWindow();
        }
    }
    internal void GetOverlayWindowHostsByZOrder(ref List<IOverlayWindowHost> overlayWindowHosts, LayoutFloatingWindowControl dragFloatingWindow)
    {
        overlayWindowHosts.Clear();
        List<IOverlayWindowHost> topFloatingWindows = new();
        List<IOverlayWindowHost> bottomFloatingWindows = new();

        foreach (WindowOrderEntry entry in PlatformServices.WindowOrder.GetWindowsByZOrder(this, floatingControls.Cast<Window>().ToArray()))
        {
            LayoutFloatingWindowControl fw = (LayoutFloatingWindowControl)entry.Window;
            if (fw is IOverlayWindowHost host && fw != dragFloatingWindow && fw.Visible)
            {
                if (fw.Model.Root != null && fw.Model.Root.Manager == this)
                {
                    if (fw.OwnedByDockingManagerWindow || entry.AboveOwner)
                    {
                        topFloatingWindows.Add(host);
                    }
                    else
                    {
                        bottomFloatingWindows.Add(host);
                    }
                }
            }
        }

        overlayWindowHosts.AddRange(topFloatingWindows);
        overlayWindowHosts.Add(this);
        overlayWindowHosts.AddRange(bottomFloatingWindows);
    }
}
