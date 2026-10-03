# Frame Studio

**An independent Avalonia port of ScreenToGif, built for the Avalonia Port Challenge.**

Frame Studio is a new desktop workspace for recording, editing, and exporting short screen animations. The migration keeps the original ScreenToGif repository as its functional reference while building a new Avalonia UI and separating reusable logic from Windows-specific services.

This is an independent project. It is not the official ScreenToGif application and is not affiliated with or endorsed by Nicke Manarin or N-Tech.

## Current status

The first Avalonia shell is implemented and launches on macOS. It includes a custom home screen, semantic dark and light themes, neutral frame/project models, and platform service contracts. Capture actions are intentionally disabled until a real capture adapter is connected.

| Area | Status |
| --- | --- |
| Avalonia home shell | Implemented; builds and launches |
| Dark and light appearance | Implemented |
| Neutral frame/project foundation | Started |
| Platform service contracts | Defined |
| Screen and window recording | Not migrated yet |
| Webcam and sketchboard | Not migrated yet |
| Editor and timeline | Not migrated yet |
| GIF/video export | Not migrated yet |
| Windows platform adapter | Not implemented yet |

The original WPF application remains in this repository as the baseline in `GifRecorder.sln`. The new application is in `FrameStudio.sln` and does not reference the WPF UI projects.

## Shell preview

This macOS capture documents the current Avalonia shell only. It is not a Windows before/after comparison, and its capture actions remain disabled until the recording adapter is implemented.

![Frame Studio early home shell on macOS](docs/before-after/after-home-shell-macos.png)

## Build and run

Requirements: .NET SDK 9.0.318 or a compatible .NET 9 feature-band SDK, with NuGet access for the Avalonia packages.

The Avalonia UI targets Windows, macOS, and Linux; this shell has been built and launched on macOS arm64. Screen capture, editing, and export are not yet implemented on any platform. Windows is the first target for those services.

```sh
dotnet build FrameStudio.sln
dotnet test FrameStudio.sln
dotnet run --project FrameStudio.Avalonia/FrameStudio.Avalonia.csproj
```

`global.json` selects .NET 9 for this migration. The original WPF application targets Windows; this macOS development host can compile it with Windows targeting enabled, but cannot run it.

## Project structure

- `FrameStudio.Avalonia` — Avalonia desktop UI and MVVM.
- `FrameStudio.Core` — framework-neutral pixel geometry and frame/project models.
- `FrameStudio.Platform.Abstractions` — capture, monitor, camera, hotkey, clipboard, notification, permission, and file-dialog contracts.
- `FrameStudio.Tests` — cross-platform core tests.
- `ScreenToGif`, `ScreenToGif.Model`, `ScreenToGif.Native`, `ScreenToGif.Util`, and `ScreenToGif.ViewModel` — original WPF implementation retained as the behavior and migration reference.

See [the source audit](docs/MIGRATION_AUDIT.md) for reusable modules, framework coupling, and the proposed architecture. The [migration journal](docs/MIGRATION.md) records decisions and lessons as the port progresses.

## Attribution and license

The source application is [ScreenToGif](https://github.com/NickeManarin/ScreenToGif), created by Nicke Manarin and contributors. Its original source is licensed under the **Microsoft Public License (MS-PL)**. The complete upstream license and required notices are preserved in [`LICENSE.txt`](LICENSE.txt). Source derived from ScreenToGif is distributed under the terms of that license.

The MS-PL does not grant rights to the ScreenToGif name, logo, or other contributor trademarks. Frame Studio uses its own name and visual identity.
