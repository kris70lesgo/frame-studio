using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.Services;

/// <summary>Reports implemented and planned capture features without claiming unverified runtime support.</summary>
public sealed class ShellCapabilityProvider : IPlatformCapabilityProvider
{
    private const string WindowsScreenDetail = "Windows 10 version 2004 or later is required to keep Frame Studio out of captured frames. Validate capture on this Windows desktop before release.";
    private const string OlderWindowsDetail = "Screen recording requires Windows 10 version 2004 or later so the recorder controls can be excluded from captured frames.";
    private const string WindowsWindowDetail = "Window capture renders the selected window separately from overlapping windows. Some hardware-accelerated apps may return incomplete frames; use screen-region recording for those apps.";
    private const string WindowsWebcamDetail = "Webcam recording is planned after the screen capture workflow is validated.";
    private const string SketchboardDetail = "Records a fixed-size drawing canvas directly into a Frame Studio project.";
    private const string OtherPlatformDetail = "The initial capture target is Windows; capture is unavailable on this platform.";

    public IReadOnlyList<CaptureCapability> GetCaptureCapabilities()
    {
        var isWindows = OperatingSystem.IsWindows();
        var supportsCaptureExclusion = isWindows && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
        return Enum.GetValues<CaptureSource>()
            .Select(source => CreateCapability(source, isWindows, supportsCaptureExclusion))
            .ToArray();
    }

    private static CaptureCapability CreateCapability(CaptureSource source, bool isWindows, bool supportsCaptureExclusion)
    {
        if (source == CaptureSource.Sketchboard)
            return new CaptureCapability(source, FeatureReadiness.Ready, SketchboardDetail);

        if (!isWindows)
            return new CaptureCapability(source, FeatureReadiness.Unsupported, OtherPlatformDetail);

        if (source == CaptureSource.Screen && !supportsCaptureExclusion)
            return new CaptureCapability(source, FeatureReadiness.Unsupported, OlderWindowsDetail);

        return source switch
        {
            CaptureSource.Screen => new CaptureCapability(source, FeatureReadiness.Ready, WindowsScreenDetail),
            CaptureSource.Window => new CaptureCapability(source, FeatureReadiness.Ready, WindowsWindowDetail),
            CaptureSource.Webcam => new CaptureCapability(source, FeatureReadiness.Planned, WindowsWebcamDetail),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown capture source.")
        };
    }
}
