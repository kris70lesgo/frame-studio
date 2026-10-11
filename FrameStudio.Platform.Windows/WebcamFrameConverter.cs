using System.Runtime.InteropServices;
using FrameStudio.Core.Models;

namespace FrameStudio.Platform.Windows;

internal static class WebcamFrameConverter
{
    public static byte[] CopyBgraToRgba(IntPtr source, uint capacity, PixelSize size, int stride, int startIndex)
    {
        if (source == IntPtr.Zero)
            throw new ArgumentException("Camera frame buffer pointer cannot be null.", nameof(source));
        if (size.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(size), "Camera frame dimensions must be positive.");

        var rowLength = checked(size.Width * 4);
        if (stride < rowLength || startIndex < 0)
            throw new InvalidDataException("Windows returned an invalid camera frame layout.");

        var requiredLength = checked((long)startIndex + ((long)(size.Height - 1) * stride) + rowLength);
        if (capacity < requiredLength)
            throw new InvalidDataException("Windows returned a camera buffer smaller than its declared frame layout.");

        var rgba = new byte[checked(rowLength * size.Height)];
        var bgraRow = new byte[rowLength];
        for (var y = 0; y < size.Height; y++)
        {
            Marshal.Copy(IntPtr.Add(source, checked(startIndex + (y * stride))), bgraRow, 0, rowLength);
            var destinationRow = y * rowLength;
            for (var x = 0; x < rowLength; x += 4)
            {
                rgba[destinationRow + x] = bgraRow[x + 2];
                rgba[destinationRow + x + 1] = bgraRow[x + 1];
                rgba[destinationRow + x + 2] = bgraRow[x];
                rgba[destinationRow + x + 3] = 255;
            }
        }
        return rgba;
    }
}
