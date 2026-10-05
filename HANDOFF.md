# Handoff — Fungi vs Bacteria (Unity Tower Defense)

Last updated 2026-10-05 (phase 33, the battle debrief, wave scouting, the
Archer branches, mycelium links, boss phases and Nunito Sans - built by Codex,
committed as `4c3d4df`; then test ads off and the privacy policy linked).
Phase 32 was combat allocations, enemy mesh cost and the three tower roles.
Committed and pushed on `main`.

**`LevelProgress.UnlockAll` is compile-gated now** - `true` in the editor and in
any **Development Build**, `false` in a release build. There is nothing left to
remember to turn off; just make sure the store build is not a Development Build.
An earlier state is bookmarked as branch `handoff/2026-08-visual-overhaul`.

**Start here if you are a new session.** Read this file first; it supersedes the
per-phase notes elsewhere. Section 5 is the work queue, section 6 is every trap
that has actually cost debugging time, section 7 has the house rules.

## 0. Where things stand, and the immediate next steps

**Status 2026-10-05: waiting on the owner's first real-device playtest.** They
are testing on a physical device and will come back with findings. Do not tune
balance, economy or performance further until those arrive - every open item
below is "built, never played". Feed the findings into section 5.

**Latest: phase 33 — the game explains itself.** Built by Codex, compile- and
render-checked, **none of it played by a human**:
- **Battle debrief** (`BattleReport`, `BattleDebrief`). The victory and game-over
  screens share one safe-area layout showing the run: which enemies escaped and
  how much health each cost, ranked, a one-line lesson and the next star goal.
  The report covers the whole scene including continues and stores nothing.
- **Wave scouting** (`WaveIntel`, `WavePreview`). A persistent forecast of the
  next wave's composition in the bottom-left info slot (tower and booster panels
  take precedence), plus a scout button in the timer's top-HUD slot. The same
  button starts the first wave and sends a prepared wave early; only `Update`
  owns the timer, so a manual start can no longer double-send a wave.
- **Archer specialization** (`ArcherSpecialization`). A one-time per-tower
  choice once upgraded: Flurry (x0.8 damage, x1.4 rate, x0.85 reach) or Longshot
  (x1.35 damage, x0.75 rate, x1.25 reach). Never mutates the shared config.
- **Mycelium links** (`MyceliumRoots`, `TowerBuffs`). Support is an
  event-driven graph now: adjacent attackers get a small extra bonus on top of
  ordinary aura coverage, and selecting a tower draws its real connections on
  the ground. No Update, colliders or particles.
- **Boss phases** (`BossCue`, `Enemy.BossStage` Stable/Fortified/Rushing) - only
  on the one opted-in encounter, Environment 1 Level 04, through the new
  `BossEncounterEnemy` asset (`Tools/Levels/Apply Phase 4 Encounter`). Every
  other boss keeps its old behaviour until this one has been played.
- **Combat pulses** (`CombatPulse`). Small ground cues with a hard live budget
  and reserved slots, so ordinary hits cannot crowd out shield breaks, split
  births and boss warnings.
- **Authored levels.** `CampaignChallenges` keeps ten hand-authored openings and
  `BiomeChallenges` one transition challenge per biome (each Level01), both
  re-applied in place after regeneration (`Tools/Levels/Apply ...`).
- **Typography.** Nunito Sans (OFL, licence in `Assets/Fonts/NunitoSans/`) is
  the UI font via `UiFont`, as TMP assets in `Resources/Fonts`. New
  splitter daughter-cell and healer-plus meshes, and rendered enemy portraits in
  `Resources/EnemyPortraits`.
- New editor checks/previews for all of the above (`*Checks`, `*PlayChecks`,
  `*Preview` in `Assets/Editor`).

**Store prep done the same day:**
- **Ad debug off.** `verboseLogging` is `0` on the `Ads` component in
  `MainMenu.unity` (it was `1`, which also turned on adapter debug and logged
  the advertising ID); `launchTestSuiteOnInit` was already off. **Still on the
  owner, on the dashboards:** test mode off in LevelPlay and AdMob. Keep the
  test device registered in both lists so playtesting does not tap real ads.
- **Privacy policy linked**: `https://oktayshakirov.com/privacy-policy/fungi-vs-bacteria`,
  `SettingScreen.PrivacyPolicyUrl`, opened by a **PRIVACY POLICY** corner button
  bottom-right of Settings, shown to everyone (the UMP **PRIVACY OPTIONS** button
  stays bottom-left, EEA/UK only).
- Android keystore and the RevenueCat project exist; the RevenueCat public SDK
  keys are already in `IapSetup.cs`.

**What still stands between this and a store submission** (`DISTRIBUTION.md`
has the full checklist): the device playtest and whatever it turns up; the four
coin-pack products created in both stores and put in a RevenueCat offering, then
a sandbox purchase on a device; the iOS Paid Applications agreement; the Play
data-safety form, Apple privacy label and target-audience/content-rating answers;
store text and screenshots; ad test mode off on the dashboards. After launch:
mark the app live on LevelPlay and re-enable Google bidding.

### Phase 32 (previous)

**Latest: phase 32 — the combat code stopped allocating, the two heavy enemy
bodies stopped costing five million triangles, and three towers that were the
same tower became three different towers.**

**Allocation, in the three places a busy wave felt it:**
- **Targeting no longer searches the scene.** `TowerTargeting` called
  `GameObject.FindGameObjectsWithTag("Enemy")` every frame it had no target -
  one array allocated per idle tower per frame, which is worst exactly when the
  board is full of towers and the wave has not arrived yet. It reads
  `Enemy.Active` now, and a tower that finds nothing waits 0.10s before looking
  again, staggered off its instance id so thirty towers do not all search on
  the same frame.
- **Projectiles and impacts are pooled** (`CombatPool`). Every shot used to be
  an `Instantiate` and every impact another, both `Destroy`d - at the Shock
  tower's two shots a second, per tower.
- **`Physics.OverlapSphere` became `OverlapSphereNonAlloc`** in the splash path,
  with the buffer grown and kept rather than reallocated.

**The enemy bodies are 20x and 31x cheaper.** Basic was **161,280 triangles**
and Armored **245,760**, against 4,078 for the Boss and 6,361 for Fast. A late
wave holds 30+ enemies, so those two models alone were on the order of **five
million triangles** against an environment of 56,000. Both are now **8,000**,
decimated by `Tools/Blender/optimize_enemies.py`; the originals are untouched
in `Assets/Meshes/Enemies` and the prefabs point at new mesh assets in
`Assets/Models/OptimizedEnemies`. It propagates to Shielded, Splitter, Healer
and Swarm, which are prefab variants of Basic and Armored.

8,000 is **measured, not guessed**: at 4,000 the trumpet mouths and the eye
whites facet badly enough to read as broken art, and 12,000 is indistinguishable
from 8,000 even in `EnemyPreview`'s close-up, which is far nearer than the play
camera ever gets. Before and after are indistinguishable in the lineup too. The
decimated Armored is actually CLEANER than the original, whose 171k-vertex
surface shows a visible ripple under the key light.

**Three towers were the same tower.** Ice, Shock and Poison were all
splash-plus-slow with different numbers, so Shock was a strictly better Ice at a
higher price and Poison was an Ice with a smaller radius. Shock's card said it
"chains" and it did not; Poison's never mentioned poison and `TowerConfig` had
carried a `//TODO: Add Damage over time effect (PoisonTower)` the whole time.

| Tower | Role now |
|---|---|
| Ice | splash + slow — the only slower, unchanged |
| Shock | **chain** — hits 1 + 3, each hop within 3 units of the LAST enemy, -25% damage per hop |
| Poison | **damage over time** — 15/s for 4s, refreshes rather than stacks, **ignores armour** |
| Inferno | splash burst — unchanged |

Poison ignoring armour is the point of it: damage arriving in many small pieces
is exactly what a flat percentage reduction punishes hardest, so without that
Poison was a worse Inferno against the one enemy type it should answer. Shields
still absorb it, so Shielded stays a counter to chip damage.

**`ChainArc` is new**: a pooled `LineRenderer` bolt, additive and unlit, 0.11s.
It is the only thing that tells the player chaining happened at all - the splash
it replaced was invisible, which is why the card's "chains" was never believed.
Pooled for a harder reason than `DeathEffect`: a chain of three fires three per
SHOT, and Shock is the fastest-firing tower in the game.

**`CombatCheck` is new, and it is the first real assertion suite in the project**
(`-executeMethod CombatCheck.RunBatch`, exits non-zero on failure). Nine checks
over the REAL `Projectile` and `Enemy` - chain walks the line, chain never hits
the same enemy twice, chain stops at its reach, splash hits each enemy once,
splash counts an enemy with child colliders once, poison ignores armour, poison
expires, poison does not survive a pooled enemy's reuse, and an old shot does
not hit a recycled enemy. All of that is logic that fails SILENTLY: it does not
show in a render and it does not fail a compile.

**It found two things on its first run:**
- **The spawn-version guard was in the wrong place.** It lived in
  `Projectile.Update`, so any path that resolved an impact without going through
  the movement code would damage a recycled enemy. It is now in
  `ResolveImpact`, where the guarantee is about.
- **A poison dose always delivered one point less than it promised.** Ten
  tenth-second ticks of 10/s accumulate to 9.999... in float, so the last whole
  point was never reached and was thrown away with the carry. The leftover
  fraction is now rounded and spent when the dose ends. Poison's duration also
  moved from an absolute `Time.time` deadline to seconds counted down by the
  same `dt` that meters the dose, so the whole mechanic runs off one clock -
  which is what makes it checkable at all.

**Two things in the visual pass that was sitting uncommitted were half-done,
and the render loop found both:**

- **The Shielded enemy had lost its shield cue entirely.** The phase replaced
  the translucent orb with an opaque plated carapace baked into the body texture
  (right call - a transparent sphere over a detailed body read as a lump of
  glass), but `EnemySurfaceSetup` DESTROYS the orb's `EnemyTrait`, and that
  trait was the only thing `Enemy.SyncShieldCue` had to act on. So shield-up and
  shield-down looked identical, and the 4-second regen delay became invisible.
  `ShieldSkin` is new: a material swap on the body, plated while the absorb pool
  holds and bare once it is empty. The bare skin is the same hue but **dimmer**,
  and the brightness lives in the TEXTURE rather than in `_BaseColor`, because
  `Enemy` writes the type colour and biome tint into a MaterialPropertyBlock on
  the renderer and would erase any difference authored into the colour. First
  attempt had shield-DOWN reading brighter than shield-up, which is backwards.
