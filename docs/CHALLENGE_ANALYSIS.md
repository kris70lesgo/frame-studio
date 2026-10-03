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

## Submission requirements and dates

- Entries close at **23:59 UTC on 23 October 2026**. Winners are announced on 6 November 2026.
- Open-source entries need a public repository under an OSI-approved license, downloadable builds for each target platform, before-and-after screenshots, and a short migration write-up. The upstream MS-PL is OSI-approved ([OSI license page](https://opensource.org/license/MS-PL)); its conditions still require preserving notices and including the complete license with source distributions.
- The migration write-up is mandatory and counts toward judging. It should state how much work the migration took, what moved cleanly, what needed redesign, and what surprised the team.
- The original application must predate the contest. Rewrites count, and another author's code may be used when its license permits.
- Teams can include up to three people; AI-assisted work is permitted.

## Fit and project decisions

- ScreenToGif predates the challenge and uses the Microsoft Public License (MS-PL), which is preserved in this repository. Keep the port independently branded and retain the complete license/attribution notices.
- Frame Studio is being built as a separate Avalonia product beside the original WPF solution. This makes the migration boundary and source comparison reviewable.
- The challenge allows a complete, carefully made small port to outperform a sprawling unfinished one. Prioritize the full screen recording → editable project → GIF export path before webcam, sketchboard, advanced effects, or broad platform claims.
- The most credible initial positioning is Legacy Revival, with a refined everyday recording workflow. Claim an additional platform only after the build is downloadable and its capture path has been exercised on that OS.
- Preserve dated notes and time spent as the work proceeds so the mandatory cost/lessons write-up can be based on evidence rather than memory.

## Current evidence and gaps

- The independent Avalonia shell builds and runs on macOS arm64.
- A Windows GDI monitor/window/screen-region recording backend has been added behind neutral contracts, but it still needs runtime verification on Windows and UI integration.
- The original WPF app could not be launched on the macOS development host. The original project was compiled with its Windows-only post-build event disabled. A Windows machine is still needed to capture true before/after images and exercise the recorder.
- There is not yet a complete recording → edit → GIF export flow or a downloadable Windows build. Those are the highest-priority gaps for a strong entry.
