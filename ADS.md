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

## If Xcode says `IronSource/IronSource.h` file not found

LevelPlay's native iOS/Android SDK is not part of the UPM package — it is
fetched separately by a "Network Manager" installer the first time the
**Unity Editor** (not batch mode) registers the package, and it writes the
result to `Assets/LevelPlay/Editor/*.xml` (read by EDM4U to add the CocoaPods
entry). If those files are missing, EDM4U never adds `pod 'IronSourceSDK'` to
the Podfile, and Xcode fails on the header.

Already fixed once (2026-08-20) by installing the same two dependency
descriptors the Editor's installer would have written — `IronSourceSDKDependencies.xml`
and `ISAdMobAdapterDependencies.xml`, both now committed in
`Assets/LevelPlay/Editor/`. If this happens again (e.g. after bumping the
LevelPlay package version), the fix is:

1. **Tools -> Build -> iOS (Update existing Xcode export in place)** —
   re-exports into the checked-in `iOS/` folder rather than a scratch
   location, which is what regenerates `iOS/Podfile` from every
   `Dependencies.xml` under `Assets`.
2. In `iOS/`, run `pod install`. If CocoaPods errors with a Ruby
   `UnicodeNormalize`/`ASCII-8BIT` crash, your shell's locale isn't UTF-8 —
   run `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 pod install` instead.
3. Open `Unity-iPhone.xcworkspace` (never the `.xcodeproj` — CocoaPods only
   links correctly through the workspace) and build.

If the LevelPlay package version changes, the compatible SDK/adapter versions
also change. They are resolved the same way LevelPlay's own installer does:
read `Assets/LevelPlay/Editor/Json` — no, that path is package-internal;
instead open the package's bundled
`Editor/Json/LevelPlayVersions.json` (inside
`Library/PackageCache/com.unity.services.levelplay@<version>/`), find
`unityPackage.versions["<installed package version>"].ironSourceSdkVersion`
(a range), pick the newest `IronSourceSdk.versions` entry inside that range,
then the newest `adapters.AdMob.versions` entry whose own range covers *that*
SDK version. Their `dependencyXmlURL` (with `${VERSION}` substituted) is a
plain HTTPS S3 URL — fetch both and drop them at
`Assets/LevelPlay/Editor/<dependencyXmlFileName>`, then repeat steps 1-3
above. This is exactly what **Ads Mediation -> Network Manager** does from
inside a normal (non-batch) Editor session, if you would rather use the GUI.

## If ads never load and the log shows "UMP unavailable... Value cannot be null. Parameter name: type"

This is a real bug, not a dashboard/config problem, and it will keep the UMP
consent flow (and possibly ad fill on networks that check consent) broken on
every device build until `Assets/GoogleMobileAds/link.xml` exists.

**Cause:** the GoogleMobileAds package's own bundled `link.xml`
(`Library/PackageCache/com.google.ads.mobile@<version>/GoogleMobileAds/link.xml`)
tries to preserve the platform UMP assemblies with a `<namespace preserve="all">`
child element. That is not valid Unity link.xml syntax - `preserve="all"` is
only recognized on `<assembly>` and `<type>` - so UnityLinker treats the rule
as empty and strips the entire `GoogleMobileAds.Ump.iOS` (and `...Ump.Android`)
assembly as unreachable, since nothing else references it statically -
`GoogleMobileAds.Ump.Api.Utils.GetClientFactory()` only resolves it via
`Type.GetType("GoogleMobileAds.Ump.iOS.UmpClientFactory, GoogleMobileAds.Ump.iOS")`,
a plain runtime string invisible to the linker's reachability analysis. With
the type gone, `Type.GetType` returns null and the following
`Activator.CreateInstance(type)` throws exactly
`ArgumentNullException: Value cannot be null. Parameter name: type`.

**Fix (already applied, 2026-08-20):** `Assets/GoogleMobileAds/link.xml` adds
the same preservation with the correct, documented syntax:

```xml
<assembly fullname="GoogleMobileAds.Ump.iOS" preserve="all" ignoreIfMissing="1" />
```

Verified by checking the generated IL2Cpp output before/after: before the fix,
no `GoogleMobileAds.Ump.iOS.cpp` existed anywhere in
`iOS/Il2CppOutputProject/Source/il2cppOutput/`; after, it exists with a fully
compiled `UmpClientFactory` (was 0 lines of generated code, is 2600+ after).
Confirmed with a full unsigned `xcodebuild` through the workspace both times.

Re-export (Tools -> Build -> iOS...) is required after any change here for it
to take effect - the link.xml is only read during the Unity export step, not
by Xcode or CocoaPods afterward.

## "Mediation No fill" (error 509) - no ad networks configured yet

If init succeeds (`[Ads] LevelPlay init succeeded.`) but every load fails with

```
[Ads] Rewarded load failed: 509 - Mediation No fill
```

