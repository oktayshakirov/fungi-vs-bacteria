# Handoff — Fungi vs Bacteria (Unity Tower Defense)

Last updated 2026-09-06. Working tree clean at `c13cffe` on `main`, pushed.
An earlier state is bookmarked as branch `handoff/2026-08-visual-overhaul`.

**Start here if you are a new session.** Read this file first; it supersedes the
per-phase notes elsewhere. Section 5 is the work queue, section 6 is every trap
that has actually cost debugging time.

## 0. The immediate next step: a device playtest

Everything in phases 13-15 is **render-verified or sim-verified only**. Nothing
below has been played by a human. The user is going to test next, so if you are
picking this up mid-test, expect findings rather than a clean slate.

What is worth deliberately checking, and what to look for:

| Area | What to check | Why it is uncertain |
|---|---|---|
| Drag-and-drop towers | Drag a card onto the board; also tap-card-then-tap-tile; also drag a card and drop it back on the tray | Never testable here — needs live touch input. The tap flow is unchanged; the drag flow is new |
| Towers panel | Scroll it, collapse it with HIDE TOWERS | Dragging **on a card** starts a tower drag, so the list can only be scrolled from the gaps between cards or the scrollbar. Known trade-off — see if it is annoying in practice |
| Placement bar / sell panel | Arm a tower, then tap a placed one | They share the bottom-left slot and are mutually exclusive by construction; the sell panel is the one piece of UI **not** render-verified |
| Haptics | A busy wave, then a base hit | Throttle intervals are first guesses; the whole point is that it must not buzz continuously |
| Tile indicators | Arm a tower on the snow and ash biomes | The old wash was invisible there; the new marker is untested against those grounds |
| Enemy tints | Play one level in env 3, 5 and 6 | Tints are eyeballed. Types must still be distinguishable from each other |
| Balance | Env 7 levels 3, 6 and 10 | The sim cannot win these. It plays optimally, so if it loses, a human loses — but the real player enters richer than the sim models |
| Locked states | Set `LevelProgress.UnlockAll = false` and walk the flow | Still `true`; no padlock or dimmed tile has ever been seen |

## 1. The goal

Ship a landscape, mobile-first tower defense game. The core game has worked for
a long time; everything recent is **visual quality, readability and UX**. The
current standard is "it should look like a real game and be comfortable to play
on a phone", benchmarked against Storm Wars / Plants vs Zombies.

Two things have **never been validated**: whether the game is *fun/balanced*,
and whether it *runs well* across devices. Both are called out in section 5.

## 2. Where the project is now

- Unity **6000.2.9f1**, URP, landscape-only, Android primary / iOS second.
- Board **10x5** (cellSize 5), 7 environments x 10 levels = **70 levels**,
  procedurally generated and all currently unlocked for testing.
- Environment art, props, sky, cliff and clouds are **generated in code**
  (`MeshFactory`, `GroundTextureFactory`) — the project ships no environment art.
- UI is skinned from code (`UiSprites` / `UiSkin`) — the project ships no UI art
  beyond the menu's Play button, gear icon and background.

## 3. Phases already done

Roughly in order. Each is committed.

1. **Core completion** — win condition, level configs, generator, pooling,
   tutorial, star ratings, game speed, haptics.
2. **Floating-island visuals** — camera rig, per-environment theming, gradient
   sky shader, procedural ground textures. (`43ade34`)
3. **Procedural environment art** — replaced Unity primitives with generated
   meshes; added a UI skin; added environments 4-7. (`49f81f3`)
4. **Readability pass** — board 13x6 -> 10x5, closer camera, larger units,
   reshaped island underside, static batching. (`0bec495`)
5. **UI scale-up + pooling** — global 1.5x UI, flattened platform, pooled
   per-hit effects, unified main menu. (`ff39acb`)
6. **First device-test fixes** — iOS audio session, splash removed, settings
   close button, UI haptics, Xcode scheme name.
7. **Ads + coin economy** — LevelPlay mediation behind an `Ads` facade, a
   persistent `Wallet`, rewarded ads, paced interstitials, the wallet screen,
   the start boost and the continue offer. See `ADS.md`. Keys are set
   (`Tools/Ads/Apply Ad Keys`); ironSource bidding is verified working end to
   end on device. Google bidding will return no fill until the app is live —
   expected, not a bug (see `ADS.md`).
