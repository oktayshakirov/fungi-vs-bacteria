# Handoff — Fungi vs Bacteria (Unity Tower Defense)

Last updated 2026-09-11. Working tree clean at `86ac026` on `main`.
An earlier state is bookmarked as branch `handoff/2026-08-visual-overhaul`.

**Start here if you are a new session.** Read this file first; it supersedes the
per-phase notes elsewhere. Section 5 is the work queue, section 6 is every trap
that has actually cost debugging time.

## 0. The immediate next step: a device playtest

Everything in phases 13-18 is **render-verified or sim-verified only**. Nothing
below has been played by a human. The user is going to test next, so if you are
picking this up mid-test, expect findings rather than a clean slate.

What is worth deliberately checking, and what to look for:

| Area | What to check | Why it is uncertain |
|---|---|---|
| Drag-and-drop towers | Drag a card onto the board; also tap-card-then-tap-tile; also drag a card and drop it back on the tray | Never testable here — needs live touch input. The tap flow is unchanged; the drag flow is new |
| Towers panel | Scroll it, collapse it with HIDE TOWERS | Dragging **on a card** starts a tower drag, so the list can only be scrolled from the gaps between cards or the scrollbar. Known trade-off — see if it is annoying in practice |
| Placement bar / sell panel | Arm a tower, then tap a placed one | They share the bottom-left slot and are mutually exclusive by construction. The sell panel is render-verified as of phase 16 (`hud-tower-actions`), but only as a rebuilt stand-in - the real one is authored in `MainGame.unity` and the preview mirrors its structure by hand |
| Haptics | A busy wave, then a base hit | Throttle intervals are first guesses; the whole point is that it must not buzz continuously |
| Tile indicators | Arm a tower on the snow and ash biomes | The old wash was invisible there; the new marker is untested against those grounds |
| Tower upgrades | Tap a placed tower, upgrade it twice, then sell it | New in phase 16. The price is deliberately poor value and may read as a trap; the tier cue is only a size bump and a warm tint, never seen in motion |
| Unlocking a biome | Finish Environment 1 and watch Environment 2 open | New in phase 17. The locked STATES are render-verified, the unlock moment is not |
| Kill effect | Watch a few enemies die | The fragments now arc under gravity instead of flying straight - an old struct-copy bug, fixed while pooling. Visibly different from every previous build |
| Enemy tints | Play one level in env 3, 5 and 6 | Tints are eyeballed. Types must still be distinguishable from each other |
| Variety enemy art | Play env 2-5 and watch a pack arrive; break a Shielded enemy's shield | New in phase 18. The four types now have authored silhouettes, verified in `Builds/EnemyPreview` at three biomes - but only in a still, one enemy at a time. Open questions: does a trait read at phone size inside a pack of thirty, and does the carapace vanishing read as "shield broken" or as a glitch |
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

16. **Tower upgrades** — `Tower.Upgrade()` is real: three tiers, compounding
    +60% damage / +15% fire rate / +10% range, priced at 3.5x the build cost
    then 1.5x again. The Upgrade button is unhidden, support auras and radii
    scale with the tier, sell value returns 70% of everything invested, and
    `BalanceSim` models the whole thing. Measured over five sweeps — see
    Priority 5 below and the long comment in `TowerConfig`.

18. **Real art for the four variety enemy types** — Swarm / Shielded /
    Splitter / Healer were a tint and a scale on three shared bodies; each is
    now composed out of parts of the existing models, so every surface is
    authored art. Splitter and Healer no longer share a prefab. An earlier
    version of this phase bolted on Blender-generated meshes and was rejected
    as looking unnatural — see Priority 2b, which is worth reading before
    adding any new enemy.

