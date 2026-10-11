# Export options design

## Goal

Make the export flow match the project brief's format-specific options while preserving the current one-click defaults for users who accept them. The GIF flow needs frame rate, palette quality, dithering, loop behavior, and output dimensions. MP4 needs codec/quality, frame rate, and output dimensions. The dialog should show output dimensions, frame count, and duration before the file picker; compressed file size remains content-dependent and is not estimated.

## Design

Keep Preferences as the place to save defaults. When the user chooses Export GIF or Export MP4, show a compact format-specific settings dialog initialized from those defaults. The dialog returns an immutable export-options record; the existing file picker then chooses the destination. Canceling either step stops the export without writing a file.

GIF supports source frame timing or fixed-rate resampling, existing color quantizers and palette sizes, optional Floyd–Steinberg dithering, loop/no-loop, and either the selected scale preset or a custom output size. MP4 stays H.264/libx264, with the existing CRF presets, optional fixed output frame rate (source timing remains the default), and scale or custom dimensions. Both exporters validate output dimensions against the project canvas limits before allocating resized frames. H.264 output continues to pad odd dimensions to even values.

The preview shows final dimensions, output frame count, and total duration. It does not promise an output byte count because that depends on image content, palette, motion, and encoder behavior.

## Verification

Core tests now cover GIF fixed-rate resampling, exact custom dimensions, dithering changing decoded gradient pixels, and MP4 custom dimensions/fixed-rate output when FFmpeg/ffprobe are available. The local Release build succeeds and all 50 runnable tests pass; four Windows-only capture tests skip on macOS. Both export dialogs have been opened and interacted with in the local app; custom dimensions, fixed-rate frame-count preview, aspect-ratio entry, and MP4 even-dimension preview were checked. CI run 38122088760 passed both platform jobs and package smoke for the aspect-ratio fix. The refreshed candidate is in the Preview 4 draft; the manual Windows workflow remains open.
