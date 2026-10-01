# WinUI.AvalonDock

[![NuGet version](https://img.shields.io/nuget/vpre/WinUI.AvalonDock?label=NuGet)](https://www.nuget.org/packages/WinUI.AvalonDock)

WinUI.AvalonDock is an unofficial WinUI 3 port of [AvalonDock](https://github.com/Dirkster99/AvalonDock) for Windows desktop applications. It provides document tabs and dockable tool panes for IDEs, editors, and other applications with customizable workspaces.

**AI leads the development and ongoing maintenance of this project**, including feature implementation, bug fixes, refactoring, and documentation.

## Features

- Classic document and tool docking with `DockingManager`.
- Six-zone tool layouts with `ToggleDockingManager`: upper and lower areas on each side, plus left and right areas along the bottom.
- Drag-and-drop docking, floating windows, independent tool windows, and resizable panes.
- Auto-hide and tool visibility controls.
- Light and dark themes, custom styles, and content templates.
- Collection binding and integration with AvalonDock's MVVM models and layout service.
- XML layout saving and loading through AvalonDock's serialization package.

## Requirements

- Windows 10 build 19041 or later.
- .NET 10 and a WinUI 3 desktop application using the Windows App SDK.

The library targets `net10.0-windows10.0.19041.0`.

## Installation

```shell
dotnet add package WinUI.AvalonDock --prerelease
```

## Quick start

Add a `DockingManager` to your WinUI window or page. The `AvalonDock` namespace contains the controls, and `AvalonDock.Layout` contains the layout models:

```xml
<dock:DockingManager x:Name="Manager"
    xmlns:dock="using:AvalonDock"
    xmlns:layout="using:AvalonDock.Layout">
    <layout:LayoutRoot>
        <layout:LayoutPanel Orientation="Horizontal">
            <layout:LayoutAnchorablePane DockWidth="240">
                <layout:LayoutAnchorable Title="Explorer" ContentId="explorer"
                                         Content="Tool content" />
            </layout:LayoutAnchorablePane>
            <layout:LayoutDocumentPane>
                <layout:LayoutDocument Title="Document" ContentId="document"
                                       Content="Document content" />
            </layout:LayoutDocumentPane>
        </layout:LayoutPanel>
    </layout:LayoutRoot>
</dock:DockingManager>
```

For a collection-based workspace, bind documents and tools through `DocumentsSource` and `AnchorablesSource`. For a six-zone workspace, use `ToggleDockingManager` and set each tool model's initial placement through `IToolbox.Zone`.

## MVVM and layout serialization

Add the upstream packages for the integrations your application uses:

```shell
dotnet add package Dirkster.AvalonDock.Mvvm --version 5.0.0
dotnet add package Dirkster.AvalonDock.Serializer.Xml --version 5.0.0
```

The MVVM integration uses `DockLayout` with AvalonDock's models and `DockLayoutService`. XML layouts use `XmlLayoutSerializer`; assign a stable, unique `ContentId` to each document and tool. The serializer reuses existing content with matching identifiers, and `LayoutSerializationCallback` supplies content that needs to be created or retrieved.

See the [sample applications](https://github.com/qian-o/WinUI.AvalonDock/tree/master/sources/Experiments) for complete workspace setup and integration examples.

## Samples

| Application | Demonstrates |
| --- | --- |
| [Docking](https://github.com/qian-o/WinUI.AvalonDock/tree/master/sources/Experiments/Docking) | Classic document and tool docking, collection binding, auto-hide, floating windows, themes, and XML layout saving and loading. |
| [ToggleDocking](https://github.com/qian-o/WinUI.AvalonDock/tree/master/sources/Experiments/ToggleDocking) | Six-zone tools, zone movement, independent windows, layout priorities, and themes. |
| [Mvvm](https://github.com/qian-o/WinUI.AvalonDock/tree/master/sources/Experiments/Mvvm) | View models, commands, document and tool collections, active-item binding, and XML layout saving and loading. |

Build and run instructions are available in the [samples README](https://github.com/qian-o/WinUI.AvalonDock/blob/master/sources/Experiments/README.md).

## Screenshots

![Classic docking in Light theme](https://raw.githubusercontent.com/qian-o/WinUI.AvalonDock/master/assets/classic-light.png)

![Toggle docking in Dark theme](https://raw.githubusercontent.com/qian-o/WinUI.AvalonDock/master/assets/toggle-dark.png)

## Feedback

Report bugs and request features through [GitHub Issues](https://github.com/qian-o/WinUI.AvalonDock/issues). For a bug report, include your package version, Windows version, and a minimal example or steps to reproduce the issue.

## License

WinUI.AvalonDock is distributed under the [Microsoft Public License (Ms-PL)](https://github.com/qian-o/WinUI.AvalonDock/blob/master/LICENSE). The project builds on [AvalonDock](https://github.com/Dirkster99/AvalonDock).
