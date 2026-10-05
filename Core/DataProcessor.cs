namespace HeatmapBench.Core;

public sealed class DataProcessor
{
    private readonly HeatmapBuffer _buffer;

    public DataProcessor(HeatmapBuffer buffer)
    {
        _buffer = buffer;
    }

    public unsafe void Process(DataChunk chunk)
    {
        int count = chunk.Count;
        if (count < 0 || count > chunk.X.Length || count > chunk.Y.Length || count > chunk.Value.Length)
        {
            throw new ArgumentException("Count is larger than the chunk's arrays.", nameof(chunk));
        }

        int width = _buffer.Width;
        int height = _buffer.Height;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        // The grid is addressed as one flat block: indexing double[,] with
        // [y, x] costs two range checks and an offset calculation per point,
        // and the range test below already guarantees the cell exists.
        fixed (double* grid = _buffer.Data)
        fixed (double* xs = chunk.X, ys = chunk.Y, values = chunk.Value)
        {
            for (int i = 0; i < count; i++)
            {
                int x = (int)xs[i];
                int y = (int)ys[i];

                if ((uint)x >= (uint)width || (uint)y >= (uint)height)
                {
                    continue;
                }

                grid[(long)y * width + x] = values[i];

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        // Cells are written before the region is published, so a render that
        // starts mid-batch is followed by another one covering the rest.
        if (maxX >= 0)
        {
            _buffer.MarkDirty(minX, minY, maxX, maxY);
        }
    }
}
