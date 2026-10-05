using System.Diagnostics;

namespace HeatmapBench.Core;

/// <summary>
/// Stands in for the real acquisition thread: one batch of points per cycle,
/// the same sequence on every run for a given seed.
/// </summary>
public sealed class SyntheticProducer : IDisposable
{
    private readonly DataProcessor _processor;
    private readonly TimingSeries _writeTiming;
    private readonly int _width;
    private readonly int _height;
    private readonly int _pointCount;
    private readonly int _cycleMs;
    private readonly bool _scattered;
    private readonly int _seed;
    private readonly Thread _thread;

    private volatile bool _stop;

    /// <param name="scattered">
    /// true: points land anywhere on the grid. false: points fall inside a small window that drifts across it.
    /// </param>
    public SyntheticProducer(
        HeatmapBuffer buffer,
        TimingSeries writeTiming,
        int pointCount,
        int cycleMs,
        bool scattered,
        int seed)
    {
        _processor = new DataProcessor(buffer);
        _writeTiming = writeTiming;
        _width = buffer.Width;
        _height = buffer.Height;
        _pointCount = pointCount;
        _cycleMs = cycleMs;
        _scattered = scattered;
        _seed = seed;

        _thread = new Thread(Run) { IsBackground = true, Name = "Producer " + seed };
        _thread.Start();
    }

    private void Run()
    {
        var rng = new Random(_seed);
        double[] xs = new double[_pointCount];
        double[] ys = new double[_pointCount];
        double[] values = new double[_pointCount];
        long batch = 0;

        while (!_stop)
        {
            long cycleStart = Stopwatch.GetTimestamp();

            Fill(rng, batch++, xs, ys, values);

            long t0 = Stopwatch.GetTimestamp();
            _processor.Process(new DataChunk(_pointCount, xs, ys, values));
            _writeTiming.Add(t0, Stopwatch.GetTimestamp());

            // Sleep out the rest of the cycle to imitate the acquisition time.
            int remaining = _cycleMs - (int)Stopwatch.GetElapsedTime(cycleStart).TotalMilliseconds;
            if (remaining > 0)
            {
                Thread.Sleep(remaining);
            }
        }
    }

    private void Fill(Random rng, long batch, double[] xs, double[] ys, double[] values)
    {
        double t = batch * 0.02;
        double centreX = _width * (0.5 + 0.4 * Math.Cos(t));
        double centreY = _height * (0.5 + 0.4 * Math.Sin(1.7 * t));
        double halfWidth = Math.Max(8, _width / 8.0);
        double halfHeight = Math.Max(8, _height / 8.0);

        for (int i = 0; i < xs.Length; i++)
        {
            if (_scattered)
            {
                xs[i] = rng.NextDouble() * _width;
                ys[i] = rng.NextDouble() * _height;
            }
            else
            {
                xs[i] = centreX + (rng.NextDouble() * 2 - 1) * halfWidth;
                ys[i] = centreY + (rng.NextDouble() * 2 - 1) * halfHeight;
            }

            values[i] = 50 + 50 * Math.Sin(3 * t + i * 0.01);
        }
    }

    public void Dispose()
    {
        _stop = true;
        _thread.Join(1000);
    }
}
