// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), DockingManager window enumeration.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace AvalonDock.Platforms.Windows;

internal sealed class WindowsWindowOrderService : IWindowOrderService
{
    public void BringToFront(Window window) => BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(window));
    public void BringToFront(FrameworkElement element)
    {
        if (element.XamlRoot != null)
        {
            BringToFront(GetAncestor(Win32Interop.GetWindowFromWindowId(element.XamlRoot.ContentIslandEnvironment.AppWindowId), 2));
        }
    }
    private static void BringToFront(nint handle)
    {
        if (handle != 0)
        {
            SetWindowPos(handle, 0, 0, 0, 0, 0, 0x13 /* NOMOVE | NOSIZE | NOACTIVATE */);
        }
    }
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    public IReadOnlyList<WindowOrderEntry> GetWindowsByZOrder(FrameworkElement owner, IReadOnlyList<Window> windows)
    {
        if (owner.XamlRoot == null)
        {
            return [];
        }

        nint parent = Win32Interop.GetWindowFromWindowId(owner.XamlRoot.ContentIslandEnvironment.AppWindowId);
        parent = GetAncestor(parent, 2 /* GA_ROOT */);
        (Window Window, nint Handle)[] candidates = windows.Select(window => (Window: window, Handle: WinRT.Interop.WindowNative.GetWindowHandle(window))).ToArray();
        List<WindowOrderEntry> result = new();
        bool aboveOwner = true;
        // The original GetWindowZOrder counts from the bottom; walking front-to-back
        // preserves the exact fw_z > mainWindow_z relation without exporting HWNDs.
        for (nint currentHandle = GetWindow(parent, 0 /* GW_HWNDFIRST */); currentHandle != 0; currentHandle = GetWindow(currentHandle, 2 /* GW_HWNDNEXT */))
        {
            if (currentHandle == parent)
            {
                aboveOwner = false;
            }

            foreach ((Window Window, nint Handle) candidate in candidates)
            {
                if (candidate.Handle == currentHandle)
                {
                    result.Add(new WindowOrderEntry(candidate.Window, aboveOwner));
                    break;
                }
            }
        }
        return result;
    }
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
}
