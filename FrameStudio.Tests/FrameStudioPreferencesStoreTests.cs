using FrameStudio.Avalonia.Services;
using FrameStudio.Avalonia.ViewModels;

namespace FrameStudio.Tests;

public sealed class FrameStudioPreferencesStoreTests
{
    [Fact]
    public void Load_MissingFileReturnsDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-preferences-{Guid.NewGuid():N}");
        var store = new FrameStudioPreferencesStore(Path.Combine(directory, "preferences.json"));

        Assert.Equal(FrameStudioPreferences.Default, store.Load());
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void SaveAndLoad_PersistsAppearanceAndRecordingDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-preferences-{Guid.NewGuid():N}");
        var store = new FrameStudioPreferencesStore(Path.Combine(directory, "preferences.json"));
        var expected = new FrameStudioPreferences(IsDarkTheme: true, DefaultFramesPerSecond: 60, CaptureCursor: false,
            LoopEditorPreview: false, GifRepeatCount: -1, GifMaximumColors: 64,
            GifQuantization: FrameStudio.Core.Codification.Gif.Encoder.ColorQuantizationTypes.MedianCut,
            GifScalePercent: 75, Mp4Crf: 18, Mp4ScalePercent: 50, FfmpegExecutablePath: "/opt/tools/ffmpeg");

        try
        {
            store.Save(expected);

            Assert.Equal(expected, store.Load());
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_CorruptOrUnsupportedSettingsFallBackToDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-preferences-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "preferences.json");
        var store = new FrameStudioPreferencesStore(path);

        try
        {
            File.WriteAllText(path, "{ invalid json");
            Assert.Equal(FrameStudioPreferences.Default, store.Load());

            File.WriteAllText(path, "{\"IsDarkTheme\":true,\"DefaultFramesPerSecond\":25,\"CaptureCursor\":false,\"GifMaximumColors\":17,\"GifScalePercent\":400,\"Mp4Crf\":60,\"FfmpegExecutablePath\":\"  /local/ffmpeg  \"}");
            Assert.Equal(new FrameStudioPreferences(IsDarkTheme: true, DefaultFramesPerSecond: 30, CaptureCursor: false,
                FfmpegExecutablePath: "/local/ffmpeg"), store.Load());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MainViewModel_LoadsDefaultsAndPersistsThemeToggle()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"frame-studio-preferences-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var store = new FrameStudioPreferencesStore(Path.Combine(directory, "preferences.json"));

        try
        {
            store.Save(new FrameStudioPreferences(IsDarkTheme: true, DefaultFramesPerSecond: 60, CaptureCursor: false));
            var viewModel = new MainViewModel(new ShellCapabilityProvider(), preferencesStore: store);

            Assert.True(viewModel.IsDarkTheme);
            Assert.Equal(60, viewModel.DefaultFramesPerSecond);
            Assert.False(viewModel.CaptureCursor);

            viewModel.ToggleThemeCommand.Execute(null);

            Assert.False(viewModel.IsDarkTheme);
            Assert.Equal(new FrameStudioPreferences(IsDarkTheme: false, DefaultFramesPerSecond: 60, CaptureCursor: false), store.Load());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
