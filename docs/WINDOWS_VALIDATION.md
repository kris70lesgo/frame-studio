# Windows validation checklist

The Windows native capture backend now passes its integration suite on a hosted Windows runner. The app itself has not yet been launched in an interactive Windows desktop session; this checklist records the remaining evidence needed before claiming the record → edit → export workflow is verified. Screen recording requires Windows 10 version 2004 (build 19041) or later because the app excludes its own windows using [`WDA_EXCLUDEFROMCAPTURE`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity). MP4 export also needs an installed FFmpeg build with `libx264` available on `PATH`.

## Current Windows candidate (2026-10-11)

- The current candidate is attached to the unpublished [Preview 4 draft release](https://github.com/kris70lesgo/frame-studio/releases/tag/untagged-de6181b6f1e45addf0d1). It contains app-code commit `7df7c6e0bba7f1892810e3f4d63a2b450b8cdd25`, including the Windows MediaCapture webcam workflow and software Sketchboard. The release remains unpublished until the interactive Windows workflow and screenshot comparison are complete.
- [CI run 38142554754](https://github.com/kris70lesgo/frame-studio/actions/runs/38142554754) passed macOS and Windows builds/tests, seven native Windows capture checks, and the packaged-app startup smoke.
- Local CI artifact: [dist/FrameStudio-win-x64.zip](/Users/agastya/Documents/alvonia/ScreenToGif/dist/FrameStudio-win-x64.zip) (self-contained `win-x64`; package directory is ignored by Git). Package revision: `7df7c6e`; ZIP SHA-256: `d2c71afe9b7f358912979dd672379fa778f104c9c83bb190c29b345d05c59944`. The sidecar matches, ZIP integrity passed, and the included README identifies the hosted Windows build.
- The latest local Release build completed with zero warnings/errors; 55 tests passed and four Windows-only desktop tests skipped on macOS. Hosted CI verifies the Windows-targeted webcam build, screen/window backend, and package startup, but no physical camera was available to CI.
- Interactive camera/privacy behavior, recorder controls, app-window exclusion in real recordings, mixed-DPI selection, and the full record → edit → export workflow still need your Windows desktop run. This is not yet a verified capture release.

## Download, verify, and launch

1. Download `FrameStudio-win-x64.zip` and its `.sha256` sidecar from the [Preview 4 draft release](https://github.com/kris70lesgo/frame-studio/releases/tag/untagged-de6181b6f1e45addf0d1). The current package identifies source revision `7df7c6e` in its `README.txt`.
2. In PowerShell, from the folder containing that inner ZIP, verify its hash:

   ```powershell
   Get-FileHash .\FrameStudio-win-x64.zip -Algorithm SHA256
   ```

   Confirm the result matches the SHA-256 in the sidecar and draft release notes.
3. Extract the candidate and launch the app:

   ```powershell
   Expand-Archive .\FrameStudio-win-x64.zip -DestinationPath .\candidate
   .\candidate\FrameStudio-win-x64\FrameStudio.Avalonia.exe
   ```

The candidate requires Windows 10 version 2004 or later, or Windows 11, and an interactive desktop session.

## Preview 3 release record

- Older public preview: [Frame Studio 0.1.0 Preview 3](https://github.com/kris70lesgo/frame-studio/releases/tag/v0.1.0-preview.3), built from an earlier revision.
- Rebuild the current local package with `python3 scripts/package-windows-candidate.py`; it publishes the current source, includes the README and license, checks archive contents, and writes the matching `.sha256` file.
- Source revision: `5184f661`; Frame Studio `0.1.0`, self-contained `win-x64` publish using .NET SDK `9.0.318` on macOS arm64.
- SHA-256: `de2c97b7ff8a63d1fd0be7ba62c30a502d6b8b3797389cec02e65a910cc9fb65` (recorded with the Preview 3 release asset).
- At release time, the package script, an independent `unzip -t` check, and a separate SHA-256 calculation confirmed archive integrity; GitHub reports the same ZIP digest. The archive contains the executable, package README, and complete MS-PL license. Its README identifies the commit and macOS build host and explicitly says Windows execution, capture, mixed-DPI selection, and end-to-end workflow are unverified. The [hosted CI run](https://github.com/kris70lesgo/frame-studio/actions/runs/37146520922) passed its macOS build/test, Windows build/test, and Windows x64 publishing jobs for this revision.

## Automated desktop check

Run from a Windows 10 version 2004+ or Windows 11 desktop session with at least one active display:

```powershell
dotnet test FrameStudio.sln --configuration Debug
```

`WindowsCaptureIntegrationTests` includes three request-validation checks that run on every OS. Its four Windows checks cover capture-window affinity apply/restore, cleanup after a window closes, screen-frame dimensions and pause/resume/stop, and pixel-accurate `PrintWindow` output when another window overlaps the target. All 7 checks passed on hosted Windows in [run 38119222955](https://github.com/kris70lesgo/frame-studio/actions/runs/38119222955); the four desktop checks skip on macOS. The hosted tests and main-window smoke are backend/launch evidence only; they do not replace launching Frame Studio on the target Windows desktop and checking the actual recorder windows, display picker, and mixed-DPI behavior.

The MP4 editor-export integration test requires both `ffmpeg` and `ffprobe` on `PATH`. It checks an edited two-frame project with changed durations by reading the emitted MP4 presentation timestamps.

## Manual recording and editing check

1. Launch Frame Studio. Confirm Screen, Window, Webcam, and Sketchboard actions are available. Check Windows Settings > Privacy & security > Camera > Let desktop apps access your camera. Select a camera, start recording, verify its live preview, pause/resume, stop, and confirm the `.fsp` opens with the expected camera frames and dimensions. Then open Sketchboard, draw a few strokes, use undo and clear, record, pause/resume, and stop; confirm its `.fsp` opens with the drawing animation. In Settings, change the theme, recording defaults, preview loop, GIF palette/loop/scale/frame rate/dithering, and MP4 quality/scale/frame rate. Save, restart, and confirm those choices persist. Check that the Advanced page can open the app data folder and configure a local FFmpeg executable when available.
2. Choose a display and a small region. Record for several seconds, pause, resume, and stop. Verify that neither Frame Studio's home window nor the floating recorder controls appear in the captured frames.
3. Confirm a `.fsp` project is written and opens in the editor. Check preview, playback, thumbnail rendering, frame durations, duplicate/delete, and move earlier/later.
4. Drag a crop over the preview and apply it. Resize the project with aspect ratio retained, save, close, reopen, and confirm dimensions and edits persist.
5. Add a text overlay and confirm it appears in every frame. Save and reopen the project, then verify the overlay remains in the preview.
6. Export a GIF. In the options dialog, check source timing and fixed frame rates, palette/quantizer, color limit, dithering, loop behavior, preset/custom dimensions, and the output preview. In the completion window, verify Open GIF, Show in folder, and Copy path; then check the GIF in a separate viewer for animation, edited ordering, overlay, timing, and expected dimensions.
7. Export an MP4 with FFmpeg and `libx264` installed. Check source timing and fixed frame rates, CRF quality, preset/custom dimensions, and the output preview. Confirm the file opens in a media player and check edited ordering, text overlay, timing, and padded even dimensions. Also verify the app explains the FFmpeg requirement when FFmpeg is absent.
8. Repeat with a full display. On a multi-monitor setup, test a non-primary display and a mixed-DPI display if available.
9. Record a window while another window overlaps it. Confirm the recording shows the selected window's content rather than the covering window; note any blank or incomplete output from hardware-accelerated or protected apps.

## Challenge evidence to capture

Run the original WPF app and Frame Studio against the same harmless test scene and project. The source baseline is [ScreenToGif 2.43.2](https://github.com/NickeManarin/ScreenToGif/releases/tag/2.43.2), built from the audited upstream commit `a4d0a67`. Save the four challenge comparison pairs under `docs/before-after/`: home, recorder, editor, and export. Use matching content and comparable window sizes where possible. The software sketchboard is an implemented feature, but it is not a substitute for webcam capture; record its Windows behavior separately if tested. Record the Windows version, display resolution and scale, selected region, frame rate, capture duration, frame count, and GIF dimensions. Include failures and platform limitations in the migration write-up.

Use [the Windows result template](WINDOWS_TEST_RESULTS_TEMPLATE.md) to record the run and collect the screenshot names. Do not include the raw recording or project if it contains private desktop content; a harmless test scene is sufficient for the comparison.

The hosted integration suite is backend evidence only. Fill in the environment and results after the interactive Windows app run; do not present the CI result as verification of the app UI, app-window exclusion in recordings, or the full record → edit → export workflow. Preferences provide all seven requested categories and persist editor/export defaults. The current export dialog supports GIF dithering, fixed frame rate, and custom dimensions, plus MP4 fixed frame rate and custom dimensions; verify these controls and the resulting files during the Windows run.
