using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;
using FrameStudio.Platform.Windows;
using System.Diagnostics;
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
    public async Task ScreenCapture_EnumeratesDisplayAndStreamsFramesAcrossPauseResumeAndStop()
    {
        var service = new WindowsPlatformServices();
        var monitors = await service.GetMonitorsAsync();
        Assert.NotEmpty(monitors);

        var monitor = monitors.FirstOrDefault(candidate => candidate.IsPrimary) ?? monitors[0];
        Assert.True(monitor.Bounds.Width >= 16);
        Assert.True(monitor.Bounds.Height >= 16);

        var width = Math.Min(64, monitor.Bounds.Width);
        var height = Math.Min(64, monitor.Bounds.Height);
        var regionX = (monitor.Bounds.Width - width) / 2;
        var regionY = (monitor.Bounds.Height - height) / 2;

        // Hosted desktop composition can obscure synthetic windows; check stream shape and controls here.
        var session = await service.StartAsync(new ScreenCaptureRequest(
            monitor.Id,
            new PixelRect(regionX, regionY, width, height),
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

    [WindowsDesktopFact]
    public void WindowCapture_RendersTheSelectedWindowWhenAnotherWindowOverlapsIt()
    {
        const int size = 64;
        var service = new WindowsPlatformServices();
        var monitors = service.GetMonitorsAsync().AsTask().GetAwaiter().GetResult();
        var monitor = monitors.FirstOrDefault(candidate => candidate.IsPrimary) ?? monitors[0];
        Assert.True(monitor.Bounds.Width >= size + 16);
        Assert.True(monitor.Bounds.Height >= size + 16);
        var left = monitor.Bounds.Right - size - 16;
        var top = monitor.Bounds.Bottom - size - 16;
        using var targetWindow = NativeTestWindow.CreateWhiteMarker(left, top, size);
        using var overlappingWindow = NativeTestWindow.CreateBlackMarker(left, top, size);
        var expectedColor = NativeMethods.GetSysColor(NativeMethods.ColorWindow);
        Thread.Sleep(100);

        var session = service.StartAsync(new WindowCaptureRequest(
            targetWindow.Handle, FramesPerSecond: 10, CaptureCursor: false)).GetAwaiter().GetResult();
        using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var frames = session.ReadFramesAsync(readTimeout.Token).GetAsyncEnumerator();

        try
        {
            var nextFrame = frames.MoveNextAsync().AsTask();
            Assert.True(WaitWithMessagePump(nextFrame, readTimeout.Token));
            AssertCapturedFrame(frames.Current, size, size);
            AssertPixelColor(frames.Current, size, size / 2, size / 2, expectedColor);
            session.StopAsync().GetAwaiter().GetResult();
        }
        finally
        {
            frames.DisposeAsync().AsTask().GetAwaiter().GetResult();
            session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static T WaitWithMessagePump<T>(Task<T> task, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (NativeMethods.PeekMessage(out var message, IntPtr.Zero, 0, 0, NativeMethods.PmRemove))
            {
                _ = NativeMethods.TranslateMessage(ref message);
                _ = NativeMethods.DispatchMessage(ref message);
            }

            if (timer.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException("The window capture frame did not arrive before the test timeout.");
            Thread.Sleep(1);
        }

        return task.GetAwaiter().GetResult();
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

            _ = NativeMethods.ShowWindow(handle, NativeMethods.SwShowNoActivate);
            _ = NativeMethods.UpdateWindow(handle);
            return new NativeTestWindow(handle);
        }

        public static NativeTestWindow CreateBlackMarker(int x, int y, int size)
        {
            const uint ssBlackRect = 0x00000004;
            return CreateMarkerWindow("Frame Studio overlapping window", x, y, size, ssBlackRect);
        }

        private static NativeTestWindow CreateMarkerWindow(string title, int x, int y, int size, uint controlStyle)
        {
            const uint wsPopupVisible = 0x90000000;
            const uint wsExTopmostToolNoActivate = 0x08000088;
            var handle = NativeMethods.CreateWindowEx(wsExTopmostToolNoActivate, "STATIC", title,
                wsPopupVisible | controlStyle, x, y, size, size, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.GetModuleHandle(null), IntPtr.Zero);
            if (handle == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not create the native capture test marker.");

            _ = NativeMethods.ShowWindow(handle, NativeMethods.SwShowNoActivate);
            _ = NativeMethods.UpdateWindow(handle);
            return new NativeTestWindow(handle);
        }

        public void Dispose() => _ = NativeMethods.DestroyWindow(Handle);
    }

    private static class NativeMethods
    {
        // COLOR_WINDOW is the color used by the SS_WHITERECT test marker.
        internal const int ColorWindow = 5;
        internal const int SwShowNoActivate = 4;
        internal const uint PmRemove = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Message
        {
            public nint Window;
            public uint Id;
            public nuint WParam;
            public nint LParam;
            public uint Time;
            public int X;
            public int Y;
            public uint Private;
        }

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
        internal static extern bool ShowWindow(nint window, int command);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekMessage(out Message message, nint window, uint minMessage, uint maxMessage, uint removeMessage);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TranslateMessage(ref Message message);

        [DllImport("user32.dll")]
        internal static extern nint DispatchMessage(ref Message message);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateWindow(nint window);

        [DllImport("user32.dll")]
        internal static extern uint GetSysColor(int index);
    }
}
