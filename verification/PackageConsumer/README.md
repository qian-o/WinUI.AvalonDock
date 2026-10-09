# Local NuGet package consumer

This independent WinUI desktop application consumes `WinUI.AvalonDock` **0.1.0-alpha** from the repository's local Release `.nupkg`. It has no product `ProjectReference`, does not join the root solution, and does not import the root build or central package version settings. This verifies the package that a consumer would install, including packaged templates, resources, upstream MVVM models, and XML serialization.

The only shared source files are verification code and content models from `sources/Experiments/Shared`. The product assembly and resources come exclusively from the package. Source mapping restricts `WinUI.AvalonDock` to the local candidate source; dependencies use nuget.org. Packages restore into `artifacts/local-package-cache`, separate from the user's global cache. The runner verifies the actual restore source, absence of project dependencies, package SHA256, and matching restored/output product assembly SHA256.

## Run

Build the library candidate first from the repository root:

```powershell
dotnet build .\sources\WinUI.AvalonDock\WinUI.AvalonDock.csproj -c Release
```

Run the checks in both configurations:

```powershell
.\verification\PackageConsumer\Run-Checks.ps1 -Configuration Debug -RefreshCandidate
.\verification\PackageConsumer\Run-Checks.ps1 -Configuration Release
```

Use `-RefreshCandidate` after rebuilding a package with the same version. It removes only this consumer's cached `WinUI.AvalonDock` package, forces restore, and checks that the restored `.nupkg` SHA256 matches the local candidate. Results and package identity evidence are written under `artifacts/package-consumer`.

To inspect normal application behavior:

```powershell
dotnet run --project .\verification\PackageConsumer\PackageConsumer.csproj -c Debug
```

Normal startup presents editable documents, a tool, float/dock actions, in-memory layout save/restore, and theme switching. Checks run only when an opt-in command-line switch is provided.

The host binds `LayoutItem.Title` and `LayoutItem.ContentId` to its own models through a consumer style selector. Its `DocumentClosed` handler removes the closed model from its own document collection, as required by the collection-based application pattern; that handler is application setup, not a product behavior change.

## What the checks cover

`--package-smoke <absolute-log-path>` checks the loaded assembly's candidate version, package default templates and resource loading, consumer collections and live title binding, floating state saved and restored by the upstream XML serializer, original content reconnected by `ContentId`, collection replacement, MVVM model mapping, and upstream cancel/hide/close synchronization. Product access in these checks uses public APIs.

`--boundary-smoke <absolute-log-path>` runs the shared boundary regression against the package assembly on the real WinUI UI thread and native windows. It additionally inspects some internal behavior via reflection. This separate check validates the same consumer binding, style, template, resource, and cleanup invariants as the examples, followed by the same-dispatcher workspace lifecycle and pinned upstream modal-window compatibility checks. Close synchronization and same-URI theme removal are checked against the pinned upstream behavior.

`--overlay-lifetime <absolute-log-path>` starts the shared opt-in overlay lifecycle check through the consumer window. It is separate from normal startup and the 130-check boundary run. `Run-Checks.ps1` now runs package smoke, boundary smoke, and this independent overlay lifetime check in sequence for each configuration; overlay success is checked through its `RESULT: PASS` line. Rebuild and refresh the package candidate before comparing product changes so this host does not test a stale package assembly. The runner checks candidate, cache, and loaded-output assembly hashes before the runs and again at the end to detect a changed candidate during validation.

These are controlled single-thread, local candidate checks. They do not establish upgrade compatibility with an older release, long-duration resource stability, multi-monitor/DPI support, or the safety of every possible embedded user control. The host adds no product features or public product APIs and does not publish the candidate.

## Verified local candidate, 2026-10-08

Both Debug and Release consumer builds completed with zero warnings and zero errors. Both configurations passed **27 public package integration checks** (14 consumer integration assertions plus 13 pinned upstream MVVM behavior assertions), **130 shared boundary/compatibility checks**, and a separate **20-cycle overlay lifetime regression** (`RESULT: PASS`). These are repeated configuration runs, not hundreds of independent usage scenarios.

