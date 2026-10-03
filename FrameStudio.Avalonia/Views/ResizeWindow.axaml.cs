using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FrameStudio.Core.Models;

namespace FrameStudio.Avalonia.Views;

public partial class ResizeWindow : Window
{
    private readonly TextBox _widthInput;
    private readonly TextBox _heightInput;
    private readonly CheckBox _keepAspectRatio;
    private readonly TextBlock _errorLabel;
    private readonly double _aspectRatio;
    private bool _updatingDimensions;

    public ResizeWindow() : this(new PixelSize(640, 360))
    {
    }

    public ResizeWindow(PixelSize currentSize)
    {
        AvaloniaXamlLoader.Load(this);
        _widthInput = this.FindControl<TextBox>("WidthInput")!;
        _heightInput = this.FindControl<TextBox>("HeightInput")!;
        _keepAspectRatio = this.FindControl<CheckBox>("KeepAspectRatioCheckBox")!;
        _errorLabel = this.FindControl<TextBlock>("ErrorLabel")!;
        _aspectRatio = currentSize.Height > 0 ? (double)currentSize.Width / currentSize.Height : 1;
        _widthInput.Text = currentSize.Width.ToString();
        _heightInput.Text = currentSize.Height.ToString();
    }

    private void Width_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingDimensions || _keepAspectRatio.IsChecked != true ||
            !int.TryParse(_widthInput.Text, out var width) || width <= 0)
            return;

        _updatingDimensions = true;
        _heightInput.Text = Math.Max(1, (int)Math.Round(width / _aspectRatio)).ToString();
        _updatingDimensions = false;
    }

    private void Height_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingDimensions || _keepAspectRatio.IsChecked != true ||
            !int.TryParse(_heightInput.Text, out var height) || height <= 0)
            return;

        _updatingDimensions = true;
        _widthInput.Text = Math.Max(1, (int)Math.Round(height * _aspectRatio)).ToString();
        _updatingDimensions = false;
    }

    private void Apply_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(_widthInput.Text, out var width) || !int.TryParse(_heightInput.Text, out var height) ||
            width is < 1 or > 16_384 || height is < 1 or > 16_384 || (long)width * height > 16_777_216)
        {
            _errorLabel.Text = "Enter valid dimensions within the 16 megapixel project limit.";
            _errorLabel.Foreground = Brushes.OrangeRed;
            return;
        }

        Close(new PixelSize(width, height));
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}
