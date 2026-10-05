# HeatmapBench

A WPF test bench for the heatmap rendering handoff: the ScottPlot baseline, a
`WriteableBitmap` renderer, and timing for each step of a frame.

## Run (Windows, .NET 8 SDK or later)

```
dotnet run -c Release
```

Run it in Release, outside the debugger; Debug timings are not comparable.

Pick a renderer, press Start, let it run for 20 to 30 seconds, then press
"Copy results". Press Stop before changing any setting. Do this once per
renderer with the same settings.

## What the numbers mean (milliseconds)

| Row | Meaning |
|---|---|
| Producer buffer write | The background thread writing one batch into the grid |
| Dispatcher wait | Timer tick until the render callback starts on the UI thread |
| Update() | Bringing the renderer's image up to date |
| Refresh() call | The call that asks for the image to be shown |
| Deferred paint | ScottPlot only: the plot being drawn later, in WPF's render pass |
| Tick to UI idle | Timer tick until the UI thread has finished that frame's render pass |

For ScottPlot the cost of a frame is Update() plus Deferred paint; Refresh()
only schedules the paint. "Tick to UI idle" is the figure to compare across
renderers. The first second after Start is not recorded.

## Layout

- `Core/` is the renderer-independent pipeline from the handoff (`DataChunk`,
  `HeatmapBuffer`, `DataProcessor`, `IHeatmapRenderer`, `RenderScheduler`,
  `HeatmapViewModel`) plus `SyntheticProducer` and `FrameStats`.
- `Renderers/` holds one class per renderer. A new one implements
  `IHeatmapRenderer` and is added to the choice in `MainWindow.xaml.cs`.
