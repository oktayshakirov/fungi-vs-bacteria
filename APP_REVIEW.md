# App Review — Guideline 2.1 "Information Needed" reply

Apple asked (new submission, 2026-10) for a device screen recording plus five
answers, to be sent in the App Store Connect reply **and** pasted into
**App Review Information → Notes** so future submissions carry them. The text
below is that reply. Check the bracketed items before sending.

---

Hello, thank you for the review. Answers below; the screen recording is attached.

**1. Screen recording**
Attached: recorded on an iPhone [model] running iOS [version], starting from a
cold launch. It shows the tracking permission prompt, the main menu, level
selection, a full level (placing, upgrading and selling towers, using a booster,
healing the base, the victory screen), the in-game store including a sandbox
purchase of a coin pack, an optional rewarded ad, and the settings screen with
the privacy policy link.
The app has no account registration or login and no user-generated content.

**2. Purpose and target audience**
Fungi vs Bacteria is an offline single-player tower-defense game. The player
grows fungus towers along a path to stop waves of bacteria from reaching their
base, across 70 levels in 7 environments. Each level is scored with up to three
stars. It is a casual strategy game for a general audience aged 13 and over who
want short, replayable puzzle-strategy sessions. It is not directed at children.

**3. How to access the main features**
No login, credentials or sample files are needed. Tap PLAY on the main menu to
start the next level; the first level is available immediately and each won
level unlocks the next. Coins earned in levels are spent in the STORE (from the
main menu) on boosters. Optional coin packs are sold as consumable
in-app purchases; "Remove Ads" is bought with in-game coins, not real money.

**4. External services**
- Apple StoreKit, through RevenueCat (purchase handling and receipt
  validation for the consumable coin packs)
- Unity LevelPlay (ad mediation for interstitial and opt-in rewarded ads)
- Google AdMob, including Google's User Messaging Platform for the consent
  form in the EEA and UK
- Apple App Tracking Transparency for the tracking permission prompt

The app has no server or analytics of its own; game progress is stored only on
the device. It uses no AI services.

**5. Regional differences**
The game's features and content are the same in every region. The only
differences are the ad consent form, which appears only in the EEA and UK as
the law requires, in-app purchase prices, which come from the App Store in the
local currency, and ad availability, which depends on the ad networks.

**6. Regulated industry / third-party material**
Not applicable. The app is not in a regulated industry and contains no
protected third-party material. The artwork, audio and levels were made for
this app; the font is Nunito Sans (SIL Open Font License).

---

## Before sending — checklist

- [ ] The four coin packs (`fungivsbacteria.coins.2500` / `10000` / `20000` /
      `50000`) exist in App Store Connect and are **attached to this version**
      under "In-App Purchases and Subscriptions" (Guideline 3.1.1); each has a
      review screenshot. Paid Applications agreement signed.
- [ ] The recorded build is the **release** build (not a Development Build, so
      levels are not all unlocked) and is the same build that is submitted.
- [ ] Ads are out of test mode on the LevelPlay and AdMob dashboards.
- [ ] The age rating and the App Privacy label agree with section 2 and
      DISTRIBUTION.md (13+, tracking declared for the IDFA).
- [ ] The App Store screenshots show gameplay, not only the title or menu (2.3.3).
