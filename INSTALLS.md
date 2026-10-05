# Install log (HeatmapBench)

Everything a Claude Code agent has installed or downloaded for this project.
The same rows are in the machine-wide log at `~/.claude/INSTALLS.md`.

| Date | Item | Version | Location | Size | Why | Remove with | Status |
|---|---|---|---|---|---|---|---|
| 2026-10-06 | NuGet packages for ScottPlot.WPF and its dependencies (ScottPlot, SkiaSharp, HarfBuzzSharp, OpenTK; 30 package folders, downloaded by `dotnet build`) | ScottPlot and ScottPlot.WPF 5.1.59, SkiaSharp 3.119.0, HarfBuzzSharp 8.3.1.1, OpenTK 3.3.1 to 4.9.4 | `~/.nuget/packages/` (`scottplot*`, `skiasharp*`, `harfbuzzsharp*`, `opentk*`) | about 404 MB | Compile check of the benchmark on this Mac | `rm -rf ~/.nuget/packages/{scottplot,skiasharp,harfbuzzsharp,opentk}*` | installed |
| 2026-10-06 | Windows reference packs for compiling a WPF project off Windows (downloaded by `dotnet build` because of `EnableWindowsTargeting`) | Microsoft.WindowsDesktop.App.Ref 8.0.15, Microsoft.Windows.SDK.NET.Ref 10.0.19041.57 | `~/.nuget/packages/microsoft.windowsdesktop.app.ref`, `~/.nuget/packages/microsoft.windows.sdk.net.ref` | 108 MB | Same compile check | `rm -rf ~/.nuget/packages/microsoft.windowsdesktop.app.ref ~/.nuget/packages/microsoft.windows.sdk.net.ref` | installed |

## Downloaded content

None.
