using FrameStudio.Avalonia.Services;
using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Tests;

public sealed class SketchboardRecordingSessionTests
{
    [Fact]
    public async Task PublishesFramesInOrderAndOmitsPausedIntervals()
    {
        var size = new PixelSize(1, 1);
        byte[] firstPixels = [20, 40, 60, 255];
        byte[] secondPixels = [80, 100, 120, 255];
        var session = new SketchboardRecordingSession(size);
        var readTask = ReadFramesAsync(session);

        Assert.True(session.PublishFrame(new CapturedFrame(size, firstPixels, 100, DateTimeOffset.UtcNow)));
        firstPixels[0] = 250;
        await session.PauseAsync();
        Assert.True(session.IsPaused);
        Assert.False(session.PublishFrame(new CapturedFrame(size, new byte[] { 1, 2, 3, 255 }, 100, DateTimeOffset.UtcNow)));
        await session.ResumeAsync();
        Assert.False(session.IsPaused);
        Assert.True(session.PublishFrame(new CapturedFrame(size, secondPixels, 200, DateTimeOffset.UtcNow)));
        await session.StopAsync();

        var frames = await readTask;
        Assert.Equal(2, frames.Count);
        Assert.Equal(100, frames[0].DurationMilliseconds);
        Assert.Equal(new byte[] { 20, 40, 60, 255 }, frames[0].RgbaPixels.ToArray());
        Assert.Equal(200, frames[1].DurationMilliseconds);
        Assert.Equal(secondPixels, frames[1].RgbaPixels.ToArray());
        Assert.True(session.IsStopped);
        Assert.False(session.PublishFrame(new CapturedFrame(size, secondPixels, 100, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void RejectsFramesOutsideTheConfiguredCanvas()
    {
        var session = new SketchboardRecordingSession(new PixelSize(1, 1));

        Assert.Throws<ArgumentException>(() => session.PublishFrame(
            new CapturedFrame(new PixelSize(2, 1), new byte[8], 100, DateTimeOffset.UtcNow)));
        Assert.Throws<ArgumentException>(() => session.PublishFrame(
            new CapturedFrame(new PixelSize(1, 1), new byte[3], 100, DateTimeOffset.UtcNow)));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.PublishFrame(
            new CapturedFrame(new PixelSize(1, 1), new byte[4], 0, DateTimeOffset.UtcNow)));
    }

    private static async Task<List<CapturedFrame>> ReadFramesAsync(SketchboardRecordingSession session)
    {
        var frames = new List<CapturedFrame>();
        await foreach (var frame in session.ReadFramesAsync())
            frames.Add(frame);
        return frames;
    }
}
