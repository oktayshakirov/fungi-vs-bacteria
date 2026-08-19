# Ads and the coin economy

Mirrors the setup in **snowman-run**: Unity LevelPlay (ironSource) as the
mediation layer, with AdMob as a demand source and Google's UMP SDK for consent.

## What you need to supply

Nothing in this repo contains a real key — every field is blank, and with blank
fields `LevelPlayAds` logs a warning and every ad call quietly no-ops. Fill these
in and the ads turn on.

### 1. ironSource / LevelPlay dashboard

On the component **`Ads`** in `Assets/Scenes/MainMenu.unity`:

| Field | Where it comes from |
|---|---|
| Android App Key | LevelPlay -> your Android app -> App Settings -> App Key |
| iOS App Key | LevelPlay -> your iOS app -> App Settings -> App Key |
| Android Interstitial Ad Unit Id | LevelPlay -> Ad Units -> Interstitial (Android) |
| iOS Interstitial Ad Unit Id | LevelPlay -> Ad Units -> Interstitial (iOS) |
| Android Rewarded Ad Unit Id | LevelPlay -> Ad Units -> Rewarded Video (Android) |
| iOS Rewarded Ad Unit Id | LevelPlay -> Ad Units -> Rewarded Video (iOS) |

Six values. The two app keys are per-platform *apps*; the four ad unit IDs are
per-platform *placements*.

### 2. AdMob

Open **Assets -> Google Mobile Ads -> Settings** once (this creates
`Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`) and set:

| Field | Where it comes from |
|---|---|
| Google Mobile Ads Android App ID | AdMob -> App settings -> App ID (`ca-app-pub-XXXX~YYYY`) |
| Google Mobile Ads iOS App ID | AdMob -> App settings -> App ID |

**The Android App ID is not optional** — a build without it crashes on launch.
Note the `~` in an App ID; an ad *unit* ID uses `/` and is a different thing.

The AdMob **ad unit** IDs are not needed in Unity at all: they are entered on the
LevelPlay dashboard when AdMob is added as a network for each placement.

### 3. Set the reward amount on the dashboard

The rewarded placement's reward amount is read from LevelPlay at runtime, so the
payout can be changed without shipping a build. `fallbackRewardAmount` (75) is
only used when the dashboard supplies nothing.

## Where ads appear

**Rewarded** — always opt-in, never forced:
- Wallet screen: "Watch ad" for coins.
- Game over: "Continue - watch ad" resumes the run *and* banks the coins.

**Interstitial** — at level end only, never during play. `Ads.OnLevelEnded()` is
called when the victory or game over screen appears, and `Ads` decides whether one
is actually due:
- none for a new player's first 3 level ends;
- then at most 1 per 3 level ends;
- and never within 90s of the last one;
- and never right after a rewarded ad (`Ads.DeferInterstitial`).

Those four constants at the top of `Assets/Scripts/Ads/Ads.cs` are the whole ad
frequency policy. Tune them there.

## Architecture

`Ads` is a static facade over an `IAdProvider`. `LevelPlayAds` is the only
implementation and registers itself in `Awake`. Nothing else in the game
references the LevelPlay SDK, which means:

- the game runs normally in the editor and in any build with no SDK or no keys —
  every ad call no-ops and the UI shows "AD UNAVAILABLE";
- the interstitial pacing is testable game logic, not SDK behaviour;
- swapping mediation later touches one file.

## The coin economy

`Wallet` (PlayerPrefs, like `LevelProgress`) holds the persistent coin balance.
Keep it distinct from `GameManager.currentGold`, which is the per-level resource
and resets on every run.

**Earning**
- First clear of a level pays by stars: 10 / 20 / 35.
- Improving your star rating pays the *difference*, so replaying a cleared level
  cannot be farmed.
- Rewarded ads.

**Spending** (`Boosters`)
- **Start Boost** — 100 coins for +300 starting gold. Armed on the level screen;
  charged on arming and refunded if you leave without playing.
- **Continue** — 200 coins (or a rewarded ad) for +50 health, resuming the lost
  run in place. Once per run, or the difficulty curve stops meaning anything.

## Before the store build

- Turn `launchTestSuiteOnInit` **off** on the `Ads` component.
- Verify the ATT prompt string in the AdMob settings asset
  (`userTrackingUsageDescription`).
- Show a "Privacy options" button in Settings when
  `LevelPlayAds.IsPrivacyOptionsRequired` is true — GDPR requires EEA players be
  able to change their consent choice. **This is not wired up yet.**
