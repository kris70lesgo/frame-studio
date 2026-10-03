using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.Services;

/// <summary>Reports implemented and planned capture features without claiming unverified runtime support.</summary>
public sealed class ShellCapabilityProvider : IPlatformCapabilityProvider
{
    private const string WindowsScreenDetail = "Windows screen recording is connected to the editor; validate capture on this Windows desktop before release.";
    private const string WindowsWindowDetail = "Window selection is not connected to the recording workflow yet.";
    private const string WindowsWebcamDetail = "Webcam recording is planned after the screen capture workflow is validated.";
    private const string WindowsSketchboardDetail = "Sketchboard recording is planned after the screen capture workflow is validated.";
    private const string OtherPlatformDetail = "The initial capture target is Windows; capture is unavailable on this platform.";

    public IReadOnlyList<CaptureCapability> GetCaptureCapabilities()
    {
        return Enum.GetValues<CaptureSource>()
            .Select(source => CreateCapability(source, OperatingSystem.IsWindows()))
            .ToArray();
    }

    private static CaptureCapability CreateCapability(CaptureSource source, bool isWindows)
    {
        if (!isWindows)
            return new CaptureCapability(source, FeatureReadiness.Unsupported, OtherPlatformDetail);

        return source switch
        {
            CaptureSource.Screen => new CaptureCapability(source, FeatureReadiness.Ready, WindowsScreenDetail),
            CaptureSource.Window => new CaptureCapability(source, FeatureReadiness.Planned, WindowsWindowDetail),
            CaptureSource.Webcam => new CaptureCapability(source, FeatureReadiness.Planned, WindowsWebcamDetail),
            CaptureSource.Sketchboard => new CaptureCapability(source, FeatureReadiness.Planned, WindowsSketchboardDetail),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown capture source.")
        };
    }
}
