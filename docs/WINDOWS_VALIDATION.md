# Windows validation checklist

The Windows capture backend and app package have been cross-compiled on macOS, but have not yet been run on Windows. This checklist records the evidence needed before claiming the record → edit → GIF workflow is verified. Screen recording requires Windows 10 version 2004 (build 19041) or later because the app excludes its own windows using [`WDA_EXCLUDEFROMCAPTURE`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity).

## Current local Windows x64 candidate

- Package: `dist/FrameStudio-win-x64.zip` (ignored local build output; not publicly downloadable).
- Rebuild it with `python3 scripts/package-windows-candidate.py`; this reads the Frame Studio version from MSBuild, includes the README and license, checks archive contents, and writes the matching `.sha256` file.
- Source revision: `5b589a6`; Frame Studio `0.1.0`, self-contained `win-x64` publish using .NET SDK `9.0.318` on macOS arm64.
- SHA-256: `95c4a2271c919abd5ac0f80e2588e0cb7bf1998b888b05e1daf0152eaa00a157` (also written to `dist/FrameStudio-win-x64.zip.sha256`).
- Archive integrity passed with `unzip -t`; the archive includes the package README and complete MS-PL license. This does not establish that the executable runs or captures correctly on Windows.

## Automated desktop check

Run from a Windows 10 version 2004+ or Windows 11 desktop session with at least one active display:

```powershell
dotnet test FrameStudio.sln --configuration Debug
```

`WindowsCaptureIntegrationTests` includes cross-platform checks that malformed screen and window requests fail before native calls. Three desktop-only tests check capture-window affinity apply/restore, cleanup after a window closes, and monitor enumeration plus a 64 × 64 pixel capture with pause, resume, and stop. The desktop tests are intentionally skipped off Windows and are not evidence until they pass on a Windows desktop.

## Manual recording and editing check

1. Launch Frame Studio. Confirm the Screen action is available and the unsupported sources remain disabled.
2. Choose a display and a small region. Record for several seconds, pause, resume, and stop. Verify that neither Frame Studio's home window nor the floating recorder controls appear in the captured frames.
3. Confirm a `.fsp` project is written and opens in the editor. Check preview, playback, thumbnail rendering, frame durations, duplicate/delete, and move earlier/later.
4. Drag a crop over the preview and apply it. Resize the project with aspect ratio retained, save, close, reopen, and confirm dimensions and edits persist.
5. Export a looping GIF. In the completion window, verify Open GIF, Show in folder, and Copy path; then check the GIF in a separate viewer for animation, edited ordering and timing, and expected canvas dimensions.
6. Repeat with a full display. On a multi-monitor setup, test a non-primary display and a mixed-DPI display if available.

## Challenge evidence to capture

Run the original WPF app and Frame Studio against the same content. Save paired home, recorder, editor, and export screenshots under `docs/before-after/`. Record the Windows version, display resolution and scale, selected region, frame rate, capture duration, frame count, and GIF dimensions. Include failures and any platform limitations in the migration write-up.

This checklist is preparation only. Fill in the environment and results after the Windows run; do not present the current macOS cross-publish or skipped test as runtime verification.
