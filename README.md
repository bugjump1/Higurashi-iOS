# Higurashi iOS research ports

Personal research project for running legally owned PC copies of *Higurashi When They Cry Hou* chapters as independent iOS apps on iOS/iPadOS 15 or newer. Chapters 1 and 2 are currently configured.

The repository contains no game scripts, images, audio, video, or proprietary Unity binaries. Game data is packaged locally from the owner's PC installation and imported on-device after installation.

## Fixed targets

- iOS/iPadOS 15.0+
- ARM64 and IL2CPP
- iPhone and iPad
- Landscape left and landscape right
- GitHub Actions build; no local Mac required
- Independent apps and save containers per chapter
- Bundle IDs: `com.bugjump.higurashi.ep01`, `com.bugjump.higurashi.ep02`, and so on
- Unsigned artifacts: `Higurashi-NN-iOS-unsigned.ipa`
- Signing is intentionally outside the build pipeline

## Repository layout

- `ios-port/` - Unity project
- `tools/Higurashi.DataPack/` - local game-data pack generator
- `tools/Higurashi.Core.SmokeTests/` - dependency-free core behavior tests
- `tools/Higurashi.ScriptAudit/` - compiled Buriko container validator
- `tools/Higurashi.RuntimeProbe/` - headless Buriko startup probe
- `tools/Higurashi.IconExtractor/` - extracts the original executable's multi-size icon
- `docs/ARCHITECTURE.md` - design and compatibility boundaries

## Local data pack

The pack tool detects the `HigurashiEpNN_Data` directory and writes a chapter-specific manifest. For chapter 2:

```powershell
dotnet run --project .\tools\Higurashi.DataPack -- `
  "D:\project\PCtoiOS\game files\Higurashi When They Cry 02" `
  "D:\project\PCtoiOS\output data zip\Higurashi-02-data.zip"
```

Copy the matching `Higurashi-NN-data.zip` into that chapter app's Files directory, launch the app, and choose Import. The source game directory is never modified.

## GitHub Actions

The workflow uses GameCI to generate an Xcode project on Linux, then compiles it without signing on a `macos-15` runner. Unity Personal users need to configure the documented GameCI license secrets:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

The iOS display name follows each executable's file base name (`HigurashiEp01`,
`HigurashiEp02`). Icons remain local-only and are restored during CI from two
chapter-specific secrets (split to stay below GitHub's per-secret size limit):

- `HIGURASHI_APP_ICON_BASE64_1`
- `HIGURASHI_APP_ICON_BASE64_2`
- `HIGURASHI_EP02_APP_ICON_BASE64_1`
- `HIGURASHI_EP02_APP_ICON_BASE64_2`

For this workspace the ready-to-paste values are stored locally at:

- `.tools/HIGURASHI_APP_ICON_BASE64_1.txt`
- `.tools/HIGURASHI_APP_ICON_BASE64_2.txt`
- `.tools/HIGURASHI_EP02_APP_ICON_BASE64_1.txt`
- `.tools/HIGURASHI_EP02_APP_ICON_BASE64_2.txt`

These files and `ios-port/Assets/Branding/AppIcon.png` are ignored by Git. The
workflow reconstructs the icon before Unity starts, so no original artwork is
committed.

No Apple signing secrets are used.
