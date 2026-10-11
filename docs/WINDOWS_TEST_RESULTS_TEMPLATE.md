# Windows candidate test record

Fill this out while running the current Windows candidate. Replace each `TBD` with the observed result; write `not tested` when a check cannot be run. Keep failures and limitations visible.

Hosted run [38121593525](https://github.com/kris70lesgo/frame-studio/actions/runs/38121593525) passed all 7 native capture checks and confirmed the packaged app created its “Frame Studio” main window. This record is for the separate interactive app, desktop, and screenshot checks.

## Test environment

- App-code commit: `f5ce93f9d23cfa3ed290f152e1e41e9d6ce8cfab`
- Package revision: `f5ce93f`; ZIP SHA-256: `795a65d55adf059793db9edaba75abfe13235c7069e36672786293fc5840812f`
- Candidate download: [Preview 4 draft release](https://github.com/kris70lesgo/frame-studio/releases/tag/untagged-d8d708cea4310c45574d)
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
| GIF export options and output | TBD | FPS: ; palette/colors: ; dithering: ; loop: ; dimensions: ; file size: |
| MP4 export options and output (optional) | TBD | FPS: ; quality: ; dimensions: ; FFmpeg/libx264 result: |
| Theme, recording, editor, and export defaults persist after restart | TBD | |
| Advanced FFmpeg path and app-data folder controls work | TBD | |
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