17. **Locked progression + trial-font removal** — `LevelProgress.UnlockAll` is
    off for the first time, which exposed a biome-gating hole and an oversized
    padlock; environments now unlock sequentially. The THEREN Trial font is
    gone, and the settings screen that was quietly rendering it now uses Groovy.
    See Priority 6 below.

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
| `UiPreview.Render` | HUD + every screen, as PNGs — including the main menu, the placement bar (`hud-placing`), the selected-tower panel (`hud-tower-actions`) and the tutorial (`screen-tutorial`) | **no** |
| `EnemyArtSetup.BuildVariants` | Rebuilds the four variety enemy prefabs by composing existing model parts | yes |
| `EnemyArtSetup.ReportBaseParts` | Lists every reusable part of the four base models, with vert counts | yes |
| `EnemyPreview.Render` | The whole enemy cast in a row, per biome, plus close-ups | **no** |
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
- **Locked states — DONE and render-verified (phase 17).** `UnlockAll` is now
  `false`. Flip it back to `true` if you need to jump straight to a late level
  while debugging; with it off you have to play there. Turning it off found two
  real bugs, see Priority 6 below. Still unplayed: the actual act of completing
  a biome and watching the next one open.
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
**the fix has not been re-measured on device**. `DeathEffect` is now pooled too
(phase 17) — it was the last unpooled per-kill allocation, and the heaviest:
a GameObject, seven sphere primitives and a Material per kill, all destroyed
0.45s later. **So the whole per-enemy allocation story is now fixed in code and
none of it is measured.** One device run with a busy wave settles all three.

Pooling it also exposed a latent bug worth knowing about: `Fragment` is a
struct, and the old `Update` never wrote the copy back to the list, so the
gravity integration was discarded every frame and fragments flew off in
near-straight lines. They now arc and fall. **That is a visible change to how a
kill looks** — if it reads worse in the hand, delete the `fragments[i] = f;`
line in `Update`; the pooling and the arc are independent changes.

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

**Priority 2b — Give the new types real art. DONE (phase 18), not yet played.**
All four used to reuse the Basic/Fast/Armored prefabs, told apart only by a tint
and a scale. Each is now **composed out of parts of the existing models** by
`EnemyArtSetup` (see section 4). The four base models are untouched.

| Type | Base | Added from | Reads as |
|---|---|---|---|
| Shielded | ArmoredEnemy | one eye-white sphere, translucent | a bubble enclosing the whole body, gone when the shield breaks |
| Splitter | BasicEnemy | two eye-white spheres, amber | daughter cells budding out of opposite sides |
| Swarm | FastEnemy | two Fast bodies | a colony of rods rather than one small enemy |
| Healer | BasicEnemy | Fast's hair mesh, near-white | an aura reaching out past the body towards neighbours |

**This is the second attempt, and the first one is the lesson.** It bolted on
four small meshes generated in Blender - a carapace, a spore crown, budding
lobes, a cilia fringe. They were 28KB, under 560 triangles, readable in every
biome, and they were **rejected on sight**: script-made geometry beside detailed
organic models reads as damage, not design. The verdict was "unnatural and
distorted". Composing from existing parts cannot have that problem, because
every surface in the game is authored art.

Two parts carry all four compositions, and the choice between them matters:
- **Fast's body** (552 verts) wherever a bacterial ROD is wanted. It is not
  usable as a sphere - squashing it round exposes its facets, and the first
  shield bubble built that way read as a lump of faceted glass.
- **An eye white** (481 verts) wherever a SPHERE is wanted. It is the only
  proper sphere in the project's art, and at a flat colour nothing about it
  reads as an eye.

Four things that each took a render to see, all of which generalise:
- **An added part must clear the host body's silhouette.** Basic's spike field
  reaches its full bounding radius, so a part centred anywhere inside it is
  swallowed whatever its colour. Both the daughter cells and the healer's aura
  had to be pushed past the body's own radius.
- **Parts must be spread around the body, not clustered on one side.** An enemy
  turns to follow the path, so a pair of cells both on -X is invisible for half
  of every corner. The splitter's two cells sit roughly opposite.
- **A part must contrast with the body, not harmonise.** Purple cells on a
  purple body and a green aura on a green body both vanished. Parts opt out of
  `EnvironmentTheme.EnemyTint` entirely: the body carries the biome, the part is
  type identity.
- **Translucency wants LOW smoothness.** At high smoothness the shield bubble
  read as polished glass. And URP transparency is not one property - surface
  mode, blend factors, depth write, render queue and a shader keyword must all
  agree, or the material stays opaque at runtime and it looks like the alpha is
  being ignored. `EnemyArtSetup.MakeTransparent` sets all of them.

Also fixed on the way: Shielded and Armored were the same saturated blue spiky
sphere. Shielded's body is now dark slate, which is what keeps the type
identifiable **with its bubble popped** - rendered as
`close-ShieldedEnemy-shielddown.png`.

`BalanceSim` output is **byte-identical** across both attempts, which is the
check that this changed nothing but appearance.