- **The victory screen's payout chip was mislaid.** The coin was anchored to the
  left edge of a 300-wide pill while the number was centred in what was left of
  it, so they sat about a hundred units apart with the coin floating off on its
  own. Both are now one centred group in the same `HorizontalLayoutGroup` shape
  `HudTheme` uses for the HUD stat chips. The pill keeps its 300 width - that is
  deliberate, it matches the buttons under it.

**`BalanceSim.AuditTowers` is new, and it exists because of a trap.** Changing
Shock and Poison left all 70 verdicts AND the entire CSV byte-identical. That is
not evidence the change was harmless: the player proxy is greedy on
value-per-gold, so it buys Archer and Inferno and **had never built either tower
in the first place**. The audit plays eight levels across the range with exactly
ONE tower type on the board, so each tower is measured against the same enemies
rather than against the proxy's shopping preferences:

| Tower | cost | won | health left | kill depth |
|---|---|---|---|---|
| Archer | 100 | 6/8 | 73% | 48% |
| Ice | 150 | 3/8 | 7% | 66% |
| Inferno | 175 | 5/8 | 45% | 58% |
| Sniper | 175 | 7/8 | 81% | 48% |
| Poison | 200 | 6/8 | 74% | 51% |
| Shock | 225 | 8/8 | 83% | 56% |

Read it as "can this tower hold a level alone, and how does that compare",
**never as the difficulty curve** - a one-type board is not how the game is
played, which is also why Ice's 7% is not a bug. Before the change Shock sat at
8/8 and **100% health**, i.e. it simply trivialised every level it was given;
Poison's numbers were tuned against this table to land between Inferno and
Shock, where its cost puts it. The 70-level curve is untouched.

**Previously: phase 31 — a polish pass on the nest, all four from one round of
feedback on phase 30:**

- **The mouth is lighter** - `NestMaw` went from 0.36 to 0.52 grey (and
  `NestMawLit` with it). It reads as a stone basin now rather than a dark hole.
- **The mist has three variants**, picked at random per spawn: *billow* (a
  round mass straight up), *gout* (six big fast puffs, one hard belch) and
  *creep* (eleven small slow ones spilling sideways). Everything was already
  jittered per puff, but the SHAPE of the burst was fixed, and a late wave
  pushing twenty enemies through one mouth read as the same puff stamped
  twenty times. Puffs are still built once, at the largest variant's count;
  a variant wanting fewer parks the tail at zero size, so choosing one never
  allocates.
- **The pools were leaving gaps** that showed the road underneath. Every ring
  here is lumpy or broken on purpose, so its inner radius is a RANGE, not a
  number - the dunes' inner terrace wanders between 1.23 and 1.33, the tundra's
  slabs between 1.34 and 1.75 - and a pool sized to the nominal inner edge
  leaks wherever the ring happens to wander outward. Six of the seven pools are
  now sized to the widest the ring's inner edge gets, plus a margin, and tuck
  under the rest. Only the meadow already had the margin. The rule is written
  above `nest_pool()`.
- **`NestPulse` is new**: the nest heaves up and in when something climbs out
  of it, the counterpart to `BaseFlinch` at the other end of the path. It is
  the opposite squash - something pushing out from underneath rather than
  landing on top - and it runs on SCALED time where BaseFlinch runs on
  unscaled, because it is tied to a spawn and the spawns come faster at 2x
  and 3x.

**The trap in that last one: the nest was inside the static batch.**
`LevelDecorator` built the portal BEFORE `StaticBatchingUtility.Combine` and
the base after it, because until now only the base moved - and a batched object
cannot be moved or reparented, so a NestPulse on it would have silently done
nothing at all. Both landmarks are now built after the merge. Anything new that
MOVES has to go in that block.

**Previously: phase 30 — the nest's mouth went neutral, and enemies now climb out
of it.** Two changes that only make sense together.

**The mouth is a neutral grey on every biome** (`EnvironmentTheme.NestMaw`),
where it was a hot magenta ooze. The ooze was the loudest thing on the board
and it was carrying the whole "something lives down there" idea by itself,
because nothing ever moved. The ring around it - rim, shards, crust - still
takes the biome, so each nest keeps its identity; only the pool, the bubbles
and the secondary glow went neutral.

**`SpawnEffect` is new**: a burst of mist out of the mouth whenever the spawner
places an enemy, paired with an emergence ramp on the enemy itself
(`Enemy.PlayEmergence`), so a wave reads as something climbing out of the
ground instead of models blinking into existence on top of a ring.
- Pooled, exactly like `DeathEffect` and for the same reason - it fires once
  per enemy and a late wave spawns dozens.
- **Unlit**, not Lit. A lit sphere takes the key light and a terminator across
  its surface, which is what makes a ball of vapour read as a grey pebble; the
  first pass used Lit and the puffs looked like gravel piled in the mouth.
- Tinted 40% toward the biome's own `fogColor`, so it is ash-orange on the
  volcano and bruised mauve on the marsh rather than the same stark white
  everywhere, which read as a flashbulb on the dark biomes.
- Purely visual: the enemy is on the path, at full health and targetable from
  the frame it spawns. Targeting is distance-based (`TowerTargeting` measures
  `transform.position`), so the scale ramp cannot affect acquisition.
- **Splitter children deliberately do NOT get it.** They are already introduced
  by the parent's death burst, and swelling them as well read as that burst
  stuttering.

**Two things this cost:**
- **The emergence curve has to be a smoothstep, not an ease-out.** An ease-out
  is fastest at the start, so the enemy was already a third of its size in the
  first frame - fully visible before the mist had built to anything, which is
  the one thing the effect exists to prevent.
- **The mist has to thicken fast and then thin, not decay from full.** Starting
  at its most opaque puts peak alpha at the one moment the puffs are still too
  small to hide anything, and by the time they had grown over the enemy they
  were already half gone. Peak is now a quarter of the way in.

**`CameraPreview.RenderSpawn` is new and is the only render in this file that
can show an effect made of nothing but motion** - every other one is a single
frame at t=0, where a spawn effect has by definition not happened yet.
`SpawnEffect.Step` and `Enemy.EmergeScaleAt` are public so it can drive both by
hand, the same way `WaddleScale` is public so the cast can be posed mid-walk.
`SPAWN_ENVS=35` narrows it. **Still never seen in motion on a device** - six
stills across half a second is as close as a batch render gets.

**Previously: phase 29 — seven bases and seven nests, one of each per
environment.** Until now one mushroom house stood at the end of every path and
one crater at the start of it, both recoloured from the palette, so a biome
changed hue and never shape. Both ends are now per-environment: fourteen
authored models out of `Tools/Blender/structures.py`, selected by the new
`Palette.baseModel` and `Palette.nestModel`.

| Env | Model | Reads as |
|---|---|---|
| 1 meadow | `BaseMeadow` | the original red toadstool cottage, **unchanged** |
| 2 dunes | `BaseDunes` | a tall parasol on a thin stem, canvas porch over the door |
| 3 marsh | `BaseMarsh` | a bell leaning back off the path, glowing pods hung round its rim |
| 4 tundra | `BaseTundra` | a squat snow-laden dome with a stone chimney and icicles |
| 5 ember | `BaseEmber` | an angular basalt cap on a columnar stem, lava in its fissures |
| 6 bloom | `BaseBloom` | three fused pods with glowing pores and tendrils |
| 7 blossom | `BaseBlossom` | a two-tier pagoda with upturned eaves and blossom |

| Env | Nest | Reads as |
|---|---|---|
| 1 meadow | `NestMeadow` | the original lumpy crater, **unchanged** |
| 2 dunes | `NestDunes` | a terraced sand berm with cracked crust plates |
| 3 marsh | `NestMarsh` | a lumpy bog vent with a thicket of cilia and ooze running over its lip |
| 4 tundra | `NestTundra` | heaved slabs of pack ice with shards driven up between them |
| 5 ember | `NestEmber` | a collar of basalt columns with lava in the gaps |
| 6 bloom | `NestBloom` | bulbous egg sacs crowding the mouth, with pores and tendrils |
| 7 blossom | `NestBlossom` | a carrion flower - drooping petals round a ring of upright filaments |

The nests have a hard constraint the bases do not: **`EnemySpawner` puts every
enemy at `pathPoints[0]`, which is the nest's exact centre**, so a nest may not
build up or close over in the middle. All seven keep a ground-level pool and a
clear mouth of radius 1.3 or more, and do their distinguishing work in the ring
around it. The first pass built four of them (dunes, ember, bloom, blossom) on
a solid base disc, which simply capped the pool - all four came back as a lid
with no mouth at all. `nest_ring()` exists so that cannot happen again: every
surround is an annulus.

Their part vocabulary is `Rim, Pool, Spikes, Bubbles` plus `Crust` (the
biome-matched plates, slabs or petals) and `Glow`, skinned by
`LevelDecorator.NestSkinFor` - which, unlike the bases', starts from one shared
skin each biome overrides only where it needs to.

**The ooze is the same magenta on every biome, deliberately**, mirroring the
bases' always-warm windows: one colour at each end of the path that never takes
the environment, so home and not-home read the same however far the rest of the
board moves. Everything around it - rim, shards, crust - takes the biome.

The bases share one part vocabulary - Plinth, Stem, Cap, Spots, Door, Windows, plus
the new **Trim** (that model's structural accent: awning, icicles, tendrils,
eaves) and **Glow** (its emissive biome accent) - so a single table,
`LevelDecorator.SkinFor`, skins all seven. It is keyed on the MODEL and not on
the environment, because what a part means belongs to the model: the marsh's
Trim is reeds and the tundra's is icicles, and they would not take each other's
colour. Footprints and heights stay in one band (radius <= 2.6, height
3.1-4.7), so camera framing, `BaseFlinch` and `LandmarkClearance` needed no
per-biome numbers. 520-1,210 triangles each.