8. **Currency merge + ad-economy hardening** — gold and coins were the same
   sprite/colour for two different balances, which read as a bug; merged into
   one `Wallet` with a floor (`Wallet.EnsureMinimum`) so losing everything
   can't strand a player unable to afford towers. Added `RewardedGate`
   (escalating cooldown + daily cap) and `DailyStreak` (5-day, ad-gated,
   escalating payout) so the rewarded faucet can't be farmed. Continues no
   longer cap at one — free-via-ad once per run, then coins at 200/400/800.
   Added `BootSplash` (bounded, cold-launch only) and music ducking around
   full-screen ads to hide the ad SDK's main-thread init work, which was
   producing an audible crackle and a couple seconds of stutter on launch and
   on returning from an ad. (`8eb3005`)
9. **iOS identity** — bundle id is `com.shadev.fungivsbacteria` on every
   platform, and the exported Xcode project/target/workspace/scheme is renamed
   from `Unity-iPhone` to `Fungi vs Bacteria` on every export, with `pod
   install` re-run automatically. See section 6.
10. **Balance** — `BalanceSim` harness plus a retuned generator; enemies now
    scale in strength, not just count. See Priority 1 below for the findings.
11. **Support towers** — Aura and Defense were purchasable but did nothing;
    they now buff nearby towers' damage / fire rate via `TowerBuffs`.
12. **Environment + level screen redesign** — named biomes with generated art,
    modern tiles, neon cues, shared header and corner-button styling.
13. **Enemy variety + turret aim fix** — four behaviour-driven enemy types
    (Swarm, Shielded, Splitter, Healer), staged one per environment from
    difficulty 11, and a fix for towers firing out of the side of their heads.
    See Priority 2 below for what the sim says about it.

14. **UI overhaul from the first UI playtest** — wallet dialog capped and made
    scrollable, main menu re-centred, environment/level screens wrapped in a
    runtime SafeArea, the towers panel rebuilt as a scrollable + collapsible
    frame, Start Wave moved to the bottom-left, and drag-and-drop tower
    placement added alongside the existing tap flow. (`6531810`)
15. **Queued UX list + gameplay haptics** — tower info box, sell/upgrade panel,
    tile indicators, onboarding card, per-environment enemy tint, and haptics
    on kills / base damage / tower shots. (`c13cffe`)

## 4. How to verify work — read this before changing anything

There is a real verification loop here. Use it; several bugs were only ever
caught by it.

**Fast compile check (does NOT need Unity closed):**

```
dotnet build Assembly-CSharp.csproj
```

Roughly one second. **New .cs files must be added to `Assembly-CSharp.csproj`
by hand** — it is auto-generated and gitignored, so Unity will regenerate it,
but until then a new file is silently not compiled.

**Rendering (the Unity editor must be CLOSED — the lock file fails the batch):**

```
/Applications/Unity/Hub/Editor/6000.2.9f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . -executeMethod <Method> -logFile <path>
```

| Method | What it gives you | `-nographics`? |
|---|---|---|
| `DisplaySetup.Apply` | Rewrites scenes: board size, camera presets, canvas scaler, menu layout, safe areas | yes |
| `LevelGenerator.GenerateBatch` | Regenerates all 70 levels | yes |
| `Phase1Validator.Validate` | Level asset QA gate | yes |
| `CameraPreview.Render` | The 3D board per environment | **no** |
| `CameraPreview.RenderEnvironmentCards` | Regenerates the environment card art | **no** |
| `UiPreview.Render` | HUD + every screen, as PNGs — including the main menu, the placement bar (`hud-placing`) and the tutorial (`screen-tutorial`) | **no** |
| `SceneCost.Report` | Draw calls / triangles / materials | **no** |
| `SceneCost.RenderCliff` | The island underside | **no** |
| `BalanceSim.RunBatch` | Plays all 70 levels, writes `Builds/Balance/balance.csv` | yes |

`CameraPreview` **cannot** capture the HUD — a ScreenSpaceOverlay canvas draws
straight to the backbuffer and never lands in a RenderTexture. That is why
`UiPreview` exists; it rebuilds the UI on a ScreenSpaceCamera canvas and runs
the real skin/theme code.

## 5. What to do next

