# Sample applications

The three Windows samples use the library in separate workspaces. Each sample can be launched on its own; they share a small set of sample models and styles in [`Shared`](Shared/).

| Sample | What it demonstrates |
| --- | --- |
| [`Docking`](Docking/) | A classic IDE layout with documents, tool panes, floating windows, auto-hide, commands, themes, and XML layout persistence. |
| [`ToggleDocking`](ToggleDocking/) | Six dock zones with expandable tool panes, layout priority options, and drag targets. |
| [`Mvvm`](Mvvm/) | An MVVM workspace backed by the upstream docking models and layout service. |

Build the solution from the repository root, then launch a sample:

```powershell
dotnet build .\WinUI.AvalonDock.slnx -c Debug
dotnet run --project .\sources\Experiments\Docking\Docking.csproj -c Debug
```

Replace `Docking` with `ToggleDocking` or `Mvvm` to run another sample.