What still needs a human: whether a part reads at phone size in a pack of
thirty, whether the bubble popping reads as "shield broken", and whether the
translucent bubble costs anything in overdraw on a real device.

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
- *Sell/upgrade UI*: rebuilt. The Upgrade button was hidden here because
  `Tower.Upgrade()` only logged; **phase 16 made it real and it is now visible.**
  Upgrades were deliberately held back from this pass — they are a new power
  lever and `BalanceSim` did not model them, so shipping them alongside a
  balance retune would have made the playtest unreadable.
- *Tile green/red indicators*: generated rounded-square markers with a gutter,
  blocked tiles deliberately much fainter than available ones.
- *Onboarding layout*: card, step counter, pips, Got It button, over a lighter
  scrim, centred on the BOARD so it never covers the towers panel.
- *Enemy colours per environment*: `EnvironmentTheme.Palette.enemyTint`.

**Priority 5 — Tower upgrades. DONE, NOT yet in real play.** Three tiers per
tower, all driven from `TowerConfig` (`DamageAt` / `UpgradeCostFrom` /
`SellValueAt`), read through `Tower.Level`, mirrored in `BalanceSim`.

The finding that matters, because it is counter-intuitive: **the obvious price
broke the game.** At an upgrade cost of 1x the build cost, all 70 levels went
TRIVIAL at 100% health and the proxy's median tower count FELL from 24 to 15.5 —
it stopped filling the board. Upgrading a high-coverage cell beats building on
the next-best FREE cell, and the coverage gap between the best and the marginal
cell on a 10x5 board is wide enough to swamp any sane value-per-gold ratio. An
upgrade has to be priced as bad value (9.75x the build cost to max a tower, for
~3.4x its output) because the only thing it buys that a second tower does not is
"no cell required".

At the shipped 3.5x: the first upgrade is bought at difficulty 48, and **d1-47
is bit-identical to the pre-upgrade run.** Six verdicts move, all at d60+, all
on levels that had 1,200-3,400 gold sitting dead:

| | Before | After |
|---|---|---|
| E6L10 (d60) | HARD | TRIVIAL |
| E7L01 (d61) | HARD | EASY |
| E7L06 (d66) | **LOSS** | FAIR |
| E7L07 (d67) | HARD | EASY |
| E7L09 (d69) | HARD | FAIR |
| E7L10 (d70) | **LOSS** | HARD |

So two of the three known-unwinnable levels become winnable, and **E7L03 (d63)
stays a loss** — it had the smallest surplus, so upgrades could not rescue it.
It is now the single hardest level in the game and the one to watch.

Past ~3.5x the price stops mattering (5.0x barely differed), because the proxy
dumps its surplus either way. Do not grind this further against the model — the
handoff's standing warning applies doubly here, since the real player enters
RICHER than the sim assumes and upgrades will therefore do MORE in practice.

**What still needs a human:**
- Whether the Upgrade button reads as worth pressing. The price is deliberately
  poor value and a player doing arithmetic may correctly conclude it is a trap.
  If it feels like one, the honest fix is fewer/cheaper tiers, not a stealth
  buff — and re-run the sim, because 1x demonstrably breaks the board.
- **Whether the prices read as absurd.** The render made this concrete: an Ice
  Tower costs 150, and at Lv 2 the panel offers `SELL +472` next to
  `UPGRADE 788`. The numbers are all correct and the sim says the price is
  right, but "788 to upgrade a 150-gold tower" is a hard sell. A `Next: Damage
  26  Range 7.3  1.3/s` line was added under the stat line specifically so the
  money is legible; whether that is enough is a question only a human can
  answer.
- The tier cue. There is no upgrade art, so a tier is an 8%-per-step size bump
  plus a warmer body tint multiplied into the model's own colour. Never seen in
  motion; it may be too subtle at phone size, or it may make an upgraded tower
  overlap its neighbours.
- `Lv 2` in the panel title is text because the TMP atlases are ASCII-only —
  there are no pip or star glyphs available.

**Traps this created, all live in the code as comments:**
- `TowerBuffs` now reads `Tower.Range` / `EffectiveDamageBoost`, NOT the config.
  Reading the config is how an upgraded support tower silently does nothing.
- `Tower.Upgrade` re-arms `targeting.Initialize(Range)`. Without it the panel
  advertises a longer reach than the tower will actually shoot.
- The tier tint is multiplied INTO the material colour via a
  `MaterialPropertyBlock`, and the base colour is re-read from `sharedMaterial`
  every time — reading it back from the block would compound the tint to white
  by level 3, and writing `sharedMaterial` would re-tier every other tower on
  that prefab.
