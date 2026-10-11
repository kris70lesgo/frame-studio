# Windows candidate test record

Fill this out while running the current Windows candidate. Replace each `TBD` with the observed result; write `not tested` when a check cannot be run. Keep failures and limitations visible.

Hosted run [38118014644](https://github.com/kris70lesgo/frame-studio/actions/runs/38118014644) passed all 7 native capture checks and confirmed the packaged app created its “Frame Studio” main window. This record is for the separate interactive app, desktop, and screenshot checks.

## Test environment

- PR head: `b30fa220f4959d61bf782a5befed0eafdefc6003`
- Latest app-code commit: `cc0aefd8710cd4ff7bdc55783291cc12fd037bf5`
- Package revision from bundled README: `ff0b2cf` (GitHub synthetic merge checkout)
- Candidate ZIP SHA-256: `a88fe762f053bb5260ff2e67352882ebb2d9a6cfd46b0609b77b9598411008f9`
- Candidate download: [FrameStudio-win-x64 artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38118014644/artifacts/11694457167)
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
| Window capture while another window overlaps the target | TBD | App/window used: ; output complete: |
| `.fsp` project opens and playback works | TBD | |
| Reorder, duplicate, delete, and retime frames | TBD | |
| Crop, resize, text overlay, save, and reopen | TBD | |
| GIF export plays with expected order/timing/dimensions | TBD | Output dimensions: ; file size: |
| MP4 export (optional) | TBD | FFmpeg/libx264 result: |
| Theme and recording defaults persist after restart | TBD | |
| Webcam and Sketchboard availability states are clear | TBD | These modes are not implemented in this port |
| Full display / secondary display (if available) | TBD | |

## Before/after screenshots

Use ScreenToGif 2.43.2 and Frame Studio against the same harmless sample content where the mode exists. Save these pairs under `docs/before-after/` and list their filenames here. For webcam and sketchboard, capture the original mode and Frame Studio's clearly marked unavailable state; do not imply the mode was migrated:

- Home: TBD
- Recorder: TBD
- Webcam state: TBD
- Sketchboard state: TBD
- Editor: TBD
- Export completion: TBD
- Settings: TBD

## Issues and limitations

TBD

## Migration effort

Measured hands-on time, if available: TBD. If no timer was kept, leave it unknown; Git commit elapsed time is not a substitute for active work time.
