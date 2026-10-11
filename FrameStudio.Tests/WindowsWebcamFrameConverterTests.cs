using System.Runtime.InteropServices;
using FrameStudio.Core.Models;
using FrameStudio.Platform.Windows;

namespace FrameStudio.Tests;

public sealed class WindowsWebcamFrameConverterTests
{
    [Fact]
    public void ConvertsBgraRowsWithPaddingAndStartOffsetToOpaqueRgba()
    {
        byte[] source =
        [
            90, 91, // prefix before the first plane
            30, 20, 10, 2, 60, 50, 40, 3, 92, 93, // first 2-pixel row and padding
            70, 80, 90, 4, 120, 110, 100, 5
        ];
        var buffer = Marshal.AllocHGlobal(source.Length);
        try
        {
            Marshal.Copy(source, 0, buffer, source.Length);

            var rgba = WebcamFrameConverter.CopyBgraToRgba(
                buffer, (uint)source.Length, new PixelSize(2, 2), stride: 10, startIndex: 2);

            Assert.Equal(new byte[]
            {
                10, 20, 30, 255, 40, 50, 60, 255,
                90, 80, 70, 255, 100, 110, 120, 255
            }, rgba);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public void RejectsInsufficientCameraBuffer()
    {
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            Assert.Throws<InvalidDataException>(() => WebcamFrameConverter.CopyBgraToRgba(
                buffer, 4, new PixelSize(2, 1), stride: 8, startIndex: 0));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