then the SDK is working correctly and the problem is entirely dashboard-side:
LevelPlay asked its waterfall for an ad and no network in it had one. On a new
app the usual reason is simply that **the waterfall is empty** - the app keys
and ad unit IDs are right (a wrong ad unit gives a different error), the
networks just have not been attached to the ad units yet.

This is the "add AdMob as a network on each placement" step; nothing in the
Unity project can fix it.

### First: register the device as a test device

Both networks being *present* in the Test Suite but still not filling means
they are being asked for **real** inventory, which a brand-new app has none of.
The supported fix is to tell LevelPlay this is a test device:

1. Build and run with `launchTestSuiteOnInit` or `verboseLogging` on. Init
   logs the advertising ID:
   `[Ads] Advertising ID (paste into LevelPlay -> Settings -> Test devices): ...`
   (iOS hides the IDFA otherwise; this saves installing a third-party app to
   find it. It is debug-flag gated so a shipping build never prints it.)
2. LevelPlay dashboard -> **Settings -> Test devices -> Add test device** -
   device name, that advertising ID, platform.
3. Rebuild and run. The Test Suite's per-ad-unit **Load ad** button now runs
   the auction against test inventory, and bidding line items expose a
   **Live/Test** toggle.

Note that mediated test ads render **without** the "Test mode" label that
Google's own direct-integration test ads carry - do not take the missing label
as a sign it did not work.

### ironSource fills but Google does not: two separate test-device lists

These are independent, and registering in one does nothing for the other:

* **LevelPlay -> Settings -> Test devices** makes *ironSource* serve test ads.
* **AdMob console -> Settings -> Test devices** makes *AdMob* serve test ads.

The Test Suite's Live/Test toggle is a LevelPlay/bidding control - it does not
reach into AdMob. For a waterfall AdMob instance LevelPlay simply calls the
AdMob adapter with the real ad unit ID, and AdMob alone decides whether to
return a test ad, a real ad, or nothing. So "ironSource test ads work, Google
509s" is the expected symptom of having registered the device with LevelPlay
only, on an app whose real AdMob units have no inventory yet.

Google's docs also warn that mediated ads render **without** the "Test mode"
label, and that it is on you to enable test mode per network - the label's
absence is not evidence of failure.

### Also: a guaranteed test ad, bypassing the networks entirely

Google publishes demo ad units that always fill. Configuring an AdMob instance
on the LevelPlay dashboard with these proves the whole pipeline end to end,
without touching your real AdMob account or risking invalid traffic:

| | Android | iOS |
|---|---|---|
| Interstitial | `ca-app-pub-3940256099942544/1033173712` | `ca-app-pub-3940256099942544/4411468910` |
| Rewarded | `ca-app-pub-3940256099942544/5224354917` | `ca-app-pub-3940256099942544/1712485313` |

