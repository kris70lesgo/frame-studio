using FrameStudio.Avalonia.Services;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Tests;

public sealed class ShellCapabilityProviderTests
{
    [Fact]
    public void CapabilityReportMatchesTheCurrentPlatformAndImplementedWorkflow()
    {
        var capabilities = new ShellCapabilityProvider().GetCaptureCapabilities();

        Assert.Equal(Enum.GetValues<CaptureSource>(), capabilities.Select(capability => capability.Source));
        if (OperatingSystem.IsWindows())
        {
            var screen = capabilities.Single(item => item.Source == CaptureSource.Screen);
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                Assert.Equal(FeatureReadiness.Ready, screen.Readiness);
                Assert.Contains("Validate capture", screen.Detail);
            }
            else
            {
                Assert.Equal(FeatureReadiness.Unsupported, screen.Readiness);
                Assert.Contains("Windows 10 version 2004", screen.Detail);
            }
            Assert.Equal(FeatureReadiness.Ready, capabilities.Single(item => item.Source == CaptureSource.Window).Readiness);
            Assert.All(capabilities.Where(item => item.Source is CaptureSource.Webcam or CaptureSource.Sketchboard),
                item => Assert.Equal(FeatureReadiness.Planned, item.Readiness));
        }
        else
        {
            Assert.All(capabilities, item => Assert.Equal(FeatureReadiness.Unsupported, item.Readiness));
        }
    }
}
