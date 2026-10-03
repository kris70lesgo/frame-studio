using FrameStudio.Core.Models;
using FrameStudio.Core.Codification.Gif.Encoder;
using FrameStudio.Core.Export;
using FrameStudio.Core.Projects;

namespace FrameStudio.Tests;

public sealed class FrameProjectTests
{
    [Theory]
    [InlineData(640, 480, false)]
    [InlineData(1, 1, false)]
    [InlineData(0, 480, true)]
    [InlineData(640, 0, true)]
    public void PixelSize_ReportsEmptyWhenAnAxisIsNotPositive(int width, int height, bool isEmpty)
    {
        Assert.Equal(isEmpty, new PixelSize(width, height).IsEmpty);
    }

    [Fact]
    public void FrameProjectDuration_SumsNonNegativeFrameDurations()
    {
        var frames = new[]
        {
            new FrameDescriptor(0, 80, new PixelRect(0, 0, 320, 180)),
            new FrameDescriptor(1, 120, new PixelRect(0, 0, 320, 180)),
            new FrameDescriptor(2, -30, new PixelRect(0, 0, 320, 180))
        };

        var project = new FrameProject("Demo", new PixelSize(320, 180), frames, DateTimeOffset.UnixEpoch);

        Assert.Equal(TimeSpan.FromMilliseconds(200), project.Duration);
    }

    [Fact]
    public void FrameProjectDuration_DoesNotOverflowAtIntMilliseconds()
    {
        var frames = new[]
        {
            new FrameDescriptor(0, int.MaxValue, new PixelRect(0, 0, 1, 1)),
            new FrameDescriptor(1, int.MaxValue, new PixelRect(0, 0, 1, 1))
        };
        var project = new FrameProject("Long", new PixelSize(1, 1), frames, DateTimeOffset.UnixEpoch);

        Assert.Equal(TimeSpan.FromMilliseconds(2L * int.MaxValue), project.Duration);
    }

    [Fact]
    public void PixelRect_UsesCheckedBoundsToExposeOverflow()
    {
        var rectangle = new PixelRect(int.MaxValue, 0, 1, 1);

        Assert.Throws<OverflowException>(() => _ = rectangle.Right);
    }

    [Fact]
    public void RgbaFrameTransform_CropsAndResizesPixelsWithoutChangingChannels()
    {
        byte[] pixels =
        [
            255, 0, 0, 255, 0, 255, 0, 255,
            0, 0, 255, 255, 255, 255, 255, 255
        ];

        var cropped = RgbaFrameTransform.Crop(pixels, new PixelSize(2, 2), new PixelRect(1, 0, 1, 2));
        Assert.Equal<byte>([0, 255, 0, 255, 255, 255, 255, 255], cropped);

        var resized = RgbaFrameTransform.ResizeNearestNeighbor(cropped, new PixelSize(1, 2), new PixelSize(2, 2));
        Assert.Equal<byte>([0, 255, 0, 255, 0, 255, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255], resized);
    }

    [Fact]
    public void GifFile_EncodesRgbaFramesIntoAnAnimatedGif()
    {
        using var output = new MemoryStream();
        using (var encoder = new GifFile(output))
        {
            encoder.AddFrame(
                [
                    255, 0, 0, 255, 0, 255, 0, 255,
                    0, 0, 255, 255, 255, 255, 255, 255
                ],
                new PixelRect(0, 0, 2, 2),
                delay: 80,
                isLastFrame: true);
        }

        var gif = output.ToArray();
        Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
        Assert.Contains((byte)0x2c, gif); // Image descriptor.
        Assert.Equal(0x3b, gif[^1]); // GIF trailer.
    }

    [Fact]
    public void GifFile_RejectsPixelBuffersThatDoNotMatchTheFrameSize()
    {
        using var output = new MemoryStream();
        using var encoder = new GifFile(output);

        Assert.Throws<ArgumentException>(() => encoder.AddFrame([255, 0, 0, 255], new PixelRect(0, 0, 2, 2)));
    }

