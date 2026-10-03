# Avalonia Port Challenge analysis

Reviewed on 2026-10-03: [The Avalonia Port Challenge](https://avaloniaui.net/blog/avalonia-port-challenge), published by Avalonia on 2026-08-28.

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

- The independent Avalonia solution builds on macOS arm64. On 2026-10-03, the published app opened a local 24-frame `.fsp` fixture in its editor window. The 640 × 360 preview, timeline thumbnails, frame properties, and editor actions were visible. This confirms a macOS UI smoke check only; it does not exercise Windows capture or GIF playback in an external viewer. See [the editor preview](before-after/after-editor-preview-macos.jpg).
- Self-contained publish checks succeeded for macOS arm64 and Windows x64. The current Windows x64 package was rebuilt from `d453ff4` and passed ZIP and SHA-256 checks, but has not been run on Windows and is not yet a public challenge download. See [the Windows validation record](WINDOWS_VALIDATION.md).
- Windows desktop integration tests cover window-affinity lifecycle and a short display capture/pause/resume/stop session. They are explicitly skipped on macOS and still need to pass on Windows.
- The macOS test run passes 25 tests and skips three Windows runtime checks: applying/restoring window display affinity, disposal after a window closes, and desktop capture. Cross-platform checks verify malformed capture requests fail before native calls and cover text rasterization, all-frame project editing, save/reopen, GIF export, and MP4 timing. An end-to-end model test feeds fake captured frames through recording, reorders and retimes them in the editor, saves and reopens the project, then decodes the GIF and verifies its RGBA frame order and timing. The MP4 integration check exercises reordered editor frames with 120 ms and 80 ms durations and probes timestamps at 0, 120, and 199 ms in a 200 ms stream. The 1 ms terminal sample repeats the last image, preserving its visible duration and the total. The editor view-model test drives reorder, duplicate, duration, delete, save, and export commands, then checks saved pixels and GIF dimensions, frame delays, and looping metadata. A separate 64 × 64 pseudorandom frame exercises LZW code-width growth and checks decoded palette indexes. The capability-report test checks that screen capture is clearly marked unavailable on this non-Windows host. The end-to-end model test uses a fake recording source; native capture and external GIF playback remain unverified.
- The Windows screen recording flow connects display/region setup, bounded GDI capture, pause/resume/stop, `.fsp` project storage, an editor with frame thumbnails/playback/reordering/crop/resize/text edits, and GIF/MP4 export. MP4 export requires an installed FFmpeg build with `libx264` available on `PATH`.
- The light and dark themes and the timeline's teal selection outline were visually checked on macOS. Editor keyboard checks covered save, duplicate, frame ordering/deletion, playback, and Escape from crop mode.
- The Windows GDI monitor/window/screen-region service cross-compiles, but screen capture still needs runtime verification on Windows. Window selection stays disabled: its current service records a desktop rectangle, so other windows can obscure the target. Webcam, sketchboard, freehand drawing annotations, and additional video encoders remain incomplete. The text dialog and MP4 export action still need a fresh visual smoke check.
- A Win32 ownership review corrected how common desktop device contexts are acquired and released: every `GetDC(NULL)` is now paired with `ReleaseDC` on the acquiring thread. The fix builds and the cross-platform suite passes, but the skipped Windows desktop capture test is still the required runtime check.
- Monitor selection now takes per-display scaling from Avalonia's `Screen.Scaling`, matched against native monitor bounds in device pixels. This replaces `GetDpiForMonitor`, which Microsoft marks as unsuitable for per-monitor-aware threads; the mixed-DPI selection path still needs a Windows runtime check.
- The original WPF app could not be launched on the macOS development host. The original project was compiled with its Windows-only post-build event disabled. A Windows machine is still needed to capture true before/after images and exercise the recorder.
- No public downloadable build or public challenge repository exists yet. The next challenge-critical work is Windows runtime validation, paired screenshots, a measured migration write-up, and a public release location. Do not claim a cross-platform capture port from the Avalonia UI alone.
