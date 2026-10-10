# Migration journal

This log records the independent Avalonia port of ScreenToGif. The port uses new branding and is not an official ScreenToGif application. The upstream app remains the reference implementation.

## 2026-10-10 — Current UI and Windows candidate check

- Rebuilt the candidate against PR head `95a7c67` in [run 38028043199](https://github.com/kris70lesgo/frame-studio/actions/runs/38028043199). All three checks passed; the Windows package smoke observed a top-level window titled “Frame Studio”. I downloaded [artifact 11660393817](https://github.com/kris70lesgo/frame-studio/actions/runs/38028043199/artifacts/11660393817), verified its sidecar SHA-256 `a4628e48909f849cfa39d41436ade55ef6d2965263397e24b548be7db077a38c`, and confirmed ZIP integrity. This does not replace an interactive Windows capture/edit/export run.
- Strengthened the hosted package smoke check to require the actual “Frame Studio” main-window handle and title within 10 seconds. [Run 38027584930](https://github.com/kris70lesgo/frame-studio/actions/runs/38027584930) passed the macOS and Windows test jobs, all six native capture tests, the main-window smoke check, and Windows packaging. The smoke still does not exercise interactive recording or editing.
- Refreshed the candidate from [run 38027584930](https://github.com/kris70lesgo/frame-studio/actions/runs/38027584930). I downloaded [artifact 11661060981](https://github.com/kris70lesgo/frame-studio/actions/runs/38027584930/artifacts/11661060981), verified SHA-256 `b5883fe72387d9a93ba35e4643fe4c9b1780ffab330865211cc9e9317d1eb4c3` against its sidecar, and confirmed ZIP integrity. Interactive Windows capture and challenge screenshots still require a desktop run.
- Added a hosted Windows package smoke check that starts `FrameStudio.Avalonia.exe`, confirms it survives 10 seconds, then terminates it. This is startup evidence, not an interactive desktop test. The package README now states that distinction. [Run 38027260796](https://github.com/kris70lesgo/frame-studio/actions/runs/38027260796) passed the macOS and Windows test jobs, all six native capture tests, startup smoke, and Windows package job.
- Refreshed the candidate from [run 38027260796](https://github.com/kris70lesgo/frame-studio/actions/runs/38027260796). I downloaded [artifact 11661030597](https://github.com/kris70lesgo/frame-studio/actions/runs/38027260796/artifacts/11661030597), verified SHA-256 `2bd86c1a8bc0cc0d94af0ad67710be3c779240e6678b34ebc6b2014b0d5fb834` against its sidecar, and confirmed ZIP integrity. Interactive Windows capture and the challenge screenshot pairs still need a desktop run.
- Reused a single open ZIP read session across frame edits and exports to avoid reopening the project archive for every frame. Windows CI revealed that replacing an edited `.fsp` in place must happen only after closing the source archive handle; the rewrite paths now release that handle before atomic replacement. The local Release suite passed 44 tests and skipped the three Windows-only desktop checks.
- [GitHub Actions run 38026224608](https://github.com/kris70lesgo/frame-studio/actions/runs/38026224608) passed macOS and Windows builds/tests plus Windows x64 packaging for source commit `b429e65f`. Hosted Windows ran 40 deterministic tests (one FFmpeg-dependent MP4 test skipped) and all 6 native capture tests. I downloaded [artifact 11660630394](https://github.com/kris70lesgo/frame-studio/actions/runs/38026224608/artifacts/11660630394), verified its sidecar SHA-256 `1ed998d21940d72caa329888fe950da5602d1041ef5dd92548e18f4791524cc4`, and confirmed ZIP integrity. It still requires the interactive Windows app run before submission.

- Reworked the Avalonia home and editor into a Jitter-inspired light workspace with a compact dark toolbar, frame navigation, centered canvas, inspector, and bottom timeline. Frame Studio keeps its own branding and recording/editing actions.
- Visually reviewed the current home/editor preview and resize dialog on macOS. This is not a Windows before/after comparison; the checked-in macOS screenshots remain historical.
- Reopened the current checkout from a temporary `osx-arm64` app bundle and loaded the synthetic 8-frame `.fsp` sample. The current capture workspace and editor rendered; the screen-recording controls correctly report that capture is Windows-only. This smoke check did not record, edit, export, or produce challenge comparison screenshots.
- Updated the project picker with an Apple UTI and `.fsp` extension validation because Avalonia's glob `Patterns` are not used by Apple file pickers.
- Rechecked the Release solution locally on macOS arm64: `dotnet test FrameStudio.sln --configuration Release --no-restore` passed 44 tests and skipped the three Windows desktop checks; `dotnet build FrameStudio.sln --configuration Release --no-restore` completed with 0 warnings and 0 errors. This is core/build evidence only and does not verify the interactive Windows capture flow.
- Built the self-contained Windows x64 candidate on the Windows runner from clean app revision `408babf6`. The [CI artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/37977060116/artifacts/11638503643) contains the ZIP and checksum; I downloaded it, verified the SHA-256 `612507e387c528a9b6dcbd89611a5dc5bc687ecbfb533625ad2166dd844f6e78` with `shasum -a 256 -c`, and verified the ZIP with `unzip -t`. Its README records the source revision and says interactive Windows runtime behavior remains unverified.
- [GitHub Actions run 37977060116](https://github.com/kris70lesgo/frame-studio/actions/runs/37977060116) passed macOS and Windows build/test jobs and created the Windows package artifact using the updated Node 24 action runtimes. The hosted test command filters out the three interactive Windows desktop-capture checks.
- Re-ran the workflow on the latest PR head. [GitHub Actions run 37978232349](https://github.com/kris70lesgo/frame-studio/actions/runs/37978232349) passed macOS build/tests, Windows build/tests, and Windows x64 packaging. Its [artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/37978232349/artifacts/11639728675) was downloaded locally; its package README identifies checkout revision `7cd1b0a`, and its SHA-256 `a889b6a9781e6a722be3b5556b35d4277c86e52cd1f6305f51a570bddd2493d5` and ZIP integrity both verify. The application source is unchanged from `408babf6`; subsequent commits only update documentation and CI. The refreshed local copy is `dist/FrameStudio-win-x64.zip`.
- Added a Windows-only CI step for `WindowsCaptureIntegrationTests`. [Run 37979662238](https://github.com/kris70lesgo/frame-studio/actions/runs/37979662238) passed all 6 tests on hosted Windows: 29 deterministic tests passed with one FFmpeg-dependent skip, and the native suite verified window-affinity lifecycle plus GDI display capture across pause/resume/stop. This verifies the service layer on a Windows runner; it does not replace launching the app and checking the real recording UI, app-window exclusion, or mixed-DPI selection on the user's desktop.
- Rebuilt and refreshed the candidate from PR head `4b39179a3d587a3d0ba94244a3da9d4802cc0c40`. [Run 37980913612](https://github.com/kris70lesgo/frame-studio/actions/runs/37980913612) passed macOS and Windows builds/tests plus Windows packaging; Windows deterministic tests passed 29 with one FFmpeg-dependent skip, and all 6 native capture integration tests passed. The current [artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/37980913612/artifacts/11640408653) was downloaded to `dist/FrameStudio-win-x64.zip`; its README records `f7fc082`, GitHub's pull-request merge revision, and its SHA-256 `a8b3813ac4532d676c617877d7e18d8882e118ad077e67b2b10fe8d517d45e14` and ZIP integrity were independently verified. The package continues to state that interactive Windows runtime behavior is unverified.
- Refreshed the candidate against PR head `60e97d11dfcf01469880ab0ecab65b979cdeb40e`. [Run 37981332433](https://github.com/kris70lesgo/frame-studio/actions/runs/37981332433) passed all build, test, native capture, and package jobs. Its [artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/37981332433/artifacts/11641203018) was downloaded to `dist/FrameStudio-win-x64.zip`; its README records `66b942f`, GitHub's pull-request merge revision, and its SHA-256 `1ce102ce20e73918a313d7a71d9fb8944538fe316df9006b3226de1ae238ac4b` and ZIP integrity were independently verified. The package continues to state that interactive Windows runtime behavior is unverified.
- Refreshed the candidate again from PR head `9fbbd0adda6bf7fe38275b339661d9711fb1379c`. [Run 38023644831](https://github.com/kris70lesgo/frame-studio/actions/runs/38023644831) passed macOS and Windows builds/tests plus Windows packaging. The [artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38023644831/artifacts/11659890843) is saved as `dist/FrameStudio-win-x64.zip`; its README records `c0805d9`, GitHub's synthetic pull-request merge revision. Its SHA-256 `0f6f7f4170b8b7e8d716f366a7e24984c82d9e7e941ae24126e3d9308775a98b` matches GitHub's checksum, and `unzip -t` reports no errors. The package continues to state that interactive Windows runtime behavior is unverified.
- Implemented the public `TransparentColorIndex` property instead of returning zero unconditionally: it now finds the transparent color in the built palette, including palettes whose actual count is below the configured maximum. Added a regression test for that shorter-palette case. At that checkpoint, the local Release suite passed 34 tests with three Windows-only skips; the latest count is recorded below.
- Refreshed the Windows candidate from source commit `b635dbcf97cd02f26cc54711369c3c1adde25050`. [Run 38024427879](https://github.com/kris70lesgo/frame-studio/actions/runs/38024427879) passed macOS and Windows builds/tests plus Windows packaging. Its [artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38024427879/artifacts/11659837299) is saved as `dist/FrameStudio-win-x64.zip`; the bundled README records `b635dbc`. Its SHA-256 `5925e304b494f350eaa1c2ff92f4ffbdc59e1cbb22df6fb17d8ad06459dfcaed` matches GitHub's checksum, and `unzip -t` reports no errors. The package continues to state that interactive Windows runtime behavior is unverified.
- Updated the CI workflow to GitHub's current Node 24 action releases (`checkout@v7`, `setup-dotnet@v6`, and `upload-artifact@v7`) after the previous run warned that older actions were being forced onto Node 24. Both push and pull-request runs passed without the runtime deprecation warning.
- The Win32 capture backend passes hosted Windows integration tests, but the full app still needs an interactive Windows run. Webcam, isolated window capture, and sketchboard remain outside this focused first port. Fresh paired screenshots and the hands-on migration time were not collected, so the app is not ready for challenge submission yet.
- Added and tested a shared screen-region coordinate converter that maps device-pixel monitor bounds to Avalonia logical coordinates using the selected screen's scale. Unit cases cover 75%, 100%, 125%, 150%, and 200% scale, clipping, and invalid scales. This validates conversion math, not real mixed-DPI monitor selection.
- Refreshed the Windows candidate from source commit `8e2819bdd8215aa7ee1ed40a8bd7a0814a44ac48`. [Run 38024903827](https://github.com/kris70lesgo/frame-studio/actions/runs/38024903827) passed macOS and Windows build/test plus Windows packaging. Its [artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38024903827/artifacts/11659538490) is saved as `dist/FrameStudio-win-x64.zip`; the bundled README records synthetic PR merge revision `717e3d1`. Its SHA-256 `f356725626996e64a7afdebd8c579868befba3ae8f6dfc5453fc77c286d4b637` matches GitHub's checksum, and `unzip -t` reports no errors. The local Release suite passed 44 tests, with the three Windows desktop tests skipped. Interactive Windows runtime behavior, same-content WPF/Avalonia screenshots, and measured migration effort remain unverified.
- Opened the current editor preview on macOS and inspected the text annotation dialog plus the no-stroke freehand panel; no text or drawing was applied or saved. The MP4 export button was visible, but the export flow and output were not exercised. This is a local UI smoke check, not Windows evidence; no new challenge screenshot was saved.
- Refreshed the exact PR-head Windows x64 candidate from [run 38025357431](https://github.com/kris70lesgo/frame-studio/actions/runs/38025357431). Its [artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38025357431/artifacts/11660098796) was downloaded into ignored `dist/FrameStudio-win-x64.zip`; its bundled README records synthetic PR merge revision `7b7057a`, while the application source remains `8e2819bd`. The SHA-256 `d7a970b4737510b8bf28abd53705ad45b613902b3180c30e79896b67ff3cf80b` matches the artifact sidecar and `unzip -t` reports no errors. It includes the complete license and explicitly says interactive Windows behavior is unverified. The PR-head build, test, and packaging jobs all passed.
- Updated the disabled Window, Webcam, and Sketchboard toolbar tooltips and accessibility names to identify those features as unavailable yet instead of calling this a shell build. The Release solution build completed with zero warnings and errors.
- Refreshed the candidate after the UI copy change from [run 38025703034](https://github.com/kris70lesgo/frame-studio/actions/runs/38025703034). Its [artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38025703034/artifacts/11660164331) is saved as `dist/FrameStudio-win-x64.zip`; its bundled README records source commit `18bed75`. SHA-256 `f982579fe67a2d7824c88f5dc4eb4288418ff6dc20e3c73c2bea5f5e79d0866b` matches the sidecar, and the ZIP passes integrity validation. The local Release suite passed 44 tests with the three Windows desktop checks skipped. Hosted Windows passed 40 deterministic tests, skipped one FFmpeg-dependent MP4 test, and passed all 6 native capture integration tests. Interactive Windows behavior and screenshot evidence remain outstanding.

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
- The suite now passes 17 platform-independent tests and skips all three Windows runtime checks; the Release solution build completes with zero warnings and errors.

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

## 2026-10-03 — Candidate rebuilt with capture-window exclusion

- Rebuilt the self-contained Windows x64 candidate from clean commit `b07ffa9`. ZIP integrity and an independent SHA-256 check passed; the archive hash is `dec90ed102edee6708005010ee9a094fbc028f3b23b4f20df452e771ec214dfa`.
- The package requires Windows 10 version 2004 or later and still labels the record → edit → GIF workflow unverified until it has been run on Windows.

## 2026-10-03 — Recording-to-project handoff check

- Added a platform-independent `RecordingViewModel` test with a fake capture session. It exercises pause, resume, and stop, reads the completed `.fsp` archive, and verifies captured RGBA bytes and per-frame durations. This checks the recorder-to-project handoff without claiming native capture validation.
- The macOS suite passes 17 tests and skips the three Windows runtime checks.

## 2026-10-03 — Candidate rebuilt with recorder handoff check

- Rebuilt the self-contained Windows x64 candidate from clean commit `485c57b`. ZIP integrity and independent SHA-256 checks passed; the archive hash is `f5af7525bf4ab88b8552c20ba683322b4d16aa255f152a2ead1d66275921eee0`.
- The package carries the updated capture-window exclusion path and the Windows 10 version 2004+ requirement. It still has not been launched on Windows.

## 2026-10-03 — Theme and timeline selection smoke check

- Launched the local Avalonia debug bundle with a 24-frame `.fsp` fixture and inspected the home screen and editor in light mode. The editor displays frame preview, thumbnail timeline, duration inspector, editing controls, and GIF export; the selected frame is outlined in teal without Avalonia's default blue selection fill.
- Added explicit transparent ListBox item styling to preserve the timeline card appearance while retaining a visible teal selection border.
- This visual check ran on macOS and only verifies rendering. Windows capture and the paired original/port challenge screenshots remain outstanding.

## 2026-10-03 — Editor keyboard shortcuts

- Added save, duplicate, frame reorder, frame delete, playback, and crop-cancel shortcuts. Ctrl and Command both activate the primary shortcuts. The tooltip text lists each gesture.
- Registered the handler in the tunnel phase so the timeline does not consume Space or Delete before the editor can handle them. Live UI checks confirmed save, duplicate, reorder, delete, playback, and crop cancel against a temporary 24-frame project; the temporary edits were discarded.
- Window capture remains disabled in the home screen because the current Win32 implementation captures the target's desktop rectangle, which can include windows above the selected app.

## 2026-10-03 — Candidate rebuilt with editor shortcuts

- Rebuilt the self-contained Windows x64 candidate from clean commit `629b74f`. ZIP integrity and independent SHA-256 checks passed; its hash is `584917e877adcc91e19cd0646d92bf84cce753249f0d17d5bc5aaef5e83f1c40`.
- The package carries the editor keyboard shortcuts and keeps Windows runtime behavior labeled unverified.

## 2026-10-03 — Capture request validation checks

- Added platform-independent tests confirming that invalid screen frame rates, empty screen regions, and invalid window frame rates are rejected before monitor enumeration or other Win32 calls.
- The focused test run passes 20 tests and skips the three Windows desktop integration checks on macOS. The new checks guard early request validation; they do not verify the GDI backend or app-window exclusion on Windows.

## 2026-10-03 — Candidate rebuilt with capture validation

- Rebuilt the self-contained Windows x64 candidate from clean commit `5b589a6`. ZIP integrity and an independent SHA-256 check passed; its hash is `95c4a2271c919abd5ac0f80e2588e0cb7bf1998b888b05e1daf0152eaa00a157`.
- The package includes the new request validation checks in its source revision record. It remains a local development candidate and has not been launched on Windows.

## 2026-10-03 — Text overlays and background edits

- Added a text dialog for content, font size, color, and canvas position. Applying it rasterizes the same overlay into every frame so the preview, saved `.fsp`, and GIF export share identical pixels. The UI explains that the overlay is baked in; unsaved edits can still be discarded.
- Added canvas-bound validation and cross-platform pixel/archive tests. The editor-level test applies text to all frames, exports a GIF, saves the project, and confirms the pixels and frame durations survive reopening.
- Moved crop, resize, text rasterization, and GIF encoding onto worker tasks so frame processing does not hold the UI thread. The editor blocks closing while a raster edit writes its temporary archive.
- `dotnet test FrameStudio.sln --configuration Debug --no-restore` passes 23 tests and skips the three Windows desktop checks. Release build succeeds with zero warnings/errors. A fresh visual check of the text dialog has not yet been completed; the prior editor screenshot predates this feature.

## 2026-10-03 — Candidate rebuilt with text overlays

- Rebuilt the self-contained Windows x64 candidate from clean commit `9b5248a`. ZIP integrity and an independent SHA-256 check passed; its hash is `d7f514158ac22727e47997c25886ad768d5620be6a6ec97a0c01857f3bee45da`.
- The package includes the SkiaSharp text renderer and identifies its Mac build host. It is still a local development candidate; Windows runtime and UI validation remain outstanding.

## 2026-10-03 — FFmpeg MP4 export

- Added an MP4 export service that writes project frames as PNG inputs in a private temporary directory, creates a concat manifest with per-frame timing, and calls a local FFmpeg process with H.264 `libx264`, CRF 23, and `yuv420p`. An even-dimension pad supports H.264's common 4:2:0 format. Frame Studio does not bundle FFmpeg; the user needs an FFmpeg build with `libx264` on `PATH`.
- On macOS, executable discovery also checks the standard Homebrew prefixes because GUI-launched apps may not inherit Homebrew's shell `PATH`. The MP4 timing integration test passes with a deliberately minimal `/usr/bin:/bin` `PATH`.
- Added an editor MP4 action and generalized the completion window for GIF and MP4. The same edited frame ordering and durations feed either encoder. The final repeated input sample provides an end timestamp for the last visible frame.
- The editor integration test changes a two-frame order and sets 120 ms/80 ms durations, exports MP4, and checks `ffprobe` timestamps at 0/120/199 ms and a 200 ms stream duration. The final repeated sample carries the last 1 ms with identical pixels so the visible frame interval and total remain exact. FFmpeg's concat `duration` entries and `-fps_mode vfr` are based on the [FFmpeg format documentation](https://ffmpeg.org/ffmpeg-formats.html) and [FFmpeg command-line documentation](https://ffmpeg.org/ffmpeg.html). The macOS suite passes 24 tests and skips the three Windows capture checks.
- Windows capture remains unverified. The current candidate has been cross-published but still needs a Windows run; the MP4 action also needs a visual smoke check and verification with FFmpeg installed and absent.

## 2026-10-03 — Candidate rebuilt with MP4 export

- Rebuilt the self-contained Windows x64 candidate from clean commit `fff6398`. The archive SHA-256 is `3cf97aa271beff60f52ba1f9b5c08b822f75e46e3136f34327323c56e0d1ad99`; an independent archive check and checksum comparison passed.
- The package README states that FFmpeg with `libx264` must be installed separately. The package was cross-published on macOS arm64 and has not been run on Windows; its capture and export UI still require runtime checks.

## 2026-10-03 — End-to-end record, edit, and GIF check

- Added a platform-independent workflow test that feeds three frames through the recording view model, moves a frame and changes its duration in the editor, saves and reopens the `.fsp`, then decodes the exported GIF and checks its RGBA frames, delays, and loop metadata.
- The source is a deterministic fake `IRecordingSession`, so the test proves the app's recording-to-editor-to-export handoff but does not replace the skipped Windows GDI runtime checks. The suite now passes 25 tests and skips those three Windows checks on macOS.

## Migration decisions

- Keep the original WPF projects intact as the baseline and source reference.
- Build the Avalonia app in parallel, without a project reference to the WPF UI or other Windows-targeted projects.
- Put neutral frame/project logic in `FrameStudio.Core`; expose platform operations through `FrameStudio.Platform.Abstractions`; keep Windows implementations in `FrameStudio.Platform.Windows`.
- Keep Avalonia bitmap objects at the UI edge. Core pixel and frame data should use explicit neutral buffers and metadata.
- Start with an Avalonia shell and then connect a complete recording → project → edit → GIF export workflow before secondary features.

## 2026-10-03 — Common desktop DC ownership

- Reviewed Microsoft's `GetDC`/`ReleaseDC` contract after tracing the capture thread boundary. A common DC must be released on the same thread that acquired it; the previous grabber retained a desktop DC created by the UI thread and later released it from the capture worker.
- Updated the GDI grabber to release its setup DC on the acquiring thread, then acquire and release a desktop DC inside each synchronous frame capture. The compatible memory DC and bitmap remain allocated and reused between frames.
- `dotnet test FrameStudio.sln --configuration Release --no-restore` passes 25 tests and skips the three Windows desktop checks on macOS. `dotnet build FrameStudio.sln --configuration Release --no-restore` succeeds with zero warnings and errors. Native behavior remains unverified until these desktop checks run on Windows.
- Reference: [Microsoft GetDC documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdc).

## 2026-10-03 — Preserve recordings after capture errors

- The recording consumer now distinguishes errors from the capture stream from project-write errors. When capture stops unexpectedly, the recorder reports the condition, disables pause/resume, and keeps Stop available.
- If at least one frame reached the project writer, Stop completes the partial `.fsp` and opens it in the editor. If capture failed before the first frame, the recorder reports the cause and discards the empty draft. Project write failures still follow the existing save-error path.
- Added a deterministic test that injects a capture-stream error after one frame, then verifies the completed project retains its RGBA pixels and duration. The Release suite passes 26 tests and skips the three Windows desktop checks on macOS. This exercises recovery above the native capture boundary; it does not replace Windows runtime validation.

## 2026-10-03 — Candidate rebuilt with partial-recording recovery

- Rebuilt the self-contained Windows x64 candidate from clean commit `40cf9a1`. ZIP integrity passed and the archive SHA-256 is `b0428891f935a80080ea69444f2ba1a50dc016322b65631e8fc6647d2e70a541`.
- The package includes the recovery path and identifies its macOS arm64 build host. It remains a local development candidate; it has not been run on Windows.

## 2026-10-03 — Check captured pixels in the Windows integration test

- The desktop capture test now creates a small topmost, non-activating `STATIC` white-rectangle marker and checks its system-color RGBA value at the expected point in the first captured frame. This rejects a capture that returns correctly sized but blank or unrelated pixels.
- The test then continues through pause, resume, and stop with the marker visible. It compiled and remained skipped on macOS; it still needs to pass on an interactive Windows desktop.
- Marker behavior follows Microsoft's [static-control documentation](https://learn.microsoft.com/en-us/windows/win32/controls/about-static-controls), which defines `SS_WHITERECT` as a filled rectangle using the current window background color.

## 2026-10-03 — Clean-room median-cut quantizer

- During the license audit, the migrated median-cut quantizer was found to cite an external source repository without a discoverable license. It was removed rather than redistributed under an assumed license.
- Replaced it with an independently written implementation based only on the general median-cut concept: collect an RGB frequency histogram, select a splittable bucket by color range, split that bucket at its weighted median, average each bucket into a palette entry, then select the nearest opaque palette color for each pixel.
- Added tests for a green-channel split, frequency-weighted palette averages, transparent-palette reservation, and fully transparent input. The source comment and migration audit record the clean-room provenance.

## 2026-10-03 — Freehand editor annotations

- Added a Pencil tool to the Avalonia editor. The user can drag over the preview, clear or cancel the pending stroke, and apply the teal rounded stroke to every frame in the project.
- The UI converts preview coordinates to neutral pixel coordinates. `FrameStudio.Core` validates the stroke, rasterizes it through Skia into RGBA buffers, and rewrites the project through the same atomic draft path as crop, resize, and text edits. GIF and MP4 exports therefore receive the drawing automatically.
- Added transform validation, transform pixel-boundary, editor persistence, and all-frame application tests. The control builds on macOS; the already-running app had a user project at an unsaved-changes dialog, so a fresh live UI interaction check remains pending rather than altering that project.

## 2026-10-04 — Public preview release, freehand annotations, and CI

- Rebuilt the self-contained Windows x64 candidate from clean commit `5184f661`. ZIP integrity passed and the archive SHA-256 is `de2c97b7ff8a63d1fd0be7ba62c30a502d6b8b3797389cec02e65a910cc9fb65`.
- Published the [Frame Studio repository](https://github.com/kris70lesgo/frame-studio) and [Preview 3](https://github.com/kris70lesgo/frame-studio/releases/tag/v0.1.0-preview.3), including the ZIP and its checksum. The release notes and bundled README preserve the unverified Windows-runtime status.
- The new hosted CI workflow builds/tests Frame Studio on macOS and Windows and checks Windows self-contained publishing; all three [jobs passed](https://github.com/kris70lesgo/frame-studio/actions/runs/37146520922) for `5184f661`. It deliberately excludes desktop-capture tests, which require an interactive Windows session. The preview remains unverified for native capture until the desktop validation checklist passes.

## Ongoing log

Add dated entries here as migrations reveal framework differences, platform constraints, or performance fixes.
