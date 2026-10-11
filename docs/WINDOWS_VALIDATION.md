# Windows validation checklist

The Windows native capture backend now passes its integration suite on a hosted Windows runner. The app itself has not yet been launched in an interactive Windows desktop session; this checklist records the remaining evidence needed before claiming the record → edit → export workflow is verified. Screen recording requires Windows 10 version 2004 (build 19041) or later because the app excludes its own windows using [`WDA_EXCLUDEFROMCAPTURE`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity). MP4 export also needs an installed FFmpeg build with `libx264` available on `PATH`.

## Current Windows candidate (2026-10-11)

- The current candidate is attached to the unpublished [Preview 4 draft release](https://github.com/kris70lesgo/frame-studio/releases/tag/untagged-c005c95196d6af81208f). It contains commit `579ed9afaa30c798677880f37a93300a473dcb80`, including stable custom-size aspect-ratio entry in the format-specific GIF/MP4 export dialog. The release remains unpublished until the interactive Windows workflow and screenshot comparison are complete.
- [CI run 38122088760](https://github.com/kris70lesgo/frame-studio/actions/runs/38122088760) passed 46 deterministic Windows tests (one FFmpeg-dependent skip), all 7 native capture checks, and packaged-app startup smoke; macOS passed 46 deterministic tests (one FFmpeg-dependent skip). The downloadable [CI artifact 11695349183](https://github.com/kris70lesgo/frame-studio/actions/runs/38122088760/artifacts/11695349183) has SHA-256 `4fb41c4b23fe14f58d0207ae746c2147f70b03f6318fe403e2cd0505805715b0`.
- Local package: [dist/FrameStudio-win-x64.zip](/Users/agastya/Documents/alvonia/ScreenToGif/dist/FrameStudio-win-x64.zip) (self-contained `win-x64`; package directory is ignored by Git). Its hash matches the sidecar and draft release; ZIP integrity passed.
- The latest local macOS Release test run passed 50 tests and skipped four Windows-only desktop checks, including the screen and window desktop integrations.
- Hosted CI verifies the native backend and package startup only. It does not verify interactive controls, app-window exclusion in real recordings, mixed-DPI selection, or the full record → edit → export workflow on your desktop. This is not yet a verified capture release.

## Download, verify, and launch

1. Download `FrameStudio-win-x64.zip` and its `.sha256` sidecar from the [Preview 4 draft release](https://github.com/kris70lesgo/frame-studio/releases/tag/untagged-c005c95196d6af81208f).
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

1. Launch Frame Studio. Confirm Screen and Window actions are available while Webcam and Sketchboard remain disabled. In Settings, change the theme, recording defaults, preview loop, GIF palette/loop/scale/frame rate/dithering, and MP4 quality/scale/frame rate. Save, restart, and confirm those choices persist. Check that the Advanced page can open the app data folder and configure a local FFmpeg executable when available.
2. Choose a display and a small region. Record for several seconds, pause, resume, and stop. Verify that neither Frame Studio's home window nor the floating recorder controls appear in the captured frames.
3. Confirm a `.fsp` project is written and opens in the editor. Check preview, playback, thumbnail rendering, frame durations, duplicate/delete, and move earlier/later.
4. Drag a crop over the preview and apply it. Resize the project with aspect ratio retained, save, close, reopen, and confirm dimensions and edits persist.
5. Add a text overlay and confirm it appears in every frame. Save and reopen the project, then verify the overlay remains in the preview.
6. Export a GIF. In the options dialog, check source timing and fixed frame rates, palette/quantizer, color limit, dithering, loop behavior, preset/custom dimensions, and the output preview. In the completion window, verify Open GIF, Show in folder, and Copy path; then check the GIF in a separate viewer for animation, edited ordering, overlay, timing, and expected dimensions.
7. Export an MP4 with FFmpeg and `libx264` installed. Check source timing and fixed frame rates, CRF quality, preset/custom dimensions, and the output preview. Confirm the file opens in a media player and check edited ordering, text overlay, timing, and padded even dimensions. Also verify the app explains the FFmpeg requirement when FFmpeg is absent.
8. Repeat with a full display. On a multi-monitor setup, test a non-primary display and a mixed-DPI display if available.
9. Record a window while another window overlaps it. Confirm the recording shows the selected window's content rather than the covering window; note any blank or incomplete output from hardware-accelerated or protected apps.

## Challenge evidence to capture

Run the original WPF app and Frame Studio against the same harmless content where the mode exists. The source baseline is [ScreenToGif 2.43.2](https://github.com/NickeManarin/ScreenToGif/releases/tag/2.43.2), built from the audited upstream commit `a4d0a67`. Save before/after screenshots of the home, recorder, webcam, sketchboard, editor, export, and settings views under `docs/before-after/`. For webcam and sketchboard, show Frame Studio's clearly marked unavailable state and do not present it as migrated functionality. Record the Windows version, display resolution and scale, selected region, frame rate, capture duration, frame count, and GIF dimensions. Include failures and platform limitations in the migration write-up.

Use [the Windows result template](WINDOWS_TEST_RESULTS_TEMPLATE.md) to record the run and collect the screenshot names. Do not include the raw recording or project if it contains private desktop content; a harmless test scene is sufficient for the comparison.

The hosted integration suite is backend evidence only. Fill in the environment and results after the interactive Windows app run; do not present the CI result as verification of the app UI, app-window exclusion in recordings, or the full record → edit → export workflow. Preferences provide all seven requested categories and persist editor/export defaults. The current export dialog supports GIF dithering, fixed frame rate, and custom dimensions, plus MP4 fixed frame rate and custom dimensions; verify these controls and the resulting files during the Windows run.
