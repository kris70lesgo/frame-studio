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
            Assert.Equal(FeatureReadiness.Ready, capabilities.Single(item => item.Source == CaptureSource.Screen).Readiness);
            Assert.Contains("validate capture", capabilities.Single(item => item.Source == CaptureSource.Screen).Detail);
            Assert.All(capabilities.Where(item => item.Source != CaptureSource.Screen),
                item => Assert.Equal(FeatureReadiness.Planned, item.Readiness));
        }
        else
        {
            Assert.All(capabilities, item => Assert.Equal(FeatureReadiness.Unsupported, item.Readiness));
        }
    }
}
