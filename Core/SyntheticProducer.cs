using System.Diagnostics;

namespace HeatmapBench.Core;

/// <summary>
/// Stands in for the real acquisition thread. Every cycle it sends a burst of
/// chunks, one after another, then waits for the next cycle. The sequence is
/// the same on every run for a given seed.
/// </summary>
public sealed class SyntheticProducer : IDisposable
{
    private const int WaveLength = 1024;

    private readonly DataProcessor _processor;
    private readonly FrameStats _stats;
    private readonly int _width;
    private readonly int _height;
    private readonly int _pointCount;
    private readonly int _chunksPerCycle;
    private readonly int _cycleMs;
    private readonly bool _scattered;
    private readonly int _seed;
    private readonly double[] _wave = new double[WaveLength];
    private readonly Thread _thread;

    private volatile bool _stop;

    /// <param name="scattered">
    /// true: points land anywhere on the grid. false: points fall inside a small window that drifts across it.
    /// </param>
    public SyntheticProducer(
        HeatmapBuffer buffer,
        FrameStats stats,
        int pointCount,
        int chunksPerCycle,
        int cycleMs,
        bool scattered,
        int seed)
    {
        _processor = new DataProcessor(buffer);
        _stats = stats;
        _width = buffer.Width;
        _height = buffer.Height;
        _pointCount = pointCount;
        _chunksPerCycle = chunksPerCycle;
        _cycleMs = cycleMs;
        _scattered = scattered;
        _seed = seed;

        // Values sweep 0 to 100. A table, because a sine per point would cost
        // more than writing the point into the grid.
        for (int i = 0; i < WaveLength; i++)
        {
            _wave[i] = 50 + 50 * Math.Sin(2 * Math.PI * i / WaveLength);
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "Producer " + seed };
        _thread.Start();
    }

    private void Run()
    {
        // One set of arrays, reused for every chunk: nothing is allocated while running.
        double[] xs = new double[_pointCount];
        double[] ys = new double[_pointCount];
        double[] values = new double[_pointCount];
        var chunk = new DataChunk(_pointCount, xs, ys, values);

        ulong random = (ulong)_seed;
        long cycle = 0;
        long cycleTicks = _cycleMs * Stopwatch.Frequency / 1000;
        long deadline = Stopwatch.GetTimestamp();

        while (!_stop)
        {
            long cycleStart = Stopwatch.GetTimestamp();

            double originX = 0, originY = 0, spanX = _width, spanY = _height;
            if (!_scattered)
            {
                double t = cycle * 0.02;
                spanX = 2 * Math.Max(8, _width / 8.0);
                spanY = 2 * Math.Max(8, _height / 8.0);
                originX = _width * (0.5 + 0.4 * Math.Cos(t)) - spanX / 2;
                originY = _height * (0.5 + 0.4 * Math.Sin(1.7 * t)) - spanY / 2;
            }

            for (int c = 0; c < _chunksPerCycle; c++)
            {
                int phase = (int)((cycle * 7 + c) % WaveLength);
                random = Fill(random, phase, originX, originY, spanX, spanY, xs, ys, values);

                long t0 = Stopwatch.GetTimestamp();
                _processor.Process(chunk);
                _stats.ProducerWrite.Add(t0, Stopwatch.GetTimestamp());
            }

            long now = Stopwatch.GetTimestamp();
            _stats.ProducerCycle.Add(cycleStart, now);
            cycle++;

            // Cycles are scheduled from fixed deadlines, not "sleep for the rest",
            // so a sleep that overruns (Windows rounds sleeps up to its timer
            // tick) shortens the next wait and the average rate stays on target.
            deadline += cycleTicks;
            long remaining = deadline - now;
            if (remaining > 0)
            {
                Thread.Sleep((int)(remaining * 1000 / Stopwatch.Frequency));
            }
            else if (-remaining > cycleTicks)
            {
                // The work does not fit in a cycle; drop the backlog instead of chasing it.
                deadline = now;
            }
        }
    }

    /// <summary>Fills one chunk and returns the advanced random state (SplitMix64).</summary>
    private ulong Fill(
        ulong random, int phase,
        double originX, double originY, double spanX, double spanY,
        double[] xs, double[] ys, double[] values)
    {
        const double unit = 1.0 / 4294967296.0;
        double[] wave = _wave;

        for (int i = 0; i < xs.Length; i++)
        {
            random += 0x9E3779B97F4A7C15UL;
            ulong z = random;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;

            xs[i] = originX + (uint)(z >> 32) * unit * spanX;
            ys[i] = originY + (uint)z * unit * spanY;
            values[i] = wave[(i + phase) & (WaveLength - 1)];
        }

        return random;
    }

    public void Dispose()
    {
        _stop = true;
        _thread.Join(1000);
    }
}
