# Migration journal

This log records the independent Avalonia port of ScreenToGif. The port uses new branding and is not an official ScreenToGif application. The upstream app remains the reference implementation.

## 2026-10-03 — Baseline and architecture audit

- Cloned `https://github.com/NickeManarin/ScreenToGif` into this workspace and created local branch `avalonia-port`.
- Recorded upstream `master` at `a4d0a67c2131cd048ceec86cd40afc2f1a06f2fd`, version `2.43.2`.
- Installed .NET SDK `9.0.318` and Avalonia templates `12.1.3`.
- Built the original solution on macOS with Windows targeting enabled. Source compilation succeeded when the Windows-only `editbin` post-build event was skipped. The original WPF executable cannot be launched on this macOS host.
- Audited all seven solution projects. The upstream Model, Util, ViewModel, Native, and UI projects are Windows/WPF-bound to varying degrees. GIF decoding and several stream/byte-oriented utilities are the clearest direct-reuse candidates. See [MIGRATION_AUDIT.md](MIGRATION_AUDIT.md) for project roles and file classifications.
- Verified that upstream `LICENSE.txt` is the Microsoft Public License (MS-PL). It grants derivative-work rights, requires preservation of notices and the complete license for source distributions, and does not grant trademark rights. Keep the full license and clear attribution; use independent branding.
- The upstream README links to launcher, recorder, editor, and settings screenshots/animations. This checkout contains no local baseline raster captures. Capture new local before/after images on Windows before challenge submission.

## 2026-10-03 — Avalonia shell milestone

- Created a separate `FrameStudio.sln` with `FrameStudio.Avalonia`, `FrameStudio.Core`, `FrameStudio.Platform.Abstractions`, and `FrameStudio.Tests`; the upstream `GifRecorder.sln` remains unchanged.
- Built the home screen with new Frame Studio branding, a two-theme token set, typographic and spacing styles, keyboard focus styling, screen-reader names, recent-project empty state, and the four capture source cards.
- Added neutral pixel geometry/frame/project types and platform contracts for recording sessions, screens, windows, webcams, monitors, hotkeys, clipboard, notifications, dialogs, and platform permissions. The app has no reference to the legacy WPF projects.
- Kept capture actions disabled and labeled as unavailable because no real adapter is registered yet. On this macOS host, the shell reports that the first capture target is Windows.
- Built the new solution with .NET SDK `9.0.318`: 0 warnings and 0 errors. Ran the initial core tests: 6 passed, 0 failed.
- Published and launched a macOS arm64 development bundle. Inspected its accessibility tree and captured [an early shell screenshot](before-after/after-home-shell-macos.png). The app was opened in the background without changing the active app.
- This image is a shell check only; it is not a challenge before/after image. The real comparison needs the original and port running on Windows against the same content.

## 2026-10-03 — Windows capture backend foundation

