using FrameStudio.Core.Models;
using FrameStudio.Core.Codification.Gif.Encoder;
using FrameStudio.Core.Export;
using FrameStudio.Core.Projects;
using FrameStudio.Avalonia.ViewModels;
using System.Diagnostics;
using System.Globalization;

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
    public void RgbaFrameTransform_DrawsTextAndLeavesPixelsOutsideTheTextBoundsUntouched()
    {
        var size = new PixelSize(120, 60);
        var pixels = Enumerable.Repeat(new byte[] { 24, 40, 56, 255 }, size.Width * size.Height)
            .SelectMany(pixel => pixel).ToArray();
        var options = new TextOverlayOptions("A", X: 6, Y: 6, FontSize: 24, Color: Rgba32.FromRgb(255, 255, 255));

        var result = RgbaFrameTransform.DrawText(pixels, size, options);

        Assert.NotEqual(pixels, result);
        for (var y = 0; y < size.Height; y++)
        for (var x = 80; x < size.Width; x++)
        {
            var offset = (y * size.Width + x) * 4;
            Assert.Equal(pixels.AsSpan(offset, 4).ToArray(), result.AsSpan(offset, 4).ToArray());
        }
    }

    [Fact]
    public void RgbaFrameTransform_RejectsTextThatDoesNotFitInsideTheCanvas()
    {
        var options = new TextOverlayOptions("Too low", X: 2, Y: 56, FontSize: 24, Color: Rgba32.FromRgb(255, 255, 255));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RgbaFrameTransform.ValidateTextOverlay(new PixelSize(120, 60), options));
    }

    [Fact]
    public void RgbaFrameTransform_DrawsFreehandStrokeAndLeavesDistantPixelsUntouched()
    {
        var size = new PixelSize(40, 20);
        var pixels = Enumerable.Repeat(new byte[] { 8, 16, 24, 255 }, size.Width * size.Height)
            .SelectMany(pixel => pixel).ToArray();
        var options = new StrokeOverlayOptions(
            [new PixelCoordinate(3, 10), new PixelCoordinate(18, 10)],
            Thickness: 4,
            Color: Rgba32.FromRgb(255, 32, 32));

        var result = RgbaFrameTransform.DrawStroke(pixels, size, options);

        var strokeOffset = (10 * size.Width + 10) * 4;
        var untouchedOffset = (2 * size.Width + 32) * 4;
        Assert.True(result[strokeOffset] > pixels[strokeOffset]);
        Assert.Equal(pixels.AsSpan(untouchedOffset, 4).ToArray(), result.AsSpan(untouchedOffset, 4).ToArray());
    }

    [Fact]
    public void RgbaFrameTransform_RejectsStrokePointsOutsideTheCanvas()
    {
        var options = new StrokeOverlayOptions([new PixelCoordinate(3, 10), new PixelCoordinate(40, 10)], 4, Rgba32.FromRgb(255, 255, 255));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RgbaFrameTransform.ValidateStrokeOverlay(new PixelSize(40, 20), options));
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

    [Fact]
    public async Task ProjectArchive_TextOverlayAppliesToEveryFrameAndExportsToGif()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-text-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var sourcePath = Path.Combine(directory, "source.fsp");
        var sourceGifPath = Path.Combine(directory, "source.gif");
        var overlayGifPath = Path.Combine(directory, "annotated.gif");
        var size = new PixelSize(120, 60);
        var firstPixels = Enumerable.Repeat(new byte[] { 24, 40, 56, 255 }, size.Width * size.Height)
            .SelectMany(pixel => pixel).ToArray();
        var secondPixels = Enumerable.Repeat(new byte[] { 56, 40, 24, 255 }, size.Width * size.Height)
            .SelectMany(pixel => pixel).ToArray();

        try
        {
            await using (var writer = await FrameProjectArchiveWriter.CreateAsync(sourcePath, "Text overlay", size))
            {
                await writer.WriteFrameAsync(size, firstPixels, 70);
                await writer.WriteFrameAsync(size, secondPixels, 110);
                await writer.CompleteAsync();
            }

            var project = await FrameProjectArchiveReader.ReadProjectAsync(sourcePath);
            var editor = new EditorViewModel(sourcePath, project);
            var options = new TextOverlayOptions("A", X: 6, Y: 6, FontSize: 24, Color: Rgba32.FromRgb(255, 255, 255));
            Assert.True(await editor.AddTextOverlayAsync(options));
            Assert.True(editor.IsDirty);

            var annotatedFirst = await editor.ReadFrameAsync(0);
            var annotatedSecond = await editor.ReadFrameAsync(1);
            Assert.NotEqual(firstPixels, annotatedFirst);
            Assert.NotEqual(secondPixels, annotatedSecond);
            Assert.Equal(firstPixels.AsSpan(0, 4).ToArray(), annotatedFirst.AsSpan(0, 4).ToArray());
            Assert.Equal(secondPixels.AsSpan(0, 4).ToArray(), annotatedSecond.AsSpan(0, 4).ToArray());

            var exporter = new GifExportService();
            await exporter.ExportAsync(sourcePath, sourceGifPath);
            await editor.ExportGifAsync(overlayGifPath);
            var sourceGif = await File.ReadAllBytesAsync(sourceGifPath);
            var overlayGif = await File.ReadAllBytesAsync(overlayGifPath);
            Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(overlayGif, 0, 6));
            Assert.NotEqual(sourceGif, overlayGif);
            Assert.Equal(new[] { 70, 110 }, ReadGifMetadata(overlayGif).FrameDurationsMilliseconds);

            await editor.SaveProjectCommand.ExecuteAsync(null);
            Assert.False(editor.IsDirty);
            Assert.Equal("Project saved", editor.Status);
            var savedProject = await FrameProjectArchiveReader.ReadProjectAsync(sourcePath);
            Assert.Equal(new[] { 70, 110 }, savedProject.Frames.Select(frame => frame.DurationMilliseconds));
            Assert.Equal(annotatedFirst, await FrameProjectArchiveReader.ReadFrameRgbaAsync(sourcePath, savedProject, 0));
            Assert.Equal(annotatedSecond, await FrameProjectArchiveReader.ReadFrameRgbaAsync(sourcePath, savedProject, 1));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [FfmpegRequiredFact]
    public async Task EditorViewModel_ExportsEditedFramesToMp4WithVariableDurations()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-mp4-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "timing.fsp");
        var mp4Path = Path.Combine(directory, "timing.mp4");
        var size = new PixelSize(2, 2);
        var red = Enumerable.Repeat(new byte[] { 255, 0, 0, 255 }, 4).SelectMany(pixel => pixel).ToArray();
        var blue = Enumerable.Repeat(new byte[] { 0, 0, 255, 255 }, 4).SelectMany(pixel => pixel).ToArray();

        try
        {
            await using (var writer = await FrameProjectArchiveWriter.CreateAsync(projectPath, "MP4 timing", size))
            {
                await writer.WriteFrameAsync(size, red, 70);
                await writer.WriteFrameAsync(size, blue, 110);
                await writer.CompleteAsync();
            }

            var project = await FrameProjectArchiveReader.ReadProjectAsync(projectPath);
            var editor = new EditorViewModel(projectPath, project);
            editor.MoveSelectedFrameLaterCommand.Execute(null);
            editor.SelectedFrame = editor.Frames[0];
            editor.DurationText = "120";
            editor.ApplyDurationCommand.Execute(null);
            editor.SelectedFrame = editor.Frames[1];
            editor.DurationText = "80";
            editor.ApplyDurationCommand.Execute(null);
            await editor.ExportMp4Async(mp4Path);
            Assert.Equal("MP4 exported", editor.Status);

            var mp4Header = await File.ReadAllBytesAsync(mp4Path);
            Assert.True(mp4Header.Length > 12);
            Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(mp4Header, 4, 4));

            var startInfo = new ProcessStartInfo(FfmpegMp4ExportService.FfprobeExecutablePath ?? "ffprobe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in new[]
            {
                "-v", "error", "-select_streams", "v:0", "-show_entries",
                "frame=best_effort_timestamp_time:stream=duration", "-of", "json", mp4Path
            })
                startInfo.ArgumentList.Add(argument);

            using var probe = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start ffprobe.");
            var outputTask = probe.StandardOutput.ReadToEndAsync();
            var errorTask = probe.StandardError.ReadToEndAsync();
            await probe.WaitForExitAsync();
            Assert.True(probe.ExitCode == 0, await errorTask);
            using var metadata = System.Text.Json.JsonDocument.Parse(await outputTask);
            var timestamps = metadata.RootElement.GetProperty("frames").EnumerateArray()
                .Select(frame => double.Parse(frame.GetProperty("best_effort_timestamp_time").GetString()!, CultureInfo.InvariantCulture))
                .ToArray();
            Assert.Equal(3, timestamps.Length); // The final repeated sample closes the last frame's interval.
            Assert.Equal(120, Math.Round((timestamps[1] - timestamps[0]) * 1000));
            Assert.Equal(199, Math.Round(timestamps[2] * 1000));
            var duration = double.Parse(metadata.RootElement.GetProperty("streams")[0].GetProperty("duration").GetString()!,
                CultureInfo.InvariantCulture);
            Assert.Equal(200, Math.Round(duration * 1000));
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

public sealed class FfmpegRequiredFactAttribute : FactAttribute
{
    public FfmpegRequiredFactAttribute()
    {
        if (!FfmpegMp4ExportService.IsFfmpegAvailable || !FfmpegMp4ExportService.IsFfprobeAvailable)
            Skip = "FFmpeg and ffprobe are required for this MP4 timing integration test.";
    }
}
