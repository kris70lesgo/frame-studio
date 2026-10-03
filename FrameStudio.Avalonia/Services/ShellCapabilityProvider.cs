using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.Services;

/// <summary>Reports the current shell milestone honestly until platform adapters are registered.</summary>
public sealed class ShellCapabilityProvider : IPlatformCapabilityProvider
{
    private const string WindowsDetail = "Windows GDI capture backend is implemented; the recording workflow is being connected.";
    private const string OtherPlatformDetail = "The initial migration targets Windows. This capture source is not available in this build.";

    public IReadOnlyList<CaptureCapability> GetCaptureCapabilities()
    {
        var readiness = OperatingSystem.IsWindows() ? FeatureReadiness.Planned : FeatureReadiness.Unsupported;
        var detail = OperatingSystem.IsWindows() ? WindowsDetail : OtherPlatformDetail;

        return Enum.GetValues<CaptureSource>()
            .Select(source => new CaptureCapability(source, readiness, detail))
            .ToArray();
    }
}