- `Tower.baseScale` is captured in `Initialize`, not `Awake`: `TowerFactory`
  applies `UnitScale.Tower` after `Instantiate` but before `Initialize`, so
  `Awake` would bank the prefab scale and shrink every tower on its first
  upgrade.
- Sell value is 70% of TOTAL INVESTED, not of the build cost. Reverting that
  makes upgrading-then-selling a hidden loss.

**Priority 6 — Locked progression. DONE (phase 17), the unlock MOMENT unplayed.**
`UnlockAll` had been `true` since the beginning, so nothing behind it had ever
run. Turning it off found two bugs that had been sitting there the whole time:

- **Every biome was open on a fresh install.** Environment locking read a
  hand-authored `isLocked` flag on `EnvironmentsScreen`'s inspector list, and all
  seven entries were set to `false` — so with `UnlockAll` off a new player saw
  all seven biomes unlocked but could only play level 1 of each, including
  Environment 7 at difficulty 61. That flag is gone; `IsEnvironmentUnlocked`
  now gates a biome on the PREVIOUS one being finished, which matches the
  strictly sequential difficulty (env 1 is d1-10, env 7 is d61-70).
  **The inspector list's ORDER is now load-bearing** — each entry gates the
  next, so reordering it reorders the progression.
- **The padlock overflowed its card.** `EnvironmentCard.BuildLock` set
  `sizeDelta` to 92x92, but the prefab authors that object at `localScale 3`,
  so it rendered at 276 and hung out past the bottom of the card. Nothing had
  ever drawn a locked card, so nothing had ever caught it.

Both are verified in `screen-environments` / `screen-levels`. What a human still
has to do is finish Environment 1 and watch Environment 2 actually open — the
write path (`MarkLevelCompleted`) is exercised, the transition is not.

**Before release:** `LevelProgress.UnlockAll = false` (**done**); the
**THEREN Trial** font is **deleted** (done — see below); analytics + crash
reporting; real app icon and store art; replace the synthesized SFX. See
`DISTRIBUTION.md`.

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
- **"The body" is no longer just the first MeshRenderer.** Trait geometry is
  parented under the same root, so `GetComponentInChildren<MeshRenderer>()` can
  return a carapace or a spore crown. That would tint the trait and leave the
  body its authored colour, and would park the health bar at the trait's height.
  Use **`Enemy.FindBodyRenderer`**, which skips anything under an `EnemyTrait`;
  `Enemy`, `EnemyHealthBar` and `EnemySpawner` all go through it. Do not
  reintroduce the bare call, and do not rely on sibling order instead.
- **Composed part placement is expressed as FRACTIONS of the base body's
  measured bounds, never world units.** The four bases differ wildly (BasicEnemy
  spans 6.4 model units at prefab scale 0.2; ArmoredEnemy spans 3.3 at scale 1),
  so `EnemyArtSetup` measures each body in the ROOT's local space and sizes
  parts from that. Two consequences: the width metric is the **mean** of the two
  horizontal extents, not the max (FastEnemy is 7.07 long against 2.27 wide, and
  the max sized Swarm's parts against the body's length), and after changing a
  base model you must re-run `EnemyArtSetup.BuildVariants`.
- **`scaleShare` is the part's FINAL proportion, not a correction to the source
  mesh's aspect.** The builder already divides by the borrowed mesh's own
  extents, so a share of 1 means "as wide as the body" whatever mesh is used.
  Pre-compensating for the source's shape on top of that is how the first
  shield bubble ended up entirely inside its own body.
- **A borrowed mesh is not centred on its own pivot.** The builder subtracts
  `mesh.bounds.center` so `offsetShare` positions the part's CENTRE. Fast's hair
  is the case that proves it: its pivot sits well outside the tendrils, so
  placing it by transform alone lands it far from where the numbers say.
