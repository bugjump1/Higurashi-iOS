[简体中文](./README.md) | [English](./README_EN.md)

# Higurashi When They Cry iOS Compatibility Port

This is an unofficial personal research project for reading Episodes 01 through 08 of the legally owned PC release of *Higurashi When They Cry* on iPhone and iPad.

Each episode is built as an independent app with its own name, icon, bundle identifier, and save container. This is not a Windows emulator, cloud-streaming client, or video player. The apps run as iOS/ARM64 IL2CPP applications, while a compatibility runtime processes the original scripts, graphics, audio, saves, and touch input locally on the device.

> Neither this repository nor the generated IPA files contain original game scripts, images, audio, video, or other copyrighted game assets. Users must legally own the matching PC episode and import its matching data package on first launch.

## Current Status

- Episodes 01 through 08 can be built as independent IPA files
- iOS/iPadOS 15.0 or newer
- ARM64, iPhone, and iPad
- Landscape only, with both landscape orientations and safe areas supported
- Console, remake, and original art/background sets
- Dialogue voice, BGM, sound effects, scene transitions, and lip sync on supported art sets
- Manual saves, quick saves, auto saves, and a consolidated Latest Save slot
- History with voice replay, chapter jump, TIPS, Omake/Staff Room content
- Episode 08 fragment list, prerequisites, reading progress, and persistent fragment state
- Automatic save before story choices; bad endings can return to the choice or title screen
- Diagnostic log export from System Settings to the app's `Documents/logs` directory

## Requirements

### Device

- An iPhone or iPad
- iOS/iPadOS 15.0 or newer
- Sufficient free storage: import temporarily requires the ZIP, extracted staging data, and installed data at the same time. Reserving substantially more than twice the ZIP size is recommended.

### Two files per episode

1. The matching unsigned IPA, such as `Higurashi-01-iOS-unsigned.ipa`
2. The data package with the same episode number, such as `Higurashi-01-data.zip`

Episode numbers must match. The EP01 app cannot import an EP02 package.

## Installation and First Launch

### 1. Install the IPA

The IPA files are unsigned and cannot be installed like ordinary documents. Choose an installation method suitable for your device:

- Sign the IPA with your own certificate
- Run it through LiveContainer
- Use TrollStore when supported by the device and OS version

Certificates, signing services, and third-party installation tools are outside this project's support scope.

### 2. Import the data package

1. Open the matching episode app.
2. Tap the data-package import button on the launcher screen.
3. Select the matching `Higurashi-XX-data.zip` in the native iOS Files picker.
4. Keep the app in the foreground while validation, copying, and extraction complete.
5. Continue into the game. A successful import normally does not need to be repeated.

The native file picker may behave differently under different LiveContainer versions and settings. If a visible ZIP cannot be selected, enable LiveContainer's file-import compatibility option as documented by that project.

### 3. Do not modify the data package

The app validates the ZIP's pinned byte length and SHA-256, then validates the manifest and every extracted file. Import will fail after any of the following:

- Editing, replacing, adding, or deleting files inside the ZIP
- Extracting and recompressing the package
- Selecting another episode's package
- Using an incomplete or damaged download

Renaming the ZIP alone does not change its content fingerprint. Import uses a staging directory and atomic replacement, so a wrong, cancelled, damaged, or interrupted import cannot replace an existing working data set. Creating a package never modifies the source PC installation.

## Touch Controls

| Gesture | Action |
| --- | --- |
| Single tap | Reveal the current text, then advance |
| One-finger right-to-left swipe | Advance, like a tap |
| One-finger left-to-right swipe | Return to the previous complete text box and restore its presentation/audio state |
| One-finger swipe up | Open history |
| One-finger swipe down | Hide or show the text window |
| Three-finger right-to-left swipe | Start line-by-line fast-forward |
| Three-finger left-to-right swipe | Start line-by-line fast-rewind |
| Any touch during traversal | Stop fast-forward or fast-rewind |

Rewind stops at the first line of the current chapter and never crosses into the previous chapter. New-format saves preserve the current chapter's checkpoint history. Legacy saves begin collecting persistent history after they are first loaded by a new build.

## Saves and Extra Content

- Manual, quick, and automatic saves all refresh the Latest Save slot.
- Latest Save validation avoids restoring temporary menu or content-browser states from older builds and attempts to recover the most recent valid story checkpoint.
- Loading restores script state, dialogue, sprites, backgrounds, BGM, voice cues, and the current chapter's rewind history.
- A completed chapter-end TIPS entry returns to the TIPS list; closing that list returns to the chapter-complete screen.
- Chapter jump and TIPS unlock progressively from actual story progress.
- Completing an episode normally unlocks Omake for EP01-EP04 or Staff Room for EP05-EP08. A bad ending does not count as a normal clear.
- Normal saving is disabled while reading EP08 fragments. Fragment unlock and reading progress are stored separately and survive app restarts.

iOS saves and original PC saves use different formats and cannot be copied directly between platforms. Always back up files before using any conversion tool.

## Bug Reports and Logs

Useful reports include:

- Episode number and approximate story position
- Device model and iOS/iPadOS version
- Installation method, such as signing, LiveContainer, or TrollStore
- A screenshot or screen recording
- The diagnostic report exported from System Settings

Logs are stored under the app sandbox's `Documents/logs`. They cover runtime state, saves/loads, chapter jumps, TIPS, fragments, and errors. They do not record GitHub Secrets, Unity passwords, or game-data contents.

## Repository Layout

- `ios-port/`: Unity iOS project and compatibility runtime
- `tools/Higurashi.DataPack/`: local data-package generator
- `tools/Higurashi.Core.SmokeTests/`: core behavior tests
- `tools/Higurashi.ScriptAudit/`: compiled Buriko script auditor
- `tools/Higurashi.RuntimeProbe/`: headless runtime probe
- `tools/Higurashi.IconExtractor/`: local executable icon extractor
- `docs/ARCHITECTURE.md`: architecture and compatibility boundaries

## Compatibility Boundaries

The iOS app does not load the PC `Assembly-CSharp.dll`. Desktop-only features such as Steam integration, window placement, multiple monitors, desktop peripherals, and online DLL updates are not ported. Some 07th-Mod data and presentation behavior are supported, but complete compatibility with every PC mod, script variation, and setting is not guaranteed.

## Credits

- Personal port: Tieba @bugjump / bilibili @Hyperion233
- EP01-EP07 Chinese base: [ycx Studios](https://github.com/ycx-Studios/higurashi-docs)
- EP08 translation: 990 and 麻生早纪; proofreading: 枝瀬愛; programming: 饭; editing: 990 and 麻生早纪
- The three-finger traversal concept was inspired by the iOS release of *Umineko When They Cry* from the 日不落 localization team

*Higurashi When They Cry* and all related assets remain the property of their respective rights holders. This project does not provide or authorize redistribution of the game or its assets.
