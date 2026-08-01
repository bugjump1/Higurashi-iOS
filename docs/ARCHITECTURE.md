# Architecture

## Compatibility model

The iOS app does not load the PC `Assembly-CSharp.dll`. iOS uses IL2CPP, so the port reimplements the runtime behavior in source form and treats the existing installation as content.

07th-Mod compatibility is split into three layers:

1. Data compatibility: scripts, PNG images, OGG audio, JSON metadata, and the included MP4 video.
2. Buriko compatibility: script instructions, state, calls, flags, saves, and checkpoints.
3. Presentation compatibility: art set, audio set, censorship level, lipsync, filters, text, menus, and touch input.

Desktop-only behavior such as window placement, monitor enumeration, Steam, AVProVideo, and online DLL updates is not carried over.

The app name is `HigurashiEp01`, matching the source executable's file base name.
Its nine-frame embedded icon is extracted locally; CI reconstructs the icon from encrypted
GitHub Secrets so the repository still contains no original artwork.

## Screen model

The novel renders into a virtual canvas. Script coordinates remain independent from physical pixels. iPad and iPhone use the same canvas with safe-area-aware controls and one of three presentation modes: original 4:3, fit, or fill.

The first version is full-screen landscape-only. iPad split-screen and Stage Manager resizing are deliberately deferred.

## Touch model

All input is converted to semantic actions before it reaches the script runtime.

- Tap: reveal the current line, then advance.
- Swipe up: history.
- Swipe down: hide or restore the text window.
- Three-finger right-to-left swipe: start continuous fast-forward.
- Three-finger left-to-right swipe: start continuous fast-rewind.
- Any later touch stops continuous traversal and is consumed.

Fast traversal renders every dialogue checkpoint and its corresponding scene. It defaults to ten checkpoints per second. Choices, chapter boundaries, blocking confirmations, and non-skippable movies stop traversal.

## Data pack safety

The data pack has a manifest containing path, size, and SHA-256 for every file. Import uses a staging directory and rejects absolute paths, traversal (`..`), duplicate paths, unexpected files, size mismatches, and hash mismatches. Imported data is swapped into place only after complete verification.

## Build pipeline

1. GameCI runs Unity on Linux and exports an Xcode project.
2. The Xcode project is transferred as a short-lived workflow artifact.
3. A macOS runner compiles with code signing disabled.
4. The `.app` bundle is packaged as `Higurashi-01-iOS-unsigned.ipa`.
