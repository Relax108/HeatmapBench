namespace HeatmapBench.Core;

public readonly struct DataChunk
{
    public readonly int Count;
    public readonly double[] X;
    public readonly double[] Y;
    public readonly double[] Value;

    public DataChunk(int count, double[] x, double[] y, double[] value)
    {
        Count = count;
        X = x;
        Y = y;
        Value = value;
    }
}
