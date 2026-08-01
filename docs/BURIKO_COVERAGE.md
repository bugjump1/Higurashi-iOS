# Buriko compatibility coverage

Audit source:

- Current repaired `Assembly-CSharp.dll` version marker: commit `cf456b8`
- Reference branch: `07th-mod/higurashi-assembly`, `oni-mod`
- Script folders: `CompiledChineseScripts`, `CompiledScripts`, and `CompiledUpdateScripts`

Current bytecode audit:

- 203 MGSC scripts
- 638 callable blocks
- 161,570 line checkpoints
- 353,127 bytecode commands
- 87 distinct operations used
- 0 container or bytecode scan failures

Runtime verification:

- 15 dependency-free core smoke tests pass
- Real `init.mg` reaches `TitleScreen`
- Simulated Start/ChapterPreview reaches five input-gated Chinese dialogue checkpoints
- First verified gameplay checkpoint: `onik_op`, line 43 (`我相信她。`)
- `OutputLine` modes 0 and 2 are treated as the real per-line input/checkpoint boundary
- Runtime snapshots restore script position, call stack, scopes, and global/local flags

Implementation priority is based on actual chapter-one usage rather than the full legacy enum.

## Priority 0: interpreter and dialogue

- Calls, jumps, returns, conditions, declarations, assignments, and variables
- Global/local flags and work values
- `OutputLine`, `OutputLineAll`, `ClearMessage`, text speed, and window state
- `Wait`, `WaitForInput`, input validity, save validity, and skip validity

## Priority 1: scene and audio

- `DrawScene`, masks, backgrounds, bustshots, character filters, sprites, and fades
- `PlayBGM`, `PlaySE`, `ModPlayVoiceLS`, volume/fade, and waits
- 07th-Mod art/BGM/SE/audio-set registration and folder cascades

## Priority 2: user-visible systems

- Choices
- Saves, quick saves, read history, and rollback checkpoints
- Tips, chapter screen, chapter preview, title, extras, and achievements as local flags
- Censorship, ADV/NVL layouts, font, margins, and presentation settings

## Priority 3: effects and platform replacements

- Film, negative, shaking, blur, layer filters, lipsync, and movie playback
- Steam becomes a local no-op implementation
- AVProVideo is replaced by Unity VideoPlayer using the included H.264/AAC MP4
- Window/display operations become mobile presentation and render-quality settings
