# WinUI.AvalonDock

WinUI 3 docking controls based on [Dirkster.AvalonDock v5.0.0](https://github.com/Dirkster99/AvalonDock/tree/408dc2896e2f41f3bb79a15207f160edee8a6792). The library uses the official `Dirkster.AvalonDock.Core` layout engine to provide dockable documents and tools, floating windows, auto-hide, and XML layout persistence in Windows desktop applications. Its default appearance is adapted from [AvalonDock.Themes.WPFUI](https://github.com/qian-o/AvalonDock.Themes.WPFUI/tree/fc0592716eb6e3de2c2becdbc48e67de6e331474).

This is an independent port, not an official AvalonDock release. Windows is the supported runtime; the reserved platform interfaces do not yet have an Uno implementation.

## Build

Install the .NET 10 SDK on Windows, then build the solution from the repository root:

```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug
```

## Examples

Run an example to explore the controls and their integration patterns:

```powershell
dotnet run --project .\sources\Experiments\Docking\Docking.csproj -c Debug
dotnet run --project .\sources\Experiments\ToggleDocking\ToggleDocking.csproj -c Debug
dotnet run --project .\sources\Experiments\Mvvm\Mvvm.csproj -c Debug
```

The examples cover a classic document workspace, a six-zone Toggle workspace, and MVVM-based document and tool sources. See the [example guide](sources/Experiments/README.md) for their controls and the [current project status](memory/status.md) for implementation scope and verification.

## License

[Microsoft Public License (Ms-PL)](LICENSE). See [third-party notices](THIRD-PARTY-NOTICES.md) for adapted code and asset attributions.
