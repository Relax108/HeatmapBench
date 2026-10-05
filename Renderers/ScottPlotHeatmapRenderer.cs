using System.Diagnostics;
using System.Windows;
using HeatmapBench.Core;
using ScottPlot.WPF;

namespace HeatmapBench.Renderers;

/// <summary>The reference implementation: the current ScottPlot path.</summary>
public sealed class ScottPlotHeatmapRenderer : IHeatmapRenderer
{
    private readonly WpfPlot _plot;
    private readonly ScottPlot.Plottables.Heatmap _heatmap;

    private long _paintStart;

    public ScottPlotHeatmapRenderer(double[,] data, double min, double max, RangeColormap colormap, FrameStats stats)
    {
        _plot = new WpfPlot();

        _heatmap = _plot.Plot.Add.Heatmap(data);
        _heatmap.Smooth = false;
        _heatmap.ManualRange = new ScottPlot.Range(min, max);
        _heatmap.Colormap = new ColormapAdapter(colormap, min, max);

        // Refresh() only invalidates the control. The plot is drawn later, in
        // WPF's render pass, so that drawing has to be timed here.
        _plot.Plot.RenderManager.RenderStarting += (_, _) => _paintStart = Stopwatch.GetTimestamp();
        _plot.Plot.RenderManager.RenderFinished += (_, _) => stats.Paint.Add(_paintStart, Stopwatch.GetTimestamp());
    }

    public FrameworkElement View => _plot;

    /// <summary>ScottPlot rebuilds its whole bitmap; the dirty region is not used.</summary>
    public void Update(Int32Rect dirty)
    {
        _heatmap.Update();
    }

    public void Refresh()
    {
        _plot.Refresh();
    }

    /// <summary>
    /// Presents a <see cref="RangeColormap"/> to ScottPlot through the same
    /// lookup table the other renderers use, so every renderer shows the same colours.
    /// </summary>
    private sealed class ColormapAdapter : ScottPlot.IColormap
    {
        private readonly ScottPlot.Color[] _table;

        public ColormapAdapter(RangeColormap colormap, double min, double max)
        {
            uint[] rgb = colormap.BuildLookupTable(min, max, WriteableBitmapHeatmapRenderer.LookupSize);
            _table = new ScottPlot.Color[rgb.Length];
            for (int i = 0; i < rgb.Length; i++)
            {
                _table[i] = new ScottPlot.Color((byte)(rgb[i] >> 16), (byte)(rgb[i] >> 8), (byte)rgb[i]);
            }
        }

        public string Name => "Amplitude";

        public ScottPlot.Color GetColor(double position)
        {
            int index = (int)(position * (_table.Length - 1));
            if ((uint)index >= (uint)_table.Length)
            {
                index = index < 0 ? 0 : _table.Length - 1;
            }

            return _table[index];
        }
    }
}