- Reviewed the [Avalonia Port Challenge rules](https://avaloniaui.net/blog/avalonia-port-challenge). The entry closes at 23:59 UTC on 23 October 2026. Judging covers migration difficulty/completeness, app quality, cross-platform delivery, engineering quality, and the migration write-up. Entry requirements include runnable builds for every target platform, paired before/after screenshots, and a short account of migration cost.
- Added `FrameStudio.Platform.Windows`, with monitor/window enumeration and a GDI desktop-region capture session. It uses the same BitBlt/GDI capture family as the upstream implementation while returning neutral RGBA buffers through `IRecordingSession`.
- Capture reads on a background task, uses a bounded three-frame channel to limit memory, supports pause/resume/stop, includes optional cursor compositing, and releases native GDI handles on completion. The capture rectangle is monitor-local device-pixel geometry.
- The backend cross-compiles on macOS, but runtime behavior still needs a Windows machine. The Avalonia UI does not yet start or consume a recording session; capture actions stay disabled until that end-to-end workflow is connected.
- Migrated the upstream raw-buffer GIF encoder and its octree, median-cut, grayscale, and most-used quantizers into `FrameStudio.Core`, replacing WPF color/rectangle types with RGBA and neutral pixel geometry. The neural quantizer was omitted because its file has a separate GPLv3 notice; the Frame Studio GIF output uses new branding.
- Added the `.fsp` project archive with per-frame compressed RGBA entries, timing/dimension manifest, frame reads, and atomic completion. Added a project-to-GIF export service using the migrated encoder; Core round-trip/export tests pass. The editor is not yet wired to these services.
- Strategy: prioritize the Legacy Revival and Everyday Tool categories through a complete, useful Windows workflow. Cross-platform judging is valuable, but only claim targets that have runnable builds and real platform capture support. The write-up and Windows before/after captures are required submission work, not optional polish.

## 2026-10-03 — Recording, editor, and GIF workflow

- Wired the Windows display and region setup into the home screen. The setup lets the user choose a display or drag a region, choose a frame rate, and include the pointer. The recorder drains a bounded capture queue into a compressed `.fsp` archive and supports pause, resume, and stop.
- Added an editor window with lazy frame thumbnails, preview, timed playback, frame selection, move earlier/later, duplicate/delete, per-frame duration changes, and save back to the project. The GIF export action exports the edited selection using its edited frame order and durations.
- Added a drag crop overlay and aspect-preserving project resize dialog. Both transform every frame through neutral RGBA operations and write to a temporary `.fsp` draft; Save atomically replaces the original, while the close prompt offers save, discard, or cancel. Resize currently uses nearest-neighbor sampling.
- Added archive rewriting so project edits are saved atomically, plus bounded validation for canvas dimensions, frame count, and GIF options. The archive and export tests cover a save/reopen/edit/export round trip.
- The GIF encoder and archive layers remain in `FrameStudio.Core`; Avalonia bitmap conversion lives at the view edge. The Windows platform service is only registered on Windows, so non-Windows builds do not present a nonfunctional capture action.
- `dotnet build FrameStudio.sln --configuration Debug` succeeded with no warnings or errors. `dotnet test FrameStudio.sln --configuration Debug` passed 11 tests and skipped the Windows desktop integration test on macOS. Self-contained Release publishes succeeded for macOS arm64 and Windows x64; the Windows package is cross-compiled and has not been run on Windows.
- Windows GDI capture has only been cross-compiled here; it still requires Windows runtime validation. An attempt to launch the new Avalonia UI in the background on this macOS host terminated in Avalonia.Native's render timer before exposing a window, so this milestone has not had a fresh visual UI smoke check. The earlier shell screenshot remains an early-shell capture only.
- The current port does not yet implement window recording, webcam, sketchboard, text/drawing annotations, or video export. Frame reordering uses explicit earlier/later controls; drag-to-reorder is not implemented. These limits should be explicit in any challenge write-up.
- Added a Windows-only desktop integration test for monitor discovery, frame dimensions/timing, and capture-session pause/resume/stop. It is skipped on macOS, so it is still pending execution on a real Windows desktop. See [WINDOWS_VALIDATION.md](WINDOWS_VALIDATION.md) for the run checklist.

## 2026-10-03 — Avalonia UI smoke check

- Published and launched the current macOS arm64 bundle in the background with a local 24-frame `.fsp` fixture. The app opened both the home window and editor window; the editor showed a 640 × 360 preview, frame thumbnails and durations, frame properties, playback, editing controls, and GIF export.
- Inspected the editor screenshot and accessibility tree without raising the app over the active user application. Saved the screenshot as [after-editor-preview-macos.jpg](before-after/after-editor-preview-macos.jpg).
- This is a rendering and project-open smoke check only. It does not test screen capture, editor operations, or GIF decoding in another viewer. It is not a challenge comparison image; Windows runtime validation and paired screenshots remain pending.

## 2026-10-03 — Core workflow verification

- Expanded the archive round-trip test to assert the intended blue → red → blue frame order and RGBA pixels after project rewrite, crop, and resize.
- The test now uses the editor's selected-frame export API with a red → blue → red selection, a duplicate frame, and changed durations. It parses GIF structure and checks the 2 × 2 canvas, three frames, 120/70/90 ms delays, and infinite-loop metadata. `dotnet test FrameStudio.sln --configuration Debug --no-restore` passes 11 tests on macOS and skips the Windows capture integration test.
- This test validates archive editing and GIF metadata; it does not decode exported GIF pixels in a viewer or verify the Windows GDI capture path.

## 2026-10-03 — Per-monitor scaling source

- The Windows app manifest declares Per-Monitor V2 DPI awareness. Review of the Win32 DPI documentation showed that `GetDpiForMonitor` is marked unsuitable for a per-monitor-aware thread. The Windows adapter now supplies physical monitor bounds, and the Avalonia UI matches those bounds to `Screens.All` and uses each `Screen.Scaling` for region-selector sizing and coordinate conversion.
- This follows Avalonia's screen API, whose bounds are device pixels and whose scaling is the OS-provided display scale. It reduces the risk of selecting the wrong pixel region on mixed-DPI displays, but still needs verification on Windows.
- References: [Microsoft GetDpiForMonitor documentation](https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor), [Avalonia Screen API](https://docs.avaloniaui.net/api/avalonia/platform/screen).

## 2026-10-03 — Independent binary identity and packaging

- The original root MSBuild properties label the WPF product as ScreenToGif 2.43.2 by Nicke Manarin. Added conditional metadata for `FrameStudio.*` projects so their binaries identify as Frame Studio 0.1.0 and no longer inherit the upstream author or repository fields; the original WPF projects retain their existing metadata.
- Added `scripts/package-windows-candidate.py` to publish a self-contained Windows x64 build, include the development README and complete license, validate the archive, and write a SHA-256 checksum. The package README marks it as unverified until Windows runtime checks pass.

## Migration decisions

- Keep the original WPF projects intact as the baseline and source reference.
- Build the Avalonia app in parallel, without a project reference to the WPF UI or other Windows-targeted projects.
- Put neutral frame/project logic in `FrameStudio.Core`; expose platform operations through `FrameStudio.Platform.Abstractions`; keep Windows implementations in `FrameStudio.Platform.Windows`.
- Keep Avalonia bitmap objects at the UI edge. Core pixel and frame data should use explicit neutral buffers and metadata.
- Start with an Avalonia shell and then connect a complete recording → project → edit → GIF export workflow before secondary features.

## Ongoing log

Add dated entries here as migrations reveal framework differences, platform constraints, or performance fixes.