**Priority 1 — Balance the 70 levels. DONE in the model, NOT yet in real play.**
`BalanceSim` (see section 4) now exists and the generator has been retuned
against it over four measured iterations. What it found and fixed:
- Enemy strength was **identical on level 1 and level 70** — the generator could
  only add more enemies, which made levels *longer*, not harder. Tower
  utilization actually FELL from 27% at difficulty 1 to 11% at 70.
- **Path length, not difficulty, decided wins.** Every unwinnable level had a
  path of exactly 12 (the old minimum) and the verdict ladder mapped
  monotonically onto mean path length. Band is now 15-20.
- Levels ran up to 7.9 minutes. Now 3.9 max.
- `WaveEnemyGroup.healthMultiplier` / `rewardMultiplier` are the new scaling
  lever, applied in `Enemy.Initialize`.

Result: kill depth (how far enemies get before dying) now rises 50% -> 64%
across the game and health starts dropping around difficulty 51, where before
both were flat. **But the sim's player proxy places towers optimally, so it
flatters the player — 70% of levels still read "trivial" to it.** Do not tune
further against the model. The next move is a real playtest; treat the sim as a
regression check, not as the source of truth.

**Priority 1b — Playtest the redesigned menus and the new balance.** Nothing
below has been played by a human yet, only render-verified:
- **Locked states are completely unexercised.** `LevelProgress.UnlockAll` is
  still `true`, so no preview can show a locked level tile (padlock, dimmed
  face) or a locked biome card. Set it `false` and walk the flow once.
- The **Home button** on the level screen routes through
  `EnvironmentsScreen.ReturnToMenu()`; confirm it actually lands on the menu.
- The **neon pulse** (`UiPulse`) only moves at runtime; batch renders capture a
  single frame, so its speed and depth have never been seen in motion.
- The balance retune assumes a **fresh wallet**. The wallet carries between
  levels now, so a returning player starts richer than the sim modelled.

**Priority 2 — Enemy variety. DONE in the model, NOT yet in real play.**
Four new types now exist, all driven from `EnemyConfig` and handled inside
`Enemy` (no new prefabs, no new components — the eight enemy prefabs are
untouched). Each arrives in its own environment so the player learns one thing
at a time, and they accumulate:

| Type | From difficulty | Mechanic | Counters |
|---|---|---|---|
| Swarm | 11 (env 2) | many, fast, tiny HP, low reward | single-target saturation |
| Shielded | 21 (env 3) | absorb pool, regenerates after 4s undamaged | slow chip damage |
| Splitter | 31 (env 4) | children spawn *where it died* | boards with no AoE |
| Healer | 41 (env 5) | heals nearby enemies on a timer | towers spread thin |

Two things this cost, both worth knowing before touching the numbers again:

- **Behaviours must be PAID FOR out of raw numbers, not stacked on top.**
  Adding all four on top of the existing curve put the sim at 15 losses (21%),
  every level from d54 up, each with 31-35 towers built and up to 4,944 gold
  unspent — a full board with nowhere left to build, i.e. straight through the
  structural ceiling. `HealthRampScale` came down 0.152 -> 0.1133 (peak ~3.0x
  -> ~2.5x) and Fast/Armored/Basic counts were trimmed where Swarm/Shielded now
  cover their role. That took it to 3 losses and pulled level length back from
  4.6 to ~4.0 minutes.
- **Boss count is not the lever it looks like.** The final levels were spawning
  three bosses (`1 + d/30`); cutting it to two changed the verdict of exactly
  zero levels and left the gold-unspent figures byte-identical. The late-game
  losses are sustained wave pressure against a board-capped player, not burst.

**The remaining 3 losses (Env7 L03/L06/L10, d63/66/70) are the open question.**
The sim proxy places towers optimally, so a level IT loses a human loses too —
that inference runs one way only, which is why these three were worth chasing
and the 79% "trivial" was not. They are all full-board-with-gold-piled-up, so
they are ceiling-bound rather than tunable. Before grinding the numbers
further, note the real player enters richer than the sim models (the wallet
carries between levels, and there is a continue mechanic) — so playtest these
three first and only tune if a human also loses them.

**Priority 3 — Confirm the performance fixes.** The user measured 60fps steady,
dipping to ~20 only past ~25 enemies. That was diagnosed as per-enemy
allocation, and `FloatingText` + enemy health bars have been pooled since — but
**the fix has not been re-measured on device**. `DeathEffect` is still
unpooled (once per kill, so lower priority).

