using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AvalonDock.Verification.Diagnostics
{
    // A standalone diagnostic, never compiled into the product or its consumers.
    // Uses the documented Process Snapshotting API; it never closes target handles.
    public static class NativeHandleSnapshot
    {
        public sealed class Entry
        {
            public string Handle { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
            public uint Flags { get; set; }
            public bool? ManualReset { get; set; }
            public bool? Signaled { get; set; }
        }

        // Native x64 PSS_HANDLE_ENTRY: the largest union member (Thread) is 48 bytes.
        [StructLayout(LayoutKind.Explicit, Size = 136)]
        private struct NativeEntry
        {
            [FieldOffset(0)] public IntPtr Handle;
            [FieldOffset(8)] public uint Flags;
            [FieldOffset(12)] public uint ObjectType;
            [FieldOffset(56)] public ushort TypeNameLength;
            [FieldOffset(64)] public IntPtr TypeName;
            [FieldOffset(72)] public ushort ObjectNameLength;
            [FieldOffset(80)] public IntPtr ObjectName;
            [FieldOffset(88)] public int EventManualReset;
            [FieldOffset(92)] public int EventSignaled;
        }

        public static Entry[] Capture(int processId)
        {
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException("Use x64 PowerShell for this diagnostic.");
            IntPtr process = OpenProcess(0x0450 /* QUERY_INFORMATION | VM_READ | DUP_HANDLE */, false, processId);
            if (process == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr snapshot = IntPtr.Zero;
            IntPtr marker = IntPtr.Zero;
            try
            {
                // HANDLES | NAME_INFORMATION | BASIC_INFORMATION | TYPE_SPECIFIC_INFORMATION.
                Check(PssCaptureSnapshot(process, 0x003C, 0, out snapshot));
                Check(PssWalkMarkerCreate(IntPtr.Zero, out marker));
                List<Entry> entries = new List<Entry>();
                while (true)
                {
                    NativeEntry native;
                    uint result = PssWalkSnapshot(snapshot, 2 /* PSS_WALK_HANDLES */, marker, out native, 136);
                    if (result == 259 /* ERROR_NO_MORE_ITEMS */) break;
                    Check(result);
                    string type = ReadName(native.TypeName, native.TypeNameLength);
                    entries.Add(new Entry
                    {
                        Handle = "0x" + native.Handle.ToInt64().ToString("X"),
                        Type = type,
                        Name = ReadName(native.ObjectName, native.ObjectNameLength),
                        Flags = native.Flags,
                        ManualReset = type == "Event" && (native.Flags & 8) != 0 ? (bool?)(native.EventManualReset != 0) : null,
                        Signaled = type == "Event" && (native.Flags & 8) != 0 ? (bool?)(native.EventSignaled != 0) : null
                    });
                }
                return entries.ToArray();
            }
            finally
            {
                // Snapshots captured by this call live in the calling process.
                try { if (marker != IntPtr.Zero) Check(PssWalkMarkerFree(marker)); }
                finally
                {
                    try { if (snapshot != IntPtr.Zero) Check(PssFreeSnapshot(GetCurrentProcess(), snapshot)); }
                    finally { CloseHandle(process); }
                }
            }
        }

        private static string ReadName(IntPtr pointer, ushort bytes)
        {
            return pointer == IntPtr.Zero || bytes == 0 ? string.Empty : Marshal.PtrToStringUni(pointer, bytes / 2).TrimEnd('\0');
        }
        private static void Check(uint error)
        {
            if (error != 0) throw new Win32Exception((int)error);
        }

        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern uint PssCaptureSnapshot(IntPtr process, uint flags, uint context, out IntPtr snapshot);
        [DllImport("kernel32.dll")] private static extern uint PssFreeSnapshot(IntPtr process, IntPtr snapshot);
        [DllImport("kernel32.dll")] private static extern uint PssWalkMarkerCreate(IntPtr allocator, out IntPtr marker);
        [DllImport("kernel32.dll")] private static extern uint PssWalkMarkerFree(IntPtr marker);
        [DllImport("kernel32.dll")] private static extern uint PssWalkSnapshot(IntPtr snapshot, uint information, IntPtr marker, out NativeEntry entry, uint size);
    }
}
