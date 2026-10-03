# Before and after captures

The original WPF application requires Windows and could not be launched on the macOS host used for this initial audit. The upstream README references baseline images and animations for the launcher, recorder, editor, and settings, but the corresponding original raster images are not checked into this clone.

`after-home-shell-macos.png` is an early Avalonia shell check. It is included to track UI progress and must not be used as the final Windows before/after comparison.

`after-editor-preview-macos.jpg` is a macOS UI smoke check using a local 24-frame, 640 × 360 `.fsp` fixture. It confirms that the editor window, preview, timeline, frame properties, and export action render. It does not verify Windows capture or GIF output, and it is not a challenge before/after image.

Capture fresh, consistent Windows screenshots here before challenge submission:

- `before-home.png` / `after-home.png`
- `before-recorder.png` / `after-recorder.png`
- `before-editor.png` / `after-editor.png`
- `before-export.png` / `after-export.png`

The Avalonia screenshots should be taken from a runnable Windows build and use the same project/content as the baseline.
