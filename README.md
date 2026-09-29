# WinUI.AvalonDock

[![NuGet version](https://img.shields.io/nuget/vpre/WinUI.AvalonDock?label=NuGet)](https://www.nuget.org/packages/WinUI.AvalonDock)

WinUI.AvalonDock brings the common docking workflows of [AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792) to WinUI 3. It uses the official `Dirkster.AvalonDock.Core` layout engine rather than the WPF control library. The default appearance draws on [AvalonDock.Themes.WPFUI](https://github.com/qian-o/AvalonDock.Themes.WPFUI/tree/fc0592716eb6e3de2c2becdbc48e67de6e331474).

This repository was developed autonomously by AI under human direction. Its Classic, Toggle, and MVVM example workflows have undergone functional testing in real WinUI windows. This is an independent port, not an official AvalonDock release.

## Features

- Dockable documents and tool panes, including grouping, splitting, and resizing.
- Drag docking, floating windows, auto-hide, and pane visibility controls.
- Classic and six-zone Toggle workspaces, with Light and Dark themes.
- Commands, events, templates, MVVM integration, and XML layout persistence.

## Build and run

Windows and the .NET 10 SDK are required. Build the solution from the repository root:

```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug
```

Run one of the three examples:

```powershell
dotnet run --project .\sources\Experiments\Docking\Docking.csproj -c Debug
dotnet run --project .\sources\Experiments\ToggleDocking\ToggleDocking.csproj -c Debug
dotnet run --project .\sources\Experiments\Mvvm\Mvvm.csproj -c Debug
```

See the [example guide](https://github.com/qian-o/WinUI.AvalonDock/blob/master/sources/Experiments/README.md) for the purpose of each workspace and its functional smoke-check command. The NuGet badge tracks the intended package ID. If no package version is listed, build from source.

## Platform and license

The supported runtime is Windows. The layout architecture leaves room for a future Uno platform implementation, but no Uno backend is included. The repository is licensed under the [Microsoft Public License (Ms-PL)](https://github.com/qian-o/WinUI.AvalonDock/blob/master/LICENSE); source files adapted from upstream projects retain their attribution and license comments.
