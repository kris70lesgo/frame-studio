using FrameStudio.Core.Models;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace FrameStudio.Core.Projects;

/// <summary>Framework-neutral pixel transforms used by the frame editor.</summary>
public static class RgbaFrameTransform
{
    public static byte[] Crop(ReadOnlySpan<byte> rgbaPixels, PixelSize sourceSize, PixelRect crop)
    {
        ValidatePixels(rgbaPixels, sourceSize);
        if (crop.Width <= 0 || crop.Height <= 0 || crop.X < 0 || crop.Y < 0 ||
            (long)crop.X + crop.Width > sourceSize.Width || (long)crop.Y + crop.Height > sourceSize.Height)
            throw new ArgumentOutOfRangeException(nameof(crop), "Crop bounds must fit inside the source frame.");

        var output = new byte[checked(crop.Width * crop.Height * 4)];
        var rowLength = checked(crop.Width * 4);
        for (var row = 0; row < crop.Height; row++)
        {
            var sourceOffset = checked(((crop.Y + row) * sourceSize.Width + crop.X) * 4);
            rgbaPixels.Slice(sourceOffset, rowLength).CopyTo(output.AsSpan(row * rowLength, rowLength));
        }

        return output;
    }

    public static byte[] ResizeNearestNeighbor(ReadOnlySpan<byte> rgbaPixels, PixelSize sourceSize, PixelSize targetSize)
    {
        ValidatePixels(rgbaPixels, sourceSize);
        FrameProjectArchiveWriter.ValidateCanvasSize(targetSize);

        var output = new byte[checked(targetSize.Width * targetSize.Height * 4)];
        for (var y = 0; y < targetSize.Height; y++)
        {
            var sourceY = (int)((long)y * sourceSize.Height / targetSize.Height);
            for (var x = 0; x < targetSize.Width; x++)
            {
                var sourceX = (int)((long)x * sourceSize.Width / targetSize.Width);
                var sourceOffset = checked((sourceY * sourceSize.Width + sourceX) * 4);
                var targetOffset = (y * targetSize.Width + x) * 4;
                rgbaPixels.Slice(sourceOffset, 4).CopyTo(output.AsSpan(targetOffset, 4));
            }
        }

        return output;
    }

    public static byte[] DrawText(ReadOnlySpan<byte> rgbaPixels, PixelSize sourceSize, TextOverlayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePixels(rgbaPixels, sourceSize);
        ValidateTextOverlay(sourceSize, options);

        var output = rgbaPixels.ToArray();
        var imageInfo = new SKImageInfo(sourceSize.Width, sourceSize.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(imageInfo);
        var bitmapPixels = bitmap.GetPixels();
        if (bitmapPixels == IntPtr.Zero)
            throw new InvalidOperationException("Could not allocate a text overlay bitmap.");

        var rowLength = checked(sourceSize.Width * 4);
        for (var row = 0; row < sourceSize.Height; row++)
            Marshal.Copy(output, row * rowLength, IntPtr.Add(bitmapPixels, row * bitmap.RowBytes), rowLength);

        using var canvas = new SKCanvas(bitmap);
        using var font = new SKFont(SKTypeface.Default, options.FontSize);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(options.Color.R, options.Color.G, options.Color.B, options.Color.A)
        };
        font.GetFontMetrics(out var metrics);
        var lineHeight = Math.Max(options.FontSize, metrics.Descent - metrics.Ascent + metrics.Leading);
        var firstBaseline = options.Y - metrics.Ascent;
        var lines = NormalizeLines(options.Text);
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Length == 0)
                continue;

