# Current scope and verification

The project targets ordinary AvalonDock v5.0.0 desktop workflows on Windows with WinUI 3. Its maintained examples exercise Classic docking, Toggle docking, and MVVM integration. The product uses the official Core package; the examples use the official MVVM and XML serialization packages as needed. The pinned sources and responsibility map are listed in [upstream.md](upstream.md).

## Behavior to preserve

- Documents and tools: activation, closing, reordering, grouping, splitting, docking, floating, and returning to the layout.
- Tool visibility: hide, show, auto-hide, pin, and side placement.
- Dragging: eligible drop targets, indicators, previews, and the resulting layout, including layouts that change while a drag is in progress.
- Integration: source collection changes, cancelable events, commands, templates, floating-window policy, and XML layout restoration.
- Presentation: Classic and Toggle layouts, Light/Dark themes, floating windows, menus, and keyboard navigation.

Changes to these paths should be checked through the affected example's real WinUI window. Build and smoke checks exercise model and window behavior, while visual and pointer interactions require inspection in the running application. A successful build alone does not establish visual correctness.

The current implementation and verification target Windows. No Uno backend or cross-platform verification is claimed. Performance changes should preserve docking geometry and interaction behavior; do not claim measured frame-rate improvements without comparable measurements.
