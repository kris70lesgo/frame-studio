# ScreenToGif → Avalonia migration audit

Audit date: 2026-10-03

## Baseline

- Upstream: `https://github.com/NickeManarin/ScreenToGif`
- Upstream branch: `master`
- Baseline commit: `a4d0a67c2131cd048ceec86cd40afc2f1a06f2fd` (2026-07-28)
- Identified application version: `2.43.2` (`ScreenToGif/ScreenToGif.csproj`)
- Migration branch: `avalonia-port`
- Host: macOS 26.7, Apple Silicon (`arm64`); Windows WPF UI cannot be launched here.
- SDK installed for this work: .NET SDK `9.0.318`.
- Avalonia templates installed: `Avalonia.Templates` `12.1.3`.
- License: Microsoft Public License (MS-PL), preserved in the upstream `LICENSE.txt`.

## Solution and project roles

`GifRecorder.sln` contains seven projects:

| Project | Current target / dependencies | Migration assessment |
| --- | --- | --- |
| `ScreenToGif` | `net9.0-windows7.0`; WPF + WinForms; 258 C# files and 109 XAML files; references Native and ViewModel | Main application. Its windows, controls, themes, capture implementations, exporters, and many helpers are interleaved. Replace the presentation layer in Avalonia and call functionality through explicit services. |
| `ScreenToGif.Model` (`ScreenToGif.Domain`) | `net9.0-windows7.0`; `UseWPF` | Models are not currently framework-neutral: `Frame` and `IFrame` use WPF `Int32Rect` and `Color`; `Project` and editing sequences use WPF brushes, fonts, geometry, and other media types. Treat as a migration source, not a direct Avalonia reference. |
| `ScreenToGif.Native` | `net9.0-windows7.0`; WPF + WinForms; references Model | Windows-specific native declarations and helpers, including User32, Gdi32, Kernel32, DwmApi, ShCore, Shell32, NtDll, and WinMm. Preserve the proven Windows code behind platform interfaces. |
| `ScreenToGif.Util` | `net9.0-windows7.0`; `UseWPF`; references Model and Native; KGySoft, SharpCompress, System.Drawing | Mixed library: useful binary codecs and general algorithms sit alongside WPF converters, UI helpers, settings, capture, hotkeys, and native code. Split or selectively link only source files whose dependency closure is neutral. |
| `ScreenToGif.ViewModel` | `net9.0-windows7.0`; references Model and Util; KGySoft.Drawing.Core | Contains useful capture/editor/export concepts, but ViewModels and export presets carry WPF image, dispatcher, input, and UI types. Rebuild presentation ViewModels; adapt exporter logic only after its image boundary is neutral. |
| `ScreenToGif.Test` | `net9.0-windows7.0`; xUnit; references ViewModel and main app | Four C# test files, including theme contrast, image comparison, and Yandex upload tests. Tests are coupled to the Windows app; migrate/add core tests to a cross-platform test project. |
| `Other/Translator` | WPF translator utility in the solution | Separate contributor utility, not part of the end-user recording/editor workflow. Keep the upstream project intact; exclude it from the Avalonia product. |

Every production project currently targets Windows and/or enables WPF. The existing project files therefore cannot be referenced directly by a cross-platform Avalonia app without bringing Windows Desktop framework dependencies along.

## Classification

### A. Reuse directly after validating each dependency closure

- GIF decoding under `ScreenToGif.Util/Codification/Gif/Decoder/**`: stream/byte-oriented decoder and frame metadata; no WPF boundary was found in this area.
- Binary and general-purpose algorithms such as `BitHelper`, `CrcHelper`, `StreamHelpers`, `Serializer`, and selected `MathExtensions`, `StringExtensions`, `EnumExtensions`, and parsing helpers.
- Stream-oriented PSD serialization under `ScreenToGif.Util/Codification/Psd/**` is a candidate, but some files call shared extensions or contain project timeline metadata; link only after checking those dependencies.
- Existing migration/settings format knowledge, file format behavior, and export option semantics are valuable references even when their current types cannot be reused directly.

The existing `ScreenToGif.Util` assembly itself is **not** reusable as a cross-platform project: it has a WPF target and contains UI/native code. Reuse must happen at the source/module boundary.

### B. Reuse with small changes or a neutral boundary

- Frame/project concepts in `ScreenToGif.Model/Models/Frame.cs`, `ExportFrame.cs`, `ExportProject.cs`, `Models/Project/**`, and `Interfaces/IFrame.cs`. Replace WPF `Int32Rect`, `Color`, `Brush`, `FontFamily`, and related types with neutral geometry, RGBA/color, and style values; keep pixel storage as byte buffers/streams rather than Avalonia `Bitmap`.
- Settings serialization and format compatibility. `Serializer` is neutral in isolation, but `UserSettings` and persisted models include WPF values and UI settings; define a new neutral subset and preserve migration semantics.
- Frame timing and export preparation. `FrameRate` currently reads `UserSettings`; separate timing policy from global settings.
- GIF/APNG encoders and image processing. Their algorithms are useful, but key entry points accept WPF `BitmapSource`, `Color`, or `PixelFormats`; convert to explicit pixel-buffer contracts before reuse.
- Export preset options and FFmpeg/Gifski integration. Keep format settings and process invocation behavior where decoupled; rebuild WPF panels and bitmap handling.

