# HeatmapBench

A WPF test bench for the heatmap rendering handoff: the ScottPlot baseline, a
CPU renderer, a GPU renderer, and timing for each step of a frame.

## Run (Windows, .NET 8 SDK or later)

```
dotnet run -c Release
```

Run it in Release, outside the debugger; Debug timings are not comparable.

Pick a renderer, press Start, let it run for 20 to 30 seconds, then press
"Copy results". Press Stop before changing any setting. Do this once per
renderer with the same settings.

## Renderers

| Choice | Where the work happens |
|---|---|
| ScottPlot (baseline) | CPU. ScottPlot rebuilds its bitmap from every cell, then redraws the whole plot in software. |
| ScottPlot, OpenGL control | Same as the baseline, but ScottPlot draws the plot through OpenGL (`WpfPlotGL`). The bitmap is still rebuilt on the CPU. |
| WriteableBitmap (CPU) | CPU. Changed cells are coloured through a lookup table straight into a bitmap; WPF scales it. |
| OpenGL texture (GPU) | GPU. Cell values are kept in a float texture; a shader does the colour mapping and scaling. The CPU only narrows the changed cells to float and uploads that rectangle. |

The last two draw only the heatmap image: no axes, ticks or zoom. Both honour
"Partial updates"; the ScottPlot choices always process the whole grid.

The OpenGL choices need a graphics driver with OpenGL 3.3 and the
`WGL_NV_DX_interop` extension (current NVIDIA, AMD and Intel drivers have it).
They will not work over most remote-desktop sessions or in a virtual machine
without GPU passthrough.

## What the numbers mean (milliseconds)

| Row | Meaning |
|---|---|
| Producer buffer write | The background thread writing one batch into the grid |
| Dispatcher wait | Timer tick until the render callback starts on the UI thread |
| Update() | Bringing the renderer's copy of the data up to date |
| Refresh() call | The call that asks for the image to be shown |
| Deferred paint | Drawing done later, in WPF's render pass. ScottPlot: the plot being drawn. OpenGL texture: the texture upload and the draw, waited on until the GPU has finished. |
| Tick to UI idle | Timer tick until the UI thread has finished that frame's render pass |

"Tick to UI idle" is the figure to compare across renderers. For the ScottPlot
and OpenGL choices a frame costs Update() plus Deferred paint; Refresh() only
schedules the paint. The first second after Start is not recorded.

Not covered by any row: the time WPF's own render thread takes to put the
finished image on screen. Watch CPU and GPU load in Task Manager alongside.

## Colours

`Core/RangeColormap.cs` holds the amplitude colour stops. Every renderer uses
the same 4,096-entry lookup table built from it.

## Layout

- `Core/` is the renderer-independent pipeline from the handoff (`DataChunk`,
  `HeatmapBuffer`, `DataProcessor`, `IHeatmapRenderer`, `RenderScheduler`,
  `HeatmapViewModel`) plus `SyntheticProducer`, `FrameStats` and `RangeColormap`.
- `Renderers/` holds one class per renderer. `GlHeatmapPipeline` is the OpenGL
  part of the GPU renderer and has no WPF dependency. A new renderer implements
  `IHeatmapRenderer` and is added to the choice in `MainWindow.xaml.cs`.