(Source: Google's own test-ads pages for
[Android](https://developers.google.com/admob/android/test-ads) and
[iOS](https://developers.google.com/admob/ios/test-ads).)

On the LevelPlay dashboard:

1. **Setup -> SDK Networks -> Google (AdMob)** - add the network and enter the
   AdMob App IDs (the `~` ones, already listed above).
2. **Setup -> Instances** (or the ad unit's own page) - for *each* of the four
   placements (Interstitial + Rewarded, Android + iOS) add an AdMob instance
   and paste the matching ad unit ID. Use the demo IDs above first to confirm
   fill, then swap in your real `/`-style ad unit IDs.
3. Real AdMob units on a brand-new app can legitimately return no fill for a
   while - the app has no traffic history and may still be under review. The
   demo IDs are how you tell that apart from a broken integration.

These demo IDs come from Google's own docs and are the **same for every
developer in the world** - they live under Google's test publisher account
(`ca-app-pub-3940256099942544`), not yours (`ca-app-pub-5852582960793521`). They
are not, and should not be, your IDs. What *should* match is the ad unit ID in
the LevelPlay Google/AdMob instance and the one in the AdMob console - those are
the same value, copied across.

## UMP: "no form(s) configured for the input app ID"

```
[Ads] UMP update did not complete (Failed to read publisher's account
configuration; no form(s) configured for the input app ID ...); continuing.
```

Separate from ad fill, and dashboard-side: no consent message exists yet for
this app. **AdMob console -> Privacy & messaging** -> create and *publish* a
GDPR message (and the ATT/IDFA message) for the app, then it resolves.

The flow already degrades safely - `GatherConsent` bounds every wait and always
falls through to `Initialize()`, so a missing form costs no ads. But it means
**no consent signal is being gathered at all**, which is a GDPR problem for EEA
users and can depress fill on networks that require a TCF string. Fix before
release; it is on the pre-release list below.

When testing the form afterwards, the SDK prints a debug identifier at launch
(`<UMP SDK> To enable debug mode for this device, set:
UMPDebugSettings.testDeviceIdentifiers = @[...]`) - note it is a *different*
identifier from the advertising ID used for the test-device lists.

## AdMob dashboard shows 0 requests

This is the single most useful diagnostic, because it splits the problem
cleanly in two:

* **Requests > 0, no impressions** - AdMob is being asked and is declining.
  That is genuine no-fill: new app, no traffic history, possibly still under
  review. Wait it out, or force test ads.
* **Requests == 0** - AdMob is never being asked at all. No amount of
  test-device or test-ad-unit configuration will help, because nothing is
  reaching Google. The problem is in the LevelPlay waterfall, not in AdMob.

Note AdMob reporting lags several hours, so check that a zero is really a zero
and not just today's stats not having landed yet.

When it is genuinely zero, the instance is not in the served waterfall. Things
to check on the LevelPlay dashboard, in order:

1. The AdMob **instance** exists on *each specific ad unit* (Interstitial and
   Rewarded, per platform) - not merely that Google is enabled under
   **SDK Networks**. Network-enabled but instance-missing is the usual cause,
   and the Test Suite still lists Google in that state, which is what makes it
   misleading.
2. The instance is **enabled** (not paused) and its ad unit ID is the AdMob
   `/`-style id.
3. The instance has an **eCPM / rate** set. An instance with no rate can sort
   below everything and never get called.
4. The app-level **AdMob App ID** in LevelPlay's Google network settings is the
   `~`-style id and matches `GADApplicationIdentifier` in the exported
   `Info.plist`.
5. The AdMob **account** is fully approved (billing/payee details complete) and
   the app's status in AdMob is Ready. An account still in review serves
   nothing, on any app.

`verboseLogging` now also turns on `IronSource.Agent.setAdaptersDebug(true)`
(before init) and `IronSource.Agent.validateIntegration()` (after init). The
first makes each adapter log its own load attempts - so the log shows whether
AdMob was invoked at all; the second prints every adapter ironSource found,
its version, and anything it considers misconfigured.

## Bidding vs waterfall instances (`bidderExclusive`)

Init logs one line per format that is worth reading:

```
rewarded settings:     {parallelLoad=2, bidderExclusive=YES}
interstitial settings: {parallelLoad=2, bidderExclusive=NO}
```

`bidderExclusive=YES` means that ad unit is running **bidding-exclusive**: only
in-app-bidding instances take part in the auction. A network added as a
*traditional waterfall* instance on such an ad unit is simply never called -
which looks identical to no-fill from the app, and produces **zero requests**
on that network's own dashboard.

So if AdMob shows no requests, check whether it is set up on the LevelPlay
dashboard as **Google bidding** or as a **traditional/waterfall** instance, and
whether that matches how the ad unit itself is configured. A traditional AdMob
instance on a bidder-exclusive rewarded unit will never be requested no matter
what else is right.

This is an inference from the log, not something the client can confirm - the
app only sees the resulting auction, not the dashboard's instance types.

## What validateIntegration proves - and what it cannot

`IronSource.Agent.validateIntegration()` (on under `verboseLogging`) prints a
per-network report. A healthy one for this project looks like:

```
IntegrationHelper --- Google (AdMob and Ad Manager) ---
IntegrationHelper Adapter VERIFIED
IntegrationHelper SDK - Version 12.9.0 - VERIFIED
IntegrationHelper Adapter - Version 4.3.70 - VERIFIED
IntegrationHelper --- IronSource ---
IntegrationHelper Adapter VERIFIED
```

Every other network showing `*** MISSING ***` is expected and harmless - those
adapters simply are not installed, because they are not being used.

It checks the **client** only: adapter present, SDK present, versions
compatible. It says nothing about the server-side waterfall, so a fully
VERIFIED report is still consistent with a network never being requested
because no instance is attached to the ad unit on the dashboard. Do not read
"Google VERIFIED" as "Google is in the waterfall".

It also prints both device identifiers, which saves hunting for them:

```
IntegrationHelper IDFA is ... (use this for test devices).
IntegrationHelper IDFV is ...
```

The **IDFA** is the one both test-device lists want. The IDFV is what the UMP
SDK's debug settings use - different identifier, different purpose, easy to mix
up because the UMP log line prints the IDFV.

## Verifying ads are actually serving (test ads)

`launchTestSuiteOnInit` on the `Ads` component (**currently ON**, for the
no-fill investigation - turn it off before any real build) launches
LevelPlay's Test Suite after init, which shows every configured network and
whether each one can currently serve a real test ad - the fastest way to tell
"no fill because nothing's configured on the dashboard yet" apart from "still
broken." Turn it on, test on device, turn it back off before any build meant
for real play - the tooltip says so for a reason, and it is easy to forget.
`verboseLogging` (also off by default) additionally logs the full
consent/init sequence.

Ad load failures now log a warning either way (`[Ads] Rewarded load failed:
...` / `[Ads] Interstitial load failed: ...`) regardless of `verboseLogging` -
previously they were completely silent, which is why the UMP crash above had
to be diagnosed from a single unrelated line in a device log rather than a
clear failure trail.

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