**Priority 0 — Playtest the merged economy on a device.** This is new since
the last playtest and nothing below has been played against real usage yet:
- `Wallet.EnsureMinimum`'s floor (each level's `startingGold`, ~500) may make
  coins feel too easy to come by now that losing no longer costs you a
  separate currency — first thing to watch for.
- `RewardedGate`'s cooldown ladder (1/5/10 min) and 10/day cap, and
  `DailyStreak`'s payout curve (100/150/250/400/750) are first-guess numbers.
- `Boosters.ContinueCost` escalation (200/400/800) and the interstitial
  pacing (`Ads`) are likewise unproven.
- `BootSplash.maxSeconds`/`minSeconds` (6s / 1.2s) were never seen on device —
  confirm it doesn't linger or flash.
- Confirm the crackle/stutter fix (music duck + deferred ad reload) actually
  worked on launch and on returning from a rewarded ad; the prior fix (async
  audio session, delayed music start) reduced but did not eliminate it.

**Priority 2b — Give the new types real art.** All four currently reuse the
existing Basic/Fast/Armored prefabs, distinguished only by a tint and a scale
(`EnemyConfig.overrideBodyColor` / `scaleMultiplier`, applied through a
MaterialPropertyBlock). That is readable but not good. This is the strongest
argument for the Blender question — see section 8.

**Priority 4 — Gameplay haptics. DONE (`c13cffe`), not felt on device yet.**
Placement, wave start and sell already fired through `AudioManager.PlaySound`;
kills, base damage and tower shots now do too, via a new
`Haptics.PlayThrottled(style, minInterval)` — per style, on **unscaled** time
because the speed control runs at 2x/3x and a scaled clock would tighten the
limit exactly when most is happening. Intervals are first-guess numbers
(kills 0.12s, base damage 0.25s, shots 0.4s) and the only way to judge them is
a hand on a real phone during a busy wave.

**User's queued list — ALL FIVE DONE (`c13cffe`), none played by a human yet.**
- *Info box explaining each tower*: `TowerConfig.description`, filled in for all
  eight, surfaced in the placement bar (which absorbed the old bare Cancel
  button) and in the selected-tower panel.
- *Sell/upgrade UI*: rebuilt. **The Upgrade button is hidden, not broken** —
  `Tower.Upgrade()` still only logs. It reappears by itself the moment a real
  upgrade level exists. Deliberately not implemented in the same pass as a
  balance retune: upgrades are a new power lever and `BalanceSim` does not model
  them, so shipping both at once would have made the playtest unreadable.
- *Tile green/red indicators*: generated rounded-square markers with a gutter,
  blocked tiles deliberately much fainter than available ones.
- *Onboarding layout*: card, step counter, pips, Got It button, over a lighter
  scrim, centred on the BOARD so it never covers the towers panel.
- *Enemy colours per environment*: `EnvironmentTheme.Palette.enemyTint`.

**Priority 5 — Tower upgrades.** The one thing on the list that was scoped out
rather than finished, and now the most obvious missing feature: the sell panel
has a slot waiting for it. Needs a cost curve, stat scaling, some visual sign
of tier, and a `BalanceSim` pass, because it changes the difficulty ceiling the
whole balance model is built around (see the Balance traps below).

**Before release:** set `LevelProgress.UnlockAll = false`; delete the
**THEREN Trial** font (see below); analytics + crash reporting; real app icon
and store art; replace the synthesized SFX. See `DISTRIBUTION.md`.

## 6. Things that will bite you

These each cost real debugging time. They are not obvious from the code.

**Platform settings that only show up on a real device**
- iOS audio is silenced by the **ringer switch** unless
  `muteOtherAudioSources` is on, which puts the audio session in the Playback
  category. This is why the first device test had no sound at all; the editor
  and the simulator never reproduce it.
- The Unity splash screen is off (`m_ShowUnitySplashScreen: 0`). Unity 6 makes
  this legal on a Personal licence; on older versions it silently comes back.
