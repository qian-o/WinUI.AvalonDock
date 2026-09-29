# Project memory

These notes contain the current design boundaries and source references for the WinUI 3 port of AvalonDock. Read [repository guidance](../AGENTS.md) and check the working tree before changing code. Use [current scope](status.md) for supported workflows and verification guidance, and the [upstream map](upstream.md) when comparing a behavior with the pinned original implementation.

## Architecture

- `LayoutRoot` and its nodes own layout and docking state. The docking engine chooses targets and applies state transitions.
- `DockingManager` connects application content, commands, events, templates, and views to that state. WinUI controls present it; the Windows layer supplies input, window hosts, overlays, focus, and coordinate conversion.
- Shared docking logic must remain independent of HWND and native window messages so a future platform implementation can use the same layout model. Only Windows is implemented and verified today.
- The product uses the official AvalonDock Core package. The examples use the official MVVM and XML serialization packages where appropriate. The WPF control library and WPFUI theme serve as behavior and visual references, not runtime dependencies.

Keep these notes short and current. Replace outdated statements rather than appending session histories, phase plans, test logs, or temporary paths.
