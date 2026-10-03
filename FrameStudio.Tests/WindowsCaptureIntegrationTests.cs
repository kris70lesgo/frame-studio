using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;
using FrameStudio.Platform.Windows;
using System.Runtime.InteropServices;

namespace FrameStudio.Tests;

public sealed class WindowsDesktopFactAttribute : FactAttribute
{
    public WindowsDesktopFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires a Windows desktop session with a display device.";
    }
}

public sealed class WindowsCaptureExclusionFactAttribute : FactAttribute
{
    public WindowsCaptureExclusionFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires Windows 10 version 2004 or later.";
        else if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            Skip = "Capture-window exclusion requires Windows 10 version 2004 or later.";
    }
}

public sealed class WindowsCaptureIntegrationTests
{
    [Fact]
    public async Task ScreenCapture_RejectsInvalidFrameRateBeforeDisplayEnumeration()
    {
        var service = new WindowsPlatformServices();
        var request = new ScreenCaptureRequest("unused", new PixelRect(0, 0, 1, 1), FramesPerSecond: 0,
            CaptureCursor: false);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await service.StartAsync(request));
    }

    [Fact]
    public async Task ScreenCapture_RejectsEmptyRegionBeforeDisplayEnumeration()
    {
        var service = new WindowsPlatformServices();
        var request = new ScreenCaptureRequest("unused", new PixelRect(0, 0, 0, 10), FramesPerSecond: 10,
            CaptureCursor: false);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await service.StartAsync(request));
    }

    [Fact]
    public async Task WindowCapture_RejectsInvalidFrameRateBeforeWindowsApiCalls()
    {
        var service = new WindowsPlatformServices();
        var request = new WindowCaptureRequest(IntPtr.Zero, FramesPerSecond: 121, CaptureCursor: false);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await service.StartAsync(request));
    }

    [WindowsCaptureExclusionFact]
    public void CaptureWindowExclusion_AppliesAndRestoresDisplayAffinity()
    {
        using var window = NativeTestWindow.Create();
        Assert.True(NativeMethods.GetWindowDisplayAffinity(window.Handle, out var originalAffinity));

        var exclusion = new WindowsPlatformServices().ExcludeFromCapture([window.Handle]);
        try
        {
            Assert.True(NativeMethods.GetWindowDisplayAffinity(window.Handle, out var excludedAffinity));
            Assert.Equal(0x00000011u, excludedAffinity);
        }
        finally
        {
            exclusion.Dispose();
        }

        Assert.True(NativeMethods.GetWindowDisplayAffinity(window.Handle, out var restoredAffinity));
        Assert.Equal(originalAffinity, restoredAffinity);
    }

    [WindowsCaptureExclusionFact]
    public void CaptureWindowExclusion_DisposeAfterWindowClosesIsSafe()
    {
        using var window = NativeTestWindow.Create();
        using var exclusion = new WindowsPlatformServices().ExcludeFromCapture([window.Handle]);

        window.Dispose();

        exclusion.Dispose();
    }

    [WindowsDesktopFact]
    public async Task ScreenCapture_EnumeratesDisplayAndCapturesExpectedPixelsAcrossPauseResumeAndStop()
    {
        var service = new WindowsPlatformServices();
        var monitors = await service.GetMonitorsAsync();
        Assert.NotEmpty(monitors);

        var monitor = monitors.FirstOrDefault(candidate => candidate.IsPrimary) ?? monitors[0];
        Assert.True(monitor.Bounds.Width >= 16);
        Assert.True(monitor.Bounds.Height >= 16);

        var width = Math.Min(64, monitor.Bounds.Width);
        var height = Math.Min(64, monitor.Bounds.Height);
        const int markerOffset = 2;
        const int markerSize = 8;
        const int sampleOffset = markerOffset + 4;
        using var markerWindow = NativeTestWindow.CreateWhiteMarker(
            monitor.Bounds.X + markerOffset,
            monitor.Bounds.Y + markerOffset,
            markerSize);
        var expectedMarkerColor = NativeMethods.GetSysColor(NativeMethods.ColorWindow);
        await Task.Delay(100);

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
            AssertPixelColor(frames.Current, width, sampleOffset, sampleOffset, expectedMarkerColor);

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

    private static void AssertPixelColor(CapturedFrame frame, int frameWidth, int x, int y, uint colorRef)
    {
        var pixels = frame.RgbaPixels.ToArray();
        var pixelIndex = checked((y * frameWidth + x) * 4);
        Assert.Equal((byte)(colorRef & 0xff), pixels[pixelIndex]);
        Assert.Equal((byte)((colorRef >> 8) & 0xff), pixels[pixelIndex + 1]);
        Assert.Equal((byte)((colorRef >> 16) & 0xff), pixels[pixelIndex + 2]);
        Assert.Equal(byte.MaxValue, pixels[pixelIndex + 3]);
    }

    private sealed class NativeTestWindow(nint handle) : IDisposable
    {
        public nint Handle { get; } = handle;

        public static NativeTestWindow Create()
        {
            var handle = NativeMethods.CreateWindowEx(0, "STATIC", "Frame Studio capture affinity test",
                0x80000000, 0, 0, 32, 32, IntPtr.Zero, IntPtr.Zero, NativeMethods.GetModuleHandle(null), IntPtr.Zero);
            if (handle == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not create a test window.");
            return new NativeTestWindow(handle);
        }

        public static NativeTestWindow CreateWhiteMarker(int x, int y, int size)
        {
            const uint wsPopupVisible = 0x90000000;
            const uint ssWhiteRect = 0x00000006;
            const uint wsExTopmostToolNoActivate = 0x08000088;
            var handle = NativeMethods.CreateWindowEx(wsExTopmostToolNoActivate, "STATIC", "Frame Studio capture pixel test",
                wsPopupVisible | ssWhiteRect, x, y, size, size, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.GetModuleHandle(null), IntPtr.Zero);
            if (handle == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not create the capture pixel marker.");

            _ = NativeMethods.UpdateWindow(handle);
            return new NativeTestWindow(handle);
        }

        public void Dispose() => _ = NativeMethods.DestroyWindow(Handle);
    }

    private static class NativeMethods
    {
        // COLOR_WINDOW is the color used by the SS_WHITERECT test marker.
        internal const int ColorWindow = 5;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint CreateWindowEx(uint extendedStyle, string className, string windowName, uint style,
            int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint GetModuleHandle(string? moduleName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowDisplayAffinity(nint window, out uint affinity);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyWindow(nint window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateWindow(nint window);

        [DllImport("user32.dll")]
        internal static extern uint GetSysColor(int index);
    }
}
