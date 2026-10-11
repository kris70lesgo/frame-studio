namespace FrameStudio.Core.Codification.Gif.Encoder.Quantization;

/// <summary>Maps an RGBA row-major image to a palette with Floyd–Steinberg error diffusion.</summary>
public static class FloydSteinbergDither
{
    public static byte[] Quantize(ReadOnlySpan<byte> rgbaPixels, int width, IReadOnlyList<Color> palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (rgbaPixels.IsEmpty || rgbaPixels.Length % checked(width * 4) != 0)
            throw new ArgumentException("The RGBA buffer must contain complete rows for the requested width.", nameof(rgbaPixels));
        if (palette.Count == 0)
            throw new ArgumentException("A palette must contain at least one color.", nameof(palette));

        var opaqueIndices = Enumerable.Range(0, palette.Count)
            .Where(index => palette[index].A != 0)
            .ToArray();
        if (opaqueIndices.Length == 0)
            opaqueIndices = [0];

        var transparentIndex = -1;
        for (var index = 0; index < palette.Count; index++)
        {
            if (palette[index].A == 0)
            {
                transparentIndex = index;
                break;
            }
        }
        if (transparentIndex < 0)
            transparentIndex = palette.Count - 1;

        var height = rgbaPixels.Length / checked(width * 4);
        var result = new byte[checked(width * height)];
        var currentErrors = new double[checked((width + 2) * 3)];
        var nextErrors = new double[currentErrors.Length];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixelIndex = y * width + x;
                var sourceOffset = pixelIndex * 4;
                var errorOffset = (x + 1) * 3;

                if (rgbaPixels[sourceOffset + 3] == 0)
                {
                    result[pixelIndex] = (byte)transparentIndex;
                    currentErrors[errorOffset] = 0;
                    currentErrors[errorOffset + 1] = 0;
                    currentErrors[errorOffset + 2] = 0;
                    continue;
                }

                var red = Clamp(rgbaPixels[sourceOffset] + currentErrors[errorOffset]);
                var green = Clamp(rgbaPixels[sourceOffset + 1] + currentErrors[errorOffset + 1]);
                var blue = Clamp(rgbaPixels[sourceOffset + 2] + currentErrors[errorOffset + 2]);
                var paletteIndex = FindNearest(red, green, blue, palette, opaqueIndices);
                result[pixelIndex] = (byte)paletteIndex;

                var chosen = palette[paletteIndex];
                var redError = red - chosen.R;
                var greenError = green - chosen.G;
                var blueError = blue - chosen.B;

                AddError(currentErrors, errorOffset + 3, redError, greenError, blueError, 7d / 16);
                AddError(nextErrors, errorOffset - 3, redError, greenError, blueError, 3d / 16);
                AddError(nextErrors, errorOffset, redError, greenError, blueError, 5d / 16);
                AddError(nextErrors, errorOffset + 3, redError, greenError, blueError, 1d / 16);
            }

            (currentErrors, nextErrors) = (nextErrors, currentErrors);
            Array.Clear(nextErrors);
        }

        return result;
    }

    private static int FindNearest(double red, double green, double blue, IReadOnlyList<Color> palette,
        IReadOnlyList<int> opaqueIndices)
    {
        var bestIndex = opaqueIndices[0];
        var bestDistance = double.MaxValue;
        foreach (var index in opaqueIndices)
        {
            var color = palette[index];
            var redDifference = red - color.R;
            var greenDifference = green - color.G;
            var blueDifference = blue - color.B;
            var distance = redDifference * redDifference + greenDifference * greenDifference + blueDifference * blueDifference;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestIndex = index;
        }

        return bestIndex;
    }

    private static void AddError(double[] target, int offset, double red, double green, double blue, double weight)
    {
        target[offset] += red * weight;
        target[offset + 1] += green * weight;
        target[offset + 2] += blue * weight;
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, byte.MaxValue);
}
