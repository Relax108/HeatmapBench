using System.Text;
using System.Windows;
using System.Windows.Threading;
using HeatmapBench.Core;
using HeatmapBench.Renderers;

namespace HeatmapBench;

public partial class MainWindow : Window
{
    private sealed record Settings(
        bool UseScottPlot,
        bool PartialUpdates,
        bool Scattered,
        int PointCount,
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
            !TryParsePositive(CycleBox.Text, out int cycleMs) ||
            !TryParsePositive(IntervalBox.Text, out int intervalMs))
        {
            StatsBox.Text = "Check the settings: grid sizes look like 830x305, the other boxes take whole numbers above zero.";
            return;
        }

        var settings = new Settings(
            UseScottPlot: RendererBox.SelectedIndex == 0,
            PartialUpdates: PartialBox.IsChecked == true,
            Scattered: PatternBox.SelectedIndex == 1,
            PointCount: pointCount,
            CycleMs: cycleMs,
            RenderIntervalMs: intervalMs);

        _header =
            $"Renderer: {(settings.UseScottPlot ? "ScottPlot" : "WriteableBitmap")}\n" +
            $"Partial updates: {(settings.UseScottPlot ? "not supported" : settings.PartialUpdates ? "on" : "off")}\n" +
            $"Points: {settings.PointCount} per batch, {(settings.Scattered ? "scattered" : "moving window")}\n" +
            $"Producer cycle: {settings.CycleMs} ms   Render interval: {settings.RenderIntervalMs} ms\n";

        HostA.Content = AddHeatmap("Grid A", widthA, heightA, seed: 1, settings);
        HostB.Content = AddHeatmap("Grid B", widthB, heightB, seed: 2, settings);

        StartStopButton.Content = "Stop";
        _statsTimer.Start();
        ShowStats();
    }

    private FrameworkElement AddHeatmap(string name, int width, int height, int seed, Settings settings)
    {
        var buffer = new HeatmapBuffer(width, height);
        var stats = new FrameStats(warmUp: TimeSpan.FromSeconds(1));

        IHeatmapRenderer renderer = settings.UseScottPlot
            ? new ScottPlotHeatmapRenderer(buffer.Data, min: 0, max: 100, RangeColormap.Amplitude, stats)
            : new WriteableBitmapHeatmapRenderer(buffer.Data, min: 0, max: 100, RangeColormap.Amplitude);

        _running.Add(new HeatmapViewModel(
            Dispatcher, buffer, renderer, stats, settings.RenderIntervalMs, settings.PartialUpdates));
        _running.Add(new SyntheticProducer(
            buffer, stats.ProducerWrite, settings.PointCount, settings.CycleMs, settings.Scattered, seed));
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
