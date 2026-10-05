using System.Diagnostics;
using System.Windows.Threading;

namespace HeatmapBench.Core;

public sealed class RenderScheduler : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<long> _render;
    private readonly Action _invoke;
    private readonly System.Threading.Timer _timer;

    private long _tickTimestamp;
    private int _busy;

    /// <param name="render">Runs on the dispatcher thread; receives the timestamp of the timer tick.</param>
    public RenderScheduler(Dispatcher dispatcher, Action<long> render, int intervalMs)
    {
        _dispatcher = dispatcher;
        _render = render;
        _invoke = () => _render(_tickTimestamp);
        _timer = new System.Threading.Timer(Tick, null, 0, intervalMs);
    }

    private void Tick(object? state)
    {
        if (Interlocked.Exchange(ref _busy, 1) != 0)
        {
            return;
        }

        try
        {
            _tickTimestamp = Stopwatch.GetTimestamp();
            _dispatcher.Invoke(_invoke);
        }
        catch (OperationCanceledException)
        {
            // The dispatcher is shutting down.
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
