# Fungi vs Bacteria — Distribution Checklist

## Already configured (in this repo)

- **Identity**: product "Fungi vs Bacteria", company "Oktay Shakirov", version 1.0.0
- **Bundle ID**: `com.shadev.fungivsbacteria` (Android / iOS / Standalone)
- **Orientation**: landscape only (UI reference 1280×720, match-height — adapts to any aspect ratio)
- **Android**: IL2CPP + ARM64-only (Play Store compliant), min SDK 23
- **App icon**: `Assets/Sprites/Icons/AppIcon.png` (1024x1024, opaque), set as the default
  icon by `Tools → App Icon → Apply`; Unity scales it into every platform size at build time
- **Frame rate**: capped at 60 fps on device (set in GameManager)
- **Builds**: `Tools → Build` menu in the editor, or from the command line:

  ```sh
  Unity -batchmode -nographics -projectPath . -buildTarget Android \
        -executeMethod BuildTools.BuildAndroidAabBatch -logFile build.log
  ```

- **Level pipeline**: `Tools → Level Generator → Generate All Levels` regenerates all 70
  levels (7 environments × 10); the validator (`Phase1Validator.Validate`) checks paths, waves, and audio wiring.

## You must do (accounts & signing — cannot be automated)

### Google Play
- [ ] Google Play Console account ($25 one-time)
- [ ] Create a keystore (`Player Settings → Publishing Settings`) and **back it up** —
      losing it means losing the ability to update the app
- [ ] Store listing: title, short + full description, category (Strategy)
- [ ] Screenshots (min 2, landscape), feature graphic 1024×500
- [ ] Privacy policy URL — required, and it must name the ad SDKs (see **Privacy** below)
- [ ] Data-safety form — the game ships ad SDKs that DO collect data; answer it from
      **Privacy** below, not "no data collected"
- [ ] Content rating questionnaire (should land at PEGI 3 / Everyone) — and see
      **Target audience** below before picking an age group
- [ ] Upload the AAB from `Builds/Android/`

### Apple App Store (second target)
- [ ] Apple Developer Program ($99/year)
- [ ] Build the Xcode project (`Tools → Build → iOS`), open in Xcode, set your signing team
- [ ] App Store Connect listing + screenshots (6.7" and 13" required)
- [ ] Privacy nutrition label — see **Privacy** below; the app tracks (ATT prompt)

### Before submitting anywhere
- [ ] Playtest the difficulty curve (levels 1, 5, 10, 15, 20) and report tuning needs
- [ ] Replace placeholder art: app icon, environment card sprites (all four environments
      currently share one sprite)
- [ ] Decide on the 4th environment card: hide it or generate levels for it
- [ ] Test on a real Android device: touch placement, safe area on a notched screen,
      performance during the biggest wave (level 30)
- [ ] Optional: replace the synthesized `Assets/Audio/Victory.wav` with a real jingle

## Privacy — what the app actually collects

The game itself has no accounts, no server and no analytics: progress, stars and
the coin wallet live only in `PlayerPrefs` on the device. **But it ships three
third-party SDKs that collect data, and the store declarations have to cover
them.** An earlier version of this file said "no data collected, no third-party
SDKs"; that was never true once ads went in, and answering the store forms from it
would be a false declaration.

| SDK | Package | What it is for |
|---|---|---|
| Unity LevelPlay (ironSource) | `com.unity.services.levelplay` 8.10.2 | Ad mediation — serves the interstitial and rewarded ads |
| Google Mobile Ads (AdMob) + UMP | `com.google.ads.mobile` 10.4.2 | AdMob demand through LevelPlay; UMP shows the GDPR consent form |
| Unity iOS Support | `com.unity.ads.ios-support` 1.0.1 | The iOS App Tracking Transparency (ATT) prompt |

Consent flow at launch (`LevelPlayAds.ConsentThenInit`): **ATT prompt (iOS) →
UMP consent form (GDPR regions only) → LevelPlay init.** The networks read the
ATT status and the TCF consent string themselves. The ATT prompt text is
`NSUserTrackingUsageDescription` in `iOS/Info.plist`: *"This identifier will be
used to deliver personalized ads to you."*

