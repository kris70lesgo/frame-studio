# Text overlay design

## Goal

Add the editor's first annotation tool: place readable text on a recording and carry it through preview, save, reopen, and GIF export. The first version applies the same overlay to every frame, matching the existing crop and resize commands. The user did not answer the scope question within the initial 30-second window, so this plan uses that consistent default.

## Design

The `.fsp` format stores finished RGBA frame pixels and has no layer model. Render the text into each frame through SkiaSharp and write the result to the editor's existing temporary draft archive. This keeps project files self-contained and makes previews, later edits, and exports use the same pixels. Text is baked into the frames; users can discard the draft before saving, but cannot reposition text after applying it.

Enable the existing Text tool and open a compact Avalonia dialog. Provide text, font size, color, and top-left position in canvas pixels. Validate non-empty text, supported size, color syntax, and coordinates before processing. Cap text length and render only on a background worker. Keep SkiaSharp in the core pixel-transform boundary, which already uses neutral RGBA buffers; use the version resolved by the existing Avalonia stack.

## Data flow and failure handling

The dialog returns a validated annotation request. The editor view model applies it to the currently ordered frame references using an archive-editor operation. The operation transforms frames into a draft archive; on failure it deletes the draft and keeps the current project intact. On success the editor refreshes the preview and timeline and marks the project dirty. Existing Save and Discard actions decide whether the draft replaces the original project.

## Verification

Add pixel-transform tests that confirm text changes pixels inside the rendered glyph bounds while preserving pixels elsewhere. Add an archive-level test that checks the overlay appears in every frame after save and survives GIF export. Build and run the cross-platform suite, then inspect the text dialog and edited preview in both themes. Font fallback can differ by OS, so verify the Windows result before release.
