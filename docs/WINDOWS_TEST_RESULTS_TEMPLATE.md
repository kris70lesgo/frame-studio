# Windows candidate test record

Fill this out while running the current Windows candidate. Replace each `TBD` with the observed result; write `not tested` when a check cannot be run. Keep failures and limitations visible.

Hosted run [38028652057](https://github.com/kris70lesgo/frame-studio/actions/runs/38028652057) passed the native capture suite and confirmed the packaged app created its “Frame Studio” main window. This record is for the separate interactive app, desktop, and screenshot checks.

## Test environment

- PR head: `109d1127d6800a63cb6f7c10d226c05951363c5a`
- Package revision from bundled README: `11cf736` (GitHub pull-request merge checkout)
- Candidate ZIP SHA-256: `b689d6a336051db3b65195e605773b6105bc4b705c1d24ef5daa63dffd70563a`
- Candidate download: [FrameStudio-win-x64 artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38028652057/artifacts/11660829564)
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
