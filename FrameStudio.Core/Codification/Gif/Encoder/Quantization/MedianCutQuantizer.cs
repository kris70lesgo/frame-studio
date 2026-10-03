#nullable disable

namespace FrameStudio.Core.Codification.Gif.Encoder.Quantization;

/// <summary>
/// Quantizes RGB pixels by recursively splitting color buckets at their weighted medians.
/// </summary>
/// <remarks>
/// This is an independent, clean-room implementation written for Frame Studio from the
/// general median-cut algorithm. It does not reuse code from the previously linked,
/// unlicensed repository.
/// </remarks>
public sealed class MedianCutQuantizer : Quantizer
{
    private readonly Dictionary<int, int> _colorCounts = new();

    public MedianCutQuantizer() : base(false)
    {
    }

    protected override void InitialQuantizePixel(Color pixel)
    {
        if (pixel.A == 0)
            return;

        var key = ToKey(pixel.R, pixel.G, pixel.B);
        _colorCounts[key] = _colorCounts.GetValueOrDefault(key) + 1;
    }

    internal override List<Color> BuildPalette()
    {
        MaxColorsWithTransparency = TransparentColor.HasValue ? MaxColors - 1 : MaxColors;

        var buckets = new List<ColorBucket>();
        if (_colorCounts.Count > 0)
            buckets.Add(new ColorBucket(_colorCounts.Select(pair => ColorSample.FromKey(pair.Key, pair.Value))));

        while (buckets.Count < MaxColorsWithTransparency)
        {
            var bucketIndex = FindBucketToSplit(buckets);
            if (bucketIndex < 0)
                break;

            var (lower, upper) = buckets[bucketIndex].SplitAtWeightedMedian();
            buckets[bucketIndex] = lower;
            buckets.Insert(bucketIndex + 1, upper);
        }

        var palette = buckets.Select(bucket => bucket.AverageColor()).ToList();

        // A valid GIF palette needs a usable entry even for a fully transparent frame.
        if (palette.Count == 0)
            palette.Add(Color.FromRgb(0, 0, 0));

        if (TransparentColor.HasValue)
            palette.Add(Color.FromArgb(0, TransparentColor.Value.R, TransparentColor.Value.G, TransparentColor.Value.B));

        return palette;
    }

    protected override byte QuantizePixel(Color pixel)
    {
        var opaquePaletteCount = Math.Min(MaxColorsWithTransparency, ColorTable.Count);
        if (opaquePaletteCount == 0)
            return 0;

        var bestIndex = 0;
        var bestDistance = long.MaxValue;

        for (var index = 0; index < opaquePaletteCount; index++)
        {
            var candidate = ColorTable[index];
            var redDifference = candidate.R - pixel.R;
            var greenDifference = candidate.G - pixel.G;
            var blueDifference = candidate.B - pixel.B;
            var distance = (long)redDifference * redDifference +
                           (long)greenDifference * greenDifference +
                           (long)blueDifference * blueDifference;

            if (distance >= bestDistance)
                continue;

            bestIndex = index;
            bestDistance = distance;
        }

        return (byte)bestIndex;
    }

    private static int FindBucketToSplit(IReadOnlyList<ColorBucket> buckets)
    {
        var selectedIndex = -1;

        for (var index = 0; index < buckets.Count; index++)
        {
            if (!buckets[index].CanSplit)
                continue;

            if (selectedIndex < 0 || buckets[index].ComparePriorityTo(buckets[selectedIndex]) > 0)
                selectedIndex = index;
        }

        return selectedIndex;
    }

    private static int ToKey(byte red, byte green, byte blue) => (red << 16) | (green << 8) | blue;

    private readonly record struct ColorSample(byte Red, byte Green, byte Blue, int Count)
    {
        public static ColorSample FromKey(int key, int count) => new(
            (byte)(key >> 16),
            (byte)(key >> 8),
            (byte)key,
            count);

        public int Component(int axis) => axis switch
        {
            0 => Red,
            1 => Green,
            _ => Blue
        };
    }

    private sealed class ColorBucket
    {
        private readonly List<ColorSample> _samples;

        public ColorBucket(IEnumerable<ColorSample> samples)
        {
            _samples = samples.ToList();
            TotalCount = _samples.Sum(sample => (long)sample.Count);
            RedRange = RangeOf(sample => sample.Red);
            GreenRange = RangeOf(sample => sample.Green);
            BlueRange = RangeOf(sample => sample.Blue);
        }

        public long TotalCount { get; }
        public int RedRange { get; }
        public int GreenRange { get; }
        public int BlueRange { get; }
        public int LargestRange => Math.Max(RedRange, Math.Max(GreenRange, BlueRange));
        public bool CanSplit => _samples.Count > 1;

        public int ComparePriorityTo(ColorBucket other)
        {
            var rangeComparison = LargestRange.CompareTo(other.LargestRange);
            return rangeComparison != 0 ? rangeComparison : TotalCount.CompareTo(other.TotalCount);
        }

        public (ColorBucket Lower, ColorBucket Upper) SplitAtWeightedMedian()
        {
            var axis = SelectLargestAxis();
            var ordered = _samples
                .OrderBy(sample => sample.Component(axis))
                .ThenBy(sample => sample.Red)
                .ThenBy(sample => sample.Green)
                .ThenBy(sample => sample.Blue)
                .ToList();

            var targetCount = TotalCount / 2;
            long accumulated = 0;
            var splitIndex = 1;

            for (var index = 0; index < ordered.Count - 1; index++)
            {
                accumulated += ordered[index].Count;
                if (accumulated >= targetCount)
                {
                    splitIndex = index + 1;
                    break;
                }
            }

            return (
                new ColorBucket(ordered.Take(splitIndex)),
                new ColorBucket(ordered.Skip(splitIndex)));
        }

        public Color AverageColor()
        {
            long red = 0;
            long green = 0;
            long blue = 0;

            foreach (var sample in _samples)
            {
                red += (long)sample.Red * sample.Count;
                green += (long)sample.Green * sample.Count;
                blue += (long)sample.Blue * sample.Count;
            }

            return Color.FromRgb(
                (byte)(red / TotalCount),
                (byte)(green / TotalCount),
                (byte)(blue / TotalCount));
        }

        private int SelectLargestAxis()
        {
            if (RedRange >= GreenRange && RedRange >= BlueRange)
                return 0;

            return GreenRange >= BlueRange ? 1 : 2;
        }

        private int RangeOf(Func<ColorSample, byte> component)
        {
            var minimum = byte.MaxValue;
            var maximum = byte.MinValue;

            foreach (var sample in _samples)
            {
                var value = component(sample);
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }

            return maximum - minimum;
        }
    }
}
