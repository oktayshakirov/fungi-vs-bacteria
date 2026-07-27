# Handoff Summary — Fungi vs Bacteria (Unity Tower Defense)

## 1. Current goal
Polish a functional tower-defense game into something that **looks good and plays well on mobile**,
working toward a "Plants vs Zombies / Storm Wars floating-island" visual target. The core game is
complete; recent work is all **visual/UX**. The immediate task just finished: making the map look like
a **levitating floating island**. Next natural step is **playtest/balance** and **UI polish**.

## 2. Key decisions (and why)
- **Board reshaped to 13×6** (from the original square 20×20, then 16×9). Square looked lost; 16×9 made
  units tiny. 13×6 (cellSize 5, centered on origin) fills a landscape phone and keeps towers/enemies readable.
- **Camera = low cinematic** (`CameraRig`: pitch 26°, FOV 50, `adaptPitchToAspect=false`). The wide lens
  gives perspective depth like the reference; chosen over near-orthographic (flat) and very-low pitch 24°
  (too edge-on to read the grid).
- **Per-environment theming** via `EnvironmentTheme` (runtime, from `GameManager.Start`): gradient-sky
  skybox shader + per-env ground/light/soil/path colors. 3 environments: Meadow (day/green), Desert
  (dusk/sand), Toxic (night/dark).
- **Procedural everything** (no external art): ground textures, star sprite, cliff/props/clouds are
  code-generated primitives. Self-contained; upgradeable to real art later.
- **Floating-island illusion** from what's visible at a top-down angle: tall tapering **cliff** under the
  board, **distant floating islands** + **distant clouds** in the surrounding sky (NOT hugging the base).
- **Testing switch** `LevelProgress.UnlockAll = true` unlocks all environments/levels — **set false before release**.

## 3. Current state of code
Builds clean (macOS, ~169 MB); all 30 levels validate. Recent visual work (board reshape, camera rig,
environment/sky system, procedural textures, props/base/portal, neon, floating island, HUD buttons) is
committed as of this handoff.

Key files:
- `Assets/Scripts/Gameplay/Camera/CameraRig.cs` — framing (fits board corners), pitch/FOV, view presets
  (cinematic/isometric/angled), intro orbit, screen shake.
- `Assets/Scripts/Gameplay/Environment/EnvironmentTheme.cs` — palettes + `Current`; sky, ground, light, path recolor.
- `.../Environment/LevelDecorator.cs` — cliff, distant clouds, distant islands, debris, props, neon orbs,
  base, portal (runtime, `[DefaultExecutionOrder(100)]`).
- `.../Environment/GroundTextureFactory.cs` — Sand/Toxic/Dark procedural textures.
- `Assets/Shaders/GradientSky.shader` — sky (gradient, sun/moon, horizon clouds, stars).
- `Assets/Editor/DisplaySetup.cs` — one-shot scene surgery (camera rig config, board size, safe areas,
  board base, decorator); run via `Tools → Display`.
- `Assets/Editor/CameraPreview.cs`, `LevelGenerator.cs`, `Phase1Validator.cs` — batch tooling.
- `Assets/Scripts/Managers/HUDManager.cs` — `HudUiRoot()` parents runtime UI to the HUD canvas.

## 4. Constraints / requirements
- Unity **6000.2.9f1**, URP, landscape-only, mobile-first (Android primary, iOS second).
- **Verification loop:** run headless via
  `/Applications/Unity/Hub/Editor/6000.2.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -executeMethod X`.
  Use `-nographics` for logic (`DisplaySetup.Apply`, `LevelGenerator.GenerateBatch`, `Phase1Validator.Validate`,
  `BuildTools.BuildMacBatch`); **omit `-nographics` for `CameraPreview.Render`** (needs GPU).
  **The editor must be closed** (lock file) or batch fails.
- `CameraPreview.Render` writes env1/2/3 + framing images to `Builds/CameraPreview/` — the primary way to
  *see* changes. It **cannot capture screen-space HUD** or the runtime-only path line — those need in-game checks.
- Commits: authored as **oktayshakirov**, **no Claude co-author trailer**.

## 5. Tried and ruled out
- Square and 16×9 boards (units too small). High top-down camera (flat/no depth).
- Base-hugging cloud puffs and a cloud floor far below (looked like sitting in fog / fell off-screen).
- "Dark Ground" texture (muddy) and the toxic glow-pool ground (too busy) — replaced with a clean dark ground.
- Strong emission on neon (washed to white) — kept ~1.1 so hue survives + bloom halo.

**Bug gotchas:** `GridManager.Instance`/`PathManager.Instance` are null in edit-mode preview (need
`#if UNITY_EDITOR FindFirstObjectByType` fallback); `Mathf.SmoothStep` is NOT GLSL smoothstep; the TMP font
is ASCII-only (use `UiFont`, no special glyphs — stars are sprites); the path LineRenderer's dark texture
forced it black (theme nulls the texture); `HUDManager` sits on a non-Canvas transform so runtime UI must be
parented via `HudUiRoot()`.

## 6. Next steps
1. **User playtest** on device/editor: confirm the floating look, the SPEED/VIEW buttons, the isometric
   preset, and the bright path in-game.
2. **Balance pass** (needs the user): enemy `goldReward` and generator wave difficulty — tune from feedback.
3. Optional polish: tint distant islands per-environment (green at night is slightly off); per-level camera
   presets for variety; **UI look pass** (flagged for "later").
4. Pre-launch: set `LevelProgress.UnlockAll=false`, real app icon / environment-card art, decide on empty
   environments 4–7, analytics/crash reporting, store listing (see `DISTRIBUTION.md`).
