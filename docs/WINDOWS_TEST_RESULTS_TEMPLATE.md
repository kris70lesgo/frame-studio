# Windows candidate test record

Fill this out while running the current Windows candidate. Replace each `TBD` with the observed result; write `not tested` when a check cannot be run. Keep failures and limitations visible.

The native capture service passes its Windows integration suite in [hosted run 38025357431](https://github.com/kris70lesgo/frame-studio/actions/runs/38025357431). This record is for the separate interactive app, desktop, and screenshot checks.

## Test environment

- App source revision: `8e2819bdd8215aa7ee1ed40a8bd7a0814a44ac48`
- Package revision from bundled README: `7b7057a` (GitHub synthetic PR merge revision; application source `8e2819bd`)
- Candidate ZIP SHA-256: `d7a970b4737510b8bf28abd53705ad45b613902b3180c30e79896b67ff3cf80b`
- Candidate download: [FrameStudio-win-x64 artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38025357431/artifacts/11660098796)
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
