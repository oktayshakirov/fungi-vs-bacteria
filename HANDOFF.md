# Handoff — Fungi vs Bacteria (Unity Tower Defense)

Last updated 2026-08-24. Working tree clean at `8eb3005` on `main`.
This state is bookmarked as branch `handoff/2026-08-visual-overhaul`.

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
| `UiPreview.Render` | HUD + every screen, as PNGs | **no** |
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

**Priority 2 — Enemy variety.** Only Basic / Fast / Armored / Boss exist across
70 levels, and towers have a single upgrade level. Adding shielded / healer /
splitter / swarm types gives the difficulty curve something to scale with
besides raw count. This is the main gap in how *fun* the game is — and it is
now the main lever left, because difficulty is capped by a structural ceiling:
player power is bounded by buildable cells (~33), so peak enemy health cannot
exceed ~3x before a FULL board starts losing.

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

**Priority 4 — Gameplay haptics.** UI presses now have haptics (`Utils/Haptics`,
hooked centrally into `AudioManager.PlaySound`). Gameplay does not: tower shots,
enemy deaths, base hits and wave starts should call `Haptics.Play` directly with
styles chosen so a busy wave does not buzz continuously. Requested by the user
after the 2026-08-19 device test.

**User's queued list**, in their words: change enemy colours per environment;
improve the onboarding UI layout; improve the tile green/red indicators;
improve the sell/upgrade UI for towers; add an info box explaining what each
tower does.

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

- Commits are authored as **oktayshakirov**. Recent commits include a Claude
  co-author trailer; the user has not objected but has not confirmed either —
  worth asking once.
- Work has been committed directly to `main` (solo repo, no PR flow).
- `/iOS/` and `/Android/` build exports are gitignored (~1GB).
