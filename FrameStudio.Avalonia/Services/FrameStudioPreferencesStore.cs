using System.Text.Json;

namespace FrameStudio.Avalonia.Services;

public sealed record FrameStudioPreferences(
    bool IsDarkTheme = false,
    int DefaultFramesPerSecond = 30,
    bool CaptureCursor = true)
{
    public static IReadOnlyList<int> FrameRateOptions { get; } = [10, 20, 30, 40, 50, 60];

    public static FrameStudioPreferences Default { get; } = new();

    public FrameStudioPreferences Normalize() => this with
    {
        DefaultFramesPerSecond = FrameRateOptions.Contains(DefaultFramesPerSecond)
            ? DefaultFramesPerSecond
            : Default.DefaultFramesPerSecond
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
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        return new FrameStudioPreferencesStore(Path.Combine(localData, "FrameStudio", "preferences.json"));
    }

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
