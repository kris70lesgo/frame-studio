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

## Migration decisions

- Keep the original WPF projects intact as the baseline and source reference.
- Build the Avalonia app in parallel, without a project reference to the WPF UI or other Windows-targeted projects.
- Put neutral frame/project logic in `FrameStudio.Core`; expose platform operations through `FrameStudio.Platform.Abstractions`; keep Windows implementations in `FrameStudio.Platform.Windows`.
- Keep Avalonia bitmap objects at the UI edge. Core pixel and frame data should use explicit neutral buffers and metadata.
- Start with an Avalonia shell and then connect a complete recording → project → edit → GIF export workflow before secondary features.

## Ongoing log

Add dated entries here as migrations reveal framework differences, platform constraints, or performance fixes.