- **Parts must come from the CHEAP meshes.** Basic's body is 287k verts and
  Armored's is 171k, against 552 for Fast's and 481 for an eye. A late wave puts
  30+ enemies on screen; duplicating a Basic body adds a quarter of a million
  verts per enemy. Run `EnemyArtSetup.ReportBaseParts` before choosing one.
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
  `MainButton` **was** "THEREN Trial", a trial font. An earlier version of this
  file claimed nothing referenced it and it was safe to delete — **that was
  wrong on both counts.** Five assets referenced it (`MainGame.unity` and the
  pause / game-over / settings / victory screens), and on the settings screen it
  was actually RENDERING: the three option labels were drawn in it. The other
  four only held the reference, because runtime code restyles their text through
  `UiSkin` before it is ever seen — which is presumably how the wrong conclusion
  was reached. All five now point at `Title` (Groovy) and the asset is deleted.
  Proof it was harmless everywhere else: after the swap, `screen-pause`,
  `screen-gameover` and `screen-victory` re-rendered byte-identical, and only
  `screen-settings` changed.
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

## 8. Blender / 3D models — resolved 2026-09-11

**The headless route is the one that matters, and one connector is not needed
for it.** Blender is driven from the shell, which is strictly better here: the
geometry is a checked-in Python script rather than a binary nobody can diff.

Blender's own MCP server is **also connected now** (verified 2026-09-12: scene
read, `bpy` execution and window screenshots all work against the live app). It
is a two-part chain and both parts are needed:

1. The **server**, a Claude desktop extension (`ant.dir.gh.blender.blender-mcp`).
   Installing it requires restarting the desktop app before its tools appear.
2. A **Blender add-on** (`bl_ext.lab_blender_org.mcp`) that it reaches over
   `localhost:9876`. It requires **Blender 5.1+**, which is the only reason
   5.2.1 was installed. Install it by dragging it onto a running Blender
   **twice** from `blender.org/lab/mcp-server` - the first drop adds the Lab
   repository, the second installs the add-on - then enable it and start its
   server.

Two dead ends worth not repeating: `https://lab.blender.org/` is a **web page,
not a repository URL**, so adding it as a remote repository lists nothing; and
on Blender 4.x the add-on is filtered out of the list entirely with no
explanation, because of the 5.1 requirement.

**It only works while Blender is open**, and Blender's own page warns that the
add-on executes generated code with no guards against data loss or
exfiltration, recommending a VM. What it buys is live scene inspection. The
headless pipeline above needs none of it, so nothing in this repo depends on
it being connected.

**Nothing in the repo uses this pipeline any more.** It was built to generate
the four enemy traits, those were rejected for looking unnatural next to the
authored models (Priority 2b), and `Tools/Blender/enemy_traits.py` was deleted
with them. What follows is kept because the pipeline itself worked and the two
export traps below are not obvious - reach for it for something that is not a
creature, and not for enemy art, where composing existing parts is the approach
that survived review.

`blender` on PATH is the Homebrew cask's wrapper, currently **5.2.1 LTS**;
**4.3.2** is kept beside it at `/Applications/Blender 4.3.app` because it is
what the deleted trait meshes were generated with. That script's output was
verified **byte-identical under both** once the version comment the exporter
writes is ignored, so the pipeline is not pinned to a version - but re-check
that after the next major bump rather than assuming it.

Update Blender with `brew upgrade --cask blender`. There is **no in-app
updater**; Blender only self-updates extensions, never the program.

The project imports models as **OBJ** (there is no glTF package in
`Packages/manifest.json`), so the script exports OBJ to match the four existing
enemy models. The glTF and FBX exporters are also present if that ever changes.

**Two traps in the export itself**, both of which produced silently wrong
geometry on the first run:
- The OBJ exporter's axis arguments describe the CONVERSION, not the target. The
  traits are authored directly in Unity's convention, so the conversion must be
  the identity - `forward_axis='Y', up_axis='Z'`, which are *Blender's* axes.
  Passing Unity's `-Z`/`Y` tipped every trait onto its side.
- `normalize()` re-centres X/Z, so a deliberate fore/aft offset authored in
  Blender does **not** survive. Per-type placement belongs on the Unity side
  (`EnemyArtSetup`), where a render is one command away.

**Where this pipeline could pay off next**, in priority order:
1. Distinct silhouettes per environment, so a biome changes shape and not only
   colour. Do this by composing existing parts, the way `EnemyArtSetup` does,
   rather than by generating new geometry.
2. Tower upgrade tiers, which still have no art at all: a tier is an 8% size
   bump plus a warm tint (Priority 5).
3. Replacing the four base bodies. Not obviously worth it - they are good, and
   they are the family look.

**Where it would NOT help:** the environments, props, sky, cliff and clouds are
generated in code (`MeshFactory`, `GroundTextureFactory`) and are not authored
assets - importing hand-made meshes there would fight the whole system.