- Unity always names the exported Xcode project, main app target, workspace
  and scheme `Unity-iPhone`. `IosPostProcess` renames all of them to
  `Fungi vs Bacteria` on export (project/target via a text rewrite of
  `project.pbxproj` targeting the app target's fixed template GUIDs, workspace
  via `contents.xcworkspacedata`, scheme via its `.xcscheme` XML), rewrites the
  Podfile's target line to match, then re-runs `pod install` so CocoaPods'
  generated xcconfig files follow. Runs on every export, not just the first —
  EDM4U regenerates the whole Podfile (with the Unity name) on every export.
  Deliberately left alone: the `Unity-iPhone Tests` target and the
  `Unity-iPhone` / `Unity-iPhone Tests` group folders on disk — those are real
  paths Unity's exporter still writes into, renaming them has no visible
  benefit. The shipped app's home-screen name is unaffected either way:
  `CFBundleDisplayName` comes from `productName`.

**Configuration**
- `DisplaySetup` is the **source of truth** for board size, camera presets,
  canvas scaler and menu layout. Change a constant there, then re-run
  `DisplaySetup.Apply`, or the scene keeps the old value.
- `CameraRig.playPitch` is **dead config** whenever `viewPresets` is non-empty —
  `ResolvedPose()` reads the presets instead. Changing pitch alone does nothing.
- Board size lives in `DisplaySetup.BoardWidth/Height` **and**
  `LevelGenerator.GridWidth/Height`. Both must change together, then levels
  must be regenerated (paths are grid coordinates).

**Unity behaviour**
- `Destroy()` is deferred to end of frame. Swapping a component in one call
  needs `DestroyImmediate`, or `AddComponent` fails and you get a null.
- **Layout is deferred too, and that failure is silent.** A screen that builds
  its own content must call `Canvas.ForceUpdateCanvases()` +
  `LayoutRebuilder.ForceRebuildLayoutImmediate(content)` before the frame ends.
  Without it the content rect stays `(0,0)`, the viewport mask clips every
  child, and you get an empty screen with **no exception anywhere** — it looks
  exactly like "the cards were never created". Cost a full debug cycle; the
  giveaway was a log line showing 10 children but a zero-size rect.
- **UI draws in sibling order**, so "behind" means an EARLIER sibling. A
  backdrop inserted at index 0 still loses to an opaque prefab background that
  sits later. Prefer repainting the prefab's own `Background` over inserting a
  competing one — that also leaves `BackgroundFill` ([ExecuteAlways], it
  rewrites the rect every frame) in charge of sizing instead of fighting it.
- `ScrollRect` with `AutoHideAndExpandViewport` resizes its own viewport around
  the scrollbar. Deactivating the bar and nulling `horizontalScrollbar` moves
  the viewport and takes the content with it — fade the bar with a `CanvasGroup`
  instead.
- TMP's `TextAlignmentOptions.Center` centres on the font's full line box,
  including descender space that all-caps display text never uses, so titles sit
  visibly high in a plate. Use **`Midline`** for caps.
- `UiSkin.StyleButton` styles the button's LABEL as a side effect. Anything
  replacing it must restyle the label too, or the prefab's authored font size
  comes back and the text overflows its plate.
- A parent `LayoutGroup` silently overrides anchored children. Runtime
  decorations need `LayoutElement.ignoreLayout = true`. This caused three
  separate bugs.
- `SetAsFirstSibling()` on a **root** component's transform reorders that object
  among its **siblings**, not its children. This silently reversed the
  environment list into 7..1.
- After `StaticBatchingUtility.Combine` you cannot measure generated geometry
  from a scene renderer — `sharedMesh` returns the island-wide merged mesh.
  Probe `MeshFactory` directly.
- `String.GetHashCode()` is not stable across runtimes. The per-level scatter
  seed uses a hand-rolled hash so levels don't re-scatter between sessions.

**Ads and economy**
- Opening a scene in batchmode can dirty it: `SafeArea` is `[ExecuteAlways]`
  and `BackgroundFill` sizes itself in `OnEnable`, so both recalculate against
  the batch-mode screen size and bake wrong anchors/sizes into
  `MainMenu.unity`. Check `git diff` on the scene after any batch run and
  revert stray `m_AnchorMin`/`m_SizeDelta` changes - one such edit would have
  permanently inset the menu UI by 23%.
- **The ads integration is done and verified**: ironSource bidding serves test
  ads on device, which exercises the whole chain. Google bidding returns 509
  and will until the app is live - bidding is real advertiser demand and there
  is no Google test inventory, so a not-yet-published app gets nothing. That is
  expected, not a bug. Develop against ironSource bidding; see the top of
  `ADS.md`, and do not re-debug the integration.