The candidate package version is `0.1.0-alpha`, with SHA256 `19CA9B0C42CF0A38CBA7B74D5082CF82818676DFA941E216C5169693AE3929CE`. The package restored from the local Release directory into this host's isolated cache; assets identify it as a package and contain zero project dependencies. The restored package hash matches the candidate, and the restored/output product DLL hashes match (`E01CDB73951646DD3CFFE73660631E8AB811FA254AF49D86CB830CE06B2F0714`). The runner confirmed these identities remained unchanged at the end of each full three-stage run.

Each configuration ran 102 document create/float/dock/menu/close cycles, sampled after 12, 42, 72, and 102 cycles:

| Measurement | Debug samples | Release samples |
| --- | --- | --- |
| GDI objects | 20, 20, 20, 20 | 20, 20, 20, 20 |
| USER objects | 49, 49, 50, 50 | 48, 49, 50, 50 |
| Process handles | 1184, 1371, 1553, 1733 | 1181, 1368, 1549, 1729 |
| Private memory, MiB | 119.38, 149.05, 161.59, 163.51 | 121.80, 152.11, 161.49, 169.36 |
| Managed memory, MiB | 2.19, 3.56, 4.11, 4.68 | 2.20, 3.54, 4.11, 4.68 |

All observed document, content, layout-item, presenter, floating-control, and menu weak references were released at every sample; their observer lists retained zero weak-reference entries. Surviving floating window, host, native-window, owner-hook, subclass, and native GCHandle observations were all zero. The 102-cycle loops took 29.91 seconds in Debug and 29.90 seconds in Release. GDI/USER and released-object limits passed; process handle and memory growth remains an observation requiring longer-duration investigation, rather than a claim that every resource has reached a stable plateau.

The independent overlay regression sampled after 4, 12, and 20 closed overlay/destination pairs. Debug GDI was 54, 54, 50 with USER 39, 40, 40; Release GDI was 64, 64, 64 with USER 39, 39, 40. All overlay, template-view, surface, destination grid, destination window, and editor weak references were released at each sample. Overlay registry counts and surviving retired native HWND counts were zero. The tests completed in 8.98 and 8.62 seconds respectively.

Evidence is in `artifacts/package-consumer/{Debug,Release}-{package-identity,package-smoke,boundary-smoke,overlay-lifetime}.log`. The separate runtime baseline logs in that directory showed stable GDI after warm-up for plain windows, XAML islands, menus, and direct owned-HWND caption style changes. They also reproduced native GDI growth using only Windows App SDK custom-title-bar configuration APIs; this identifies a runtime interaction without asserting that all custom templates are covered by the product's default-path result.

## Windows App SDK runtime baseline

For resource diagnosis, `--runtime-lifetime <absolute-log-path>` starts a separate plain WinUI host without constructing any `DockingManager` or docking model. It runs 102 plain `Window`/`Grid`/`TextBox` activate and close cycles. `--xaml-source-lifetime <absolute-log-path>` instead runs 102 plain `DesktopWindowXamlSource` initialize, attach, show, clear, and dispose cycles against the host window. `--menu-lifetime <absolute-log-path>` runs 102 plain `MenuFlyout` show and hide cycles. `--caption-lifetime <absolute-log-path>` configures a plain window's custom native caption, resets all eight button colors to null eight times per window, and subscribes then removes a named native-region callback before closing. All sample process handles, GDI, USER, private memory, managed memory, and weak references after 12 warm-up cycles and three equal 30-cycle batches. These baseline observations help distinguish product retention from runtime behavior; they do not declare all resource growth harmless.

