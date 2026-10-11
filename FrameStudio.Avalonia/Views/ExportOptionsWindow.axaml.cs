using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FrameStudio.Avalonia.Services;
using FrameStudio.Core.Codification.Gif.Encoder;
using FrameStudio.Core.Export;
using FrameStudio.Core.Models;

namespace FrameStudio.Avalonia.Views;

public enum ExportMediaFormat
{
    Gif,
    Mp4
}

public sealed record ExportOptionsResult(GifExportOptions? GifOptions, FfmpegMp4ExportOptions? Mp4Options);

public partial class ExportOptionsWindow : Window
{
    private enum DimensionSyncAxis
    {
        None,
        Width,
        Height
    }

    private readonly ExportMediaFormat _format;
    private readonly PixelSize _sourceSize;
    private readonly int _sourceFrameCount;
    private readonly long _sourceDurationMilliseconds;
    private readonly double _aspectRatio;
    private readonly GifExportOptions _gifDefaults;
    private readonly FfmpegMp4ExportOptions _mp4Defaults;
    private readonly StackPanel _gifSettingsPanel;
    private readonly StackPanel _mp4SettingsPanel;
    private readonly ComboBox _gifFrameRatePicker;
    private readonly ComboBox _gifPalettePicker;
    private readonly ComboBox _gifColorCountPicker;
    private readonly ComboBox _mp4QualityPicker;
    private readonly ComboBox _mp4FrameRatePicker;
    private readonly ComboBox _scalePicker;
    private readonly CheckBox _gifLoopCheckBox;
    private readonly CheckBox _gifDitherCheckBox;
    private readonly CheckBox _customDimensionsCheckBox;
    private readonly CheckBox _keepAspectRatioCheckBox;
    private readonly Grid _dimensionsInputGrid;
    private readonly TextBox _widthInput;
    private readonly TextBox _heightInput;
    private readonly TextBlock _dimensionErrorLabel;
    private readonly TextBlock _estimateLabel;
    private readonly Button _exportButton;
    private bool _updatingDimensions;
    private DimensionSyncAxis _dimensionSyncAxis;

    public ExportOptionsWindow() : this(ExportMediaFormat.Gif, new PixelSize(640, 360), 1, 1000,
        new GifExportOptions(), new FfmpegMp4ExportOptions())
    {
    }