    [Fact]
    public async Task ProjectArchive_RoundTripsEditedSelectionAndExportsGif()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "capture.fsp");
        var gifPath = Path.Combine(directory, "capture.gif");
        byte[] redFrame = [255, 0, 0, 255, 255, 0, 0, 255];
        byte[] blueFrame = [0, 0, 255, 255, 0, 0, 255, 255];

        try
        {
            await using (var writer = await FrameProjectArchiveWriter.CreateAsync(projectPath, "Capture", new PixelSize(2, 1)))
            {
                await writer.WriteFrameAsync(new PixelSize(2, 1), redFrame, 75);
                await writer.WriteFrameAsync(new PixelSize(2, 1), blueFrame, 125);
                var writtenProject = await writer.CompleteAsync();

                Assert.Equal(TimeSpan.FromMilliseconds(200), writtenProject.Duration);
            }

            var loadedProject = await FrameProjectArchiveReader.ReadProjectAsync(projectPath);
            var loadedPixels = await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, loadedProject, 1);
            Assert.Equal(blueFrame, loadedPixels);

            var editedProject = await FrameProjectArchiveEditor.RewriteAsync(projectPath, projectPath, loadedProject,
            [
                new ProjectFrameReference(1, 90),
                new ProjectFrameReference(0, 110),
                new ProjectFrameReference(1, 130)
            ]);
            Assert.Equal(3, editedProject.Frames.Count);
            Assert.Equal(TimeSpan.FromMilliseconds(330), editedProject.Duration);
            Assert.Equal(blueFrame, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, editedProject, 2));

            var crop = await FrameProjectArchiveEditor.CropAsync(projectPath, projectPath, editedProject,
            [
                new ProjectFrameReference(0, 90),
                new ProjectFrameReference(1, 110),
                new ProjectFrameReference(2, 130)
            ], new PixelRect(1, 0, 1, 1));
            Assert.Equal(new PixelSize(1, 1), crop.CanvasSize);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, crop, 0));
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, crop, 1));
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, crop, 2));

            var resizedProject = await FrameProjectArchiveEditor.ResizeAsync(projectPath, projectPath, crop,
            [
                new ProjectFrameReference(0, 90),
                new ProjectFrameReference(1, 110),
                new ProjectFrameReference(2, 130)
            ], new PixelSize(2, 2));
            Assert.Equal(new PixelSize(2, 2), resizedProject.CanvasSize);
            Assert.Equal(TimeSpan.FromMilliseconds(330), resizedProject.Duration);
            var resizedBlue = await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, resizedProject, 0);
            var resizedRed = await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, resizedProject, 1);
            var resizedBlueAgain = await FrameProjectArchiveReader.ReadFrameRgbaAsync(projectPath, resizedProject, 2);
            Assert.Equal<byte>([0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255], resizedBlue);
            Assert.Equal<byte>([255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255], resizedRed);
            Assert.Equal(resizedBlue, resizedBlueAgain);

            var editorSelection = new[]
            {
                new ProjectFrameReference(1, 120), // Red frame moved first with a new duration.
                new ProjectFrameReference(0, 70),  // Blue frame moved second.
                new ProjectFrameReference(1, 90)   // Duplicate the red frame at the end.
            };
            await new GifExportService().ExportSelectionAsync(projectPath, gifPath, editorSelection,
                new GifExportOptions(RepeatCount: 0));
            var gif = await File.ReadAllBytesAsync(gifPath);
            Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
            Assert.Equal(0x3b, gif[^1]);

            var gifMetadata = ReadGifMetadata(gif);
            Assert.Equal(new PixelSize(2, 2), gifMetadata.CanvasSize);
            Assert.Equal(new[] { 120, 70, 90 }, gifMetadata.FrameDurationsMilliseconds);
            Assert.Equal(0, gifMetadata.RepeatCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    internal static GifMetadata ReadGifMetadata(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        var header = ReadExactly(reader, 6);
        if (System.Text.Encoding.ASCII.GetString(header) != "GIF89a")
            throw new InvalidDataException("Expected a GIF89a export.");

        var canvasSize = new PixelSize(reader.ReadUInt16(), reader.ReadUInt16());
        var logicalScreenFlags = reader.ReadByte();
        _ = ReadExactly(reader, 2); // Background color index and pixel aspect ratio.
        if ((logicalScreenFlags & 0x80) != 0)
            SkipExactly(stream, 3 * (1 << ((logicalScreenFlags & 0x07) + 1)));

        var delays = new List<int>();
        var nextFrameDelay = 0;
        var repeatCount = -1;
        var foundTrailer = false;

        while (stream.Position < stream.Length)
        {
            switch (reader.ReadByte())
            {
                case 0x21:
                    var label = reader.ReadByte();
                    if (label == 0xf9)
                    {
                        var controlSize = reader.ReadByte();
                        if (controlSize != 4)
                            throw new InvalidDataException("GIF graphic control extension has an invalid size.");

                        var control = ReadExactly(reader, controlSize);
                        nextFrameDelay = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(control.AsSpan(1, 2)) * 10;
                        if (reader.ReadByte() != 0)
                            throw new InvalidDataException("GIF graphic control extension is missing its terminator.");
                    }
                    else if (label == 0xff)
                    {
                        var applicationHeaderSize = reader.ReadByte();
                        var applicationHeader = ReadExactly(reader, applicationHeaderSize);
                        var applicationData = ReadSubBlocks(reader);
                        if (System.Text.Encoding.ASCII.GetString(applicationHeader) == "NETSCAPE2.0" &&
                            applicationData.Length >= 3 && applicationData[0] == 1)
                            repeatCount = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(applicationData.AsSpan(1, 2));
                    }
                    else if (label == 0x01)
                    {
                        var extensionHeaderSize = reader.ReadByte();
                        SkipExactly(stream, extensionHeaderSize);
                        SkipSubBlocks(reader);
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
                    _ = reader.ReadByte(); // LZW minimum code size.
                    SkipSubBlocks(reader);
                    delays.Add(nextFrameDelay);
                    nextFrameDelay = 0;
                    break;

                case 0x3b:
                    foundTrailer = true;
                    break;

                default:
                    throw new InvalidDataException("GIF contains an unknown block marker.");
            }

            if (foundTrailer)
                break;
        }

        if (!foundTrailer || delays.Count == 0)
            throw new InvalidDataException("GIF is missing image frames or a trailer.");

        return new GifMetadata(canvasSize, delays, repeatCount);
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

    internal sealed record GifMetadata(PixelSize CanvasSize, IReadOnlyList<int> FrameDurationsMilliseconds, int RepeatCount);
}
