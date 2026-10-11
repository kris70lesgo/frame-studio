# Windows candidate test record

Fill this out while running the current Windows candidate. Replace each `TBD` with the observed result; write `not tested` when a check cannot be run. Keep failures and limitations visible.

Hosted run [38122088760](https://github.com/kris70lesgo/frame-studio/actions/runs/38122088760) passed all 7 native capture checks and confirmed the packaged app created its “Frame Studio” main window. This record is for the separate interactive app, desktop, and screenshot checks.

## Test environment

- App-code commit: `579ed9afaa30c798677880f37a93300a473dcb80`
- Package revision: `579ed9a`; ZIP SHA-256: `4fb41c4b23fe14f58d0207ae746c2147f70b03f6318fe403e2cd0505805715b0`
- Candidate download: [Preview 4 draft release](https://github.com/kris70lesgo/frame-studio/releases/tag/untagged-d50f5bb6d114cc43469c)
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

Use ScreenToGif 2.43.2 and Frame Studio against the same harmless scene and project. Save the four challenge comparison pairs under `docs/before-after/`, using comparable window sizes where possible:

- Home: TBD
- Recorder: TBD
- Editor: TBD
- Export: TBD

Optional availability evidence (not part of the four same-content pairs):

- Webcam unavailable state: TBD
- Sketchboard unavailable state: TBD

## Issues and limitations

TBD

## Migration effort

Measured hands-on time, if available: TBD. If no timer was kept, leave it unknown; Git commit elapsed time is not a substitute for active work time.
