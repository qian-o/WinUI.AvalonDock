# WinUI.AvalonDock

[![NuGet version](https://img.shields.io/nuget/vpre/WinUI.AvalonDock?label=NuGet)](https://www.nuget.org/packages/WinUI.AvalonDock)

A WinUI 3 port of [AvalonDock](https://github.com/Dirkster99/AvalonDock), autonomously developed by AI and functionally tested.

The project targets .NET 10 and `net10.0-windows10.0.19041.0` with the Windows App SDK. The current implementation supports Windows desktop applications.

The library provides classic document/tool docking through `DockingManager`, six-zone tool layouts through `ToggleDockingManager`, floating and independent tool windows, auto-hide, layout themes, and integration with the upstream MVVM and XML serialization packages.

## Usage

```shell
dotnet add package WinUI.AvalonDock --prerelease
```

Add the manager to a WinUI 3 window:

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

Give each document and tool a stable, unique `ContentId` when saving and restoring layouts. The upstream XML serializer reuses matching content from the current layout. Use `LayoutSerializationCallback` to provide content that is not already available; the [MVVM sample](https://github.com/qian-o/WinUI.AvalonDock/tree/master/sources/Experiments/Mvvm) demonstrates that flow.

`ToggleDockingManager` follows the upstream placement rules. Declare a tool's initial six-zone placement through `IToolbox.Zone`. When a layout is rebuilt, its left/right/bottom side determines the stripe, and the connected tool model's `Zone` selects the matching half. A plain `LayoutAnchorable` uses that side's default half (`LeftTop`, `RightTop`, or `BottomLeft`). Moving a tool does not rewrite its `IToolbox.Zone`. Layout saving and loading use the upstream XML format and do not require a separate application settings store for zones.

The WinUI manager also exposes `Dispose()` for permanently releasing native resources and model subscriptions, for example when an application discards a manager:

```csharp
Closed += (_, _) => Manager.Dispose();
```

More [examples](https://github.com/qian-o/WinUI.AvalonDock/tree/master/sources/Experiments).

## Framework structure

`Layout` contains the WinUI layout tree and content state. `DockingManager` coordinates that tree with the typed views in `Controls`; `ILayoutEngine` supplies the layout placement strategy. `LayoutSyncBridge` connects upstream MVVM models, and `Serialization/LayoutDtoMapper` maps the WinUI tree to upstream serialization DTOs.

`Platforms` defines internal services for windows, input, focus, overlays, and coordinate conversion. The implementations in `Platforms/Windows` contain native Windows integration. These interfaces organize the Windows implementation; an Uno backend has not been implemented.

## Screenshots

![Classic docking in Light theme](https://raw.githubusercontent.com/qian-o/WinUI.AvalonDock/master/assets/classic-light.png)

![Toggle docking in Dark theme](https://raw.githubusercontent.com/qian-o/WinUI.AvalonDock/master/assets/toggle-dark.png)

[Microsoft Public License (Ms-PL)](https://github.com/qian-o/WinUI.AvalonDock/blob/master/LICENSE).
