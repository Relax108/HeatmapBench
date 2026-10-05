using System.Text;
using System.Windows;
using System.Windows.Threading;
using HeatmapBench.Core;
using HeatmapBench.Renderers;

namespace HeatmapBench;

public partial class MainWindow : Window
{
    private enum RendererKind
    {
        ScottPlot,
        ScottPlotOpenGl,
        WriteableBitmap,
        OpenGlTexture,
    }

    private static readonly string[] RendererNames =
    {
        "ScottPlot",
        "ScottPlot, OpenGL control",
        "WriteableBitmap (CPU)",
        "OpenGL texture (GPU)",
    };

    private sealed record Settings(
        RendererKind Renderer,
        bool PartialUpdates,
        bool Scattered,
        int PointCount,
        int ChunksPerCycle,
        int CycleMs,
        int RenderIntervalMs);

    private readonly DispatcherTimer _statsTimer;
    private readonly List<IDisposable> _running = new();
    private readonly List<(string Title, FrameStats Stats)> _stats = new();

    private string _header = "";

    public MainWindow()
    {
        InitializeComponent();

        _statsTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _statsTimer.Tick += (_, _) => ShowStats();

        Closed += (_, _) => Stop();
    }

    private void StartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_running.Count > 0)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(StatsBox.Text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another program is holding the clipboard; pressing the button again works.
        }
    }

    private void Start()
    {
        if (!TryParseSize(GridABox.Text, out int widthA, out int heightA) ||
            !TryParseSize(GridBBox.Text, out int widthB, out int heightB) ||
            !TryParsePositive(PointsBox.Text, out int pointCount) ||
            !TryParsePositive(ChunksBox.Text, out int chunksPerCycle) ||
            !TryParsePositive(CycleBox.Text, out int cycleMs) ||
            !TryParsePositive(IntervalBox.Text, out int intervalMs))
        {
            StatsBox.Text = "Check the settings: grid sizes look like 830x305, the other boxes take whole numbers above zero.";
            return;
        }

        var settings = new Settings(
            Renderer: (RendererKind)RendererBox.SelectedIndex,
            PartialUpdates: PartialBox.IsChecked == true,
            Scattered: PatternBox.SelectedIndex == 1,
            PointCount: pointCount,
            ChunksPerCycle: chunksPerCycle,
            CycleMs: cycleMs,
            RenderIntervalMs: intervalMs);

        bool supportsPartial = settings.Renderer is RendererKind.WriteableBitmap or RendererKind.OpenGlTexture;

        _header =
            $"Renderer: {RendererNames[(int)settings.Renderer]}\n" +
            $"Partial updates: {(!supportsPartial ? "not supported" : settings.PartialUpdates ? "on" : "off")}\n" +
            $"Inflow per grid: {settings.ChunksPerCycle} chunks of {settings.PointCount} points every {settings.CycleMs} ms" +
            $" ({(double)settings.ChunksPerCycle * settings.PointCount * 1000 / settings.CycleMs / 1e6:F2} M points/s)\n" +
            $"Points: {(settings.Scattered ? "scattered" : "moving window")}   Render interval: {settings.RenderIntervalMs} ms\n";

        HostA.Content = AddHeatmap("Grid A", widthA, heightA, seed: 1, settings);
        HostB.Content = AddHeatmap("Grid B", widthB, heightB, seed: 2, settings);

        StartStopButton.Content = "Stop";
        _statsTimer.Start();
        ShowStats();
    }

    private FrameworkElement AddHeatmap(string name, int width, int height, int seed, Settings settings)
    {
        var buffer = new HeatmapBuffer(width, height);
        var stats = new FrameStats(warmUp: TimeSpan.FromSeconds(1), settings.PointCount);

        RangeColormap colormap = RangeColormap.Amplitude;
        IHeatmapRenderer renderer = settings.Renderer switch
        {
            RendererKind.ScottPlot =>
                new ScottPlotHeatmapRenderer(buffer.Data, min: 0, max: 100, colormap, stats, useOpenGl: false),
            RendererKind.ScottPlotOpenGl =>
                new ScottPlotHeatmapRenderer(buffer.Data, min: 0, max: 100, colormap, stats, useOpenGl: true),
            RendererKind.WriteableBitmap =>
                new WriteableBitmapHeatmapRenderer(buffer.Data, min: 0, max: 100, colormap),
            _ =>
                new OpenGlHeatmapRenderer(buffer.Data, min: 0, max: 100, colormap, stats),
        };

        _running.Add(new HeatmapViewModel(
            Dispatcher, buffer, renderer, stats, settings.RenderIntervalMs, settings.PartialUpdates));
        _running.Add(new SyntheticProducer(
            buffer, stats, settings.PointCount, settings.ChunksPerCycle, settings.CycleMs, settings.Scattered, seed));
        _stats.Add(($"{name} {width}x{height}", stats));

        return renderer.View;
    }

    private void Stop()
    {
        _statsTimer.Stop();

        if (_running.Count > 0)
        {
            ShowStats();
        }

        foreach (IDisposable item in _running)
        {
            item.Dispose();
        }

        _running.Clear();
        _stats.Clear();
        StartStopButton.Content = "Start";
    }

    private void ShowStats()
    {
        var sb = new StringBuilder(_header);

        foreach ((string title, FrameStats stats) in _stats)
        {
            sb.AppendLine();
            sb.Append(stats.Summarize(title));
        }

        StatsBox.Text = sb.ToString();
    }

    private static bool TryParseSize(string text, out int width, out int height)
    {
        width = 0;
        height = 0;
        string[] parts = text.Split(new[] { 'x', 'X', '×' }, StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
               TryParsePositive(parts[0], out width) &&
               TryParsePositive(parts[1], out height);
    }

    private static bool TryParsePositive(string text, out int value)
    {
        return int.TryParse(text.Trim(), out value) && value > 0;
    }
}