### C. Platform-specific implementation to preserve behind interfaces

- All `ScreenToGif.Native/**` P/Invoke and WPF/WinForms message-loop helpers.
- `ScreenToGif/Capture/**`, including Direct3D/SharpDX capture and cursor/window capture assumptions.
- DirectShow/DirectX webcam implementations under `ScreenToGif/Webcam/**`.
- Monitor/window enumeration, system tray, global hotkeys/input hooks, clipboard, notifications, registry/system inspection, and native file dialogs.
- Windows-specific app startup and OS integration in `App.xaml.cs`, `Util/FrameworkHelper.cs`, and `Util/ThemeHelper.cs`.

The Avalonia-facing layer should depend on contracts such as `IScreenCaptureService`, `IWindowCaptureService`, `IWebcamService`, `IGlobalHotkeyService`, `IMonitorService`, `IClipboardService`, `INotificationService`, and `IFileDialogService`; the existing Win32 implementations should not leak into the UI project.

### D. Rebuild in Avalonia

- All user-facing WPF windows, UserControls, XAML, themes/resource dictionaries, custom controls, WPF converters, routed-event/code-behind behavior, and animation: primarily `ScreenToGif/Windows/**`, `Views/**`, `UserControls/**`, `Controls/**`, `Themes/**`, and `Resources/**`.
- UI-bound ViewModels in `ScreenToGif.ViewModel/**`, especially editor/recorder/board/webcam coordination that exposes WPF shapes, ink, bitmaps, dispatcher, and input types.
- WPF helpers in `ScreenToGif.Util/VisualHelper.cs`, `DynamicResourceBinding.cs`, `DataGridHelper.cs`, `LocalizationHelper.cs`, WPF converters, and image extensions. Port behavior to Avalonia or keep it in the UI assembly.

## Proposed migration architecture

Add new, independently named projects beside the upstream projects:

- `FrameStudio.Avalonia`: Avalonia desktop app, MVVM, design tokens, windows, views, and UI-only bitmap conversion.
- `FrameStudio.Core`: neutral frame/project model, editing operations, serialization boundaries, and reusable encoders/decoders. No Avalonia/WPF references.
- `FrameStudio.Platform.Abstractions`: platform service contracts and capability reporting.
- `FrameStudio.Platform.Windows`: Windows capture, webcam, hotkey, monitor, and integration adapters; isolated from the UI.
- `FrameStudio.Tests`: cross-platform tests for neutral model, serialization, timing, and export configuration.

Keep the original ScreenToGif projects and their history as the functional reference. Do not add project references from `FrameStudio.Avalonia` to the WPF projects. Move or link only audited source modules into Core, retaining upstream attribution and the complete MS-PL notice. Build the Avalonia shell first, then connect one working recording → project → editing → GIF export path before adding secondary features.

## Baseline observations and limitations

- The original README documents recording, webcam, sketchboard, frame editing, and GIF/APNG/video/PSD/image export. It links to upstream recorder, start-screen, editor, and settings screenshots/animations, but this clone has no local raster baseline captures. A later macOS shell screenshot is explicitly labeled as a UI check, not a Windows before/after image.
- The original source and XAML were inspected because this host is macOS. The original WPF application was not launched and no local “before” captures could be made.
- The first full-solution build on macOS compiled the Windows-targeted projects but failed in the project's post-build `editbin` batch command (`if exist`, `call`, and `editbin` are Windows commands). Rebuilding with `-p:PostBuildEvent=` succeeded (7 warnings, 0 errors); this confirms source compilation, not that the WPF app can run on macOS. The upstream build reports a SharpCompress advisory warning and missing `ManagedMinimumRules.ruleset` warnings.
- MS-PL permits derivatives but does not grant contributor trademark rights. Keep the existing complete `LICENSE.txt` and attribution, use independent app branding, and state clearly that this is an independent port.
- `ScreenToGif.Util/Codification/Gif/Encoder/**` contains a GIF encoder that already accepts raw frame bytes. It is being migrated selectively behind neutral RGBA/geometry types. `NeuralQuantizer.cs` has a separate GPLv3 notice and is excluded from the Frame Studio copy. A prior median-cut file cited an external repository with no discoverable license, so it was removed and replaced with an independently written clean-room median-cut implementation. The root MS-PL license alone does not override component-level notices or an absent third-party license.
