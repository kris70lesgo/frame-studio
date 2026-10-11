# Windows candidate test record

Fill this out while running the current Windows candidate. Replace each `TBD` with the observed result; write `not tested` when a check cannot be run. Keep failures and limitations visible.

Hosted run [38118830518](https://github.com/kris70lesgo/frame-studio/actions/runs/38118830518) passed all 7 native capture checks and confirmed the packaged app created its “Frame Studio” main window. This record is for the separate interactive app, desktop, and screenshot checks.

## Test environment

- Source commit: `e4a864ada6d8e8fc56142c4dcffb4fc2f15b91de`
- Package revision from bundled README: `1ef2a5c59759114aa6c5ff4b976e977ee7e77389` (GitHub PR merge checkout)
- Candidate ZIP SHA-256: `fc19834d7a9cbb13eb03cb29c91fd27885134a637b7be8e5b56b3846d2a63c0b`
- Candidate download: [FrameStudio-win-x64 artifact](https://github.com/kris70lesgo/frame-studio/actions/runs/38118830518/artifacts/11694438722)
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
