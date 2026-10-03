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
    public async Task ProjectArchive_RoundTripsFramesAndExportsGif()
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

            await new GifExportService().ExportAsync(projectPath, gifPath, new GifExportOptions(RepeatCount: 0));
            var gif = await File.ReadAllBytesAsync(gifPath);
            Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
            Assert.Equal(0x3b, gif[^1]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
