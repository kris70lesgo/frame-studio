using FrameStudio.Core.Codification.Gif.Encoder;
using FrameStudio.Core.Codification.Gif.Encoder.Quantization;
using FrameStudio.Core.Models;

namespace FrameStudio.Tests;

public sealed class MedianCutQuantizerTests
{
    [Fact]
    public void Quantize_SplitsAlongTheWidestColorChannel()
    {
        var pixels = Rgba(
            (60, 0, 0, 255),
            (60, 10, 0, 255),
            (60, 240, 0, 255),
            (60, 250, 0, 255));
        var quantizer = new MedianCutQuantizer { MaxColors = 2 };

        var indexes = quantizer.Quantize(pixels);

        Assert.Equal([0, 0, 1, 1], indexes);
        Assert.Equal(2, quantizer.ColorTable.Count);
        Assert.Equal((byte)60, quantizer.ColorTable[0].R);
        Assert.Equal((byte)5, quantizer.ColorTable[0].G);
        Assert.Equal((byte)245, quantizer.ColorTable[1].G);
    }

    [Fact]
    public void Quantize_UsesWeightedAveragesAndReservesTheTransparentEntry()
    {
        var pixels = Rgba(
            (0, 0, 0, 255),
            (0, 0, 0, 255),
            (0, 0, 0, 255),
            (100, 0, 0, 255),
            (100, 0, 0, 255),
            (200, 0, 0, 255),
            (0, 0, 0, 0));
        var transparent = FrameStudio.Core.Models.Rgba32.FromArgb(0, 1, 2, 3);
        var quantizer = new MedianCutQuantizer
        {
            MaxColors = 3,
            TransparentColor = transparent
        };

        var indexes = quantizer.Quantize(pixels);

        Assert.Equal(3, quantizer.ColorTable.Count);
        Assert.Equal(FrameStudio.Core.Models.Rgba32.FromRgb(0, 0, 0), quantizer.ColorTable[0]);
        Assert.Equal(FrameStudio.Core.Models.Rgba32.FromRgb(133, 0, 0), quantizer.ColorTable[1]);
        Assert.Equal(transparent, quantizer.ColorTable[2]);
        Assert.Equal((byte)2, indexes[^1]);
        Assert.Equal((byte)2, quantizer.TransparentColorIndex);
    }

    [Fact]
    public void TransparentColorIndex_UsesTheActualPalettePositionWhenPaletteHasFewerThanMaxColors()
    {
        var transparent = FrameStudio.Core.Models.Rgba32.FromArgb(0, 9, 8, 7);
        var quantizer = new MedianCutQuantizer
        {
            MaxColors = 16,
            TransparentColor = transparent
        };

        quantizer.Quantize(Rgba(
            (10, 20, 30, 255),
            (90, 80, 70, 255),
            (0, 0, 0, 0)));

        Assert.Equal(3, quantizer.ColorTable.Count);
        Assert.Equal((byte)2, quantizer.TransparentColorIndex);
    }

    [Fact]
    public void Quantize_AllTransparentPixelsProducesAUsableTransparentPalette()
    {
        var transparent = FrameStudio.Core.Models.Rgba32.FromArgb(0, 4, 5, 6);
        var quantizer = new MedianCutQuantizer
        {
            MaxColors = 2,
            TransparentColor = transparent
        };

        var indexes = quantizer.Quantize(Rgba((0, 0, 0, 0), (20, 30, 40, 0)));

        Assert.Equal([1, 1], indexes);
        Assert.Equal(transparent, quantizer.ColorTable[^1]);
        Assert.NotEmpty(quantizer.ColorTable);
    }

    [Fact]
    public void GifFile_UsesTheMedianCutPaletteForEncodedPixels()
    {
        var pixels = Rgba(
            (60, 0, 0, 255),
            (60, 10, 0, 255),
            (60, 240, 0, 255),
            (60, 250, 0, 255));
        using var output = new MemoryStream();
        using (var encoder = new GifFile(output)
               {
                   QuantizationType = ColorQuantizationTypes.MedianCut,
                   MaximumNumberColor = 2,
                   RepeatCount = -1
               })
        {
            encoder.AddFrame(pixels, new PixelRect(0, 0, 4, 1), delay: 40, isLastFrame: true);
        }

        var decoded = GifLzwRoundTripTests.DecodeFrameRgba(output.ToArray()).Single();

        Assert.Equal((byte)60, decoded[0]);
        Assert.Equal((byte)5, decoded[1]);
        Assert.Equal((byte)245, decoded[9]);
    }

    private static byte[] Rgba(params (byte R, byte G, byte B, byte A)[] pixels) =>
        pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray();
}
