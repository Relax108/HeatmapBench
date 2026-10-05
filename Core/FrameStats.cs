using System.Diagnostics;
using System.Text;

namespace HeatmapBench.Core;

/// <summary>
/// The most recent 4096 durations of one measured step, in milliseconds.
/// One thread writes; the summary may read a sample while it is being replaced.
/// </summary>
public sealed class TimingSeries
{
    private const int Capacity = 4096;

    private readonly double[] _samples = new double[Capacity];
    private readonly long _recordAfter;
    private long _count;

    public TimingSeries(string name, long recordAfter)
    {
        Name = name;
        _recordAfter = recordAfter;
    }

    public string Name { get; }

    public long Count => Volatile.Read(ref _count);

    public void Add(long startTimestamp, long endTimestamp)
    {
        if (endTimestamp < _recordAfter)
        {
            return;
        }

        long n = _count;
        _samples[n & (Capacity - 1)] =
            Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;
        Volatile.Write(ref _count, n + 1);
    }

    public void AppendSummary(StringBuilder sb)
    {
        int n = (int)Math.Min(Count, Capacity);
        if (n == 0)
        {
            return;
        }

        double[] sorted = new double[n];
        Array.Copy(_samples, sorted, n);
        Array.Sort(sorted);

        sb.AppendLine(
            $"  {Name,-22}{sorted.Average(),8:F2}{Percentile(sorted, 0.50),8:F2}" +
            $"{Percentile(sorted, 0.95),8:F2}{Percentile(sorted, 0.99),8:F2}{sorted[n - 1],8:F2}");
    }

    private static double Percentile(double[] sorted, double p)
    {
        int index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}

/// <summary>All measured steps for one heatmap.</summary>
public sealed class FrameStats
{
    private readonly long _recordAfter;
    private readonly int _pointsPerChunk;

    /// <param name="warmUp">Samples taken during this time are dropped, to keep JIT and first-frame costs out.</param>
    public FrameStats(TimeSpan warmUp, int pointsPerChunk)
    {
        _recordAfter = Stopwatch.GetTimestamp() + (long)(warmUp.TotalSeconds * Stopwatch.Frequency);
        _pointsPerChunk = pointsPerChunk;

        ProducerWrite = new TimingSeries("Producer chunk write", _recordAfter);
        ProducerCycle = new TimingSeries("Producer cycle work", _recordAfter);
        DispatcherWait = new TimingSeries("Dispatcher wait", _recordAfter);
        Update = new TimingSeries("Update()", _recordAfter);
        Refresh = new TimingSeries("Refresh() call", _recordAfter);
        Paint = new TimingSeries("Deferred paint", _recordAfter);
        Frame = new TimingSeries("Tick to UI idle", _recordAfter);
    }

    /// <summary>Time the producer spends writing one chunk into the grid.</summary>
    public TimingSeries ProducerWrite { get; }

    /// <summary>
    /// Time the producer needs for one whole cycle: generating and writing every chunk.
    /// If this reaches the cycle time, the producer cannot deliver the requested rate.
    /// </summary>
    public TimingSeries ProducerCycle { get; }

    /// <summary>Timer tick until the render callback starts on the UI thread.</summary>
    public TimingSeries DispatcherWait { get; }

    public TimingSeries Update { get; }

    public TimingSeries Refresh { get; }

    /// <summary>Drawing a renderer does later, in WPF's own render pass, after Refresh() has returned.</summary>
    public TimingSeries Paint { get; }

    /// <summary>Timer tick until the UI thread has finished the render pass for that frame.</summary>
    public TimingSeries Frame { get; }

    public string Summarize(string title)
    {
        double seconds = Stopwatch.GetElapsedTime(_recordAfter).TotalSeconds;
        if (seconds <= 0)
        {
            return $"{title}   warming up\n";
        }

        var sb = new StringBuilder();
        double chunksPerSecond = ProducerWrite.Count / seconds;
        sb.AppendLine(
            $"{title}   frames {Update.Count / seconds:F1}/s   chunks {chunksPerSecond:F0}/s   " +
            $"points {chunksPerSecond * _pointsPerChunk / 1e6:F2} M/s");
        sb.AppendLine($"  {"(ms)",-22}{"mean",8}{"p50",8}{"p95",8}{"p99",8}{"max",8}");
        ProducerWrite.AppendSummary(sb);
        ProducerCycle.AppendSummary(sb);
        DispatcherWait.AppendSummary(sb);
        Update.AppendSummary(sb);
        Refresh.AppendSummary(sb);
        Paint.AppendSummary(sb);
        Frame.AppendSummary(sb);
        return sb.ToString();
    }
}