    public ExportOptionsWindow(ExportMediaFormat format, PixelSize sourceSize, int sourceFrameCount,
        long sourceDurationMilliseconds, GifExportOptions gifDefaults, FfmpegMp4ExportOptions mp4Defaults)
    {
        ArgumentNullException.ThrowIfNull(gifDefaults);
        ArgumentNullException.ThrowIfNull(mp4Defaults);
        if (sourceSize.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(sourceSize));
        if (sourceFrameCount < 1 || sourceDurationMilliseconds < 1)
            throw new ArgumentOutOfRangeException(nameof(sourceFrameCount));

        _format = format;
        _sourceSize = sourceSize;
        _sourceFrameCount = sourceFrameCount;
        _sourceDurationMilliseconds = sourceDurationMilliseconds;
        _aspectRatio = (double)sourceSize.Width / sourceSize.Height;
        _gifDefaults = gifDefaults.Validate();
        _mp4Defaults = mp4Defaults.Validate();

        AvaloniaXamlLoader.Load(this);
        _gifSettingsPanel = this.FindControl<StackPanel>("GifSettingsPanel")!;
        _mp4SettingsPanel = this.FindControl<StackPanel>("Mp4SettingsPanel")!;
        _gifFrameRatePicker = this.FindControl<ComboBox>("GifFrameRatePicker")!;
        _gifPalettePicker = this.FindControl<ComboBox>("GifPalettePicker")!;
        _gifColorCountPicker = this.FindControl<ComboBox>("GifColorCountPicker")!;
        _mp4QualityPicker = this.FindControl<ComboBox>("Mp4QualityPicker")!;
        _mp4FrameRatePicker = this.FindControl<ComboBox>("Mp4FrameRatePicker")!;
        _scalePicker = this.FindControl<ComboBox>("ScalePicker")!;
        _gifLoopCheckBox = this.FindControl<CheckBox>("GifLoopCheckBox")!;
        _gifDitherCheckBox = this.FindControl<CheckBox>("GifDitherCheckBox")!;
        _customDimensionsCheckBox = this.FindControl<CheckBox>("CustomDimensionsCheckBox")!;
        _keepAspectRatioCheckBox = this.FindControl<CheckBox>("KeepAspectRatioCheckBox")!;
        _dimensionsInputGrid = this.FindControl<Grid>("DimensionsInputGrid")!;
        _widthInput = this.FindControl<TextBox>("WidthInput")!;
        _heightInput = this.FindControl<TextBox>("HeightInput")!;
        _dimensionErrorLabel = this.FindControl<TextBlock>("DimensionErrorLabel")!;
        _estimateLabel = this.FindControl<TextBlock>("EstimateLabel")!;
        _exportButton = this.FindControl<Button>("ExportButton")!;

        var isGif = format == ExportMediaFormat.Gif;
        _gifSettingsPanel.IsVisible = isGif;
        _mp4SettingsPanel.IsVisible = !isGif;
        Title = isGif ? "Export GIF" : "Export MP4";
        this.FindControl<TextBlock>("EyebrowLabel")!.Text = isGif ? "GIF EXPORT" : "MP4 EXPORT";
        this.FindControl<TextBlock>("TitleLabel")!.Text = isGif ? "Prepare your animation" : "Prepare your video";
        _exportButton.Content = isGif ? "Choose GIF file…" : "Choose MP4 file…";

        _gifColorCountPicker.ItemsSource = FrameStudioPreferences.GifColorCountOptions;
        _gifFrameRatePicker.SelectionChanged += (_, _) => UpdateEstimate();
        _gifPalettePicker.SelectionChanged += (_, _) => UpdateEstimate();
        _gifColorCountPicker.SelectionChanged += (_, _) => UpdateEstimate();
        _mp4QualityPicker.SelectionChanged += (_, _) => UpdateEstimate();
        _mp4FrameRatePicker.SelectionChanged += (_, _) => UpdateEstimate();
        _scalePicker.SelectionChanged += ScalePicker_OnSelectionChanged;
        _customDimensionsCheckBox.Click += CustomDimensions_OnClick;
        _keepAspectRatioCheckBox.Click += (_, _) => UpdateEstimate();

        SelectTaggedValue(_gifFrameRatePicker, _gifDefaults.FramesPerSecond);
        _gifPalettePicker.SelectedIndex = _gifDefaults.Quantization switch
        {
            ColorQuantizationTypes.MedianCut => 1,
            ColorQuantizationTypes.MostUsed => 2,
            _ => 0
        };
        _gifColorCountPicker.SelectedItem = _gifDefaults.MaximumColors;
        SelectTaggedValue(_mp4QualityPicker, _mp4Defaults.Crf);
        SelectTaggedValue(_mp4FrameRatePicker, _mp4Defaults.FramesPerSecond);
        SelectTaggedValue(_scalePicker, isGif ? _gifDefaults.ScalePercent : _mp4Defaults.ScalePercent);
        _gifLoopCheckBox.IsChecked = _gifDefaults.RepeatCount == 0;
        _gifDitherCheckBox.IsChecked = _gifDefaults.Dither;

        var customSize = isGif ? _gifDefaults.OutputSize : _mp4Defaults.OutputSize;
        var defaultScale = isGif ? _gifDefaults.ScalePercent : _mp4Defaults.ScalePercent;
        _customDimensionsCheckBox.IsChecked = customSize.HasValue;
        var initialSize = customSize ?? ScaleSize(sourceSize, defaultScale);
        _widthInput.Text = initialSize.Width.ToString(CultureInfo.InvariantCulture);
        _heightInput.Text = initialSize.Height.ToString(CultureInfo.InvariantCulture);
        SetDimensionControlsEnabled(customSize.HasValue);
        UpdateEstimate();
    }