For short caption diagnosis, add `--caption-mode A` through `--caption-mode G`. Each mode starts its own process, uses 20 cycles (five warm-up plus three five-cycle batches), and never writes button colors. A uses extended content, hidden native title bar, and input regions. B uses hidden native title bar and input regions with extended content disabled. C uses extended content and hidden native title bar without input regions. D only calls `OverlappedPresenter.SetBorderAndTitleBar(true, false)` and never accesses `AppWindow.TitleBar`. E only reads `AppWindow.TitleBar.Height`. F only enables `Window.ExtendsContentIntoTitleBar` and never accesses `AppWindow.TitleBar`. G removes `WS_CAPTION` only from each newly created test window's own HWND, refreshes its frame with `SetWindowPos`, and installs input regions; it never uses the WinUI title-bar configuration APIs. The logs distinguish behavior completion from measured native resource stability; a `PASS` line here is not a resource stability assertion.

All runtime modes also accept `--lifetime-cycles <total>` (including warm-up), `--lifetime-batch <batch-size>`, `--lifetime-idle-seconds <seconds>`, and `--lifetime-checkpoint <absolute-csv-path>`. Without these options, normal runtime modes retain 102 cycles with 12 warm-up and three 30-cycle batches; caption letter modes retain 20 cycles with five warm-up and three five-cycle batches. A non-multiple total produces a shorter final batch. Tail idle samples every five seconds without forced GC, followed by a separate final GC sample. Logs and CSV samples flush immediately; a later failed cycle does not discard earlier samples. The initial log gives the process ID and UTC start time for an external handle-type snapshot.

For a longer isolated comparison, run each baseline in its own process:

```powershell
.\verification\PackageConsumer\Run-RuntimeBaselines.ps1 -Configuration Debug -Cycles 312 -Batch 100 -IdleSeconds 30
```

The runner serializes Window, XamlSource, and Menu baselines. Results go to `artifacts/package-consumer/runtime-extended`; use `-Baselines Window` or another named subset to isolate one scenario. It does not rebuild or publish the product package. Resource totals use documented `GetProcessHandleCount` and `GetGuiResources`. Per-kernel-object type counts can be captured externally with the documented Windows Process Snapshotting (`PssCaptureSnapshot` and `PssWalkSnapshot`) APIs; this host does not introduce undocumented native handle enumeration APIs.

`--composite-lifetime <absolute-log-path>` starts another pure SDK baseline without docking models. Each cycle creates one top-level window, one `DesktopWindowXamlSource`, one editor, and one menu; the same editor moves from the main window to the floating island and back. It uses the owned-HWND caption style from caption mode G, named native-region and `TakeFocusRequested` callbacks, explicit focus at each host stage, then clears island content, disposes the source, closes the floating window, shows/hides the menu, and removes the editor. This mirrors the principal native object ownership combination of a docking cycle while avoiding the product. It uses no WinUI title-bar configuration API, and does not dispose a borrowed SDK focus controller.

Use `-Baselines Composite` with `Run-RuntimeBaselines.ps1` to run it with the same long-run options. `-CompositeNoMenu` and `-CompositeNoRegions` select comparisons without the menu or native input-region registration; equivalent direct switches are `--composite-no-menu` and `--composite-no-regions`. These variants keep the window/island/editor transfer intact. The CSV retains the first 14 shared checkpoint columns and adds cumulative released counts, composite source/menu counts, and the currently retained observer weak-reference count. At each sample, released weak references are removed from observer lists and counted cumulatively, so the diagnostic observer does not itself grow by retaining every completed cycle's weak-reference object.

Runtime samples also count the current UI thread's total, open, and connected `ContentIsland` objects through the documented `FindAllForCurrentThread`, `IsClosed`, and `IsConnected` APIs. At each explicit source disposal, the former island's flags are observed immediately before and after disposal. Borrowed SDK island references remain local to the synchronous observation method; the logger retains only counts and boolean values. An unavailable or unreadable flag is recorded as unknown with a read-error count, rather than asserted as a disposal contract. These diagnostics do not close borrowed islands. See the [ContentIsland API](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.content.contentisland) and [IsConnected property](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.content.contentisland.isconnected).
