# Pinned upstream references

| Purpose | Source |
| --- | --- |
| Docking behavior and core model | [Dirkster.AvalonDock v5.0.0, commit `408dc2896e2f41f3bb79a15207f160edee8a6792`](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) |
| Core package | `Dirkster.AvalonDock.Core` 5.0.0 |
| MVVM and XML persistence packages | `Dirkster.AvalonDock.Mvvm` and `Dirkster.AvalonDock.Serializer.Xml` 5.0.0 |
| Default visual reference | [AvalonDock.Themes.WPFUI 1.2.2, commit `fc0592716eb6e3de2c2becdbc48e67de6e331474`](https://github.com/qian-o/AvalonDock.Themes.WPFUI/tree/fc0592716eb6e3de2c2becdbc48e67de6e331474) |

The WPF control library and WPFUI theme are source and appearance references, not runtime product dependencies. Compare against the pinned commits when changing docking behavior or theme details.

| Upstream responsibility | WinUI responsibility |
| --- | --- |
| Layout tree, engine, and strategies | Store layout, selection, hidden and floating content; choose state transitions. |
| `DockingManager` and `LayoutItem` | Connect application content, commands, events, templates, and views. |
| Drag service, drop areas, and targets | Track a docking operation, show target previews, and update the layout tree. |
| Panes, tabs, grids, and splitters | Present the layout through WinUI controls. |
| Floating, auto-hide, detached windows, and navigator | Present different interactions over the same layout state. |
| `ToggleDockingManager` | Provide Toggle sidebars over the shared docking model. |
| `LayoutSyncBridge` and `LayoutDtoMapper` | Connect official Core/MVVM data and serialization DTOs. |
| WPFUI theme | Adapt visual structure, dimensions, colors, and ordinary interaction states. |

Preserve file-level notices on adapted code and resources and the root [third-party notices](../THIRD-PARTY-NOTICES.md). Equivalent WinUI types and small adapters can replace WPF-specific types; matching declaration counts is not a goal.
