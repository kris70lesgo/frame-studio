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

        public void Dispose() => _ = NativeMethods.DestroyWindow(Handle);
    }

    private static class NativeMethods
    {
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
    }
}
