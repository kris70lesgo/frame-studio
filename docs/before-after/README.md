# Before and after captures

The original WPF application requires Windows and could not be launched on the macOS host used for this initial audit. The upstream README references baseline images and animations for the launcher, recorder, editor, and settings, but the corresponding original raster images are not checked into this clone.

The upstream README at baseline commit [`a4d0a67`](https://github.com/NickeManarin/ScreenToGif/blob/a4d0a67c2131cd048ceec86cd40afc2f1a06f2fd/README.md#screenshots) links to these still-accessible project references:

- [Recorder screenshot](https://nickemanarin.github.io/ScreenToGif-Website/media/Recorder.png)
- [Startup screen screenshot](https://nickemanarin.github.io/ScreenToGif-Website/media/Startup.png)
- [Editor animation](https://nickemanarin.github.io/ScreenToGif-Website/media/Editor.gif)
- [Options animation](https://nickemanarin.github.io/ScreenToGif-Website/media/Options.gif)
- [Keyboard shortcuts animation](https://nickemanarin.github.io/ScreenToGif-Website/media/Keys.gif)

These are useful references for the original app's screens and behavior. They are not fresh captures from the exact baseline executable or same-content before/after pairs, so they should not be submitted as the challenge comparisons.

`after-home-shell-macos.png` is an early Avalonia shell check. It is included to track UI progress and must not be used as the final Windows before/after comparison.

`after-editor-preview-macos.jpg` is a macOS UI smoke check using a local 24-frame, 640 × 360 `.fsp` fixture. It confirms that the editor window, preview, timeline, frame properties, and export action render. It does not verify Windows capture or GIF output, and it is not a challenge before/after image.

Capture fresh, consistent Windows screenshots here before challenge submission:

- `before-home.png` / `after-home.png`
- `before-recorder.png` / `after-recorder.png`
- `before-editor.png` / `after-editor.png`
- `before-export.png` / `after-export.png`

The Avalonia screenshots should be taken from a runnable Windows build and use the same project/content as the baseline.
