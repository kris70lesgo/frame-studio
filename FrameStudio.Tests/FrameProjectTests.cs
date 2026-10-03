using FrameStudio.Core.Models;

namespace FrameStudio.Tests;

public sealed class FrameProjectTests
{
    [Theory]
    [InlineData(640, 480, false)]
    [InlineData(1, 1, false)]
    [InlineData(0, 480, true)]
    [InlineData(640, 0, true)]
    public void PixelSize_ReportsEmptyWhenAnAxisIsNotPositive(int width, int height, bool isEmpty)
    {
        Assert.Equal(isEmpty, new PixelSize(width, height).IsEmpty);
    }

    [Fact]
    public void FrameProjectDuration_SumsNonNegativeFrameDurations()
    {
        var frames = new[]
        {
            new FrameDescriptor(0, 80, new PixelRect(0, 0, 320, 180)),
            new FrameDescriptor(1, 120, new PixelRect(0, 0, 320, 180)),
            new FrameDescriptor(2, -30, new PixelRect(0, 0, 320, 180))
        };

        var project = new FrameProject("Demo", new PixelSize(320, 180), frames, DateTimeOffset.UnixEpoch);

        Assert.Equal(TimeSpan.FromMilliseconds(200), project.Duration);
    }

    [Fact]
    public void PixelRect_UsesCheckedBoundsToExposeOverflow()
    {
        var rectangle = new PixelRect(int.MaxValue, 0, 1, 1);

        Assert.Throws<OverflowException>(() => _ = rectangle.Right);
    }
}
