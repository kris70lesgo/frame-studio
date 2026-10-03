# Windows validation checklist

The Windows capture backend and app package have been cross-compiled on macOS, but have not yet been run on Windows. This checklist records the evidence needed before claiming the record → edit → export workflow is verified. Screen recording requires Windows 10 version 2004 (build 19041) or later because the app excludes its own windows using [`WDA_EXCLUDEFROMCAPTURE`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity). MP4 export also needs an installed FFmpeg build with `libx264` available on `PATH`.

## Current Windows x64 preview candidate

- Public preview: [Frame Studio 0.1.0 Preview 3](https://github.com/kris70lesgo/frame-studio/releases/tag/v0.1.0-preview.3).
- Local package: `dist/FrameStudio-win-x64.zip` (ignored build output; identical to the uploaded preview asset).
- Rebuild it with `python3 scripts/package-windows-candidate.py`; this reads the Frame Studio version from MSBuild, includes the README and license, checks archive contents, and writes the matching `.sha256` file.
- Source revision: `5184f661`; Frame Studio `0.1.0`, self-contained `win-x64` publish using .NET SDK `9.0.318` on macOS arm64.
- SHA-256: `de2c97b7ff8a63d1fd0be7ba62c30a502d6b8b3797389cec02e65a910cc9fb65` (also written to `dist/FrameStudio-win-x64.zip.sha256` and uploaded with the preview release).
- The package script and an independent `unzip -t` check both confirmed archive integrity. A separate SHA-256 calculation matched the checksum file; GitHub reports the same ZIP digest after upload. The archive contains the executable, package README, and complete MS-PL license. Its README identifies the commit and macOS build host and explicitly says Windows execution, capture, mixed-DPI selection, and end-to-end workflow are unverified. The [hosted CI run](https://github.com/kris70lesgo/frame-studio/actions/runs/37146520922) passed its macOS build/test, Windows build/test, and Windows x64 publishing jobs for this revision.

## Automated desktop check

Run from a Windows 10 version 2004+ or Windows 11 desktop session with at least one active display:

```powershell
dotnet test FrameStudio.sln --configuration Debug
```

`WindowsCaptureIntegrationTests` includes cross-platform checks that malformed screen and window requests fail before native calls. Three desktop-only tests check capture-window affinity apply/restore, cleanup after a window closes, and monitor enumeration plus a 64 × 64 capture whose expected system-color marker pixel must survive capture, pause, resume, and stop. The desktop tests are intentionally skipped off Windows and are not evidence until they pass on a Windows desktop.

The MP4 editor-export integration test requires both `ffmpeg` and `ffprobe` on `PATH`. It checks an edited two-frame project with changed durations by reading the emitted MP4 presentation timestamps.

## Manual recording and editing check

1. Launch Frame Studio. Confirm the Screen action is available and the unsupported sources remain disabled.
2. Choose a display and a small region. Record for several seconds, pause, resume, and stop. Verify that neither Frame Studio's home window nor the floating recorder controls appear in the captured frames.
3. Confirm a `.fsp` project is written and opens in the editor. Check preview, playback, thumbnail rendering, frame durations, duplicate/delete, and move earlier/later.
4. Drag a crop over the preview and apply it. Resize the project with aspect ratio retained, save, close, reopen, and confirm dimensions and edits persist.
5. Add a text overlay and confirm it appears in every frame. Save and reopen the project, then verify the overlay remains in the preview.
6. Export a looping GIF. In the completion window, verify Open GIF, Show in folder, and Copy path; then check the GIF in a separate viewer for animation, edited ordering, overlay, timing, and expected canvas dimensions.
7. Export an MP4 with FFmpeg and `libx264` installed. Confirm the file opens in a media player and check that edited ordering, text overlay, variable frame timing, and padded even dimensions are correct. Also verify the app explains the FFmpeg requirement when FFmpeg is absent.
8. Repeat with a full display. On a multi-monitor setup, test a non-primary display and a mixed-DPI display if available.

## Challenge evidence to capture

Run the original WPF app and Frame Studio against the same content. Save paired home, recorder, editor, and export screenshots under `docs/before-after/`. Record the Windows version, display resolution and scale, selected region, frame rate, capture duration, frame count, and GIF dimensions. Include failures and any platform limitations in the migration write-up.

This checklist is preparation only. Fill in the environment and results after the Windows run; do not present the current macOS cross-publish or skipped test as runtime verification.
