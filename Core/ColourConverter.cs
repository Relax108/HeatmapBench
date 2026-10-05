namespace HeatmapBench.Core;

/// <summary>
/// Turns grid cells into pixels through a colour lookup table.
/// Kept free of WPF types so it can be tested and timed on its own.
/// </summary>
public static unsafe class ColourConverter
{
    /// <summary>Converts one rectangle of cells into 32-bit pixels at the same positions.</summary>
    /// <param name="cells">First cell of the grid, stored row by row.</param>
    /// <param name="pixels">First byte of the pixel buffer.</param>
    /// <param name="pixelStride">Bytes per pixel row.</param>
    /// <param name="scale">(lutSize - 1) / (max - min).</param>
    public static void Convert(
        double* cells, int gridWidth,
        byte* pixels, int pixelStride,
        int x, int y, int width, int height,
        uint* lut, int lutSize, double min, double scale)
    {
        uint last = (uint)(lutSize - 1);

        for (int row = y; row < y + height; row++)
        {
            double* src = cells + (long)row * gridWidth + x;
            uint* dst = (uint*)(pixels + (long)row * pixelStride) + x;

            for (int i = 0; i < width; i++)
            {
                int index = (int)((src[i] - min) * scale);
                if ((uint)index > last)
                {
                    index = index < 0 ? 0 : (int)last;
                }

                dst[i] = lut[index];
            }
        }
    }
}
