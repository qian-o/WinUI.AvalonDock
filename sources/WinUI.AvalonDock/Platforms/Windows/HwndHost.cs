using System.Runtime.InteropServices;

namespace AvalonDock.Compatibility;

public abstract partial class HwndHost
{
    private HandleRef handle;

    public nint Handle => handle.Handle;
    protected abstract HandleRef BuildWindowCore(HandleRef hwndParent);
    protected abstract void DestroyWindowCore(HandleRef hwnd);

    internal HandleRef PreparedChild
    {
        get; set;
    }

    internal void BuildNativeHost(HandleRef parent) => handle = BuildWindowCore(parent);

    internal void DestroyNativeHost()
    {
        HandleRef previous = handle;
        handle = default;
        DestroyWindowCore(previous);
    }
}
