using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;
using FrameStudio.Platform.Windows;

namespace FrameStudio.Tests;

public sealed class WindowsDesktopFactAttribute : FactAttribute
{
    public WindowsDesktopFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires a Windows desktop session with a display device.";
    }
}

public sealed class WindowsCaptureIntegrationTests
{
    [WindowsDesktopFact]
    public async Task ScreenCapture_EnumeratesDisplayAndProducesFramesAcrossPauseResumeAndStop()
    {
        var service = new WindowsPlatformServices();
        var monitors = await service.GetMonitorsAsync();
        Assert.NotEmpty(monitors);

        var monitor = monitors.FirstOrDefault(candidate => candidate.IsPrimary) ?? monitors[0];
        Assert.True(monitor.Bounds.Width >= 16);
        Assert.True(monitor.Bounds.Height >= 16);

        var width = Math.Min(64, monitor.Bounds.Width);
        var height = Math.Min(64, monitor.Bounds.Height);
        var session = await service.StartAsync(new ScreenCaptureRequest(
            monitor.Id,
            new PixelRect(0, 0, width, height),
            FramesPerSecond: 10,
            CaptureCursor: false));
        using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var frames = session.ReadFramesAsync(readTimeout.Token).GetAsyncEnumerator();

        try
        {
            Assert.True(await frames.MoveNextAsync().AsTask().WaitAsync(readTimeout.Token));
            AssertCapturedFrame(frames.Current, width, height);

            await session.PauseAsync();
            var pausedFrameCount = 0;
            Task<bool>? pendingFrame = null;
            while (true)
            {
                pendingFrame = frames.MoveNextAsync().AsTask();
                using var silenceTimeout = new CancellationTokenSource();
                var silence = Task.Delay(TimeSpan.FromMilliseconds(600), silenceTimeout.Token);
                if (await Task.WhenAny(pendingFrame, silence) != pendingFrame)
                    break;

                silenceTimeout.Cancel();
                Assert.True(await pendingFrame);
                AssertCapturedFrame(frames.Current, width, height);
                pausedFrameCount++;
                Assert.InRange(pausedFrameCount, 0, 4);
            }

            await session.ResumeAsync();
            Assert.True(await pendingFrame!.WaitAsync(readTimeout.Token));
            AssertCapturedFrame(frames.Current, width, height);

            await session.StopAsync();
            while (await frames.MoveNextAsync())
                AssertCapturedFrame(frames.Current, width, height);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    private static void AssertCapturedFrame(CapturedFrame frame, int width, int height)
    {
        Assert.Equal(new PixelSize(width, height), frame.Size);
        Assert.Equal(width * height * 4, frame.RgbaPixels.Length);
        Assert.True(frame.DurationMilliseconds > 0);
    }
}
