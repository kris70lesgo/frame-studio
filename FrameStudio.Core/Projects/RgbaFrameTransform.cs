using FrameStudio.Core.Models;

namespace FrameStudio.Core.Projects;

/// <summary>Framework-neutral pixel transforms used by the frame editor.</summary>
public static class RgbaFrameTransform
{
    public static byte[] Crop(ReadOnlySpan<byte> rgbaPixels, PixelSize sourceSize, PixelRect crop)
    {
        ValidatePixels(rgbaPixels, sourceSize);
        if (crop.Width <= 0 || crop.Height <= 0 || crop.X < 0 || crop.Y < 0 ||
            (long)crop.X + crop.Width > sourceSize.Width || (long)crop.Y + crop.Height > sourceSize.Height)
            throw new ArgumentOutOfRangeException(nameof(crop), "Crop bounds must fit inside the source frame.");

        var output = new byte[checked(crop.Width * crop.Height * 4)];
        var rowLength = checked(crop.Width * 4);
        for (var row = 0; row < crop.Height; row++)
        {
            var sourceOffset = checked(((crop.Y + row) * sourceSize.Width + crop.X) * 4);
            rgbaPixels.Slice(sourceOffset, rowLength).CopyTo(output.AsSpan(row * rowLength, rowLength));
        }

        return output;
    }

    public static byte[] ResizeNearestNeighbor(ReadOnlySpan<byte> rgbaPixels, PixelSize sourceSize, PixelSize targetSize)
    {
        ValidatePixels(rgbaPixels, sourceSize);
        FrameProjectArchiveWriter.ValidateCanvasSize(targetSize);

        var output = new byte[checked(targetSize.Width * targetSize.Height * 4)];
        for (var y = 0; y < targetSize.Height; y++)
        {
            var sourceY = (int)((long)y * sourceSize.Height / targetSize.Height);
            for (var x = 0; x < targetSize.Width; x++)
            {
                var sourceX = (int)((long)x * sourceSize.Width / targetSize.Width);
                var sourceOffset = checked((sourceY * sourceSize.Width + sourceX) * 4);
                var targetOffset = (y * targetSize.Width + x) * 4;
                rgbaPixels.Slice(sourceOffset, 4).CopyTo(output.AsSpan(targetOffset, 4));
            }
        }

        return output;
    }

    private static void ValidatePixels(ReadOnlySpan<byte> rgbaPixels, PixelSize size)
    {
        FrameProjectArchiveWriter.ValidateCanvasSize(size);
        var expectedLength = checked(size.Width * size.Height * 4);
        if (rgbaPixels.Length != expectedLength)
            throw new ArgumentException("RGBA pixel data length must match the source frame dimensions.", nameof(rgbaPixels));
    }
}
