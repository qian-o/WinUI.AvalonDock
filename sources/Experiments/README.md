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

## Boundary regression checks

After building, each sample accepts `--boundary-smoke <absolute-log-path>` to run opt-in checks on its real WinUI dispatcher and native windows, then exit. The output directory must already exist. The log contains each passed assertion or the failing exception; failures set a nonzero process exit code.

```powershell
New-Item -ItemType Directory -Force .\artifacts\boundary-audit | Out-Null
$result = Join-Path (Get-Location) 'artifacts\boundary-audit\Docking.log'
& .\sources\Experiments\Docking\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Docking.exe --boundary-smoke $result
```

Use the corresponding executable for ToggleDocking and Mvvm. These checks cover consumer context, binding, style and resource ownership, floating and detached transitions, custom template replacement, menus, the pinned upstream MVVM close behavior, same-thread independent workspaces, modal restoration, and 102 resource lifecycle cycles. They compile into the samples only. Normal startup does not run them.

Run all three samples in Debug and Release serially from the repository root:

```powershell
.\verification\Run-BoundaryChecks.ps1
```

Use `-NoBuild` after a completed solution build. In addition to the three samples' boundary checks, the script runs an independent 20-cycle overlay regression through Docking in each configuration. `--overlay-lifetime <absolute-log-path>` can run that regression separately; it covers real Show/Hide/Show rendering, preserved editor focus, cancelled native close, and closed-object/native-window release. It is opt-in and does not run during ordinary startup. The script stops on an unsuccessful process or incomplete result and writes separate final logs in `artifacts/upstream-acceptance`. Run these foreground-sensitive checks separately from the [independent package consumer](../../verification/PackageConsumer/README.md). See the [system API and content boundary review](../../docs/BOUNDARY_REVIEW.zh-CN.md), [upstream compatibility baseline](../../docs/UPSTREAM_COMPATIBILITY.zh-CN.md), [resource lifecycle evidence](../../docs/RESOURCE_LIFETIME.zh-CN.md), and [resource follow-up](../../docs/RESOURCE_FOLLOWUP.zh-CN.md) for the audit scope, measured results, and remaining environment coverage.

For installation, basic usage, and information about the project's AI-led development and maintenance, see the [project README](../../README.md).
