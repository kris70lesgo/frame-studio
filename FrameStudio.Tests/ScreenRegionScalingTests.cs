using FrameStudio.Core.Models;

namespace FrameStudio.Tests;

public sealed class ScreenRegionScalingTests
{
    [Theory]
    [InlineData(1920, 1080, 1d, 1920d, 1080d)]
    [InlineData(2560, 1440, 1.25d, 2048d, 1152d)]
    [InlineData(2880, 1800, 1.5d, 1920d, 1200d)]
    [InlineData(3840, 2160, 2d, 1920d, 1080d)]
    public void ToLogicalSize_UsesDisplayScaling(
        int pixelWidth,
        int pixelHeight,
        double scaleFactor,
        double expectedWidth,
        double expectedHeight)
    {
        var logicalSize = ScreenRegionScaling.ToLogicalSize(new PixelSize(pixelWidth, pixelHeight), scaleFactor);

        Assert.Equal(expectedWidth, logicalSize.Width);
        Assert.Equal(expectedHeight, logicalSize.Height);
    }

    [Theory]
    [InlineData(1d, 20d, 30d, 20, 30)]
    [InlineData(1.25d, 20d, 30d, 25, 38)]
    [InlineData(1.5d, 20d, 30d, 30, 45)]
    [InlineData(0.75d, 20d, 30d, 15, 22)]
    public void ToPixelSize_ConvertsLogicalDimensions(double scaleFactor, double width, double height, int expectedWidth, int expectedHeight)
    {
        var pixelSize = ScreenRegionScaling.ToPixelSize(width, height, scaleFactor);

        Assert.Equal(expectedWidth, pixelSize.Width);
        Assert.Equal(expectedHeight, pixelSize.Height);
    }

    [Fact]
    public void ToPixelRegion_ConvertsAndClipsSelectionToDisplay()
    {
        var region = ScreenRegionScaling.ToPixelRegion(
            new PixelSize(2880, 1800),
            1.5,
            logicalLeft: 100,
            logicalTop: 40,
            logicalRight: 2000,
            logicalBottom: 1400);

        Assert.Equal(new PixelRect(150, 60, 2730, 1740), region);
    }

    [Fact]
    public void ToPixelRegion_RejectsNonFiniteOrNonPositiveScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScreenRegionScaling.ToPixelRegion(new PixelSize(1920, 1080), 0, 0, 0, 10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScreenRegionScaling.ToPixelRegion(new PixelSize(1920, 1080), double.NaN, 0, 0, 10, 10));
    }
}
