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

`CameraPreview` **cannot** capture the HUD — a ScreenSpaceOverlay canvas draws
straight to the backbuffer and never lands in a RenderTexture. That is why
`UiPreview` exists; it rebuilds the UI on a ScreenSpaceCamera canvas and runs
the real skin/theme code.

## 5. What to do next

**Priority 1 — Balance the 70 levels.** `LevelGenerator.GenerateWaves` scales
enemy **count only**, never strength, and is unbounded: Env7 Level10's final
wave spawns ~73 enemies at 0.6s intervals. **40 of the 70 levels have never
been played by anyone.** The plan agreed with the user is a headless simulation
harness that plays every level against a modelled tower loadout and reports
winnable / trivial / broken, then retunes the generator against that data
rather than by hand.

**Priority 2 — Enemy variety.** Only Basic / Fast / Armored / Boss exist across
70 levels, and towers have a single upgrade level. Adding shielded / healer /
splitter / swarm types gives the difficulty curve something to scale with
besides raw count. This is the main gap in how *fun* the game is.

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
- Unity always names the exported Xcode project, target and scheme
  `Unity-iPhone`. `IosPostProcess` renames the **scheme** on export, which is
  what Xcode shows in its toolbar. The `.xcodeproj` folder name is left alone —
  renaming it breaks append builds. The shipped app is unaffected either way:
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
