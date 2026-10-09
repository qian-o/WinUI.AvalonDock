# Resource investigation

These opt-in tools inspect processes launched from the verification hosts. They
are not compiled into or packaged with WinUI.AvalonDock. Build the selected host
first, then run from the repository root on Windows x64:

```powershell
.\verification\diagnostics\Run-ResourceInvestigation.ps1 -Scenarios Window,XamlSource,Menu,Composite,Docking -Configuration Debug -Cycles 312 -Batch 100 -IdleSeconds 30 -ResultsDirectory .\artifacts\resource-investigation-rerun
```

The scenarios run serially because their tests use real windows and focus. Window,
XamlSource, Menu and Composite use the PackageConsumer executable; their loops use
the Windows App SDK directly. Docking uses the sample's boundary checks. Composite
includes a Window, an extra DesktopWindowXamlSource, TextBox reparenting and focus,
caption/input regions, and a MenuFlyout per cycle. It is an approximate component
comparison, not a byte-for-byte reproduction of docking.

Each run writes an immediate log, a process resource checkpoint CSV, external
handle CSVs and an index correlating each snapshot with the last observed
checkpoint. The application continues running during capture: a snapshot can
observe a later state, miss a short checkpoint, or include subsequent boundary
checks. Compare timestamps and stages before interpreting counts.

`NativeHandleSnapshot.cs` uses Microsoft's documented Process Snapshotting APIs:
`OpenProcess` for query/read/duplicate access, `PssCaptureSnapshot` for handle
metadata only, and `PssWalkSnapshot` for handle names/types and optional event
state. It does not clone the virtual address space, inject code, enable system
tracing, or close handles owned by the target. Every local snapshot, walk marker
and diagnostic process handle has a matching cleanup in `finally`. Event state
is reported only when the entry has type-specific information. The process must
allow diagnostic access; do not use these tools against unrelated applications.

To inspect one already running verification process:

```powershell
.\verification\diagnostics\Capture-ProcessHandles.ps1 -TargetProcessId <test-pid> -OutputPath .\artifacts\test-handles.csv
```

Managed heap collection is optional and requires `dotnet-gcdump` to be installed.
Keep its results in a separate directory:

```powershell
.\verification\diagnostics\Run-ResourceInvestigation.ps1 -Scenarios Docking -Configuration Debug -Cycles 312 -Batch 100 -IdleSeconds 30 -CollectManagedHeap -ResultsDirectory .\artifacts\resource-investigation-managed-rerun
```

Heap collection triggers GC and changes timing. The collector starts after it
observes the 12-cycle or final-cycle checkpoint; the running application is not
paused at that checkpoint. Heap file names identify the triggering stage, not an
exact allocation boundary. HeapStat describes managed objects and cannot assign
native Composition/Event handles to their allocation sites. A separate test-only
`dotnet-dump` Heap capture and `gcroot` analysis can investigate retained managed
objects when the type comparison warrants it.

Results from the recorded runs and their limits are in
[RESOURCE_FOLLOWUP.zh-CN.md](../../docs/RESOURCE_FOLLOWUP.zh-CN.md). API contracts:
[Process Snapshotting](https://learn.microsoft.com/en-us/windows/win32/api/_processsnapshot/),
[PssCaptureSnapshot](https://learn.microsoft.com/en-us/windows/win32/api/processsnapshot/nf-processsnapshot-psscapturesnapshot),
[PSS_HANDLE_ENTRY](https://learn.microsoft.com/en-us/windows/win32/api/processsnapshot/ns-processsnapshot-pss_handle_entry),
[dotnet-gcdump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump),
and [dotnet-dump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump).
