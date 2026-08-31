# Fungi vs Bacteria — Distribution Checklist

## Already configured (in this repo)

- **Identity**: product "Fungi vs Bacteria", company "Oktay Shakirov", version 1.0.0
- **Bundle ID**: `com.shadev.fungivsbacteria` (Android / iOS / Standalone)
- **Orientation**: landscape only (UI is designed at 1920×1080)
- **Android**: IL2CPP + ARM64-only (Play Store compliant), min SDK 23
- **App icon**: default icon wired to `Assets/Sprites/Logos/StartScreen.png` (placeholder —
  consider a dedicated, simpler icon before store submission; busy art reads poorly at 48px)
- **Frame rate**: capped at 60 fps on device (set in GameManager)
- **Builds**: `Tools → Build` menu in the editor, or from the command line:

  ```sh
  Unity -batchmode -nographics -projectPath . -buildTarget Android \
        -executeMethod BuildTools.BuildAndroidAabBatch -logFile build.log
  ```

- **Level pipeline**: `Tools → Level Generator → Generate All Levels` regenerates all 30
  levels; the validator (`Phase1Validator.Validate`) checks paths, waves, and audio wiring.

## You must do (accounts & signing — cannot be automated)

### Google Play
- [ ] Google Play Console account ($25 one-time)
- [ ] Create a keystore (`Player Settings → Publishing Settings`) and **back it up** —
      losing it means losing the ability to update the app
- [ ] Store listing: title, short + full description, category (Strategy)
- [ ] Screenshots (min 2, landscape), feature graphic 1024×500
- [ ] Privacy policy URL (required even without data collection)
- [ ] Data-safety form (this game: no data collected, no third-party SDKs)
- [ ] Content rating questionnaire (should land at PEGI 3 / Everyone)
- [ ] Upload the AAB from `Builds/Android/`

### Apple App Store (second target)
- [ ] Apple Developer Program ($99/year)
- [ ] Build the Xcode project (`Tools → Build → iOS`), open in Xcode, set your signing team
- [ ] App Store Connect listing + screenshots (6.7" and 13" required)
- [ ] Privacy nutrition label (no data collected)

### Before submitting anywhere
- [ ] Playtest the difficulty curve (levels 1, 5, 10, 15, 20) and report tuning needs
- [ ] Replace placeholder art: app icon, environment card sprites (all four environments
      currently share one sprite)
- [ ] Decide on the 4th environment card: hide it or generate levels for it
- [ ] Test on a real Android device: touch placement, safe area on a notched screen,
      performance during the biggest wave (level 30)
- [ ] Optional: replace the synthesized `Assets/Audio/Victory.wav` with a real jingle

## Known gaps (deliberate, post-1.0)

- No touch gesture to cancel tower placement (right-click only) — needs a small UI button
- Unity splash screen is shown (removing it requires a Unity Pro license)
- No analytics/crash reporting — consider Unity Cloud Diagnostics or Firebase Crashlytics
  before a wide release, both require accepting their SDK terms
