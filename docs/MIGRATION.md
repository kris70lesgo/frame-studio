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
- Built the current local package from clean commit `a2ee9a3`; Release assembly metadata reports Frame Studio `0.1.0`. The archive passed integrity validation and has SHA-256 `db2844326841f470ad0f2dc1a8baf4d4523ccaeeac83f3ed4f10dac9f2fb4e19`. It remains cross-published and unexecuted on Windows.

## 2026-10-03 — Persistent recent projects

- Replaced the fixed home-screen project count and empty state with a recent-project list. Opening a valid `.fsp` project records its full path and last-opened time in the user's local application data folder; the home screen shows its dimensions, frame count, and open time.
- The catalog keeps up to eight unique existing `.fsp` paths, writes updates atomically, and ignores damaged preference JSON or missing files. The editor still opens if saving recent history fails.
- Added tests for persistence across catalog instances, newest-first ordering, duplicate updates, the entry limit, missing files, and extension validation. Full solution build succeeds with zero warnings/errors; macOS test run passes 15 and skips the Windows capture integration test.
- A fresh visual check could not be made because the desktop session is locked. The existing macOS screenshots predate this home-screen update and remain explicitly non-comparison smoke evidence.

## 2026-10-03 — GIF pixel-stream verification

- Added a test-only GIF image-data reader that expands the encoder's LZW stream and compares decoded palette indexes against the octree quantizer output for a deterministic 64 × 64 pseudorandom image. The input exercises the transition from 9-bit to wider LZW codes.
- The editor export test still verifies selected frame order, canvas size, frame count, frame timing, and looping. The local suite now passes 15 tests and skips the Windows desktop capture integration test; the full solution builds with 0 warnings and 0 errors.

## 2026-10-03 — Editor command-path verification

- Added an Avalonia project reference to the test project so it can exercise `EditorViewModel` without launching a window. The test invokes the same commands as the UI for move, duplicate, duration edit, delete, save, and GIF export, then reads the saved frame payloads and exported GIF metadata.
- The complete macOS run passes 15 tests and skips the single Windows capture integration test. The full solution builds with zero warnings and errors. This verifies editor/model/export wiring, but cannot validate Win32 capture or visual interaction.

## 2026-10-03 — GIF export completion actions

- Added a compact completion window after a successful editor export with actions to open the GIF, open its folder, or copy the full path. The shell integration uses the operating system's file association and Avalonia clipboard, with errors shown in the completion state.
- The completion view builds with the solution. A fresh visual check is unavailable while the desktop session is locked; platform shell actions still need Windows runtime verification.

## 2026-10-03 — Capture readiness status

- Updated the home-screen capability detail to say Windows screen capture is wired and still needs runtime validation. Window capture, webcam, and sketchboard remain marked as planned on Windows; all capture remains unavailable on other platforms.
- Added a capability-report test for the host platform so UI status cannot silently regress to claiming capture support elsewhere.
- Fixed the Avalonia diagnostics package reference so its compile assets are available after a normal restore, and configured the current `WithDeveloperTools` options overload.
- The macOS suite now passes 16 tests and skips the Windows capture integration test. Debug and Release solution builds both complete with zero warnings and errors.
- The capture flow now excludes both the main window and recorder controls with `WDA_EXCLUDEFROMCAPTURE` before starting GDI capture, and restores their previous affinity afterward. Destroyed windows are skipped during cleanup so the recorder can close before the lease is released. The screen action is unavailable before Windows 10 version 2004; full-display validation must confirm GDI honors the exclusion and controls remain usable.
- Added Windows runtime tests for affinity apply/restore and cleanup after a window closes. They skip on this macOS host with the GDI integration test.
- The suite now passes 16 platform-independent tests and skips all three Windows runtime checks; the Release solution build completes with zero warnings and errors.

## 2026-10-03 — Windows candidate rebuilt

- Rebuilt the self-contained Windows x64 candidate from clean commit `85b7206` after adding persistent recent projects. The packager verified archive contents and wrote `dist/FrameStudio-win-x64.zip` with SHA-256 `05b35eb68221daba27661ec64cf6b5ab59a1ce9509ba56aa5046088ea2ad76d9`.
- The embedded README identifies the source revision, host, SDK, and unverified status. This is still a local development package; it has not been launched on Windows.

## 2026-10-03 — Candidate rebuilt with GIF pixel verification

- Rebuilt the self-contained Windows x64 candidate from clean commit `d605513`. The package integrity check passed and the current local archive has SHA-256 `b7367c25f15ac36d07a497469d3c0a06d39c02afbe89f7463c3e18f0114fb650`.
- The package includes the persistent recent-project home screen. Its source README still labels it as an unverified local candidate; the Windows desktop runtime gate has not been run.

## 2026-10-03 — Candidate rebuilt with editor workflow checks

- Rebuilt the self-contained Windows x64 candidate from clean commit `716aad0`. The archive integrity check passed and its SHA-256 is `67c390f48e9b03283420faa9256e1ed989ae6d7cfda7b3cf3937703dc38e9c8b`.
- The current test project exercises the editor view model's edit, save, and GIF export commands. The candidate still has not been run on Windows; its bundled README says this explicitly.

## 2026-10-03 — Candidate rebuilt with export completion actions

- Rebuilt the self-contained Windows x64 candidate from clean commit `88ed735`. ZIP integrity and SHA-256 checks passed; the current local candidate hash is `d9cd20a388a21aa4128b89b7fff9aa0dbc6c34bdd721bad0071b9aa342931d5e`.
- The bundled build includes the GIF completion actions and accurately identifies its macOS build host and unverified Windows runtime status.

## 2026-10-03 — Candidate rebuilt with capture status and restore fix

- Rebuilt the self-contained Windows x64 candidate from clean commit `6a5b23c`. ZIP integrity and an independent SHA-256 check passed; the archive hash is `75537e8b0e044d75c1d400490af488e8114aea613278c48451e37fe9f6c3b26a`.
- The bundled README identifies its source revision and macOS build environment. It remains unlaunched on Windows, and the capture workflow is still marked unverified.

## Migration decisions

- Keep the original WPF projects intact as the baseline and source reference.
- Build the Avalonia app in parallel, without a project reference to the WPF UI or other Windows-targeted projects.
- Put neutral frame/project logic in `FrameStudio.Core`; expose platform operations through `FrameStudio.Platform.Abstractions`; keep Windows implementations in `FrameStudio.Platform.Windows`.
- Keep Avalonia bitmap objects at the UI edge. Core pixel and frame data should use explicit neutral buffers and metadata.
- Start with an Avalonia shell and then connect a complete recording → project → edit → GIF export workflow before secondary features.

## Ongoing log

Add dated entries here as migrations reveal framework differences, platform constraints, or performance fixes.
