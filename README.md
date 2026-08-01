# Higurashi 01 iOS research port

Personal research project for running a legally owned copy of *Higurashi When They Cry Hou - Ch.1 Onikakushi* on iOS/iPadOS 15 or newer.

The repository contains no game scripts, images, audio, video, or proprietary Unity binaries. Game data is packaged locally from the owner's PC installation and imported on-device after installation.

## Fixed targets

- iOS/iPadOS 15.0+
- ARM64 and IL2CPP
- iPhone and iPad
- Landscape left and landscape right
- GitHub Actions build; no local Mac required
- Unsigned artifact: `Higurashi-01-iOS-unsigned.ipa`
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

After the pack tool is built, run it against the directory that contains `HigurashiEp01_Data`:

```powershell
dotnet run --project .\tools\Higurashi.DataPack -- `
  "D:\project\PCtoiOS\Higurashi When They Cry 01" `
  "D:\project\PCtoiOS\Higurashi-01-data.zip"
```

Copy `Higurashi-01-data.zip` into the app's Files directory, launch the app, and choose Import. The source game directory is never modified.

## GitHub Actions

The workflow uses GameCI to generate an Xcode project on Linux, then compiles it without signing on a `macos-15` runner. Unity Personal users need to configure the documented GameCI license secrets:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

The original executable has no ProductName metadata, so the iOS display name is its
file base name, `HigurashiEp01`. Its icon remains local-only and is restored during CI
from two secrets (split to stay below GitHub's per-secret size limit):

- `HIGURASHI_APP_ICON_BASE64_1`
- `HIGURASHI_APP_ICON_BASE64_2`

For this workspace the ready-to-paste values are stored locally at:

- `.tools/HIGURASHI_APP_ICON_BASE64_1.txt`
- `.tools/HIGURASHI_APP_ICON_BASE64_2.txt`

These files and `ios-port/Assets/Branding/AppIcon.png` are ignored by Git. The
workflow reconstructs the icon before Unity starts, so no original artwork is
committed.

No Apple signing secrets are used.