- Before release: test mode off for both networks, and `verboseLogging` off on
  the `Ads` component.
  `launchTestSuiteOnInit` is now off; `verboseLogging` is left on and should be
  turned off before a store build.
- LevelPlay's native SDK is not in the UPM package - it is fetched by a
  Network Manager step that only runs inside a normal (non-batch) Editor
  session. Missing `Assets/LevelPlay/Editor/*.xml` is why Xcode fails on
  `IronSource/IronSource.h` not found. Already fixed once; see the
  troubleshooting section in `ADS.md` if it recurs after a package bump.
- After adding or updating a native iOS plugin, re-export with
  **Tools -> Build -> iOS (Update existing Xcode export in place)** (writes
  into the checked-in `iOS/` folder, not `Builds/iOS`) and re-run
  `pod install` in `iOS/` before building in Xcode - the Podfile is
  regenerated by the export step, not by CocoaPods itself.
- Ad identifiers live in `Assets/Editor/AdsSetup.cs` and are written into the
  scene and the AdMob asset by `Tools/Ads/Apply Ad Keys`. Editing either target
  by hand drifts from the source.
- `Ads` is a facade. Nothing outside `Assets/Scripts/Ads` may reference the
  LevelPlay SDK, or the game stops running without keys.
- The star payout reads `LevelProgress.GetStars` **before** `SetStars`
  overwrites it. Reorder those two and every replay pays out again.
- **Coins and gold are now one currency.** `GameManager.currentGold` is a
  property that reads straight through to `Wallet.Coins` — there is no
  separate per-level pool anymore. The start-gold boost was removed for
  exactly this reason (paying coins for more of the same coins is free money).
  `Wallet.EnsureMinimum(level.startingGold)` runs at level start and is the
  only thing standing between this and a death-spiral: it tops up a
  below-floor wallet but never takes anything away. Do not reintroduce a
  separate gold pool without removing this call, and do not remove this call
  without reintroducing a floor of some kind.
- `RewardedGate` and `DailyStreak` roll their day over **lazily, on read**
  (`SyncDay` / `ResolvedIndex`), not via an update loop — comparing against
  `DateTime.Now`. Moving the device clock resets/skips them, same trade-off as
  `LevelProgress`. Not worth a server for a single-player game.
- `Ads.OnFullScreenAdWillShow` / `OnFullScreenAdClosed` are what
  `AudioManager` uses to duck and restore music around an ad. If a new ad
  entry point is ever added to `LevelPlayAds` that shows a full-screen ad
  without going through `ShowRewarded`/`ShowInterstitial`, it must fire these
  too or the crackle regresses for that path specifically.
- `LevelPlayAds.postAdLoadDelay` (2s) delays the *next* ad load after one
  closes — deliberately, so the load doesn't land on the frame the game
  resumes. Firing it immediately was part of what caused the return-from-ad
  crackle.
- `BootSplash` only shows once per process (`alreadyShown` is static), and
  only from `MainMenuScreen` on the very first menu — returning to the menu
  between levels does not re-trigger it. If you add another entry point that
  can be the *first* screen shown (e.g. a deep link), it won't get the splash
  unless `BootSplash.ShouldShow` is checked there too.

**UI layout and the preview tool** (all of these cost a full debug cycle each)
- **The canvas is matched-height, so WIDTH is the variable.** Its vertical
  extent is always exactly the device's full height (720 units); the width
  shrinks on a 4:3 tablet and grows on a 20:9 phone. Two consequences that have
  each caused a real bug: a fixed-height dialog that fits one device overflows
  *every* device by the same amount (the wallet, ~858 units of content against
  720), and anything centre-anchored at the bottom collides with Start Wave on
  one side and the towers panel on the other once the canvas narrows. The
  bottom-LEFT strip above Start Wave is clear on every aspect ratio; the
  placement bar and the selected-tower panel both live there, and are made
  mutually exclusive because they share it.
