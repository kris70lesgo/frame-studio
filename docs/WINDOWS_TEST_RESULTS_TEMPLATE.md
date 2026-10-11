# Windows candidate test record

Fill this out while running the current Windows candidate. Replace each `TBD` with the observed result; write `not tested` when a check cannot be run. Keep failures and limitations visible.

Hosted run [38123652608](https://github.com/kris70lesgo/frame-studio/actions/runs/38123652608) passed the macOS and Windows build/test jobs, native Windows capture checks, and the packaged-app startup smoke. This record is for the separate interactive app, desktop, and screenshot checks.

## Test environment

- App-code commit: `8871a3ecc29ac2bfbace7cf82bb02b83dc995b1c`
- Package revision: `8871a3e`; ZIP SHA-256: `1e7021ca4a2a20aeadbd83c98052a409f978d1626d01673f357de435dba42950`
- Candidate download: [Preview 4 draft release](https://github.com/kris70lesgo/frame-studio/releases/tag/untagged-2fc6da4212fcf12834c2)
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
| Webcam device selection, live preview, record, pause/resume, stop, and project open | TBD | Camera: ; FPS: ; project dimensions: ; privacy access: |
| Sketchboard drawing, undo/clear, record, pause/resume, stop, and project open | TBD | Canvas size: ; FPS: ; frame count: |
| Full display / secondary display (if available) | TBD | |

## Before/after screenshots

Use ScreenToGif 2.43.2 and Frame Studio against the same harmless scene and project. Save the four challenge comparison pairs under `docs/before-after/`, using comparable window sizes where possible:

- Home: TBD
- Recorder: TBD
- Editor: TBD
- Export: TBD

Optional feature evidence (not part of the four same-content pairs):

- Webcam preview and recorded project: TBD
- Sketchboard canvas and recorded project: TBD

## Issues and limitations

TBD

## Migration effort

Measured hands-on time, if available: TBD. If no timer was kept, leave it unknown; Git commit elapsed time is not a substitute for active work time.