`MotherMushroom.obj` and `BacteriaNest.obj` are both gone; `BaseMeadow.obj`
and `NestMeadow.obj` are the same geometry under the new naming, and nothing
referenced either old guid. `CameraPreview.RenderLandmarks` is new - a close-up
of each biome's base AND nest, each with its own framing, since the nest is a
flat ring that wants a steeper look than a four-metre house - and the
environment cards were regenerated, as a biome look change requires.

**NOT played on a device.** Every judgement here is a batch render.

**Four things this cost, worth not re-learning:**

- **A window under a wide cap is hidden by that cap, and raising it makes it
  worse.** The play camera looks down about 30 degrees, so a sight line climbs
  at `tan(30) = 0.577` per unit outward - far faster than any cap's underside -
  which means a LOW window escapes past the rim before the ceiling catches it
  and a high one does not. The tundra's windows were buried inside the stem on
  the first pass, moved up under the cap on the second, and only read when they
  came back DOWN beside the door. The rule and its inequality are written at
  the top of `structures.py`.
- **The meadow base has the same flaw and always has.** Both of its stem
  windows sit at 1.85 and 2.05 under a cap whose underside is flat at 2.62, so
  they have never been visible from the play camera. Left alone deliberately -
  it is the reference the other six are read against, and moving it changes how
  Environment 1 looks - but it is a free improvement whenever that is wanted.
- **Re-running the generator rewrites every `.obj` even where nothing
  changed.** The OBJ exporter's normal de-duplication is not order-stable, so
  the `vn` block comes out permuted between runs while every `v` and `f` line
  is identical. Six base models showed as modified in this phase's second
  commit for that reason alone. To tell a real change from the churn, compare
  the geometry rather than the file:
  `diff <(git show HEAD:<path> | grep -E '^(v|o|usemtl) ') <(grep -E '^(v|o|usemtl) ' <path>)`.
- **A ring round a ground-level mouth has to be an annulus, not a disc.**
  See the nest constraint above; it cost a whole render pass across four
  biomes, and nothing about the models looked wrong until they were lit.
- **Emission above 1 BLEACHES a colour instead of deepening it.** The bloom
  threshold is just above white (see phase 18), so the volcanic fissures at
  emission 1.15 came back as pale yellow streaks rather than lava, and the
  marsh's pods as pale lime. Both are now below 1 (0.70 and 0.65), against the
  instinct that the glowing thing should be the brightest thing on the board.

**Previously: phase 28 — booster countdown dials, the always-on booster bar with
BUY, the locked-card refusal, the level-halo geometry and the HUD top corners.
`UiPreview.Render` was re-run and every shot checked at 20:9, 16:9 and 4:3;
the halo is even, the locked cards render exactly as they did before, and
nothing in the reworked HUD corners or the five-wide booster bar collides. NOT played on a device: the wobble, the locked thud and the dial
actually EMPTYING are all motion, and a batch render is one still frame at
t=0 - a Device Simulator pass is what is still owed, and buying a booster
mid-level is the one flow nothing here has exercised end to end. What changed:**

- **Frost Wave never froze the towers** — only `Enemy.ApplyFreeze` was ever
  called. Its store copy said "Freezes everything solid", which is what read as
  a warning; it now says every bacterium, and that the towers keep firing.
- **The three timed boosters carry a countdown dial** on the top edge of their
  HUD button: a radial-filled circle that empties clockwise, no numerals.
  `BoosterEffects.ActiveFraction` is the single source for it, and Frost now
  records a `frostEndsAt` purely so the HUD has something to read (nothing
  else consults it). The dial object is only built for the timed three and is
  hidden whenever nothing is running. `UiPreview` gained a
  **`hud-boosters-timers`** shot for it (and a `hud-boosters-buy` shot for the
  owns-nothing case), which fires the three through the real
  `BoosterEffects.Use` BEFORE building the bar - `Build` ends in `Refresh()`,
  and that is the only thing that puts the dials on screen, because `Update`
  never runs in batch mode.
- **Locked biomes and locked levels answer a tap** instead of swallowing it.
  Both cards stay `interactable` and route to a refusal: `UiShake.Nudge` (a
  decaying sideways wobble, unscaled time), a `UiShake.Punch` on the padlock,
  and `AudioManager.PlayLocked` — the ButtonClick clip at pitch 0.55 on a
  second AudioSource, so no new clip has to be assigned in the inspector and
  the shared `sfxSource` is never detuned. `UiShake.cs` is a NEW file, so it
  had to be added to `Assembly-CSharp.csproj` by hand.
  **The trap this walked into:** an interactable Button tints its target
  graphic with `normalColor` rather than `disabledColor`, so simply flipping
  `interactable` brightened every locked environment card. `EnvironmentsScreen`
  now pins the resting colour states to the disabled tint. Level tiles need no
  such fix - their target graphic is the fully transparent root image.
- **The booster bar shows all five boosters at all times.** It was built from
  the OWNED ones only, so the row changed shape between levels and a player who
  had spent everything saw no bar at all - and so had no way back in. An
  unowned booster is drawn dimmed with no count, and its panel offers
  **BUY <price>** where USE would be: it buys through the existing
  `BoosterInventory.Buy` and stays open (now showing USE), and with too few
  coins it shakes and opens Get Coins instead. Two knock-on fixes this forced:
  `MinButtonSize` is 48, and the row now clamps against the TOWERS RAIL rather
  than the screen edge - five buttons never fit the 4:3 strip, and the old
  clamp parked the last one underneath Start Wave. Growing left, under the
  transient tower info panel, is the better failure.
- **The store moved to the top right, beside pause,** at exactly pause's size
  (`HudTheme.PlaceLeftOfPause`, which measures the real button through the new
  `HudTheme.PauseRect`). It was a labelled plate in the left stack, which was
  three deep and crowding the booster bar.
- **The wave badge is centred on the top edge**, which is where the store
  button's old corner neighbour used to push it out of. On a 4:3 canvas it
  would then collide with the gold chip, so `HudKeepClear` pushes it right by
  just enough, every frame. It has to be live rather than a nudge at load:
  the gold chip's width follows the number inside it. **The trap:** the stats
  panel's own rect stops ~95 units short of the chips it contains (its layout
  group overflows), so measuring the panel alone found no collision - it walks
  the children too. It also exposes `Apply()`, because batch mode never calls
  LateUpdate and the preview shots would otherwise show the uncorrected
  position.
- **The store icon is a shopfront** (`UiSprites.Store`), on all three store
  buttons: the menu pill, the pause screen and the HUD corner. The shopping
  bag it replaced built its handle out of the same arc as the padlock's
  shackle, so at HUD size the store button read as "locked". `UiSprites.Bag`
  is gone rather than aliased - nothing should be able to draw it again. The
  wallet header carries the same mark beside its title (a plus on Get Coins,
  matching the menu pills); icon and title ride a centred layout row now,
  because anything that positions the glyph from a PREDICTED text width drifts
  as soon as TMP's autosizing shrinks the title on a narrower canvas.
- **The "next level" halo is centred again.** It was sized off the face alone
  and nudged up 26 units, which left ~46 units of glow below the tile against
  ~18 above. It now measures the face PLUS the edge plate and bleeds an equal
  `HaloBleed` (32) on all four sides.

**Phase 27 — menu and store/coins UI pass. COMPILES ONLY. The previews
have NOT been re-rendered since these changes and nothing here has been played
on a device: `UiPreview.Render` (`screen-wallet`, `screen-getcoins`,
`screen-getcoins-packs`), `CameraPreview`/`hud-boosters` and a Device Simulator
pass at both a 19.5:9 phone and a 4:3 tablet are the first thing the next
session should do. Phase 26 (store launch prep: IAP catalog, economy retune,
new icon) was render-checked before it.**

**Phase 26 — store launch prep** (render-checked). Where the release stands:

- **Store listings exist** on App Store Connect and Google Play. The user has
  created the four consumable coin packs: `fungivsbacteria.coins.2500` /
  `.10000` / `.20000` / `.50000` at EUR 0.99 / 3.99 / 6.99 / 14.99. The 8,000
  pack became 10,000; the game pays 11,000 for it so its rate beats the 0.99
  pack (see `IapCatalog`).
- **Real-money Remove Ads removed.** It is coins-only now (18,000). The
  `fungivsbacteria.noads` product, its `no_ads` entitlement, the 5,000 gift and
  Restore Purchases are all gone from code and docs.
- **Economy retuned, and three exploits closed.** Details are in section 6
  under "The economy". In short: the level top-up is a repaid loan (it was
  infinite coins), a continue bought with an ad no longer pays coins, and the ad
  payout is a code constant (150 coins, 8 a day) instead of the LevelPlay
  dashboard value. New: a **Shield** booster and a **Survival Kit** bundle.
- **New app icon** (blue mushroom vs red virus). It overwrote
  `Assets/Sprites/Icons/AppIcon.png` in place, so its GUID and every Player
  Settings reference are unchanged; `Tools -> App Icon -> Apply` was re-run.
  The gitignored `iOS/` Xcode export had its icon sizes regenerated too.
- **iOS deployment target is 15.0** (Apple requires it from spring 2027).
- **Android signing is set up**: upload keystore outside the repo (path in
  ProjectSettings, passwords are not stored). EDM4U's custom Gradle templates
  in `Assets/Plugins/Android/` are now REQUIRED — Player Settings points at
  them. Do not delete them.
- **Store copy** (subtitle, short and full description) was written for both
  stores with no counts, because maps, biomes, towers and enemies will be added
  later. It was not saved to the repo.
- **RevenueCat public SDK keys are set** (2026-10-01) in
  `Assets/Editor/IapSetup.cs`, and `Tools -> IAP -> Apply Keys` has been run,
  so they are written into the `Ads` object in `MainMenu.unity` too. Nothing
  else belongs in the repo: `Iap.FetchProducts` asks for the four product IDs
  directly, so no RevenueCat Offering or entitlement has to be configured for
  purchases to resolve. What is still owed is DASHBOARD side, and without it
  RevenueCat cannot validate a real purchase: the App Store Connect in-app
  purchase key (.p8) plus the app-specific shared secret, and the Google Play
  service-account JSON. The RevenueCat **secret** key must never enter this
  project.
- **Still open:** store coins are also in-level tower gold, so any coin pack
  makes early levels easier to brute-force. Only splitting the currencies
  fixes that. Deferred as a design decision for the user.

