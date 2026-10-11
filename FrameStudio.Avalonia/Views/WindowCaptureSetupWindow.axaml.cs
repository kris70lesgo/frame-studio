using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FrameStudio.Platform.Abstractions;

namespace FrameStudio.Avalonia.Views;

public sealed record WindowCaptureSetupResult(WindowDescriptor Window, int FramesPerSecond, bool CaptureCursor);

public partial class WindowCaptureSetupWindow : Window
{
    private readonly ComboBox _windowPicker;
    private readonly Slider _frameRateSlider;
    private readonly TextBlock _frameRateLabel;
    private readonly CheckBox _captureCursorCheckBox;

    public WindowCaptureSetupWindow()
    {
        InitializeComponent();
        _windowPicker = this.FindControl<ComboBox>("WindowPicker")!;
        _frameRateSlider = this.FindControl<Slider>("FrameRateSlider")!;
        _frameRateLabel = this.FindControl<TextBlock>("FrameRateLabel")!;
        _captureCursorCheckBox = this.FindControl<CheckBox>("CaptureCursorCheckBox")!;
    }

    public WindowCaptureSetupWindow(IReadOnlyList<WindowDescriptor> windows, int framesPerSecond = 30,
        bool captureCursor = true) : this()
    {
        ArgumentNullException.ThrowIfNull(windows);
        if (windows.Count == 0)
            throw new ArgumentException("At least one visible application window is required.", nameof(windows));

        _windowPicker.ItemsSource = windows;
        _windowPicker.SelectedIndex = 0;
        _frameRateSlider.Value = Math.Clamp(framesPerSecond, 10, 60);
        _captureCursorCheckBox.IsChecked = captureCursor;
        UpdateFrameRateLabel();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void FrameRateSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => UpdateFrameRateLabel();

    private void UpdateFrameRateLabel() => _frameRateLabel.Text = $"{(int)Math.Round(_frameRateSlider.Value)} FPS";

    private void Start_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_windowPicker.SelectedItem is not WindowDescriptor window)
            return;

        Close(new WindowCaptureSetupResult(window, (int)Math.Round(_frameRateSlider.Value),
            _captureCursorCheckBox.IsChecked == true));
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}
