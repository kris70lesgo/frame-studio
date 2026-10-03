using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace FrameStudio.Avalonia.Views;

public enum UnsavedChangesChoice
{
    Save,
    Discard,
    Cancel
}

public partial class UnsavedChangesWindow : Window
{
    public UnsavedChangesWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Save_OnClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);
    private void Discard_OnClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);
    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
