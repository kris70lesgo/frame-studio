using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FrameStudio.Avalonia.Services;

namespace FrameStudio.Avalonia.Views;

public partial class PreferencesWindow : Window
{
    private readonly ComboBox _frameRatePicker;
    private readonly ComboBox _themePicker;
    private readonly CheckBox _captureCursorCheckBox;

    public PreferencesWindow() : this(FrameStudioPreferences.Default)
    {
    }

    public PreferencesWindow(FrameStudioPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        InitializeComponent();
        _frameRatePicker = this.FindControl<ComboBox>("FrameRatePicker")!;
        _themePicker = this.FindControl<ComboBox>("ThemePicker")!;
        _captureCursorCheckBox = this.FindControl<CheckBox>("CaptureCursorCheckBox")!;

        _frameRatePicker.ItemsSource = FrameStudioPreferences.FrameRateOptions;
        var normalized = preferences.Normalize();
        _frameRatePicker.SelectedItem = normalized.DefaultFramesPerSecond;
        _themePicker.SelectedIndex = normalized.IsDarkTheme ? 1 : 0;
        _captureCursorCheckBox.IsChecked = normalized.CaptureCursor;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Save_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_frameRatePicker.SelectedItem is not int framesPerSecond)
            return;

        Close(new FrameStudioPreferences(
            IsDarkTheme: _themePicker.SelectedIndex == 1,
            DefaultFramesPerSecond: framesPerSecond,
            CaptureCursor: _captureCursorCheckBox.IsChecked == true));
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}
