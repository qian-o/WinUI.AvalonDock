# WinUI.AvalonDock

[![NuGet version](https://img.shields.io/nuget/vpre/WinUI.AvalonDock?label=NuGet)](https://www.nuget.org/packages/WinUI.AvalonDock)

A WinUI 3 port of [AvalonDock](https://github.com/Dirkster99/AvalonDock), autonomously developed by AI and functionally tested.

## Usage

```shell
dotnet add package WinUI.AvalonDock --prerelease
```

Add the manager to a WinUI 3 window:

```xml
<dock:DockingManager
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

More [examples](https://github.com/qian-o/WinUI.AvalonDock/tree/master/sources/Experiments).

## Screenshots

![Classic docking in Light theme](https://raw.githubusercontent.com/qian-o/WinUI.AvalonDock/master/assets/classic-light.png)

![Toggle docking in Dark theme](https://raw.githubusercontent.com/qian-o/WinUI.AvalonDock/master/assets/toggle-dark.png)

[Microsoft Public License (Ms-PL)](https://github.com/qian-o/WinUI.AvalonDock/blob/master/LICENSE).