- **Unity silently refuses to reparent a live Prefab Instance's child in EDIT
  mode.** `Transform.SetParent` just no-ops — no exception, no warning. This is
  editor-only; there is no "prefab instance" concept at runtime, so Play Mode
  and builds are unaffected. It matters because `UiPreview` instantiates via
  `PrefabUtility.InstantiatePrefab`, so `ScreenTheme.EnsureSafeArea`'s
  reparenting quietly did nothing there and the preview reported the screens as
  fine while the notch fix never took effect. `ShootLive`/`ShootScreen` now
  call `PrefabUtility.UnpackPrefabInstance` first.
- **`ScreenTheme.EnsureSafeArea` must NOT call `SetAsFirstSibling()`.**
  `DisplaySetup`'s edit-time version does, but it is paired with a separate
  `HoistBackgrounds` pass that puts Background back in front afterwards. Without
  that second pass, forcing the safe area to the front puts it BEHIND the
  screen's own background — the whole populated safe area renders invisible
  under its own backdrop. After reparenting, the safe area is already the last
  (frontmost) sibling; leave it alone.
- **`-executeMethod` runs in EDIT mode, so `AddComponent` does not call
  `Awake()`** on a plain MonoBehaviour (only `[ExecuteAlways]` ones). This is
  why `ShootLive` invokes `Start()` by reflection, and why the tutorial preview
  has to invoke `Awake()` the same way — it rendered as literally nothing until
  it did.
- **Converting a rect from a point anchor to a stretch anchor must clear
  `sizeDelta`.** On a point anchor `sizeDelta.x` IS the width; on a stretch it
  is an offset ADDED to the stretched width. Leaving the old value behind gave
  the towers grid 340 + 330 = 670 units of width and one column with a dead gap
  beside it.
- A `ScreenSpaceCamera` canvas with **no render target** falls back to the
  batch-mode default game view size (640x480), not the texture you are about to
  render into. Assign `cam.targetTexture` BEFORE building any UI that measures
  its parent.

**Enemies**
- The **fungi models face local -X, not +Z.** `RotateTurret` aimed +Z at the
  target, which left every tower's mouth pointing 90 degrees away from what it
  was shooting — and since `ProjectileSpawnPoint` is a child sitting at local
  -X, shots appeared to leave the SIDE of the head and swing around it as the
  turret turned. `Tower.modelYawOffset` (90) maps the model's facing axis onto
  its aim direction; the spawn point then rides around to the front by itself.
  The spawn points' own authored rotations (~-90 deg on all six) are dead
  config — `Attack()` overwrites the projectile's `forward` with the direction
  to the target, so only their POSITION ever mattered.
- Several `EnemyConfig` assets **share one prefab**, and `EnemyPool` is keyed by
  prefab. Tinting an enemy must go through a `MaterialPropertyBlock`; writing
  `sharedMaterial` recolours every other type using that prefab.
- `Enemy.Active` is a static registry, maintained in `Initialize`/`Remove`, so
  healers can find neighbours without `FindObjectsOfType` allocating every
  tick. It is cleared via `[RuntimeInitializeOnLoadMethod]` — statics outlive a
  scene change but the GameObjects they point at do not.
- Splitter children are spawned by `EnemySpawner.SpawnSplitChildren`, and their
  health/reward multipliers are derived from the **parent's already wave-scaled
  values**, not from the raw config — otherwise children spawn at level-1
  strength in level 70.
- `BalanceSim` models shields, healers and splitters (`UpdateBehaviours`,
  `MakeSplitChild`). If a new behaviour is added and NOT mirrored there, the
  sim reports it as free difficulty and the whole regression check silently
  stops meaning anything.

**Balance**
- Player power is capped by **buildable cells** (~33 once the path is carved
  out) and saturates by the mid game, so the usable difficulty window is narrow.
  Peak enemy health cannot exceed ~3x before a FULL board (31-34 towers, gold
  unspent) starts losing. This is why the health ramp is concave, not linear —
  a linear ramp either leaves the first fifty levels at 100% health or makes
  everything past difficulty 60 unwinnable. Enemy variety is the way past this
  ceiling, not bigger numbers.
- Difficulty must also ramp WITHIN a level (`FirstWaveHealthShare`). A flat
  per-level multiplier put full-strength enemies in wave 1 against a
  starting-gold-only board, so runs died at wave 3 and never earned the income
  for the rest of the board — 56% of levels became losses.