**Phase 27 — menu and store/coins UI pass, from screenshots. COMPILES ONLY,
not rendered and not played.** Same weaker guarantee as phase 25 below: the
work was done from screenshots the user sent, traced through the source. What
changed:

- **Store and Get Coins split into two screens.** `WalletScreen` builds both
  from one class (`WalletScreen.Mode`), since they share the card, header,
  balance chip and scroll body:
  - `OpenStore(parent)` — "STORE", **no tabs**: the Survival Kit card, the
    booster rows, then EXTRAS (Remove Ads, which is priced in coins).
  - `OpenCoins(parent, tab)` — "GET COINS", **two tabs**: FREE COINS (daily
    streak + rewarded ad) and COIN PACKS (the real-money products). Free is
    the default tab.
  This replaced a single three-tab dialog (BOOSTERS / COINS / FREE) that put
  spending and earning in the same place. The coin chip is now a passive
  readout — its "+" is gone — and the menu carries two labelled pills under it
  instead: gold GET COINS (with the "+" glyph) and a green STORE. Every
  in-game entry point (pause, HUD button) opens the store.
  Tab button height was fixed by pinning min/preferred/flexible height
  explicitly on both the row and its buttons — leaving only `preferredHeight`
  let a `LayoutElement` vs `HorizontalLayoutGroup` priority ambiguity blow the
  row up to near-card height (see section 6 if this regresses). Watch Ad /
  streak claim buttons shrunk (96->64, 74->56) so the FREE tab's rows fit
  without scrolling on most screens.
