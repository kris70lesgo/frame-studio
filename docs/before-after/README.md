# Before and after captures

The original WPF application requires Windows and could not be launched on the macOS host used for this initial audit. The upstream README references baseline images and animations for the launcher, recorder, editor, and settings, but the corresponding original raster images are not checked into this clone.

The upstream README at baseline commit [`a4d0a67`](https://github.com/NickeManarin/ScreenToGif/blob/a4d0a67c2131cd048ceec86cd40afc2f1a06f2fd/README.md#screenshots) links to these still-accessible project references:

- [Recorder screenshot](https://nickemanarin.github.io/ScreenToGif-Website/media/Recorder.png)
- [Startup screen screenshot](https://nickemanarin.github.io/ScreenToGif-Website/media/Startup.png)
- [Editor animation](https://nickemanarin.github.io/ScreenToGif-Website/media/Editor.gif)
- [Options animation](https://nickemanarin.github.io/ScreenToGif-Website/media/Options.gif)
- [Keyboard shortcuts animation](https://nickemanarin.github.io/ScreenToGif-Website/media/Keys.gif)

These are useful references for the original app's screens and behavior. They are not fresh captures from the exact baseline executable or same-content before/after pairs, so they should not be submitted as the challenge comparisons.

`after-home-current-macos.png` and `after-editor-current-macos.png` are macOS visual-review captures of the Avalonia home workspace and editor using synthetic sample content. They document the redesigned UI, including the recent-project workspace, preview, timeline, drawing panel, and editor controls. They are supplemental UI previews only: they do not verify Windows screen recording or replace same-content WPF/Avalonia before-and-after pairs.

The older `after-home-shell-macos.png` and `after-editor-preview-macos.jpg` remain as historical shell checks. They predate the current editor workflow and should not be used as current screenshots.

Capture fresh, consistent Windows screenshots here before challenge submission:

- `before-home.png` / `after-home.png`
- `before-recorder.png` / `after-recorder.png`
- `before-editor.png` / `after-editor.png`
- `before-export.png` / `after-export.png`

The Avalonia screenshots should be taken from a runnable Windows build and use the same project/content as the baseline.

For the original WPF application, download the [ScreenToGif 2.43.2 release](https://github.com/NickeManarin/ScreenToGif/releases/tag/2.43.2), which matches the audited source baseline. Use a harmless sample recording and the same captured region/project in both applications. The paired images are evidence of the migration, not a claim that Frame Studio has full ScreenToGif feature parity.
