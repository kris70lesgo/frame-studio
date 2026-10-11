using System.Text.Json;
using FrameStudio.Core.Codification.Gif.Encoder;

namespace FrameStudio.Avalonia.Services;

public sealed record FrameStudioPreferences(
    bool IsDarkTheme = false,
    int DefaultFramesPerSecond = 30,
    bool CaptureCursor = true,
    bool LoopEditorPreview = true,
    int GifRepeatCount = 0,
    int GifMaximumColors = 256,
    ColorQuantizationTypes GifQuantization = ColorQuantizationTypes.Octree,
    int GifScalePercent = 100,
    int Mp4Crf = 23,
    int Mp4ScalePercent = 100,
    string? FfmpegExecutablePath = null,
    int GifFramesPerSecond = 0,
    bool GifDither = false,
    int Mp4FramesPerSecond = 0)
{
    public static IReadOnlyList<int> FrameRateOptions { get; } = [10, 20, 30, 40, 50, 60];
    public static IReadOnlyList<int> GifColorCountOptions { get; } = [64, 128, 256];
    public static IReadOnlyList<int> OutputScaleOptions { get; } = [50, 75, 100];
    public static IReadOnlyList<int> Mp4CrfOptions { get; } = [18, 23, 28];
    public static IReadOnlyList<int> GifFrameRateOptions { get; } = [0, 10, 15, 24, 30, 60, 100];
    public static IReadOnlyList<int> Mp4FrameRateOptions { get; } = [0, 24, 30, 60];

    public static FrameStudioPreferences Default { get; } = new();

    public FrameStudioPreferences Normalize() => this with
    {
        DefaultFramesPerSecond = FrameRateOptions.Contains(DefaultFramesPerSecond)
            ? DefaultFramesPerSecond
            : Default.DefaultFramesPerSecond,
        GifRepeatCount = GifRepeatCount is -1 or 0 ? GifRepeatCount : Default.GifRepeatCount,
        GifMaximumColors = GifColorCountOptions.Contains(GifMaximumColors)
            ? GifMaximumColors
            : Default.GifMaximumColors,
        GifQuantization = GifQuantization is ColorQuantizationTypes.Octree or
            ColorQuantizationTypes.MedianCut or ColorQuantizationTypes.MostUsed
            ? GifQuantization
            : Default.GifQuantization,
        GifScalePercent = OutputScaleOptions.Contains(GifScalePercent) ? GifScalePercent : Default.GifScalePercent,
        Mp4Crf = Mp4CrfOptions.Contains(Mp4Crf) ? Mp4Crf : Default.Mp4Crf,
        Mp4ScalePercent = OutputScaleOptions.Contains(Mp4ScalePercent) ? Mp4ScalePercent : Default.Mp4ScalePercent,
        FfmpegExecutablePath = string.IsNullOrWhiteSpace(FfmpegExecutablePath) ? null : FfmpegExecutablePath.Trim(),
        GifFramesPerSecond = GifFrameRateOptions.Contains(GifFramesPerSecond)
            ? GifFramesPerSecond
            : Default.GifFramesPerSecond,
        Mp4FramesPerSecond = Mp4FrameRateOptions.Contains(Mp4FramesPerSecond)
            ? Mp4FramesPerSecond
            : Default.Mp4FramesPerSecond
    };
}

/// <summary>Persists small per-user preferences as JSON without introducing a platform dependency.</summary>
public sealed class FrameStudioPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public FrameStudioPreferencesStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public static FrameStudioPreferencesStore ForCurrentUser()
    {
        return new FrameStudioPreferencesStore(Path.Combine(UserDataDirectoryPath, "preferences.json"));
    }

    public static string UserDataDirectoryPath
    {
        get
        {
            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localData))
                localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(localData, "FrameStudio");
        }
    }

    public string DataDirectoryPath => Path.GetDirectoryName(_path)!;

    public FrameStudioPreferences Load()
    {
        try
        {
            if (!File.Exists(_path))
                return FrameStudioPreferences.Default;

            var loaded = JsonSerializer.Deserialize<FrameStudioPreferences>(File.ReadAllText(_path), JsonOptions);
            return (loaded ?? FrameStudioPreferences.Default).Normalize();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return FrameStudioPreferences.Default;
        }
    }

    public void Save(FrameStudioPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var normalized = preferences.Normalize();
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(normalized, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
