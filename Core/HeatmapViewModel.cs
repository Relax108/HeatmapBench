using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace HeatmapBench.Core;

public sealed class HeatmapViewModel : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly HeatmapBuffer _buffer;
    private readonly IHeatmapRenderer _renderer;
    private readonly FrameStats _stats;
    private readonly bool _partialUpdates;
    private readonly Action _frameDone;
    private readonly RenderScheduler _scheduler;

    private long _frameStart;
    private bool _framePending;
    private bool _disposed;

    public HeatmapViewModel(
        Dispatcher dispatcher,
        HeatmapBuffer buffer,
        IHeatmapRenderer renderer,
        FrameStats stats,
        int intervalMs,
        bool partialUpdates)
    {
        _dispatcher = dispatcher;
        _buffer = buffer;
        _renderer = renderer;
        _stats = stats;
        _partialUpdates = partialUpdates;
        _frameDone = OnFrameDone;
        _scheduler = new RenderScheduler(dispatcher, Render, intervalMs);
    }

    private void Render(long tickTimestamp)
    {
        if (_disposed)
        {
            return;
        }

        long t1 = Stopwatch.GetTimestamp();

        if (!_buffer.ConsumeDirty(out Int32Rect dirty))
        {
            return;
        }

        _renderer.Update(_partialUpdates ? dirty : _buffer.FullRegion);
        long t2 = Stopwatch.GetTimestamp();

        _renderer.Refresh();
        long t3 = Stopwatch.GetTimestamp();

        _stats.DispatcherWait.Add(tickTimestamp, t1);
        _stats.Update.Add(t1, t2);
        _stats.Refresh.Add(t2, t3);

        // Loaded priority runs after WPF's render pass, so this marks the point
        // where the UI thread has finished everything this frame caused.
        if (!_framePending)
        {
            _framePending = true;
            _frameStart = tickTimestamp;
            _dispatcher.BeginInvoke(DispatcherPriority.Loaded, _frameDone);
        }
    }

    private void OnFrameDone()
    {
        _stats.Frame.Add(_frameStart, Stopwatch.GetTimestamp());
        _framePending = false;
    }

    public void Dispose()
    {
        _disposed = true;
        _scheduler.Dispose();
    }
}
