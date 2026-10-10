# Avalonia Port Challenge analysis

Reviewed on 2026-10-10 against the [official Avalonia Port Challenge page](https://avaloniaui.net/blog/avalonia-port-challenge), published on 2026-08-28.

## What the challenge rewards

Entries are judged on five dimensions:

1. Difficulty and completeness of the migration.
2. Quality of the resulting application.
3. Strength of cross-platform implementation.
4. Engineering quality of the codebase.
5. Usefulness and honesty of the migration write-up.

Categories are Best Cross-Platform Port, Best Legacy Revival, Best Everyday Tool, and Best IT Pro Tool. ScreenToGif is a strong fit for Legacy Revival because the source is a substantial WPF application with Windows capture dependencies. Its daily screen recording/editing use also fits Everyday Tool. Cross-Platform Port is a longer-term opportunity; a shared Avalonia UI by itself is insufficient without real capture support and runnable builds on each claimed platform.

The prize pool is **$15,000 USD**: a $5,000 grand prize, four $2,000 category prizes, and four $500 honourable mentions. Entries targeting the same codebase on mobile or WebAssembly can receive extra recognition.

## Submission requirements and dates

- Entries close at **23:59 UTC on 23 October 2026**. Winners are announced on 6 November 2026.
- Open-source entries need a public repository under an OSI-approved license, downloadable builds for each target platform, before-and-after screenshots, and a short migration write-up. The upstream MS-PL is OSI-approved ([OSI license page](https://opensource.org/license/MS-PL)); its conditions still require preserving notices and including the complete license with source distributions.
- Closed-source entries have the same runnable-build, screenshot, and migration-write-up requirements. Because ScreenToGif's MS-PL allows this derivative to remain open, a public repository is the clearest fit for the challenge's learning and review goals.
- The migration write-up is mandatory and counts toward judging. It should state how much work the migration took, what moved cleanly, what needed redesign, and what surprised the team.
- The original application must predate the contest. Rewrites count, and another author's code may be used when its license permits.
- Teams can include up to three people; AI-assisted work is permitted.

## Fit and project decisions

- ScreenToGif predates the challenge and uses the Microsoft Public License (MS-PL), which is preserved in this repository. Keep the port independently branded and retain the complete license/attribution notices.
- Frame Studio is being built as a separate Avalonia product beside the original WPF solution. This makes the migration boundary and source comparison reviewable.
- The challenge allows a complete, carefully made small port to outperform a sprawling unfinished one. Prioritize the full screen recording → editable project → export workflow before webcam, sketchboard, advanced effects, or broad platform claims.
- The most credible initial positioning is Legacy Revival, with a refined everyday recording workflow. Claim an additional platform only after the build is downloadable and its capture path has been exercised on that OS.
- Preserve dated notes and time spent as the work proceeds so the mandatory cost/lessons write-up can be based on evidence rather than memory.

## Current evidence and gaps

- The independent Avalonia solution builds on macOS arm64. On 2026-10-08, the current Jitter-inspired home/editor UI opened with a local `.fsp` fixture, and the resize dialog was visually reviewed. This is a macOS rendering check only; it does not exercise Windows capture or substitute for challenge screenshots. The checked-in [editor image](before-after/after-editor-preview-macos.jpg) is an older smoke check, not the current UI.
- The current independently downloaded and verified Windows x64 candidate was built by the Windows runner in [CI run 38024903827](https://github.com/kris70lesgo/frame-studio/actions/runs/38024903827) from source commit `8e2819bd`; its bundled README records synthetic PR merge revision `717e3d1`. It includes the transparent GIF palette-index fix and tested device-pixel-to-logical-coordinate conversion for screen-region selection. The [CI artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38024903827/artifacts/11659538490) includes the package, full license, README, and checksum; the downloaded package's SHA-256 `f356725626996e64a7afdebd8c579868befba3ae8f6dfc5453fc77c286d4b637` matches the sidecar and ZIP integrity was verified. It has not been launched in an interactive Windows desktop session. The older [Preview 3 release](https://github.com/kris70lesgo/frame-studio/releases/tag/v0.1.0-preview.3) does not contain the latest UI. See [the Windows validation record](WINDOWS_VALIDATION.md).
- [Windows CI run 38024903827](https://github.com/kris70lesgo/frame-studio/actions/runs/38024903827) passes all 6 `WindowsCaptureIntegrationTests` on hosted Windows: the suite checks display-affinity lifecycle, monitor enumeration, and a marked GDI capture across pause/resume/stop. It does not launch the actual Avalonia app or verify that the app's own windows are excluded; interactive recording, mixed-DPI desktop behavior, and same-content screenshots remain outstanding.
- A fresh local Release verification on 2026-10-10 passed 44 tests and skipped the three Windows-only desktop tests; the full solution build completed with no warnings or errors. The Windows native suite passes in hosted CI and checks window-affinity lifecycle plus a marker-pixel GDI capture across pause/resume/stop. Cross-platform checks verify malformed capture requests fail before native calls and cover transparent-palette indexing, tested screen-region scaling at multiple scale factors, text and freehand rasterization, all-frame project editing, save/reopen, GIF export, and MP4 timing. The recorder preserves received frames on capture-stream failure. End-to-end model tests cover edited project frame order/timing and decoded GIF pixels; the MP4 integration check covers variable frame timing. The capability-report test checks that screen capture is marked unavailable on this non-Windows host. Hosted capture tests validate the native service; the interactive Avalonia workflow and external GIF playback remain unverified.
- The Windows screen recording flow connects display/region setup, bounded GDI capture, pause/resume/stop, `.fsp` project storage, an editor with frame thumbnails/playback/reordering/crop/resize/text edits, and GIF/MP4 export. MP4 export requires an installed FFmpeg build with `libx264` available on `PATH`.
- The current light Jitter-inspired home/editor layout and resize dialog were visually checked on macOS. Earlier editor keyboard checks covered save, duplicate, frame ordering/deletion, playback, and Escape from crop mode.
- The Windows GDI monitor/window/screen-region service passes native tests on hosted Windows, but the interactive app workflow and mixed-DPI selection still need runtime verification. Window selection stays disabled: its current service records a desktop rectangle, so other windows can obscure the target. Webcam, sketchboard, and additional video encoders remain incomplete. The text dialog, freehand drawing control, and MP4 export action still need a fresh visual smoke check.
- A Win32 ownership review corrected how common desktop device contexts are acquired and released: every `GetDC(NULL)` is now paired with `ReleaseDC` on the acquiring thread. The fix builds, and the hosted Windows native capture integration tests now pass. A real Windows app launch and end-to-end workflow remain required.
- Monitor selection now takes per-display scaling from Avalonia's `Screen.Scaling`, matched against native monitor bounds in device pixels. This replaces `GetDpiForMonitor`, which Microsoft marks as unsuitable for per-monitor-aware threads; the mixed-DPI selection path still needs a Windows runtime check.
- The original WPF app could not be launched on the macOS development host. The original project was compiled with its Windows-only post-build event disabled. A Windows machine is still needed to capture true before/after images and exercise the recorder.
- The public [repository](https://github.com/kris70lesgo/frame-studio) and an older downloadable Windows preview exist. The latest candidate is not yet uploaded as a permanent release. The remaining challenge-critical work is interactive Windows app validation, paired screenshots, a migration write-up that reports work cost honestly, and a permanent judge-facing Windows download. Do not claim a cross-platform capture port from the Avalonia UI alone.
