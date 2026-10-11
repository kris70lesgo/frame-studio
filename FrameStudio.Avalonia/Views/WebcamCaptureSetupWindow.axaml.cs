using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.Views;

public sealed record WebcamCaptureSetupResult(WebcamDeviceDescriptor Device, int FramesPerSecond);

public partial class WebcamCaptureSetupWindow : Window
{
    private readonly ComboBox _devicePicker;
    private readonly Slider _frameRateSlider;
    private readonly TextBlock _frameRateLabel;

    public WebcamCaptureSetupWindow()
    {
        InitializeComponent();
        _devicePicker = this.FindControl<ComboBox>("DevicePicker")!;
        _frameRateSlider = this.FindControl<Slider>("FrameRateSlider")!;
        _frameRateLabel = this.FindControl<TextBlock>("FrameRateLabel")!;
        UpdateFrameRateLabel();
    }

    public WebcamCaptureSetupWindow(IReadOnlyList<WebcamDeviceDescriptor> devices, int framesPerSecond = 30)
        : this()
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (devices.Count == 0)
            throw new ArgumentException("At least one webcam is required.", nameof(devices));

        _devicePicker.ItemsSource = devices;
        _devicePicker.SelectedIndex = 0;
        _frameRateSlider.Value = Math.Clamp(framesPerSecond, 10, 60);
        UpdateFrameRateLabel();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void FrameRateSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => UpdateFrameRateLabel();

    private void UpdateFrameRateLabel() => _frameRateLabel.Text = $"{(int)Math.Round(_frameRateSlider.Value)} FPS";

    private void Start_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_devicePicker.SelectedItem is not WebcamDeviceDescriptor device)
            return;

        Close(new WebcamCaptureSetupResult(device, (int)Math.Round(_frameRateSlider.Value)));
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}
