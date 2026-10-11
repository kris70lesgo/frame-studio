namespace FrameStudio.Core.Models;

/// <summary>Converts screen-selector coordinates between logical UI units and device pixels.</summary>
public static class ScreenRegionScaling
{
    /// <summary>Returns the logical size needed to cover a display with the given pixel size.</summary>
    public static (double Width, double Height) ToLogicalSize(PixelSize displaySize, double scaleFactor)
    {
        ValidateDisplayAndScale(displaySize, scaleFactor);
        return (displaySize.Width / scaleFactor, displaySize.Height / scaleFactor);
    }

    /// <summary>Converts a logical size into a rounded device-pixel size.</summary>
    public static PixelSize ToPixelSize(double logicalWidth, double logicalHeight, double scaleFactor)
    {
        ValidateScale(scaleFactor);
        ValidateLogicalCoordinate(logicalWidth, nameof(logicalWidth));
        ValidateLogicalCoordinate(logicalHeight, nameof(logicalHeight));
        return new PixelSize(ToPixelLength(logicalWidth, scaleFactor), ToPixelLength(logicalHeight, scaleFactor));
    }

    /// <summary>
    /// Converts a logical rectangle relative to a display into a device-pixel rectangle,
    /// clipping all edges to the display bounds.
    /// </summary>
    public static PixelRect ToPixelRegion(
        PixelSize displaySize,
        double scaleFactor,
        double logicalLeft,
        double logicalTop,
        double logicalRight,
        double logicalBottom)
    {
        ValidateDisplayAndScale(displaySize, scaleFactor);
        ValidateLogicalCoordinate(logicalLeft, nameof(logicalLeft));
        ValidateLogicalCoordinate(logicalTop, nameof(logicalTop));
        ValidateLogicalCoordinate(logicalRight, nameof(logicalRight));
        ValidateLogicalCoordinate(logicalBottom, nameof(logicalBottom));

        if (logicalRight < logicalLeft || logicalBottom < logicalTop)
            throw new ArgumentException("The right and bottom edges must not precede the left and top edges.");

        var left = ToClampedPixelCoordinate(logicalLeft, scaleFactor, displaySize.Width);
        var top = ToClampedPixelCoordinate(logicalTop, scaleFactor, displaySize.Height);
        var right = ToClampedPixelCoordinate(logicalRight, scaleFactor, displaySize.Width);
        var bottom = ToClampedPixelCoordinate(logicalBottom, scaleFactor, displaySize.Height);

        return new PixelRect(left, top, right - left, bottom - top);
    }

    private static int ToClampedPixelCoordinate(double logicalCoordinate, double scaleFactor, int maximumPixels)
    {
        var maximumLogicalCoordinate = maximumPixels / scaleFactor;
        var clampedLogicalCoordinate = Math.Clamp(logicalCoordinate, 0d, maximumLogicalCoordinate);
        return Math.Clamp((int)Math.Round(clampedLogicalCoordinate * scaleFactor), 0, maximumPixels);
    }

    private static int ToPixelLength(double logicalLength, double scaleFactor)
    {
        var pixels = logicalLength * scaleFactor;
        if (pixels > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(logicalLength), logicalLength, "The scaled size exceeds the supported pixel range.");

        return (int)Math.Round(pixels);
    }

    private static void ValidateDisplayAndScale(PixelSize displaySize, double scaleFactor)
    {
        ValidateScale(scaleFactor);
        if (displaySize.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(displaySize), displaySize, "Display dimensions must be positive.");
    }

    private static void ValidateScale(double scaleFactor)
    {
        if (!double.IsFinite(scaleFactor) || scaleFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(scaleFactor), scaleFactor, "The scale factor must be a finite positive value.");
    }

    private static void ValidateLogicalCoordinate(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "Logical coordinates and lengths must be finite and non-negative.");
    }
}