            var baseline = firstBaseline + index * lineHeight;
            canvas.DrawText(lines[index], options.X, baseline, SKTextAlign.Left, font, paint);
        }

        for (var row = 0; row < sourceSize.Height; row++)
            Marshal.Copy(IntPtr.Add(bitmapPixels, row * bitmap.RowBytes), output, row * rowLength, rowLength);

        return output;
    }

    public static byte[] DrawStroke(ReadOnlySpan<byte> rgbaPixels, PixelSize sourceSize, StrokeOverlayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePixels(rgbaPixels, sourceSize);
        ValidateStrokeOverlay(sourceSize, options);

        var output = rgbaPixels.ToArray();
        var imageInfo = new SKImageInfo(sourceSize.Width, sourceSize.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(imageInfo);
        var bitmapPixels = bitmap.GetPixels();
        if (bitmapPixels == IntPtr.Zero)
            throw new InvalidOperationException("Could not allocate a stroke overlay bitmap.");

        var rowLength = checked(sourceSize.Width * 4);
        for (var row = 0; row < sourceSize.Height; row++)
            Marshal.Copy(output, row * rowLength, IntPtr.Add(bitmapPixels, row * bitmap.RowBytes), rowLength);

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = options.Thickness,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = new SKColor(options.Color.R, options.Color.G, options.Color.B, options.Color.A)
        };

        if (options.Points.Count == 1)
        {
            var point = options.Points[0];
            canvas.DrawPoint(point.X, point.Y, paint);
        }
        else
        {
            using var path = new SKPath();
            path.MoveTo(options.Points[0].X, options.Points[0].Y);
            foreach (var point in options.Points.Skip(1))
                path.LineTo(point.X, point.Y);
            canvas.DrawPath(path, paint);
        }

        for (var row = 0; row < sourceSize.Height; row++)
            Marshal.Copy(IntPtr.Add(bitmapPixels, row * bitmap.RowBytes), output, row * rowLength, rowLength);

        return output;
    }

    public static void ValidateTextOverlay(PixelSize canvasSize, TextOverlayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        FrameProjectArchiveWriter.ValidateCanvasSize(canvasSize);
        if (string.IsNullOrWhiteSpace(options.Text) || options.Text.Length > 512 ||
            options.Text.Any(character => char.IsControl(character) && character is not ('\r' or '\n')))
            throw new ArgumentException("Text must contain 1 to 512 printable characters.", nameof(options));
        if (options.FontSize is < 6 or > 256)
            throw new ArgumentOutOfRangeException(nameof(options), "Text size must be between 6 and 256 pixels.");
        if (options.X < 0 || options.Y < 0 || options.X >= canvasSize.Width || options.Y >= canvasSize.Height)
            throw new ArgumentOutOfRangeException(nameof(options), "Text position must be inside the project canvas.");

        var lines = NormalizeLines(options.Text);
        if (lines.Length > 8 || lines.Any(line => line.Length > 256))
            throw new ArgumentException("Text can use at most 8 lines and 256 characters per line.", nameof(options));

        using var font = new SKFont(SKTypeface.Default, options.FontSize);
        using var paint = new SKPaint { IsAntialias = true };
        font.GetFontMetrics(out var metrics);
        var lineHeight = Math.Max(options.FontSize, metrics.Descent - metrics.Ascent + metrics.Leading);
        var finalBottom = options.Y - metrics.Ascent + (lines.Length - 1) * lineHeight + metrics.Descent;
        if (finalBottom > canvasSize.Height)
            throw new ArgumentOutOfRangeException(nameof(options), "Text does not fit vertically inside the project canvas.");

        foreach (var line in lines)
        {
            if (font.MeasureText(line, paint) > canvasSize.Width - options.X)
                throw new ArgumentOutOfRangeException(nameof(options), "Text does not fit horizontally inside the project canvas.");
        }
    }

    public static void ValidateStrokeOverlay(PixelSize canvasSize, StrokeOverlayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Points);
        FrameProjectArchiveWriter.ValidateCanvasSize(canvasSize);
        if (options.Points.Count is < 1 or > 4_096)
            throw new ArgumentException("A stroke must contain between 1 and 4,096 points.", nameof(options));
        if (options.Thickness is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(options), "Stroke thickness must be between 1 and 128 pixels.");
        if (options.Points.Any(point => point.X < 0 || point.Y < 0 || point.X >= canvasSize.Width || point.Y >= canvasSize.Height))
            throw new ArgumentOutOfRangeException(nameof(options), "Every stroke point must be inside the project canvas.");
    }

    private static string[] NormalizeLines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n').Split('\n');

    private static void ValidatePixels(ReadOnlySpan<byte> rgbaPixels, PixelSize size)
    {
        FrameProjectArchiveWriter.ValidateCanvasSize(size);
        var expectedLength = checked(size.Width * size.Height * 4);
        if (rgbaPixels.Length != expectedLength)
            throw new ArgumentException("RGBA pixel data length must match the source frame dimensions.", nameof(rgbaPixels));
    }
}
