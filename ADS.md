# Ads and the coin economy

Mirrors the setup in **snowman-run**: Unity LevelPlay (ironSource) as the
mediation layer, with AdMob as a demand source and Google's UMP SDK for consent.

## The identifiers

All of them live in **`Assets/Editor/AdsSetup.cs`**, and
**Tools -> Ads -> Apply Ad Keys** writes them into the two places the SDKs
actually read:

* the `Ads` component in `Assets/Scenes/MainMenu.unity` (LevelPlay), and
* `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` (AdMob).

Edit the constants, re-run the menu item; never edit the scene or the asset by
hand, or the two drift.

These are not secrets — ad unit IDs ship inside every APK and IPA and are
readable by anyone who unzips one. The dashboards are what authenticate.

### LevelPlay (configured)

| | Android | iOS |
|---|---|---|
| App key | `278709fa5` | `27870661d` |
| Interstitial | `c47fie8fgtkqqwes` | `iuhic3ldrsi2p2o1` |
| Rewarded | `2g3ezdg4mymfssc8` | `7juzu67ubc1el0b3` |

### AdMob app IDs (configured)

| | App ID |
|---|---|
| Android | `ca-app-pub-5852582960793521~5375331742` |
| iOS | `ca-app-pub-5852582960793521~8390056055` |

Note the `~`. The Android App ID is **not optional** — a build without it
crashes on launch.

### AdMob ad unit IDs — dashboard only, NOT in Unity

These belong on the LevelPlay dashboard, entered against each placement when
AdMob is added as a network. Nothing in this project reads them; they are
recorded here so they are not lost.

| | Android | iOS |
|---|---|---|
| Interstitial | `ca-app-pub-5852582960793521/2719192906` | `ca-app-pub-5852582960793521/7394990424` |
| Rewarded | `ca-app-pub-5852582960793521/4898633682` | `ca-app-pub-5852582960793521/9582170060` |

(Ad *unit* IDs use `/`; app IDs use `~`. Mixing them up is the classic no-fill
cause.)

### Still to do on the dashboards

1. Add AdMob as a network on each of the four LevelPlay placements, using the
   ad unit IDs above.
2. Set the **reward amount** on both rewarded placements. It is read from
   LevelPlay at runtime, so the coin payout can be retuned without shipping a
   build. `fallbackRewardAmount` (300) is only used when the dashboard supplies
   nothing.

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