    private void Width_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingDimensions || _customDimensionsCheckBox.IsChecked != true ||
            _keepAspectRatioCheckBox.IsChecked != true ||
            !int.TryParse(_widthInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) || width <= 0)
        {
            UpdateEstimate();
            return;
        }

        var expectedHeight = RoundPositiveDimension(width / _aspectRatio);
        if (_dimensionSyncAxis == DimensionSyncAxis.Height &&
            int.TryParse(_heightInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var heightForWidth) &&
            RoundPositiveDimension(heightForWidth * _aspectRatio) == width)
        {
            UpdateEstimate();
            return;
        }
        if (int.TryParse(_heightInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentHeight) &&
            currentHeight == expectedHeight)
        {
            UpdateEstimate();
            return;
        }

        _dimensionSyncAxis = DimensionSyncAxis.Width;
        _updatingDimensions = true;
        try
        {
            _heightInput.Text = expectedHeight.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _updatingDimensions = false;
        }
        UpdateEstimate();
    }

    private void Height_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingDimensions || _customDimensionsCheckBox.IsChecked != true ||
            _keepAspectRatioCheckBox.IsChecked != true ||
            !int.TryParse(_heightInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) || height <= 0)
        {
            UpdateEstimate();
            return;
        }

        var expectedWidth = RoundPositiveDimension(height * _aspectRatio);
        if (_dimensionSyncAxis == DimensionSyncAxis.Width &&
            int.TryParse(_widthInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var widthForHeight) &&
            RoundPositiveDimension(widthForHeight / _aspectRatio) == height)
        {
            UpdateEstimate();
            return;
        }
        if (int.TryParse(_widthInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentWidth) &&
            currentWidth == expectedWidth)
        {
            UpdateEstimate();
            return;
        }

        _dimensionSyncAxis = DimensionSyncAxis.Height;
        _updatingDimensions = true;
        try
        {
            _widthInput.Text = expectedWidth.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _updatingDimensions = false;
        }
        UpdateEstimate();
    }

    private void ScalePicker_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_customDimensionsCheckBox.IsChecked == true)
        {
            var size = ScaleSize(_sourceSize, SelectedScale());
            SetDimensionText(size);
        }
        UpdateEstimate();
    }

    private void CustomDimensions_OnClick(object? sender, RoutedEventArgs e)
    {
        var enabled = _customDimensionsCheckBox.IsChecked == true;
        if (enabled)
        {
            var size = ScaleSize(_sourceSize, SelectedScale());
            SetDimensionText(size);
        }
        SetDimensionControlsEnabled(enabled);
        UpdateEstimate();
    }

    private void SetDimensionText(PixelSize size)
    {
        _dimensionSyncAxis = DimensionSyncAxis.None;
        _updatingDimensions = true;
        _widthInput.Text = size.Width.ToString(CultureInfo.InvariantCulture);
        _heightInput.Text = size.Height.ToString(CultureInfo.InvariantCulture);
        _updatingDimensions = false;
    }

    private void SetDimensionControlsEnabled(bool enabled)
    {
        _widthInput.IsEnabled = enabled;
        _heightInput.IsEnabled = enabled;
        _keepAspectRatioCheckBox.IsEnabled = enabled;
        _dimensionsInputGrid.IsVisible = enabled;
        _keepAspectRatioCheckBox.IsVisible = enabled;
        _scalePicker.IsEnabled = !enabled;
    }

    private void UpdateEstimate()
    {
        var outputSize = ResolveOutputSize(out var invalidSize);
        _exportButton.IsEnabled = !invalidSize && EstimateOutputFrameCount() <= 100_000;
        if (invalidSize)
        {
            _dimensionErrorLabel.Text = "Enter valid dimensions within the 16 megapixel canvas limit.";
            _dimensionErrorLabel.Foreground = Brushes.OrangeRed;
            _estimateLabel.Text = "Output dimensions are invalid.";
            return;
        }

        _dimensionErrorLabel.Text = "Each side is limited to 16,384 px; canvas area is limited to 16 megapixels.";
        _dimensionErrorLabel.Foreground = Brushes.Gray;
        if (_format == ExportMediaFormat.Mp4)
            outputSize = new PixelSize(outputSize.Width + outputSize.Width % 2, outputSize.Height + outputSize.Height % 2);

        var framesPerSecond = SelectedFrameRate();
        var frameCount = EstimateOutputFrameCount();
        var durationSeconds = _sourceDurationMilliseconds / 1000d;
        var rateLabel = framesPerSecond == 0 ? "source timing" : $"{framesPerSecond} FPS";
        _estimateLabel.Text = $"{outputSize.Width:N0} × {outputSize.Height:N0} · {rateLabel} · {frameCount:N0} frames · {durationSeconds:0.###} s";
        if (frameCount > 100_000)
        {
            _exportButton.IsEnabled = false;
            _dimensionErrorLabel.Text = "This frame rate would produce more than 100,000 output frames.";
            _dimensionErrorLabel.Foreground = Brushes.OrangeRed;
        }
    }

    private PixelSize ResolveOutputSize(out bool invalid)
    {
        invalid = false;
        if (_customDimensionsCheckBox.IsChecked != true)
            return ScaleSize(_sourceSize, SelectedScale());

        if (!int.TryParse(_widthInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
            !int.TryParse(_heightInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ||
            width is < 1 or > 16_384 || height is < 1 or > 16_384 || (long)width * height > 16_777_216)
        {
            invalid = true;
            return _sourceSize;
        }

        return new PixelSize(width, height);
    }

    private long EstimateOutputFrameCount()
    {
        var framesPerSecond = SelectedFrameRate();
        if (framesPerSecond == 0)
            return _sourceFrameCount;
        return Math.Max(1L, (long)Math.Round(_sourceDurationMilliseconds * framesPerSecond / 1000d,
            MidpointRounding.AwayFromZero));
    }

    private int SelectedFrameRate() => _format == ExportMediaFormat.Gif
        ? SelectedTag(_gifFrameRatePicker, _gifDefaults.FramesPerSecond)
        : SelectedTag(_mp4FrameRatePicker, _mp4Defaults.FramesPerSecond);

    private int SelectedScale() => SelectedTag(_scalePicker, 100);

    private void Export_OnClick(object? sender, RoutedEventArgs e)
    {
        var outputSize = ResolveOutputSize(out var invalid);
        if (invalid || EstimateOutputFrameCount() > 100_000)
        {
            UpdateEstimate();
            return;
        }

        if (_format == ExportMediaFormat.Gif)
        {
            var quantization = _gifPalettePicker.SelectedIndex switch
            {
                1 => ColorQuantizationTypes.MedianCut,
                2 => ColorQuantizationTypes.MostUsed,
                _ => ColorQuantizationTypes.Octree
            };
            var colorCount = _gifColorCountPicker.SelectedItem is int selectedColorCount
                ? selectedColorCount
                : _gifDefaults.MaximumColors;
            var options = _gifDefaults with
            {
                RepeatCount = _gifLoopCheckBox.IsChecked == true ? 0 : -1,
                MaximumColors = colorCount,
                Quantization = quantization,
                ScalePercent = SelectedScale(),
                FramesPerSecond = SelectedFrameRate(),
                Dither = _gifDitherCheckBox.IsChecked == true,
                OutputSize = _customDimensionsCheckBox.IsChecked == true ? outputSize : null
            };
            Close(new ExportOptionsResult(options.Validate(), null));
            return;
        }

        var mp4Options = _mp4Defaults with
        {
            Crf = SelectedTag(_mp4QualityPicker, _mp4Defaults.Crf),
            ScalePercent = SelectedScale(),
            FramesPerSecond = SelectedFrameRate(),
            OutputSize = _customDimensionsCheckBox.IsChecked == true ? outputSize : null
        };
        Close(new ExportOptionsResult(null, mp4Options.Validate()));
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private static PixelSize ScaleSize(PixelSize source, int percent)
    {
        var width = Math.Max(1, (int)Math.Round(source.Width * percent / 100d, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(source.Height * percent / 100d, MidpointRounding.AwayFromZero));
        return new PixelSize(width, height);
    }

    private static int RoundPositiveDimension(double value) =>
        Math.Clamp((int)Math.Min(int.MaxValue, Math.Round(value, MidpointRounding.AwayFromZero)), 1, int.MaxValue);

    private static void SelectTaggedValue(ComboBox picker, int value)
    {
        picker.SelectedItem = picker.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => int.TryParse(item.Tag?.ToString(), out var tag) && tag == value);
    }

    private static int SelectedTag(ComboBox picker, int fallback) =>
        picker.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var value)
            ? value
            : fallback;
}
