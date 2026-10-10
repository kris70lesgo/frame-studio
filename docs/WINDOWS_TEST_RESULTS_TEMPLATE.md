# Windows candidate test record

Fill this out while running the current Windows candidate. Replace each `TBD` with the observed result; write `not tested` when a check cannot be run. Keep failures and limitations visible.

The native capture service already passes 6 Windows integration tests in [hosted run 37981332433](https://github.com/kris70lesgo/frame-studio/actions/runs/37981332433). This record is for the separate interactive app, desktop, and screenshot checks.

## Test environment

- App source revision: `60e97d11dfcf01469880ab0ecab65b979cdeb40e` (app implementation unchanged from `408babf6`)
- Package revision from bundled README: `66b942f` (GitHub's synthetic pull-request merge revision)
- Candidate ZIP SHA-256: `1ce102ce20e73918a313d7a71d9fb8944538fe316df9006b3226de1ae238ac4b`
- Windows edition and build: TBD
- PC / CPU architecture: TBD
- Display resolution and scale: TBD
- Number of displays; mixed-DPI setup: TBD
- FFmpeg installed; `libx264` available: TBD
- Date tested: TBD

## Workflow results

| Check | Result | Notes |
| --- | --- | --- |
| App launches from extracted ZIP | TBD | |
| Select display and region | TBD | Region dimensions: |
| Record, pause, resume, stop | TBD | FPS: ; duration: ; captured frames: |
| App home and recorder controls stay out of captured frames | TBD | |
| `.fsp` project opens and playback works | TBD | |
| Reorder, duplicate, delete, and retime frames | TBD | |
| Crop, resize, text overlay, save, and reopen | TBD | |
| GIF export plays with expected order/timing/dimensions | TBD | Output dimensions: ; file size: |
| MP4 export (optional) | TBD | FFmpeg/libx264 result: |
| Full display / secondary display (if available) | TBD | |

## Before/after screenshots

Use ScreenToGif 2.43.2 and Frame Studio against the same harmless sample content. Save these pairs under `docs/before-after/` and list their filenames here:

- Home: TBD
- Recorder: TBD
- Editor: TBD
- Export completion: TBD

## Issues and limitations

TBD

## Migration effort

Measured hands-on time, if available: TBD. If no timer was kept, leave it unknown; Git commit elapsed time is not a substitute for active work time.
