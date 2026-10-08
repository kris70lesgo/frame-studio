# Frame Studio Jitter-inspired interface

## Goal

Apply the visual language and editor hierarchy of the user's open Jitter workspace across Frame Studio. The interface should feel like a compact creative tool rather than a separate capture dashboard while keeping Frame Studio's capture, frame-editing, and export behavior.

## Direction

Use Jitter's near-black 48-pixel command strip, compact monochrome tools, white side panels, pale gray work surface, thin neutral dividers, small corner radii, and violet action color. Keep a centered artboard and inspector tabs. Frame Studio retains its own project and capture concepts: projects and frames replace layers, the recording setup replaces shape creation, and the lower filmstrip remains for playback and per-frame timing.

The home workspace uses the same three-part editor shell: recent projects at left, a screen preview artboard in the center, and Capture/Project tabs at right. The editor uses a frame outline at left, captured pixels on the central stage, Frame/Project tabs at right, and its timeline along the bottom.

## Behavior and constraints

- Keep the screen-recording, open-project, recent-project, theme-toggle, edit, save, playback, and export actions connected to their current handlers or commands.
- Preserve unavailable Window, Webcam, and Sketch actions as visibly unavailable in this shell build.
- Default to Jitter-like light surfaces and violet accents while retaining the existing light/dark theme toggle.
- Apply the shared palette to capture setup, recording, editing dialogs, and the full-screen region selector.
- Do not change project data, capture services, or export behavior.

## Verification

Build the Avalonia app in Release mode and inspect both the home workspace and editor with the existing local sample project. This macOS visual check does not validate native Windows recording.