- **Tower "utilization" is a confounded metric**: it is actual/potential dps, so
  when the player is gold-starved the denominator collapses and utilization
  RISES while the game gets harder. Use **kill depth** (mean fraction of the
  path an enemy covers before dying) — it still discriminates when a level is
  won at 100% health.
- `environmentName` ("Environment 3") is a **persistence key**, baked into every
  level asset and into `HighestCompletedLevel_<name>` / `Stars_<name>_<n>`.
  Renaming it wipes progress. Display names live in `EnvironmentInfo`.

**Art and text**
- The TMP atlases are **static and ASCII-only** (~97 glyphs). No stars, arrows
  or checkmarks — use sprites (`UiSprites`, `StarSprite`). Any new runtime text
  must go through `UiFont` / `UiSkin.Label`.
- Fonts: **Lato** = body, **"Groovy Font"** = display (titles, buttons, values).
  **`MainButton` is "THEREN Trial"** — a trial font. Nothing references it, so
  it is safe to delete, but do not start using it in a shipping build.
- `LevelDecorator` reads `EnvironmentTheme.Current` **while building**. Apply
  the theme first or everything comes out unthemed.
- `EnvironmentTheme.Current` is a **struct**, so before `Apply()` has run every
  colour on it is (0,0,0). `enemyTint` multiplies into every enemy's body
  colour, so read it through `EnvironmentTheme.EnemyTint`, which falls back to
  white — reading `Current.enemyTint` directly turns the whole cast black.
- Several `TowerConfig`/`EnemyConfig` fields are **written into the .asset by
  hand** (the tower `description` strings were). Unity re-serialises in field
  order, so adding a field above an existing one and then hand-editing assets
  is how they end up mismatched — add new fields and let Unity rewrite, or
  insert in the right place.
- Tower/enemy sizes come from `UnitScale`, applied in `TowerFactory` and
  `EnemyPool`. Do not edit the eight tower prefabs.
- Environment card art is generated into `Resources/EnvPreviews`. The inspector's
  `environmentSprite` is deliberately ignored — all seven entries point at one
  grey placeholder. To use custom art, drop a PNG in that folder.
- The main menu's Play button and gear are **real scene sprites**. `MenuLayout`
  only repositions and tints them; never call `UiSkin.StyleButton` on them, it
  replaces the sprite.

**UI scale**
- Canvas reference is **1280x720** with match-height. On a 1080-tall phone that
  is a 1.5x scale factor. To make the whole UI bigger or smaller, change the
  reference in `DisplaySetup.ConfigureScaler` — do not resize elements one by
  one.
- Several scene-authored HUD rects are anchored at x = **+10**, i.e. past the
  right edge. `HudTheme.PullInside()` clamps them.

## 7. Conventions

- Commits are authored as **oktayshakirov**, with **no Claude/Anthropic
  co-author trailer**. Asked and settled 2026-08-31: the user's global
  instructions forbid it and that wins. Existing commits that carry one were
  left alone.
- Work has been committed directly to `main` (solo repo, no PR flow).
- `/iOS/` and `/Android/` build exports are gitignored (~1GB).

## 8. Blender / 3D models — asked 2026-08-31

**There is no Blender connector available in this setup.** The MCP registry
returns nothing for blender/3d/mesh, and no Blender tools are connected. So
Claude cannot currently open, edit or export the models.

What exists in the wider world is a community `blender-mcp` (a Blender add-on
plus a local MCP server) — not an Anthropic product, not in the registry, and
it would have to be installed and trusted manually. If it were connected it
could drive Blender's Python API: create and modify meshes, materials and
scenes, and export glTF/FBX. That is a real capability, not a hypothetical.

**Where it would actually pay off here**, in priority order:
1. **The four new enemy types (Priority 2b)** — they currently reuse three
   existing prefabs with only a tint and a scale to tell them apart. This is
   the biggest visual gap in the game right now.
2. Distinct silhouettes per environment for enemies (already on the user's
   queued list as "change enemy colours per environment" — shape reads better
   than colour at phone size).
3. Tower upgrade tiers, which have no art at all.

**Where it would NOT help:** the environments, props, sky, cliff and clouds are
generated in code (`MeshFactory`, `GroundTextureFactory`) and are not authored
assets — importing hand-made meshes there would fight the whole system.

Worth weighing against just hiring/buying: the game already ships almost no
authored art by design, and a code-driven pipeline has been the project's
working assumption throughout.
