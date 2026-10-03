using FrameStudio.Core.Codification.Gif.Encoder;
using FrameStudio.Core.Codification.Gif.Encoder.Quantization;
using FrameStudio.Core.Models;

namespace FrameStudio.Tests;

public sealed class GifLzwRoundTripTests
{
    [Fact]
    public void GifFile_RoundTripsQuantizedPixelsAcrossLzwCodeWidthGrowth()
    {
        const int width = 64;
        const int height = 64;
        var pixels = new byte[width * height * 4];
        new Random(68127).NextBytes(pixels);
        for (var index = 3; index < pixels.Length; index += 4)
            pixels[index] = byte.MaxValue;

        var expectedIndexes = new OctreeQuantizer { MaxColors = 256 }.Quantize(pixels);
        using var output = new MemoryStream();
        using (var encoder = new GifFile(output) { RepeatCount = -1 })
            encoder.AddFrame(pixels, new PixelRect(0, 0, width, height), delay: 40, isLastFrame: true);

        var decoded = DecodeFirstImage(output.ToArray(), width * height);

        Assert.Equal(expectedIndexes, decoded.PixelIndexes);
        Assert.True(decoded.MaximumCodeWidth >= 10, $"Expected LZW code-width growth, observed {decoded.MaximumCodeWidth} bits.");
    }

    private static DecodedImage DecodeFirstImage(byte[] gif, int expectedPixelCount)
    {
        using var stream = new MemoryStream(gif);
        using var reader = new BinaryReader(stream);
        if (System.Text.Encoding.ASCII.GetString(ReadExactly(reader, 6)) != "GIF89a")
            throw new InvalidDataException("Expected a GIF89a image.");

        _ = ReadExactly(reader, 4); // Logical canvas size.
        var screenFlags = reader.ReadByte();
        _ = ReadExactly(reader, 2); // Background index and pixel aspect ratio.
        if ((screenFlags & 0x80) != 0)
            SkipExactly(stream, 3 * (1 << ((screenFlags & 0x07) + 1)));

        while (stream.Position < stream.Length)
        {
            switch (reader.ReadByte())
            {
                case 0x21:
                    var label = reader.ReadByte();
                    if (label == 0xf9)
                    {
                        var blockSize = reader.ReadByte();
                        _ = ReadExactly(reader, blockSize);
                        if (reader.ReadByte() != 0)
                            throw new InvalidDataException("GIF graphic control extension is missing its terminator.");
                    }
                    else
                    {
                        SkipSubBlocks(reader);
                    }
                    break;

                case 0x2c:
                    var descriptor = ReadExactly(reader, 9);
                    var imageFlags = descriptor[8];
                    if ((imageFlags & 0x80) != 0)
                        SkipExactly(stream, 3 * (1 << ((imageFlags & 0x07) + 1)));

                    var minimumCodeSize = reader.ReadByte();
                    var compressedData = ReadSubBlocks(reader);
                    var (indexes, maximumCodeWidth) = DecodeLzw(compressedData, minimumCodeSize, expectedPixelCount);
                    return new DecodedImage(indexes, maximumCodeWidth);

                case 0x3b:
                    throw new InvalidDataException("GIF ended before an image descriptor was found.");

                default:
                    throw new InvalidDataException("GIF contains an unknown block marker.");
            }
        }

        throw new EndOfStreamException("GIF is missing an image descriptor.");
    }

    private static (byte[] PixelIndexes, int MaximumCodeWidth) DecodeLzw(byte[] data, int minimumCodeSize, int expectedPixelCount)
    {
        if (minimumCodeSize is < 2 or > 8)
            throw new InvalidDataException("GIF LZW minimum code size is outside the supported range.");

        var clearCode = 1 << minimumCodeSize;
        var endCode = clearCode + 1;
        var dictionary = new byte[1 << 12][];
        var output = new byte[expectedPixelCount];
        var outputLength = 0;
        var nextCode = clearCode + 2;
        var codeWidth = minimumCodeSize + 1;
        var maximumCodeWidth = codeWidth;
        byte[]? previous = null;
        var bitOffset = 0;

        while (true)
        {
            var code = ReadCode(data, ref bitOffset, codeWidth);
            if (code == clearCode)
            {
                Array.Clear(dictionary);
                for (var index = 0; index < clearCode; index++)
                    dictionary[index] = [(byte)index];

                nextCode = clearCode + 2;
                codeWidth = minimumCodeSize + 1;
                previous = null;
                continue;
            }

            if (code == endCode)
                break;

            byte[] current;
            if (code < nextCode && dictionary[code] is { } knownSequence)
                current = knownSequence;
            else if (code == nextCode && previous is not null)
                current = Append(previous, previous[0]);
            else
                throw new InvalidDataException("GIF LZW stream contains an invalid dictionary reference.");

            if (outputLength + current.Length > output.Length)
                throw new InvalidDataException("GIF LZW stream expands beyond the image dimensions.");
            current.CopyTo(output, outputLength);
            outputLength += current.Length;

            if (previous is not null && nextCode < dictionary.Length)
            {
                dictionary[nextCode++] = Append(previous, current[0]);
                if (nextCode == 1 << codeWidth && codeWidth < 12)
                {
                    codeWidth++;
                    maximumCodeWidth = Math.Max(maximumCodeWidth, codeWidth);
                }
            }

            previous = current;
        }

        if (outputLength != expectedPixelCount)
            throw new InvalidDataException($"GIF LZW stream decoded {outputLength} pixels; expected {expectedPixelCount}.");

        return (output, maximumCodeWidth);
    }

    private static int ReadCode(byte[] data, ref int bitOffset, int width)
    {
        if (bitOffset + width > data.Length * 8)
            throw new EndOfStreamException("GIF LZW data ended before its end code.");

        var code = 0;
        for (var bit = 0; bit < width; bit++, bitOffset++)
            code |= ((data[bitOffset >> 3] >> (bitOffset & 7)) & 1) << bit;
        return code;
    }

    private static byte[] Append(byte[] prefix, byte value)
    {
        var result = new byte[prefix.Length + 1];
        prefix.CopyTo(result, 0);
        result[^1] = value;
        return result;
    }

    private static byte[] ReadSubBlocks(BinaryReader reader)
    {
        using var output = new MemoryStream();
        while (true)
        {
            var size = reader.ReadByte();
            if (size == 0)
                return output.ToArray();
            output.Write(ReadExactly(reader, size));
        }
    }

    private static void SkipSubBlocks(BinaryReader reader)
    {
        while (true)
        {
            var size = reader.ReadByte();
            if (size == 0)
                return;
            SkipExactly(reader.BaseStream, size);
        }
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        var data = reader.ReadBytes(count);
        return data.Length == count ? data : throw new EndOfStreamException();
    }

    private static void SkipExactly(Stream stream, int count)
    {
        if (count < 0 || stream.Length - stream.Position < count)
            throw new EndOfStreamException();
        stream.Position += count;
    }

    private sealed record DecodedImage(byte[] PixelIndexes, int MaximumCodeWidth);
}
