# Windows validation checklist

The Windows capture backend and app package have been cross-compiled on macOS, but have not yet been run on Windows. This checklist records the evidence needed before claiming the record → edit → GIF workflow is verified.

## Current local Windows x64 candidate

- Package: `dist/FrameStudio-win-x64.zip` (ignored local build output; not publicly downloadable).
- Rebuild it with `python3 scripts/package-windows-candidate.py`; this reads the Frame Studio version from MSBuild, includes the README and license, checks archive contents, and writes the matching `.sha256` file.
- Source revision: `6a5b23c`; Frame Studio `0.1.0`, self-contained `win-x64` publish using .NET SDK `9.0.318` on macOS arm64.
- SHA-256: `75537e8b0e044d75c1d400490af488e8114aea613278c48451e37fe9f6c3b26a` (also written to `dist/FrameStudio-win-x64.zip.sha256`).
- Archive integrity passed with `unzip -t`; the archive includes the package README and complete MS-PL license. This does not establish that the executable runs or captures correctly on Windows.

## Automated desktop check

Run from a Windows 10 or Windows 11 desktop session with at least one active display:

```powershell
dotnet test FrameStudio.sln --configuration Debug
```

`WindowsCaptureIntegrationTests` enumerates a monitor, captures a 64 × 64 pixel region, checks RGBA frame dimensions and timing, and exercises pause, resume, and stop. It is intentionally skipped on non-Windows systems and is not evidence until it passes on a Windows desktop.

## Manual recording and editing check

1. Launch Frame Studio. Confirm the Screen action is available and the unsupported sources remain disabled.
2. Choose a display and a small region. Record for several seconds, pause, resume, and stop.
3. Confirm a `.fsp` project is written and opens in the editor. Check preview, playback, thumbnail rendering, frame durations, duplicate/delete, and move earlier/later.
4. Drag a crop over the preview and apply it. Resize the project with aspect ratio retained, save, close, reopen, and confirm dimensions and edits persist.
5. Export a looping GIF. In the completion window, verify Open GIF, Show in folder, and Copy path; then check the GIF in a separate viewer for animation, edited ordering and timing, and expected canvas dimensions.
6. Repeat with a full display. On a multi-monitor setup, test a non-primary display and a mixed-DPI display if available.

## Challenge evidence to capture

Run the original WPF app and Frame Studio against the same content. Save paired home, recorder, editor, and export screenshots under `docs/before-after/`. Record the Windows version, display resolution and scale, selected region, frame rate, capture duration, frame count, and GIF dimensions. Include failures and any platform limitations in the migration write-up.

This checklist is preparation only. Fill in the environment and results after the Windows run; do not present the current macOS cross-publish or skipped test as runtime verification.
