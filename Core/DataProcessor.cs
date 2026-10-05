namespace HeatmapBench.Core;

public sealed class DataProcessor
{
    private readonly HeatmapBuffer _buffer;

    public DataProcessor(HeatmapBuffer buffer)
    {
        _buffer = buffer;
    }

    public void Process(DataChunk chunk)
    {
        double[,] data = _buffer.Data;
        int width = _buffer.Width;
        int height = _buffer.Height;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        for (int i = 0; i < chunk.Count; i++)
        {
            int x = (int)chunk.X[i];
            int y = (int)chunk.Y[i];

            if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            {
                continue;
            }

            data[y, x] = chunk.Value[i];

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        // Cells are written before the region is published, so a render that
        // starts mid-batch is followed by another one covering the rest.
        if (maxX >= 0)
        {
            _buffer.MarkDirty(minX, minY, maxX, maxY);
        }
    }
}
