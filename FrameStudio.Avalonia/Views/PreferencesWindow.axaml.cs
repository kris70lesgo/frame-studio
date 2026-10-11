using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FrameStudio.Avalonia.Services;
using FrameStudio.Core.Codification.Gif.Encoder;

namespace FrameStudio.Avalonia.Views;

public partial class PreferencesWindow : Window
{
    private readonly ComboBox _frameRatePicker;
    private readonly ComboBox _gifPalettePicker;
    private readonly ComboBox _gifColorCountPicker;
    private readonly ComboBox _gifScalePicker;
    private readonly ComboBox _mp4QualityPicker;
    private readonly ComboBox _mp4ScalePicker;
    private readonly ComboBox _themePicker;
    private readonly CheckBox _captureCursorCheckBox;
    private readonly CheckBox _loopPreviewCheckBox;
    private readonly CheckBox _loopGifCheckBox;
    private readonly TextBox _ffmpegPathTextBox;
    private readonly TextBlock _advancedStatusText;

    public PreferencesWindow() : this(FrameStudioPreferences.Default)
    {
    }

    public PreferencesWindow(FrameStudioPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        InitializeComponent();
        _frameRatePicker = this.FindControl<ComboBox>("FrameRatePicker")!;
        _gifPalettePicker = this.FindControl<ComboBox>("GifPalettePicker")!;
        _gifColorCountPicker = this.FindControl<ComboBox>("GifColorCountPicker")!;
        _gifScalePicker = this.FindControl<ComboBox>("GifScalePicker")!;
        _mp4QualityPicker = this.FindControl<ComboBox>("Mp4QualityPicker")!;
        _mp4ScalePicker = this.FindControl<ComboBox>("Mp4ScalePicker")!;
        _themePicker = this.FindControl<ComboBox>("ThemePicker")!;
        _captureCursorCheckBox = this.FindControl<CheckBox>("CaptureCursorCheckBox")!;
        _loopPreviewCheckBox = this.FindControl<CheckBox>("LoopPreviewCheckBox")!;
        _loopGifCheckBox = this.FindControl<CheckBox>("LoopGifCheckBox")!;
        _ffmpegPathTextBox = this.FindControl<TextBox>("FfmpegPathTextBox")!;
        _advancedStatusText = this.FindControl<TextBlock>("AdvancedStatusText")!;

        _frameRatePicker.ItemsSource = FrameStudioPreferences.FrameRateOptions;
        _gifColorCountPicker.ItemsSource = FrameStudioPreferences.GifColorCountOptions;
        var normalized = preferences.Normalize();
        _frameRatePicker.SelectedItem = normalized.DefaultFramesPerSecond;
        _themePicker.SelectedIndex = normalized.IsDarkTheme ? 1 : 0;
        _captureCursorCheckBox.IsChecked = normalized.CaptureCursor;
        _loopPreviewCheckBox.IsChecked = normalized.LoopEditorPreview;
        _loopGifCheckBox.IsChecked = normalized.GifRepeatCount == 0;
        _gifPalettePicker.SelectedIndex = normalized.GifQuantization switch
        {
            ColorQuantizationTypes.MedianCut => 1,
            ColorQuantizationTypes.MostUsed => 2,
            _ => 0
        };
        _gifColorCountPicker.SelectedItem = normalized.GifMaximumColors;
        _gifScalePicker.SelectedIndex = ScaleIndex(normalized.GifScalePercent);
        _mp4QualityPicker.SelectedIndex = FrameStudioPreferences.Mp4CrfOptions.ToList().IndexOf(normalized.Mp4Crf);
        _mp4ScalePicker.SelectedIndex = ScaleIndex(normalized.Mp4ScalePercent);
        _ffmpegPathTextBox.Text = normalized.FfmpegExecutablePath;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Save_OnClick(object? sender, RoutedEventArgs e)
    {
        var quantization = _gifPalettePicker.SelectedIndex switch
        {
            1 => ColorQuantizationTypes.MedianCut,
            2 => ColorQuantizationTypes.MostUsed,
            _ => ColorQuantizationTypes.Octree
        };
        var crf = _mp4QualityPicker.SelectedIndex is >= 0 and < 3
            ? FrameStudioPreferences.Mp4CrfOptions[_mp4QualityPicker.SelectedIndex]
            : FrameStudioPreferences.Default.Mp4Crf;

        Close(new FrameStudioPreferences(
            IsDarkTheme: _themePicker.SelectedIndex == 1,
            DefaultFramesPerSecond: _frameRatePicker.SelectedItem is int framesPerSecond
                ? framesPerSecond
                : FrameStudioPreferences.Default.DefaultFramesPerSecond,
            CaptureCursor: _captureCursorCheckBox.IsChecked == true,
            LoopEditorPreview: _loopPreviewCheckBox.IsChecked == true,
            GifRepeatCount: _loopGifCheckBox.IsChecked == true ? 0 : -1,
            GifMaximumColors: _gifColorCountPicker.SelectedItem is int colorCount
                ? colorCount
                : FrameStudioPreferences.Default.GifMaximumColors,
            GifQuantization: quantization,
            GifScalePercent: SelectedScale(_gifScalePicker),
            Mp4Crf: crf,
            Mp4ScalePercent: SelectedScale(_mp4ScalePicker),
            FfmpegExecutablePath: _ffmpegPathTextBox.Text));
    }

    private async void BrowseFfmpeg_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select FFmpeg executable",
                AllowMultiple = false
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                _ffmpegPathTextBox.Text = path;
                _advancedStatusText.Text = "The selected path will be used for MP4 export after saving.";
            }
        }
        catch (Exception exception)
        {
            _advancedStatusText.Text = $"Could not choose FFmpeg: {exception.Message}";
        }
    }

    private void ClearFfmpeg_OnClick(object? sender, RoutedEventArgs e)
    {
        _ffmpegPathTextBox.Text = string.Empty;
        _advancedStatusText.Text = "MP4 export will search for ffmpeg on PATH.";
    }

    private void OpenDataFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dataDirectory = FrameStudioPreferencesStore.UserDataDirectoryPath;
            Directory.CreateDirectory(dataDirectory);
            Process.Start(new ProcessStartInfo(dataDirectory) { UseShellExecute = true });
            _advancedStatusText.Text = "Opened the Frame Studio data folder.";
        }
        catch (Exception exception)
        {
            _advancedStatusText.Text = $"Could not open the app data folder: {exception.Message}";
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private static int ScaleIndex(int scalePercent) => scalePercent switch
    {
        75 => 1,
        50 => 2,
        _ => 0
    };

    private int SelectedScale(ComboBox picker) => picker.SelectedIndex switch
    {
        1 => 75,
        2 => 50,
        _ => 100
    };
}