- **Booster bar moved to the bottom-centre strip and made bigger.** It was a
  column down the left edge, which the STORE HUD button landed on top of, and
  it had shrunk its buttons to 46 units to share that edge with four other
  controls. It is now a horizontal row (92 units, min 62) centred in the band
  between the tower info panel's right edge and the towers rail
  (`HudTheme.RightRailSpan`, published when the rail is measured since its
  width comes from the tower grid's cell size at runtime). Tapping a booster
  opens `BoosterPanel` in the bottom-LEFT corner, which the row is clear of.
- **New players start with one of each booster.**
  `BoosterInventory.GrantStarterBoosters` runs once per install
  (`Booster_StarterGranted`, BeforeSceneLoad) so the booster bar has something
  in it on the first level instead of five permanently greyed buttons. Running
  out is what sends the player to the store; the key is written before the
  grant so a kill mid-grant cannot hand out a second kit.
- **One watch-ad button, not two.** The FREE COINS tab used to stack a streak
  claim button and a plain watch-ad button, both reading "WATCH AD" with
  different numbers. There is now a single button whose payout moves:
  `WalletScreen.TodayAdReward` = the streak rung (200/250/350/500/1000) while
  today's check-in is unclaimed, `RewardedGate.CoinsPerAd` (150) after. The
  streak claim is still exempt from the gate's cooldown and daily cap, so the
  cap/cooldown states only apply once it is claimed. The streak card is now a
  readout (pips + one caption line) and its pips rebuild on claim, which they
  never did before. Fixed an off-by-one in the claim message (`Day 0 claimed`).
- **Streak ladder confirmed:** unbroken days step 1->5, day 5 wraps back to
  day 1 (the cycle restarts rather than paying 1,000/day forever); any gap, and
  a clock moved backwards, reset to day 1. Resolved on read from the last-claim
  date, so nothing has to run while the app is closed.
- **Menu logo fitted to the space it has.** The vs-battle art was a 90x90 rect
  at `localScale 5` - it rendered at 450 units while every layout calculation
  that read the rect saw 90, which is why it kept colliding with PLAY.
  `MenuLayout.FitLogo` now measures the band between the title and the PLAY
  plate and sizes the art to it; `MenuLogoFit` re-runs that whenever the band
  changes (SafeArea resolving, rotation, Device Simulator).
- **"NEXT: <biome> - LEVEL n" moved out of the PLAY button** to a dimmed strip
  along the bottom of the screen with a play triangle in front of it. The
  button's inset and height are now paired (`64 + 116 = 180`) so its TOP edge
  is exactly where it was - raising the inset alone pushed the plate up into
  the characters.

**Previous: phase 25 — HUD and store fixes from screenshots, NOT played.** The
user sent screenshots of the running game (not renders) across several rounds
of feedback and this session made the fixes from them blind — no Unity GUI,
no device, no `UiPreview` render loop, just reading the screenshot, tracing the
bug through the source, and fixing it. That is a materially weaker guarantee
than every other phase in this file, which was at minimum render-verified.
**Everything below needs a real look before it counts as done.** What changed:

- **`LoadingScreen` NullReferenceException on first level load — FIXED.**
  `SceneController.LoadScene` called `Prepare()` (which sets `.outlineWidth` on
  a TMP label) BEFORE `activeLoadingScreen.SetActive(true)`. A freshly
  instantiated, still-inactive prefab has not run `Awake`/`OnEnable` on its TMP
  text yet, and TMP throws inside `SetOutlineThickness` if styled that early.
  Fixed by activating before styling (`SceneController.cs`). This was a hard
  crash, not a polish item, and blocked ANY level load until fixed.
- **Towers rail cards visually spilling past the panel's rounded corners —
  root cause found, fixed twice.** First pass added a `RectMask2D` inset from
  the frame's rounded edge (`HudTheme.StyleTowersPanel`). That was masking a
  bigger bug: `HudTheme` clamps each card's cell HEIGHT to 132 to fit more rows
  in the rail, but `TowerButton`'s prefab positions the coin-icon/price row at
  a fixed offset from the card's CENTRE, calibrated for the card's original
  160-unit height. `GridLayoutGroup` resizes a centre-pivoted card
  symmetrically, so that fixed centre offset put the price row's bottom edge
  outside the shrunk card's real bottom edge — the actual cause of "coins
  outside the tower cards" across three rounds of feedback. Fixed by
  re-anchoring the cost row to the card's BOTTOM edge instead
  (`TowerButton.StyleCostRow`), which is correct at any cell height. Also
  unified the "can afford" (was 15x15) and "can't afford" (was 30x30) icon
  sizes to one 22x22 in a `HorizontalLayoutGroup` next to the price, since they
  used to be sized independently and swapped size when affordability changed.
- **Duplicate pause button.** The scene authors its own `PauseIcon` child image
  on the pause button, sized for the button's ORIGINAL (larger) dimensions.
  `HudTheme.CompactPause` shrinks the button and adds a NEW `PauseGlyph` icon
  on top, but never hid the old one — so the stale, oversized icon rendered
  bleeding out over the wave readout beside it. Now explicitly disabled.
- **More store entry points.** A STORE button now exists in three places it
  didn't before: under the coin chip on the main menu, in the in-level HUD
  (stacked under the speed/camera buttons), and in the pause screen (under
  Resume). All three, plus the coin chip, use a new procedural shopping-bag
  icon (`UiSprites.Bag`) tinted **gold**, matching the "VS" in the menu title —
  was blue (`UiSkin.Accent`) through the first two rounds, changed on request.
- **Menu title recoloured.** FUNGI and BACTERIA are now both `UiSkin.Primary`
  green (were red/violet, matching the character art, then briefly
  green/blue) with VS in gold — the ask was specifically "make both green".
  The PLAY button's "NEXT: biome - level n" caption is now white with a dark
  outline (was a near-black green that was hard to read on the green button).

**This session ran a Unity batch-mode compile check** (`Unity -batchmode
-nographics -quit -projectPath .`, full asset import, no `-executeMethod`) as
the only verification available without a GUI or a device — **it passed, zero
`error CS` lines, exit code 0**, so nothing above is a compile-breaker. That is
the ONLY thing it confirms — nothing about whether any of this actually looks
or plays right. No `UiPreview` renders were regenerated for any of this
phase's changes; the store and HUD preview shots on disk predate all of it.
**Next session: open the editor, play a level, open the store from all four
entry points, and re-run `UiPreview.Render` / `hud-towers` / `screen-wallet`
so there is a real render to check future changes against.**

**Before that: phase 23, a visual polish pass before the first device test**
(see the phase list in section 3 for the full account). Maps, the environment
and level screens, the store, the victory screen, the tower icons, the main
menu title and the base/portal landmarks all changed. Every change is
render-verified; **none of it has been played on a device.** Add these to the
device-run table below:

| Area | What to check |
|---|---|
| Path | The road is now ~2.8 units wide with a dark rim (was a 1-unit line). Does it still read as "unbuildable" at the cell edges, and does drag-placement next to it feel right? |
| Base + nest | Each biome has its OWN Blender-authored base at the path end and nest at the path start (phase 29). Does each base read as the thing you are defending at play distance, and are its lit windows visible in the hand? Does a wave spawning out of the nest's mouth look right in motion - the mist and the enemy swelling inside it (phase 30)? That is the one thing a still frame cannot really answer, and `RenderSpawn`'s six stills are only an approximation of it. Watch especially whether the mist is too thick at 2x and 3x speed, where the same 0.6s covers much more of a wave. The base flinches when an enemy gets through - never seen in motion |
| Environment screen | Card art is now a render of each biome's level 1; the strip opens on the furthest unlocked biome and the next card peeks in |
| Store | Rebuilt layout: balance + close cross in the title bar, section headers, two-line buy buttons with the bundle saving |
| Victory | Stars and coin payout used to overlap the buttons - check it on a real win |
| Tower rail | Icons are transparent renders of the models now (were grey-backed photos) |
| Main menu | New "FUNGI vs BACTERIA" title between the coin chip and the gear |
| HUD top row | Pause is now a compact icon button, 12 down off the top edge, with the wave plate snapped beside it |
| Modals | Pause / Game Over / Victory titles sit on accent plates; pause and game over show "BIOME - LEVEL n - WAVE x/y" |
| Victory | REPLAY appears under 3 stars; earned stars pop in one by one (only visible in motion) |
| Settings | Rows are plates with icons and animated on/off switches (`ToggleSwitch`); version bottom-right |
| Loading | Names the level, sits over the biome art, shows a gameplay tip; the % now reaches 100 |
| Main menu | PLAY carries "NEXT: BIOME - LEVEL n" |
| Level screen | Star total chip under the title |
| Tower panel | Sell refund is on the button ("SELL +472"); the rail's tower icons are bigger and behind the labels |

The last session (phases 19-22) was driven by the user playtesting on a device
and reporting back in rounds. Everything from phase 19 on was **render-verified**
through `UiPreview`; the user then confirmed the UI pass by eye on device
("generally better", "the game looks good"). What that session **added but no
human has touched yet** is the whole monetisation layer: the store, in-app
purchases, boosters and coin-bought Remove Ads.

### Blocked on the user (accounts - cannot be done from here)

In-app purchases are fully coded and **cannot work until these exist**. Until
then the store correctly shows "Coin packs are unavailable right now" and hides
the money half of Remove Ads. Full checklist: DISTRIBUTION.md -> In-app purchases.

1. Create the four consumable coin packs in App Store Connect and Play Console
   with the exact IDs in `IapCatalog` (2.5k/10k/20k/50k).
2. RevenueCat project with an offering holding all four.
3. Public SDK keys into `Assets/Editor/IapSetup.cs`, then Tools -> IAP -> Apply
   Keys. **Never the secret key.**
4. iOS: sign the Paid Applications agreement, or no product ever loads.
5. Pick the store TARGET AUDIENCE before the content rating - a children's
   audience forces child-directed ad requests the code does not make
   (DISTRIBUTION.md -> Target audience). 13+ matches the current code.

### Worth checking on the next device run

| Area | What to check | Why it is uncertain |
|---|---|---|
| Sell / upgrade panel | Tap a placed tower, upgrade twice, sell | **It was unreachable until phase 20** (towers were on the wrong physics layer), so it has never been used by anyone. The upgrade price is deliberately poor value and may read as a trap |
| Boosters | Buy one of each in the store, then use them in a level | Nothing has been FIRED in a running level. Check: the bomb clears the board and pays no gold, Frost Wave stops everything for 5s and they resume, Overclock visibly speeds towers for 15s, Mend never exceeds starting health, and the per-wave limits free up on the next wave |
| Booster bar | Own all five, then open a tower's panel, on BOTH a 19.5:9 phone and a 4:3 tablet | The row is centred in the strip between the info panel and the towers rail. On a 4:3 canvas five buttons at the 62 minimum overflow that strip and get clamped to the screen instead, reaching under the info panel's corner |
| Starter boosters | Wipe PlayerPrefs, launch, start a level | New installs are granted one of each (`Booster_StarterGranted`). Never seen: every test device already has the key set after one launch |
| Get Coins / one ad button | Open GET COINS on a fresh day, watch, then watch again | The first ad of the day pays the streak rung and ignores the cooldown/cap; the second pays 150 and is gated. Check the button's number changes and the pips fill |
| Remove Ads with coins | Reach 15,000 coins, buy it, finish 3+ levels | No interstitial should appear. The coin unlock and the paid entitlement are stored separately on purpose (section 6) |
| Store scroll | Open STORE and scroll to the bottom | Survival Kit, five booster rows, then EXTRAS / Remove Ads - one unbroken scroll with no tabs, and the row density has never been looked at |
| Privacy Options | Settings, from inside the EEA/UK | Only shown where UMP says it is required, so it never appears in the editor or outside those regions. Use a UMP debug geography to see it |
| Towers rail | Collapse and expand it on a notched phone | The rail and Start Wave are hoisted OUT of the SafeArea to use the right-hand strip; `RailInset` (18) is the only thing clearing the rounded corner |
| App icon | Home screen after a fresh install | Set through PlayerSettings, never seen on a device |
| Frame time, bloom, balance | See Priority 3 and the enemy-art notes below | Still unmeasured from before this session |

### What to build next

Nothing is queued in code. The obvious candidates, in the order they would pay
off:
1. **Whatever the next device run finds** - the last three rounds each found
   real bugs (an unreachable sell panel, a scrollbar handle longer than its
   track, a missing TMP default font).
2. **BalanceSim does not model boosters.** Every balance number in section 5
   describes a player who owns none. Worth modelling at least the bomb before
   retuning anything.
3. **Store tabs.** The plan was BOOSTERS / COINS / FREE tabs; it shipped as one
   long scrolling dialog. Fine for now, worth doing if the dialog gets longer.
4. **Starter Pack** ($1.99, once: coins + boosters) - planned, not built.

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
  procedurally generated. Progression is LOCKED (`LevelProgress.UnlockAll` is
  false since phase 17): biomes open in order, levels open one at a time.
- Monetisation: LevelPlay ads (see ADS.md), a coin wallet, RevenueCat in-app
  purchases, coin-bought boosters and Remove Ads (money or 15,000 coins).
- Environment art, props, sky, cliff and clouds are **generated in code**
  (`MeshFactory`, `GroundTextureFactory`) — the only authored environment meshes
  are the base and spawn portal (`Resources/Structures`, from Blender).
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

18. **Enemy art and motion** — Swarm / Shielded / Splitter / Healer were a tint
    and a scale on three shared bodies; each is now composed out of parts of
    the existing models, so every surface is authored art. Splitter and Healer
    no longer share a prefab. Every enemy also **moves** now, which none of
    them did before, and **bloom is on**, which it never was. An earlier
    version of this phase bolted on Blender-generated meshes and was rejected
    as looking unnatural — read Priority 2b before adding any new enemy or
    touching an emissive material.

17. **Locked progression + trial-font removal** — `LevelProgress.UnlockAll` is
    off for the first time, which exposed a biome-gating hole and an oversized
    padlock; environments now unlock sequentially. The THEREN Trial font is
    gone, and the settings screen that was quietly rendering it now uses Groovy.
    See Priority 6 below.

19. **Adaptive UI pass (from the second UI playtest)** — every layout that was
    sized against the 1280x720 reference is now sized against the canvas the
    device actually produces. The canvas is match-HEIGHT, so it is always 720
    units tall and its WIDTH is the aspect ratio: 960 on a 4:3 tablet, 1280 at
    16:9, ~1600 on a 20:9 phone. What changed:
    - `ScreenTheme.LayoutWidth` is the one place that answers "how wide is this
      screen". It derives the width from the canvas rect's ASPECT rather than
      reading its width — see Priority 7 below, this is a trap worth knowing.
    - The environment/level header is one set of constants
      (`ScreenTheme.HeaderInset/BackButtonWidth/HeaderSideReserve`) and the
      title chip is clamped to the gap between the two corner buttons, so
      "SELECT ENVIRONMENT" no longer runs under BACK.
    - Level tiles read 1-5 / 6-10 left to right (they filled columns before),
      locked tiles now show their NUMBER with the padlock demoted to a corner
      badge, and `LevelCard.SetTileSize` shrinks the tile so one row of five
      fits a 4:3 screen.
    - The towers panel is a single-column rail with a collapse control that has
      two shapes: a bare chevron tucked against its LEFT edge while open, and a
      labelled "TOWERS" pill sitting in the rail's own top slot while closed
      (`hud-towers-collapsed` shoots the second one). A bare chevron left
      floating over empty board had nothing to belong to and nothing saying
      what it did.
      The rail itself has a PERMANENT scrollbar, and Start Wave directly underneath at
      the rail's width. That gave the bottom-left corner back to the two info
      panels. The rail and Start Wave are the one part of the HUD hoisted OUT
      of the SafeArea: in landscape the notch is on the other side, so the
      inset was reserving 40-odd units of empty board down the right that
      nothing else would ever use. `RailInset` clears the rounded corner.
    - `TowerInfoPanel` is new and owns the chrome for BOTH the placement bar
      and the selected-tower panel, which had drifted into two different sizes
      in two different positions. Same corner, same width, same rows: name +
      coin value, description, stats, actions.
    - `MenuLayout`: Play dropped to the bottom, the art placed just above it
      (`LogoOffset`, verified against a render - the rect is 90 units tall and
      the artwork inside it is several times that, so the rect says nothing
      about where the art's edges land), corner inset tightened to 16.

20. **The sell/upgrade panel was unreachable, and always had been.** Every tower
    prefab is authored on layer 0, but `HUDManager.selectableLayerMask` is the
    "Tower" layer (7) and nothing ever moved them, so the selection raycast
    could not hit a placed tower - tapping one did nothing. Render-verified but
    never tapped, exactly as section 0 predicted. `Tower.MakeSelectable()` now
    sets the layer on the tower and ALL its colliders at Initialize (in code, so
    a ninth tower cannot be added with the same hole; on the colliders, because
    several sit on a nested model prefab and raycasts filter on the collider's
    own object). `TrySelectTower` also walks up with `GetComponentInParent`
    rather than requiring the collider and the `Tower` component on one object.
    - `UiPreview` now renders at the GAME's 1280x720 reference (it was building
      every shot on a 1920x1080 canvas, i.e. a screen 50% wider in units than
      any device ships) and adds `hud-4x3`, `screen-levels-4x3`,
      `screen-levels-20x9` and `screen-environments-4x3`.

21. **In-app purchases (phase 2 of the store plan)** — RevenueCat
    (`com.revenuecat.purchases-unity` 8.9.0) behind an `Iap` facade shaped like
    the existing `Ads` one, so screens never touch SDK types. Four consumable
    coin packs (2.5k/8k/20k/50k) and a non-consumable Remove Ads carrying the
    `no_ads` entitlement, which gates interstitials in `Ads.OnLevelEnded` and
    deliberately does NOT gate rewarded ads. `IapGrant` pays out from
    CustomerInfo on all four paths (purchase, restore, startup fetch, SDK push),
    deduped by store transaction id in PlayerPrefs, and logs an error rather
    than silently skipping a product missing from `IapCatalog`. The wallet
    screen is now the STORE: balance, packs, Remove Ads, then the free sources,
    then Restore. Keys go in `Assets/Editor/IapSetup.cs` (public SDK keys only)
    via Tools -> IAP -> Apply Keys; products and the RevenueCat dashboard are
    the user's to create - see DISTRIBUTION.md. **Nothing here has been through
    a real purchase**: the editor has no store, so the store screen is
    render-verified against injected prices (`Iap.SetPreviewPrices`) and the
    grant/entitlement paths have never run.

    Two bugs fixed on the way, both in the wallet screen and both pre-existing:
    its scroll content had the default sizeDelta of (100,100) on a
    stretch-anchored axis, so every row was 100 units too wide and clipped 50
    units at each end; and the card sized itself from `parent.rect`, which is
    raw pixels before the first layout pass (see section 6).

22. **Boosters (phase 1 of the store plan)** — four consumables bought with
    COINS and used in a level: Spore Bomb (clears the board, 600), Frost Wave
    (freezes everything 5s, 250), Overclock (+50% fire rate for 15s, 350) and
    Mend (+25 base health, 300). Owned counts live in `BoosterInventory`
    (PlayerPrefs, one key per kind); `BoosterCatalog` is the single table of
    price/limit/copy/icon; `BoosterEffects` owns the rules and the effects.
    Limits are per LEVEL for the two that can undo a mistake (bomb, mend) and
    per WAVE for the two that buy time - the wallet has no cap, so without a
    limit a rich player just presses the bomb every wave.
    - **The bomb deliberately pays no gold** (`Enemy.Vaporize`, which also
      skips splitting). A bomb that paid kill rewards would earn back its own
      price on a dense late wave and become the cheapest way to farm coins.
    - The HUD bar is a row along the BOTTOM of the screen, centred between the
      tower info panel and the towers rail (see phase 27). It was a left-edge
      column until the STORE HUD button landed on top of it.
    - A booster is armed, not fired, by tapping it: the bar opens the shared
      `TowerInfoPanel` plate with what it does and a USE button, so a stray tap
      cannot spend a 600-coin bomb. It is mutually exclusive with the placement
      and selected-tower panels, the same way those two already were.
    - Prices, limits and magnitudes are all first guesses, like the ad numbers.
      **`BalanceSim` does not model boosters at all**, so the balance figures in
      Priority 1 describe a player who owns none.
    Render-verified (`hud-boosters`, `screen-wallet`); **no booster has been
    fired in a running level.**

    (Later changed: the money Remove Ads product and Restore were removed and
    the 8k pack became 10k at $3.69 - see "Remove Ads is coins-only" below.)

    Remove Ads can also be bought for 15,000 COINS (`NoAds.CoinPrice`), beside
    the money button. `NoAds` stores the two routes separately (`Entitled` from
    RevenueCat, `BoughtWithCoins` local) and `Active` is either - they must not
    share a flag, because every CustomerInfo re-applies the entitlement and a
    coin unlocker has none, so a shared flag switched the ads back on at the
    next refresh. The 5,000-coin gift and the Restore message key on `Entitled`
    only; keyed on `Active`, paying 15,000 coins refunded 5,000 of them.

23. **Pre-test polish pass** — one round over everything the player sees,
    each item found in a render, fixed, and re-rendered.
    - **Maps.** The path was a 1-unit LineRenderer on 5-unit cells - thinner
      than the enemies on it, and invisible on the wetland's sand. It is now
      `cellSize * 0.56` wide with a darker, wider rim line under it
      (`PathVisualizer` builds a `PathEdge` child; `EnvironmentTheme.ApplyPath`
      colours both via `SetColors`). The wetland and tundra path colours were
      changed for contrast. Toxic Marsh got its own `MARSH` ground (bog moss,
      acid-green pools) instead of sharing the night biome's flat navy `DARK`.
      The border's neon shards are now 5 clusters in the biome's own accent,
      kept out of the front band, instead of 12 lone shards in a fixed
      magenta/cyan/amber mix on every biome.
    - **Base and spawn portal** are authored meshes now
      (`Tools/Blender/structures.py` -> `Resources/Structures/*.obj`): seven
      bases (cap, spots, stem, door, lit windows, plinth, trim, glow) and seven
      nests (rim, pool, spikes, bubbles, crust, glow), one of each per
      environment - see section 0. Parts are coloured per biome by submesh
      material name in `LevelDecorator.Landmark`. Props
      keep `LandmarkClearance` away from both (a boulder used to sit on the
      portal). The house is built AFTER static batching so `BaseFlinch` can
      squash it on `GameManager.OnBaseDamaged`.
    - **Environment screen.** Card art is a render of each biome's level 1
      (`CameraPreview.RenderEnvironmentCards` -> `Resources/EnvPreviews`), not
      the palette gradient, which now only backs the LEVEL screen
      (`EnvironmentInfo.BackdropArt`) where a busy render fought the tiles.
      Cards are 340 wide so the next one always peeks in (three exactly filled
      a 16:9 screen, so nothing said there were seven), and the strip opens on
      the furthest unlocked biome.
    - **Level screen.** The ringed tile is the first unlocked level not yet
      COMPLETED (was: first with zero stars, which could ring a beaten level),
      and it no longer shows three grey stars under it.
    - **Store.** Balance and a close cross moved into the title bar (the
      balance had a whole row; BACK floated outside the card). Section headers
      for Boosters / Coin Packs / Remove Ads / Free Coins. Booster rows got
      icon discs and two-line buy buttons ("x1 / 600", "x3 -15% / 1,530").
      Pack bonuses are lime pills. The small print is caption-sized and sized
      by its text.
    - **Victory screen was broken on every win:** stars hung over the button
      card and "+N COINS" was drawn on top of NEXT LEVEL. The preview never
      called `Initialize`, so no render ever showed it; it does now.
    - **Every star in the game was upside down** (StarSprite used -90 degrees in
      a bottom-up texture) and stair-stepped (64 px, no AA). Fixed: +90, 160 px,
      4x4 supersampled.
    - **Tower icons** were photos on opaque grey squares, which sat in the dark
      rail as light boxes. `TowerIconRender.Render` re-renders all eight from
      the prefabs onto transparency, into the same PNGs (no GUID changes).
    - Main menu title ("FUNGI vs BACTERIA", `MenuLayout.ApplyTitle`); HUD top
      row inset off the top edge (`HudTheme.InsetFromTop`); tutorial GOT IT is
      dark-on-lime like every other primary button.

24. **UI pass: menus, modals and HUD** — all render-verified, none played.
    - `UiPreview` now draws every HUD and modal shot over a real board render
      (`BoardBackdrop`, reading `CameraPreview.RenderBoards` output) instead
      of flat green; run `RenderBoards` first on a fresh checkout.
    - Modals: `ScreenTheme.Apply` takes a `titleAccent` and puts the title on
      the same `TitleChip` plate the list screens use; `ScreenTheme.Subtitle` +
      `RunSummary` give the pause and game-over screens a context line (reads
      `HUDManager.CurrentWave/TotalWaves`). List screens use `PlainTitle`, since
      they add their own chip - calling the chipping `Title` there would stack
      two plates.
    - Victory: REPLAY (cloned from Main Menu) below three stars; the card is
      centred at 0.33 on this screen only so the stars and payout clear it;
      stars pop in (`VictoryScreen.PopIn`, unscaled time, light haptic each).
    - Settings rows: **the prefab authors them at localScale 4**, which is why
      everything sized inside them came out four times too big on the first
      try - `SpaceSettingsRows` resets the scale first. Icons (`UiSprites.Music/
      Speaker/Vibrate`), `ToggleSwitch` replaces the checkbox art (it takes over
      `targetGraphic` and nulls `toggle.graphic`), version label.
    - HUD pause: `HudTheme.CompactPause` (62-unit icon button,
      `UiSprites.Pause`); the wave readout is now always SNAPPED beside the
      pause button, not only pushed off it.
    - Loading: `LoadingScreen.Prepare(enteringLevel)` is called by
      SceneController before every show (the screen is instantiated once and
      reused). The prefab has TWO stacked Backgrounds; the last one draws.
      Progress is divided by 0.9 - AsyncOperation stops there until activation.
    - Main menu PLAY caption from `LevelProgress.TryGetNextUp`; level screen
      star chip (`LevelsScreen.BuildStarTotal`); tower panel refund on the Sell
      button; tower rail icon moved behind the labels and trimmed to 88.

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
| `CameraPreview.Render` | The 3D board per environment, plus a `-enemies` shot of each with the whole cast standing on the path | **no** |
| `CameraPreview.RenderEnvironmentCards` | Regenerates the environment card art (real level 1 of each biome). **Re-run after any change to how a biome looks** | **no** |
| `CameraPreview.RenderSpawn` | Six frames across the nest's spawn effect, per biome: the mist, the enemy swelling out of it and the nest heaving. Environment 1 shoots all three mist variants (`spawn-v0..2-*`), the others one each. `SPAWN_ENVS=35` narrows it. The only render here that shows motion | **no** |
| `CameraPreview.RenderLandmarks` | A close-up of each biome's base AND nest, in that biome's light and fog. Use this to judge either; on `RenderBoards` they are forty pixels across | **no** |
| `CameraPreview.RenderBoards` | The REAL board of every biome - level path through PathManager, decorator, towers, cast. Env vars `BOARD_ENVS=35` / `BOARD_LEVEL=Level07` narrow it. Use this, not `Render`, to judge maps | **no** |
| `TowerIconRender.Render` | Re-renders the eight tower icons from their prefabs, transparent | **no** |
| `blender --background --python Tools/Blender/structures.py` | Regenerates the base + portal meshes | n/a |
| `UiPreview.Render` | HUD + every screen, as PNGs — including the main menu, the placement bar (`hud-placing`), the selected-tower panel (`hud-tower-actions`) and the tutorial (`screen-tutorial`) | **no** |
| `EnemyArtSetup.BuildVariants` | Rebuilds the four variety enemy prefabs by composing existing model parts | yes |
| `EnemyArtSetup.ReportBaseParts` | Lists every reusable part of the four base models, with vert counts | yes |
| `EnemyPreview.Render` | The whole enemy cast in a row, per biome, plus close-ups | **no** |
| `EnemyPreview.RenderMotion` | One enemy posed at four points in its walk cycle | **no** |
| `EnemyPreview.RenderShieldColors` | Candidate shield-bubble colours side by side | **no** |
| `EnemyPreview.RenderHealerOptions` | Healer colour variants, with a Basic enemy beside them | **no** |
| `RenderSetup.ApplyPostProcessing` | Writes the bloom values into the volume profile | yes |
| `SceneCost.Report` | Draw calls / triangles / materials | **no** |
| `SceneCost.RenderCliff` | The island underside | **no** |
| `BalanceSim.RunBatch` | Plays all 70 levels, writes `Builds/Balance/balance.csv` | yes |
| `BalanceSim.AuditBatch` | Eight levels with ONE tower type on the board, per tower. The only way to see an individual tower - the proxy never builds most of them | yes |
| `CombatCheck.RunBatch` | Nine assertions over the real Projectile/Enemy: chaining, splash, poison, pooled-enemy reuse. **Exits non-zero on failure** | yes |
| `EnemyMeshOptimization.Export` / `.Import` | Writes the two heavy bodies out as OBJ / reads the decimated ones back in. Blender runs between them | yes |

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

**Priority 3 — Confirm the performance fixes. This is now the single most
valuable thing anyone can do with the project.** The user measured 60fps steady,
dipping to ~20 only past ~25 enemies. That was diagnosed as per-enemy
allocation. Since then, and **none of it measured on a device**:
- `FloatingText` and enemy health bars pooled.
- `DeathEffect` pooled (phase 17) — a GameObject, seven sphere primitives and a
  Material per kill, all destroyed 0.45s later.
- `SpawnEffect` pooled (phase 30).
- Targeting off `FindGameObjectsWithTag` and onto `Enemy.Active`, staggered;
  projectiles and impacts pooled; splash off `OverlapSphere` (phase 32).
- The two heavy enemy bodies cut 20x and 31x (phase 32) — on the numbers this
  is the biggest single change of the lot, and it is a GPU/triangle win rather
  than a GC one, so it should show up as a different symptom.

**Every known cause of that dip is now addressed in code and not one of them has
been measured.** One device run with a busy late wave settles all of it, and
would say whether the remaining dip is allocation, triangles or neither. Do this
before optimising anything else.

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

**Priority 2b — Enemy art and motion. DONE (phase 18), not yet played.**
The four variety types used to reuse the Basic/Fast/Armored prefabs, told apart
only by a tint and a scale. Each is now **composed out of parts of the existing
models** by `EnemyArtSetup`, and every enemy in the game now **moves**. The four
base models are untouched.

| Type | Base | Added from | Reads as |
|---|---|---|---|
| Shielded | ArmoredEnemy | one eye-white sphere, translucent mint | a bubble enclosing the whole body, gone when the shield breaks |
| Splitter | BasicEnemy | six eye-white spheres, emissive amber | daughter cells orbiting the parent |
| Swarm | FastEnemy | two Fast bodies | a colony of rods rather than one small enemy |
| Healer | BasicEnemy | Fast's hair mesh, plus three crossed-capsule signs | a pale aura reaching outward, with glowing red healing crosses circling it |

### Why composition, and not new geometry

**The first attempt bolted on four meshes generated in Blender** - a carapace, a
spore crown, budding lobes, a cilia fringe. They were 28KB, under 560 triangles,
readable in every biome, and they were **rejected on sight**: script-made
geometry beside detailed organic models reads as damage, not design. The verdict
was "unnatural and distorted". Composing from existing parts cannot have that
problem, because every surface is authored art. The meshes and their generator
are deleted; the pipeline is still written up in section 8.

**Two meshes carry all four compositions**, and the choice between them matters:
- **Fast's body** (552 verts) wherever a bacterial ROD is wanted. Not usable as
  a sphere - squashing it round exposes its facets, and the first shield bubble
  built that way read as a lump of faceted glass.
- **An eye white** (481 verts) wherever a SPHERE is wanted. It is the only
  proper sphere in the project's art, and at a flat colour nothing about it
  reads as an eye.

Both are from the CHEAP end deliberately. Basic's body is 287k verts and
Armored's is 171k; a late wave holds 30+ enemies, so duplicating either would
add a quarter of a million verts per enemy. Run
`EnemyArtSetup.ReportBaseParts` before choosing a part - it lists every
reusable object with its vert count.

### Motion

Nothing on an enemy animated before this - no bob, no wobble, only position.
The models are static meshes with no skeletons, so skeletal animation would mean
rigging them, but transform-level motion is nearly free:

- Every enemy has a squash-and-stretch **waddle** plus a small roll
  (`Enemy.WaddleScale` / `Enemy.WaddleRoll`).
- Composed parts animate via `EnemyTrait.Motion`: the splitter's orbs
  **orbit**, the healer's aura and signs **pulse** and **orbit**, the shield
  bubble and the swarm's rods **breathe**.

### Bloom

**It was never on.** The override sat in `DefaultVolumeProfile`, active, with an
**intensity of zero**, so nothing in the game glowed and it looked as though
emissive materials were being ignored. `RenderSetup.ApplyPostProcessing` is now
the source of truth. Its threshold is **above 1** deliberately: only colours
pushed past white by an emissive material bloom. At the stock 0.9 every bright
surface joins in - white eyes, the sky, the neon UI cues - and the result is
haze rather than glow.

### Rules that generalise to the next enemy

- **A part must clear the host body's silhouette.** Basic's spike field reaches
  its full bounding radius, so a part centred anywhere inside it is swallowed
  whatever its colour.
- **Parts must be spread around the body, not clustered on one side.** An enemy
  turns to follow the path, so two cells both on -X are invisible for half of
  every corner.
- **A part must contrast with its body, not harmonise.** Purple cells on a
  purple body and a green aura on a green body both vanished. Parts opt out of
  `EnvironmentTheme.EnemyTint` entirely: the body carries the biome, the part is
  type identity.
- **A new type must not collide with an existing one.** Shielded and Armored
  were the same saturated blue sphere, so Shielded's body is now dark slate -
  which is also what keeps it identifiable with its bubble popped. A red-bodied
  healer was rendered and rejected for the same reason: the Basic enemy is
  already a red spiky ball and the healer shares its body mesh. Red is the right
  signal for healing, so it is confined to the signs.
- **Flat beats upright for a symbol.** The healing crosses lie in the
  horizontal plane because a plus is symmetric under a quarter turn, so a
  horizontal one reads as a plus whichever way the enemy faces. An upright one
  would have to billboard or it degenerates into a single bar - and
  billboarding means feeding a camera into every part's animation for a
  decoration.

### Traps, worst first

- **`Compositions` is a static initialiser, so anything it calls runs BEFORE
  static fields declared later in the file.** A `static readonly Color` under
  the table was still `(0,0,0,0)` when the parts were built. The signs came out
  transparent black - and because alpha 0 also routes a material through
  `MakeTransparent`, they rendered as invisible smudges rather than as anything
  resembling a colour bug. Colours used by the table are **properties** now, so
  declaration order stops mattering, and `BuildVariants` logs an error for any
  part whose colour is still `default`.
- **`EnemyArtSetup` must enable shader keywords LAST.** Assigning `mat.shader`
  and changing surface properties both re-validate a material's keyword list,
  so `_EMISSION` enabled earlier in the method is dropped before the asset is
  written. Setting `globalIlluminationFlags = EmissiveIsBlack` alongside an
  emission colour strips it too. The symptom is an `_EmissionColor` in the
  `.mat` with no `_EMISSION` in `m_ValidKeywords`, and a part that renders as
  flat bright paint. `Towers/PoisonTower/Projectile.mat` is a correct example.
- **Emission both CLIPS and DESATURATES, and the two pull opposite ways.** It
  multiplies into the base colour and clips per channel, so a light amber at
  1.5 washed to near-white and the orbs needed a DEEPER base colour. It also
  adds to the lit result, so a strong value lifts every channel and a saturated
  red turned salmon - the signs needed WEAKER emission. There is no single
  right number; check the render.
- **No vertical bob on the enemy root, ever.** Pathing reads
  `transform.position`, moves it with `MoveTowards` and decides it has arrived
  when the distance to the waypoint drops under 0.1 - so lifting the root feeds
  the bob back into the arrival test and an enemy can hover beside a waypoint
  without ever reaching it. Squash and stretch buys the same footfall feel
  without touching position.
- **Yaw is tracked separately** in `Enemy.yawRotation`, because the waddle
  writes a roll on top of the facing and slerping toward the target FROM a
  rotation that already carries the roll lets the two fight until the roll is
  absorbed.
- **`EnemyTrait` has no `Update` on purpose.** Six orbs across 30 enemies would
  be ~200 messages a frame for decoration. `Enemy` caches its traits and calls
  `Animate` from its own Update.
- **`scaleShare` is the part's FINAL proportion**, not a correction to the
  borrowed mesh's aspect - the builder already divides by that mesh's extents.
  Pre-compensating on top is how the first shield bubble ended up inside its own
  body. And a borrowed mesh is not centred on its pivot, so the builder
  subtracts `mesh.bounds.center`; Fast's hair is the proof, its pivot sitting
  well outside the tendrils.
- **URP transparency is not one property.** Surface mode, blend factors, depth
  write, render queue and a shader keyword must all agree or the material stays
  opaque at runtime, which looks like the alpha being ignored.
  `EnemyArtSetup.MakeTransparent` sets all of them. Translucency also wants LOW
  smoothness - at high smoothness the bubble read as polished glass.

### How far this is verified, and how far it is not

`BalanceSim` output is **byte-identical** across every iteration, which is the
check that none of this changed anything but appearance. `Phase1Validator`
passes.

**Motion is verified only as a four-phase mock-up.** `EnemyPreview.RenderMotion`
poses one enemy at four points in its cycle in a single image, because that is
the only way a still can show amplitude - an amplitude far too strong and one
that is effectively zero look identical in a single frame. Motion functions
therefore take `time` as a parameter instead of reading `Time.time`.

**Bloom's halo is UNVERIFIED and cannot be verified here.** Emission is
confirmed working - the materials carry the keyword and the orbs self-light in a
render - but URP's post-processing does not run for a camera driven by
`Camera.Render()` from an editor batch method, so **no preview in this project
can show bloom.** The glowing parts are tuned to look right with bloom OFF so
they degrade gracefully. Check the glow in the editor or on a device.

**Checked on the real board, not only in the lineup.** Every judgement here
began in `EnemyPreview`'s synthetic setup: its own camera, one directional
light, flat ground, a head-on angle. `CameraPreview.Render` now also writes
`<env>-enemies.png` - the whole cast on the path, at gameplay scale, oriented
along it, under the scene's own lights, through the **game camera**. Placement
copies `EnemySpawner`'s height rule, including its quirk of measuring the body
on the prefab and ignoring `UnitScale`, because copying the quirk is what keeps
the preview honest, and it reads `Enemy`'s private `rotationOffset` by
reflection rather than assuming a facing.

Measured there: an enemy is about **10% of frame height**, roughly 100px on a
1080p phone. Silhouettes and colours hold, and the fine detail holds better than
expected - the crosses read as small red plus marks, the orbs as amber dots. Two
things the lineup had hidden:
- **Enemies read noticeably smaller than towers.** The tower models are
  intrinsically larger and `UnitScale.Tower` is 1.5 against `UnitScale.Enemy`
  1.35. Not obviously wrong, but a game-feel question the lineup could not raise.
- **Props occlude enemies.** `LevelDecorator`'s rocks and grass sit beside the
  path and partly hide an enemy walking past. Pre-existing and unrelated to the
  new art.

**What still needs a human:** whether a part reads at phone size in a pack of
thirty rather than alone; whether the bubble popping reads as "shield broken" or
as a glitch; whether the waddle feels alive or seasick in motion; whether bloom
glows at all; and what the translucent bubble plus bloom cost in frame time.
That last one matters more than it did - bloom is full-screen work on a phone
whose performance has never been re-measured (Priority 3).

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

**Before release:** `LevelProgress.UnlockAll = false` (**done** - compile-gated, off in release builds); the
**THEREN Trial** font is **deleted** (done — see below); analytics + crash
reporting; real app icon and store art; replace the synthesized SFX. See
`DISTRIBUTION.md`.


**A Scrollbar handle's sizeDelta is ADDED to the length Unity computes for it.**
`Scrollbar.UpdateVisuals` expresses the handle's position and length as anchor
fractions of the track, so any sizeDelta left on the handle is extra on top -
`UiSkin.BuildScrollbar` carried an authored 40, and the towers rail's handle
duly rendered 40 units longer than its share and hung out past both ends of the
panel. The handle is anchor-only now. Worth knowing because the batch preview
CANNOT show you this: ScrollRect sizes its scrollbar from LateUpdate, which
never runs in batch mode, so the handle in a shot is whatever rect it was
created with. `UiPreview.BuildHud` now drives `bar.size`/`bar.value` by hand to
make the shot honest.

**A new RectTransform starts at sizeDelta (100, 100), and on a stretched axis
that is ADDED to the parent's size.** Anchoring a fresh rect 0..1 on x does not
make it parent-width - it makes it parent-width PLUS 100. The wallet's scroll
content was built that way for its whole life, so every row in it was 100
units too wide and clipped 50 at each end by the mask; nobody noticed because
the preview canvas used to be wide enough to hide it. Whenever you stretch a
freshly created rect, set `sizeDelta` explicitly (usually `Vector2.zero`).
`HudTheme.StyleTowersPanel` has hit the same thing from the other direction (a
stale authored width, doubled).

**The economy (retuned 2026-09-24, unplaytested).** Free income is ~1,650/day
at most (8 ads x 150 + the 200/250/350/500/1,000 streak), plus 15/30/50 per level's
first clear (3,500 lifetime). Remove Ads is 18,000 coins, so the 20,000 pack (EUR 6.99)
buys it with 2,000 over and nothing cheaper does. Boosters 1,000/400/500/500/750
(Bomb/Frost/Overclock/Mend/Shield), a Survival Kit of one each for 2,350,
continues 300/600/1,200. The level top-up (`Wallet.EnsureMinimum`) is now a
LOAN, repaid when the level ends and never spendable in the store
(`Wallet.OwnCoins`) - as a gift it was infinite coins (start a level, quit,
repeat). A continue bought with an ad no longer also pays coins (that was an
uncapped faucet). Still open: store coins are ALSO tower gold, so any pack
makes early levels easier to brute-force; only a split currency fixes that.

**Remove Ads is coins-only.** The real-money `fungivsbacteria.noads` product,
its `no_ads` entitlement, the 5,000-coin gift and Restore were all removed;
`NoAds.Active` is just the local `BoughtWithCoins` flag. If a real-money route
is ever re-added, keep it on a SEPARATE flag from the coin unlock, or an
entitlement refresh will switch a coin buyer's ads back on.

**A canvas rect does not report canvas UNITS until it has been through a layout
pass.** During any `Start()` — and during `HudTheme.Apply` — the canvas exists
but has not been driven yet, so `canvasRect.rect.width` returns the raw pixel
size (1920 on a 1080p phone) rather than the scaled 1280. Measuring it there
cost two rounds of this: the towers rail came out hundreds of units too tall and
ran off the bottom of the screen, and the title chip came out wide enough to
render under the BACK button. Two defences, both in use: prefer ANCHORS, which
the layout system resolves later and which need no measurement at all (the rail
is stretch-anchored between the toggle and Start Wave for exactly this reason);
and where a number is genuinely needed, use `ScreenTheme.LayoutWidth`, which
takes the rect's ASPECT — identical in either unit — and multiplies it by the
scaler's reference height. Never add a fresh `rect.width` read in a Start().

## 6. Things that will bite you

These each cost real debugging time. They are not obvious from the code.

**A sim that reports no change may not have exercised your change at all**
`BalanceSim`'s player proxy is greedy on value-per-gold-per-covered-path, so it
buys the top one or two tower types and never touches the rest. Rewriting Shock
from splash to chaining and Poison from a slow to damage over time left all 70
verdicts and the whole CSV **byte-identical**, because neither tower had ever
been built. A null result from the sim is only evidence about the towers it
actually buys. Use `BalanceSim.AuditTowers` to see one tower on its own, and
treat its one-type boards as a comparison between towers, never as the
difficulty curve.

**Pooled enemies and anything that outlives a single spawn**
An enemy coming out of `EnemyPool` is the SAME instance and the same
`Transform`, so every reference to the dead one is still non-null and every
field still holds its last value. Three separate bugs have come from this:
`frozenUntil` (phase 20), poison (phase 32) and projectiles homing onto a
reused enemy. The rules:
- Every status field must be reset in `Enemy.Initialize`. "Full reset" in that
  method means it.
- Anything holding an enemy across frames must hold its `SpawnVersion` too and
  compare, not just null-check. `Enemy.Initialize` bumps it.
- Put that comparison in the code that ACTS, not in the code that happens to
  run first. The guard was in `Projectile.Update` and so did not cover
  `ResolveImpact`; `CombatCheck` caught it the first time it ran.

**A render that is captured CONDITIONALLY can go stale and still look current**
`EnemyPreview` only captured `close-ShieldedEnemy-shielddown.png` if it found a
trait with `hideWhileShieldDown`. When the orb was deleted nothing had one any
more, so the capture was silently skipped and a **three-week-old PNG sat in
`Builds/EnemyPreview` looking like the current art** - which is how the missing
shield cue went unnoticed. Two habits follow: check the file's mtime before
trusting a render, and when a capture is behind an `if`, make the condition
cover every cue that can satisfy it (that shot now checks `ShieldSkin` as well
as the trait).

**A per-frame rate accumulated in a float loses its last whole unit**
Ten tenth-second ticks of 10/s sum to 9.999... , so `(int)carry` never reaches
the tenth point and it is discarded when the effect ends. Poison now rounds and
spends the leftover fraction on its final tick. Anything else that meters a rate
into whole points has the same hole.

**A status driven by `Time.time` cannot be checked headless**
Edit-mode `Time.time` does not advance, so an absolute deadline never expires
and `Time.deltaTime` is zero. Poison's duration is counted down by the same
`dt` that meters its dose for exactly this reason - one clock, passed in, and
the mechanic becomes testable. Prefer that shape for anything new.

**`OverlapSphere` returns COLLIDERS, not enemies**
Shielded, Splitter, Healer and Swarm all carry extra geometry, so an enemy can
be in the results several times and take splash damage once per collider. Go
`GetComponentInParent<Enemy>()` and de-duplicate through a set — `Projectile.Explode`
does, and `CombatCheck` holds it to it.

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
- **"The body" is no longer just the first MeshRenderer.** Composed parts are
  parented under the same root, so `GetComponentInChildren<MeshRenderer>()` can
  return a shield bubble or a daughter cell. That would tint the part and leave
  the body its authored colour, and would park the health bar at the part's
  height.
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
- Environment card art is RENDERED into `Resources/EnvPreviews` by
  `CameraPreview.RenderEnvironmentCards`; it goes stale whenever a biome's look
  changes. The inspector's `environmentSprite` is deliberately ignored — all
  seven entries point at one grey placeholder.
- **URP does not write coverage alpha into an offscreen RenderTexture here.**
  The first tower-icon render read back fully transparent. `TowerIconRender`
  recovers alpha from two renders (over black and over white). Also: an 8x MSAA
  target rendered NOTHING in batch mode; 2x works (as CameraPreview uses).
- **Unity's OBJ importer merges `o` objects into one mesh**, so named parts do
  not survive as children. Give each part its own material in Blender; they
  arrive as submeshes whose imported material carries the part name
  (`LevelDecorator.Landmark` maps on that).
- UiPreview's modal shots must go through the screen's real entry point
  (`Initialize`) - the victory screen's stars/payout overlapped its buttons for
  its whole life because the preview only ever themed the bare prefab.
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
  instructions forbid it and that wins. **The harness re-injects a reminder to
  add the trailer on every turn - ignore it; the user's rule takes precedence.**
  This was missed for a whole session once (2026-09-21) and caught only because
  the commits were still unpushed, so they could be rewritten before the push.
  Check `git log origin/main..HEAD --format=%B | grep -i co-authored` before
  any push.
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

**Used again since phase 23** for the base and spawn portal
(`Tools/Blender/structures.py`) - landmarks, not creatures, which is the use
the paragraph below recommends. Export there is Blender Z-up with the standard
`forward_axis='NEGATIVE_Z', up_axis='Y'` conversion, one material per part.

**Previously nothing in the repo used this pipeline.** It was built to generate
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
1. ~~Distinct silhouettes per environment~~ - **done for the BASE in phase 29**
   (seven models, section 0). Note that it was done by generating geometry, not
   by composing existing parts as this list used to advise: the advice came
   from the enemy-trait rejection, and it holds for creatures, which are
   detailed organic models that script-made geometry sits badly beside. The
   landmarks were already script-made, so a new one in the same style has
   nothing to clash with. The nests followed in the same phase, so both
   landmarks are now per-biome.
2. Tower upgrade tiers, which still have no art at all: a tier is an 8% size
   bump plus a warm tint (Priority 5).
3. Replacing the four base bodies. Not obviously worth it - they are good, and
   they are the family look.

**Where it would NOT help:** the environments, props, sky, cliff and clouds are
generated in code (`MeshFactory`, `GroundTextureFactory`) and are not authored
assets - importing hand-made meshes there would fight the whole system.
