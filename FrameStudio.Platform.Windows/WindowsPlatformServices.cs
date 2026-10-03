using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FrameStudio.Core.Models;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Platform.Windows;

/// <summary>Windows monitor, window, and desktop-region capture using the original app's GDI path.</summary>
public sealed class WindowsPlatformServices : IScreenCaptureService, IWindowCaptureService, IMonitorService
{
    public ValueTask<IReadOnlyList<MonitorDescriptor>> GetMonitorsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        var monitors = new List<MonitorDescriptor>();
        Win32Native.MonitorEnumProc callback = (IntPtr monitor, IntPtr dc, ref Win32Native.Rect bounds, IntPtr data) =>
        {
            var info = new Win32Native.MonitorInfo { Size = (uint)Marshal.SizeOf<Win32Native.MonitorInfo>() };
            if (!Win32Native.GetMonitorInfo(monitor, ref info))
                return true;

            var dpi = 96u;
            try
            {
                if (Win32Native.GetDpiForMonitor(monitor, 0, out var xDpi, out _) == 0)
                    dpi = xDpi;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            monitors.Add(new MonitorDescriptor(
                monitor.ToInt64().ToString("X"),
                $"Display {monitors.Count + 1}",
                new PixelRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top),
                dpi / 96d,
                (info.Flags & Win32Native.MonitorInfoPrimary) != 0));
            return true;
        };

        if (!Win32Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate displays.");

        return ValueTask.FromResult<IReadOnlyList<MonitorDescriptor>>(monitors);
    }

    public ValueTask<IReadOnlyList<WindowDescriptor>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        var windows = new List<WindowDescriptor>();
        Win32Native.EnumWindowsProc callback = (IntPtr handle, IntPtr data) =>
        {
            if (!Win32Native.IsWindowVisible(handle))
                return true;

            var titleLength = Win32Native.GetWindowTextLength(handle);
            if (titleLength <= 0)
                return true;

            var title = new StringBuilder(titleLength + 1);
            _ = Win32Native.GetWindowText(handle, title, title.Capacity);
            if (title.Length == 0)
                return true;

            _ = Win32Native.GetWindowThreadProcessId(handle, out var processId);
            if (processId == Environment.ProcessId)
                return true;

            if (!Win32Native.GetWindowRect(handle, out var bounds))
                return true;

            windows.Add(new WindowDescriptor(handle,
                title.ToString(),
                new PixelRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top),
                TryGetProcessName(processId)));
            return true;
        };

        if (!Win32Native.EnumWindows(callback, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate windows.");

        return ValueTask.FromResult<IReadOnlyList<WindowDescriptor>>(windows);
    }

    public async ValueTask<IRecordingSession> StartAsync(ScreenCaptureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateFramesPerSecond(request.FramesPerSecond);
        if (request.Region.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(request), "Capture region must have a positive width and height.");

        var monitor = (await GetMonitorsAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(candidate => string.Equals(candidate.Id, request.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (monitor is null)
            throw new ArgumentException("The selected display is no longer available.", nameof(request));

        if (request.Region.X < 0 || request.Region.Y < 0 ||
            request.Region.Right > monitor.Bounds.Width || request.Region.Bottom > monitor.Bounds.Height)
            throw new ArgumentOutOfRangeException(nameof(request), "Capture region must fit inside the selected display.");

        var desktopRegion = new PixelRect(
            checked(monitor.Bounds.X + request.Region.X),
            checked(monitor.Bounds.Y + request.Region.Y),
            request.Region.Width,
            request.Region.Height);
        return CreateSession(desktopRegion, request.FramesPerSecond, request.CaptureCursor);
    }

    public ValueTask<IRecordingSession> StartAsync(WindowCaptureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateFramesPerSecond(request.FramesPerSecond);
        EnsureWindows();

        if (!Win32Native.IsWindow(request.WindowHandle) || !Win32Native.GetWindowRect(request.WindowHandle, out var bounds))
            throw new ArgumentException("The selected window is no longer available.", nameof(request));

        var rectangle = new PixelRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        ValidateRegion(rectangle);
        return ValueTask.FromResult<IRecordingSession>(CreateSession(rectangle, request.FramesPerSecond, request.CaptureCursor));
    }

    private static WindowsRecordingSession CreateSession(PixelRect region, int framesPerSecond, bool captureCursor)
    {
        EnsureWindows();
        ValidateRegion(region);
        return new WindowsRecordingSession(new Win32FrameGrabber(region, captureCursor), framesPerSecond);
    }

    private static void ValidateFramesPerSecond(int framesPerSecond)
    {
        if (framesPerSecond is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "Frame rate must be between 1 and 120 FPS.");
    }

    private static void ValidateRegion(PixelRect region)
    {
        if (region.IsEmpty || region.Width > 16_384 || region.Height > 16_384 || (long)region.Width * region.Height > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(region), "Capture bounds must fit within 16,384 pixels per side and 16 megapixels total.");
    }

    private static string? TryGetProcessName(uint processId)
    {
        try { return Process.GetProcessById((int)processId).ProcessName; }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows desktop capture is available only on Windows.");
    }
}
