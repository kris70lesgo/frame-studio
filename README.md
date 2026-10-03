# Frame Studio

**An independent Avalonia port of ScreenToGif, built for the Avalonia Port Challenge.**

Frame Studio is a new desktop workspace for recording, editing, and exporting short screen animations. The migration keeps the original ScreenToGif repository as its functional reference while building a new Avalonia UI and separating reusable logic from Windows-specific services.

This is an independent project. It is not the official ScreenToGif application and is not affiliated with or endorsed by Nicke Manarin or N-Tech.

## Current status

The Avalonia app now connects Windows screen-region recording to a native `.fsp` project, a frame editor, and GIF export. The Win32/GDI capture backend is implemented and wired into the UI, but its runtime behavior still needs validation on Windows. macOS builds and core tests work; screen capture is Windows-only.

| Area | Status |
| --- | --- |
| Avalonia home shell | Implemented; persistent recent-project list; builds; early macOS shell capture included below |
| Dark and light appearance | Implemented |
| Neutral frame/project foundation | Frame model and compressed `.fsp` archive read/write implemented |
| Platform service contracts | Defined |
| Windows screen-region capture | Setup, display selection, area selection, frame rate, cursor option, pause, and stop are wired; Windows runtime still needs verification |
| End-to-end recording workflow | Records frames into `.fsp` projects and opens the editor when recording stops |
| Editor | Frame thumbnails, preview/playback, selection, earlier/later ordering, duplicate/delete, duration edits, drag crop, project-wide resize, save/discard, and GIF export implemented |
| GIF export | Migrated encoder, editor export flow, and completion actions for opening the GIF, showing its folder, or copying its path |
| Core workflow checks | 15 tests pass on macOS; editor commands, archive editing, recent-project history, and exported GIF pixels, dimensions, frame timing, and looping are checked |
| Webcam, window capture, sketchboard | Not migrated yet |
| Text/drawing annotations and video export | Not migrated yet |
| Windows platform adapter | Win32 monitor/window enumeration and bounded GDI desktop-region recording implemented; runtime needs Windows verification |

The original WPF application remains in this repository as the baseline in `GifRecorder.sln`. The new application is in `FrameStudio.sln` and does not reference the WPF UI projects.

## Shell preview

This macOS capture documents an early Avalonia shell only. It is not a Windows before/after comparison, and it predates the recording and editor workflow shown in the status table.
The [capture notes](docs/before-after/README.md) link to the upstream screenshots and list the fresh Windows pairs still needed for submission.

![Frame Studio early home shell on macOS](docs/before-after/after-home-shell-macos.png)

The current editor has also had a macOS UI smoke check with a local sample project. It is visual evidence of the editor shell only; capture still requires Windows validation.

![Frame Studio editor preview on macOS](docs/before-after/after-editor-preview-macos.jpg)

## Build and run

Requirements: .NET SDK 9.0.318 or a compatible .NET 9 feature-band SDK, with NuGet access for the Avalonia packages.

The Avalonia UI uses the desktop Avalonia stack. This branch has a local macOS arm64 publish and a Windows x64 cross-publish; neither is a released challenge download, and only Windows has a capture backend. Core project editing and GIF export are platform-neutral. The full recording workflow must be exercised on Windows before claiming a verified Windows release; no macOS or Linux capture support is claimed.

```sh
dotnet build FrameStudio.sln
dotnet test FrameStudio.sln
dotnet run --project FrameStudio.Avalonia/FrameStudio.Avalonia.csproj
```

To create a self-contained local Windows x64 candidate, run `python3 scripts/package-windows-candidate.py`. It writes the package, README, full license, and SHA-256 checksum under ignored `dist/`. The package remains a development candidate until its Windows runtime checks pass.

`global.json` selects .NET 9 for this migration. The original WPF application targets Windows; this macOS development host can compile it with Windows targeting enabled, but cannot run it.

## Project structure

- `FrameStudio.Avalonia` — Avalonia desktop UI and MVVM.
- `FrameStudio.Core` — framework-neutral pixel geometry and frame/project models.
- `FrameStudio.Platform.Abstractions` — capture, monitor, camera, hotkey, clipboard, notification, permission, and file-dialog contracts.
- `FrameStudio.Platform.Windows` — Win32 monitor/window discovery and bounded GDI screen-region recording sessions.
- `FrameStudio.Tests` — cross-platform core tests.
- `ScreenToGif`, `ScreenToGif.Model`, `ScreenToGif.Native`, `ScreenToGif.Util`, and `ScreenToGif.ViewModel` — original WPF implementation retained as the behavior and migration reference.

See [the source audit](docs/MIGRATION_AUDIT.md) for reusable modules, framework coupling, and the proposed architecture. The [migration journal](docs/MIGRATION.md) records decisions and lessons as the port progresses.

See [the challenge analysis](docs/CHALLENGE_ANALYSIS.md) for the judging criteria, entry requirements, and the schedule used to prioritize migration work.

The [Windows validation checklist](docs/WINDOWS_VALIDATION.md) describes the automated and manual checks still required before claiming the capture workflow is verified.

## Attribution and license

The source application is [ScreenToGif](https://github.com/NickeManarin/ScreenToGif), created by Nicke Manarin and contributors. Its original source is licensed under the **Microsoft Public License (MS-PL)**. The complete upstream license and required notices are preserved in [`LICENSE.txt`](LICENSE.txt). Source derived from ScreenToGif is distributed under the terms of that license.

The MS-PL does not grant rights to the ScreenToGif name, logo, or other contributor trademarks. Frame Studio uses its own name and visual identity.