The categories below are what AdMob and LevelPlay collect **by their own
published disclosures**. They are a starting point, not a substitute: **check
each SDK's current data-disclosure page before submitting** — Google publishes
one for AdMob on each platform, and Unity publishes one for LevelPlay — since
SDK updates change them.

### Google Play — Data safety form

- **Does the app collect or share data?** Yes.
- **Device or other IDs** (Advertising ID) — collected **and shared**; purpose:
  advertising or marketing, analytics, fraud prevention.
- **Approximate location** (derived from IP) — collected and shared; advertising,
  fraud prevention.
- **App activity → app interactions** — collected; advertising, analytics.
- **App info and performance → crash logs, diagnostics** — collected; analytics.
- **Encrypted in transit:** yes (both SDKs use HTTPS).
- **Advertising ID declaration** (separate Play Console question): **yes** — the
  AdMob SDK merges `com.google.android.gms.permission.AD_ID` into the manifest.
- **Deletion requests:** the app stores nothing server-side; point users to the
  ad networks' own processes in the privacy policy.

### Apple — App Privacy ("nutrition label")

- **Data used to track you:** Identifiers → Device ID (IDFA), for Third-Party
  Advertising. This is what the ATT prompt covers — if a user declines, the IDFA
  is not available, but the label still declares it.
- **Data linked to you / not linked:** Usage Data (advertising data, product
  interaction), Diagnostics (crash data, performance data), Location (coarse),
  all for Third-Party Advertising and/or Analytics.
- Each SDK ships a `PrivacyInfo.xcprivacy` privacy manifest, and Xcode merges
  them into the app's privacy report (**Product → Archive → Generate Privacy
  Report**). **Use that generated report as the source of truth** for this label:
  it reflects the exact SDK versions in the build.

### Open gap: players cannot change their consent

**Must fix before release.** GDPR, and Google's EU user consent policy that the UMP
SDK exists to satisfy, require that a player who answered the consent form can
revisit that choice later. `LevelPlayAds` already has both halves —
`IsPrivacyOptionsRequired` and `ShowPrivacyOptionsForm()` — but **nothing calls
them**: there is no button anywhere in the game. It needs a "Privacy options"
button on the settings screen, shown only when `IsPrivacyOptionsRequired` is true
(so players outside the EEA/UK never see it).

### Target audience — decide this before the content rating

The art is bright and cartoonish and the rating will land at Everyone / PEGI 3,
which is exactly what makes a store reviewer ask whether it is **for children**.
That decision has hard consequences for the ad setup:

- **If children are a target audience** (Google Play: any age group under 13
  selected; Apple: Kids category), Google Play's Families policy requires only
  Families-certified ad SDKs, no personalised ads and no Advertising ID for those
  users, and the ad requests must be tagged child-directed. **Nothing in the
  current code does that** — `LevelPlayAds` sets no child-directed flag.
- **If the target audience is 13+**, none of that applies, but the store listing
  must not be designed to appeal primarily to children.

Choosing **13+** matches the current integration and needs no code change.
Choosing a children's audience is a real piece of work (child-directed flags on
LevelPlay and AdMob, and a check that every mediated network is Families
certified) and should be decided before the first submission, not after a
rejection.

### Privacy policy — minimum contents

Must be a public URL, linked from both store listings and ideally from the
settings screen. At minimum: that the game stores progress only on the device;
that it shows ads through Unity LevelPlay and Google AdMob, which collect the
data above for advertising; how consent works (ATT on iOS, the consent form in
the EEA/UK) and that users can change it; and a contact address.

## Known gaps (deliberate, post-1.0)

- Unity splash screen is shown (removing it requires a Unity Pro license)
- No analytics/crash reporting — consider Unity Cloud Diagnostics or Firebase Crashlytics
  before a wide release, both require accepting their SDK terms
