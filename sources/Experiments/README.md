# Sample applications

Explore WinUI.AvalonDock through three standalone WinUI 3 desktop applications. Each application demonstrates a different way to build a docking workspace. Common content models and styles are available in [`Shared`](Shared/).

| Sample | What it demonstrates |
| --- | --- |
| [`Docking`](Docking/) | A classic workspace with document and tool collection binding, floating windows, auto-hide, close commands, themes, and XML layout saving and loading. |
| [`ToggleDocking`](ToggleDocking/) | Six-zone tool layouts with zone movement, visibility controls, independent windows, layout priorities, and themes. |
| [`Mvvm`](Mvvm/) | An MVVM workspace using AvalonDock's models and layout service, with commands, active-item binding, document and tool collections, and XML layout saving and loading. |

## Requirements

Use Windows 10 build 19041 or later and the .NET 10 SDK. The samples run as unpackaged x64 desktop applications and include the Windows App SDK runtime in their build output.

## Build and run

From the repository root, build the solution:

```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug
```

Launch the sample you want to explore:

```powershell
dotnet run --project .\sources\Experiments\Docking\Docking.csproj -c Debug
```

```powershell
dotnet run --project .\sources\Experiments\ToggleDocking\ToggleDocking.csproj -c Debug
```

```powershell
dotnet run --project .\sources\Experiments\Mvvm\Mvvm.csproj -c Debug
```

Use the menus to open and close documents, change tool placement, and switch themes. In the Docking and Mvvm samples, Save Layout and Restore Layout keep the XML layout in memory for the current session.

For installation, basic usage, and information about the project's AI-led development and maintenance, see the [project README](../../README.md).
