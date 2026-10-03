using FrameStudio.Core.Models;

namespace FrameStudio.Platform.Abstractions;

public enum CaptureSource
{
    Screen,
    Window,
    Webcam,
    Sketchboard
}

public enum FeatureReadiness
{
    Ready,
    Planned,
    Unsupported
}

public sealed record CaptureCapability(
    CaptureSource Source,
    FeatureReadiness Readiness,
    string Detail);

public sealed record MonitorDescriptor(
    string Id,
    string Name,
    PixelRect Bounds,
    double ScaleFactor,
    bool IsPrimary);

public sealed record WindowDescriptor(
    nint Handle,
    string Title,
    PixelRect Bounds,
    string? ProcessName);

public sealed record WebcamDeviceDescriptor(string Id, string Name);

public sealed record CapturedFrame(
    PixelSize Size,
    ReadOnlyMemory<byte> RgbaPixels,
    int DurationMilliseconds,
    DateTimeOffset CapturedAt);

public sealed record ScreenCaptureRequest(
    string MonitorId,
    PixelRect Region,
    int FramesPerSecond,
    bool CaptureCursor);

public sealed record WindowCaptureRequest(
    nint WindowHandle,
    int FramesPerSecond,
    bool CaptureCursor);

public interface IPlatformCapabilityProvider
{
    IReadOnlyList<CaptureCapability> GetCaptureCapabilities();
}

public interface IRecordingSession : IAsyncDisposable
{
    IAsyncEnumerable<CapturedFrame> ReadFramesAsync(CancellationToken cancellationToken = default);
    ValueTask PauseAsync(CancellationToken cancellationToken = default);
    ValueTask ResumeAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

public interface IScreenCaptureService
{
    ValueTask<IReadOnlyList<MonitorDescriptor>> GetMonitorsAsync(CancellationToken cancellationToken = default);
    ValueTask<IRecordingSession> StartAsync(ScreenCaptureRequest request, CancellationToken cancellationToken = default);
}

public interface IWindowCaptureService
{
    ValueTask<IReadOnlyList<WindowDescriptor>> GetWindowsAsync(CancellationToken cancellationToken = default);
    ValueTask<IRecordingSession> StartAsync(WindowCaptureRequest request, CancellationToken cancellationToken = default);
}

public interface IWebcamService
{
    ValueTask<IReadOnlyList<WebcamDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default);
    ValueTask<IRecordingSession> StartAsync(string deviceId, int framesPerSecond, CancellationToken cancellationToken = default);
}

public interface IMonitorService
{
    ValueTask<IReadOnlyList<MonitorDescriptor>> GetMonitorsAsync(CancellationToken cancellationToken = default);
}

public interface IGlobalHotkeyService
{
    IDisposable Register(string gesture, Action callback);
}

public interface IClipboardService
{
    ValueTask SetTextAsync(string text, CancellationToken cancellationToken = default);
    ValueTask SetFilePathAsync(string path, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    ValueTask ShowAsync(string title, string message, CancellationToken cancellationToken = default);
}

public interface IFileDialogService
{
    ValueTask<string?> OpenProjectAsync(CancellationToken cancellationToken = default);
    ValueTask<string?> SaveProjectAsync(string suggestedName, CancellationToken cancellationToken = default);
    ValueTask<string?> SaveExportAsync(string suggestedName, string extension, CancellationToken cancellationToken = default);
}

public interface IPlatformPermissions
{
    ValueTask<bool> RequestScreenRecordingAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> RequestCameraAsync(CancellationToken cancellationToken = default);
}
