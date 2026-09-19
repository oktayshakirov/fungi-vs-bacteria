using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Restyles the full-screen modals (pause, victory, game over) with the shared
// skin. The three prefabs were authored to the same convention —
//   Root / Background, Root / SafeArea / ScreenTitle, Root / SafeArea / ButtonsPanel
// — so this walks that structure by name instead of guessing which Image is the
// backdrop and which is the card.
public static class ScreenTheme
{
  // `primary` gets the call-to-action colour; every other button on the screen
  // is styled as a neutral secondary.
  public static void Apply(Transform root, Button primary, Color? primaryColor = null)
  {
    if (root == null) return;

    Dim(root);
    Card(root);
    Title(root);

    foreach (Button button in root.GetComponentsInChildren<Button>(true))
    {
      bool isPrimary = button == primary;
      UiSkin.StyleButton(button,
        isPrimary ? (primaryColor ?? UiSkin.Primary) : UiSkin.Neutral,
        UiSkin.RadiusButton);
    }
  }

  // The main menu has its own structure (Logo / Play / Settings over a full
  // background image) rather than the title-and-card layout of the modals.
  //
  // It also re-anchors both buttons: the scene positions them with absolute
  // pixel offsets against a top-left anchor — the settings button sits at
  // x=1969, which falls off the right edge of a 1920-wide canvas.
  public static void ApplyMainMenu(Transform root, Button play, Button settings)
  {
    if (root == null) return;

    // Delegates to MenuLayout, which DisplaySetup also bakes into the scene, so
    // the editor and play mode cannot drift apart.
    MenuLayout.ApplyPlay(play);
    MenuLayout.ApplySettings(settings);

    Transform logo = FindDeep(root, "Logo");
    if (logo != null) MenuLayout.ApplyLogo((RectTransform)logo);
  }


  // The list screens (select environment / select level): a title across the
  // top, a back button in the corner, and a scrollable body. Unlike the modals
  // these have no ButtonsPanel card.
  // dimBackground replaces an opaque placeholder backdrop with a scrim. The
  // level screen's is solid black, which reads as a rendering failure; the
  // environment screen has real art and must be left alone.
  public static void ApplyListScreen(Transform root, Button back, bool dimBackground = false)
  {
    if (root == null) return;

    Title(root);

    if (dimBackground)
    {
      Transform background = FindDeep(root, "Background");
      var image = background != null ? background.GetComponent<Image>() : null;
      if (image != null) image.color = UiSkin.Scrim;
    }

    if (back != null)
    {
      var rect = (RectTransform)back.transform;
      rect.anchorMin = new Vector2(0f, 1f);
      rect.anchorMax = new Vector2(0f, 1f);
      rect.pivot = new Vector2(0f, 1f);
      rect.anchoredPosition = new Vector2(HeaderInset, -HeaderInset);
      rect.sizeDelta = new Vector2(BackButtonWidth, HeaderButtonHeight);
      CornerButton(back);
    }
  }

  // The header band shared by the environment and level screens: a corner
  // button at each end and the title chip centred between them.
  //
  // These are constants rather than three screens' worth of literals because
  // the title chip is SIZED FROM THEM (see HeaderSideReserve). The chip used
  // to clamp at a flat 1000 units wide, which is wider than the gap between
  // the two corner buttons on a 1280-unit canvas - "SELECT ENVIRONMENT" ran
  // straight under BACK, and every fix that only moved the title would have
  // come undone the next time either button was resized.
  public const float HeaderInset = 24f;
  public const float HeaderButtonHeight = 72f;
  public const float BackButtonWidth = 172f;
  public const float HomeButtonWidth = 80f;

  // Clear space the title chip leaves at EACH end of the header. Taken from
  // the wider of the two corner buttons so one number covers both ends, plus
  // the chip's own 26-unit glow and a gap.
  public const float HeaderSideReserve = HeaderInset + BackButtonWidth + 26f + 28f;

  // Settings is neither a modal card nor a list screen: three toggles stacked
  // in the middle with a close button in the corner.
  //
  // The prefab authors Close at (846.9, 365.9) against a *centre* anchor, which
  // needs a canvas wider than 1694x732 to be on screen at all — on the 1280x720
  // reference it sits well past the right edge, so the button was invisible from
  // every entry point. Re-anchoring it to the top-right corner here fixes both
  // the main menu and the in-game pause route in one place.
  public static void ApplySettingsScreen(Transform root, Button close)
  {
    if (root == null) return;

    Dim(root);
    Title(root);
    SpaceSettingsRows(root);

    if (close == null) return;

    var rect = (RectTransform)close.transform;
    rect.anchorMin = new Vector2(1f, 1f);
    rect.anchorMax = new Vector2(1f, 1f);
    rect.pivot = new Vector2(1f, 1f);
    rect.anchoredPosition = new Vector2(-28f, -28f);
    rect.sizeDelta = new Vector2(84f, 84f);

    // The prefab's X icon is kept; only the tint and the press feedback come
    // from the skin, so StyleButton (which would replace the sprite) is not used.
    var image = close.GetComponent<Image>();
    if (image != null)
    {
      image.color = Color.white;
      close.targetGraphic = image;
    }

    close.transition = Selectable.Transition.ColorTint;
    var colors = close.colors;
    colors.normalColor = Color.white;
    colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
    colors.pressedColor = new Color(0.82f, 0.82f, 0.86f, 1f);
    colors.fadeDuration = 0.08f;
    close.colors = colors;
  }

  // The three toggle rows, re-spaced a little tighter and a little higher than
  // the prefab's 80 / -80 / -240. The prefab left the last row's display-font
  // label ~80 units off the bottom edge, which was fine with nothing else on
  // the screen - but the bottom-left corner now holds the Privacy Options
  // button (SettingScreen.BuildPrivacyButton), and at the old spacing its top
  // edge sat about ten units under the VIBRATION label. Offsets from the
  // centre line, so they hold on every aspect ratio.
  private static readonly (string name, float y)[] SettingsRows =
  {
    ("MusicToggle", 100f), ("SFXToggle", -40f), ("Vibration", -180f),
  };

  private static void SpaceSettingsRows(Transform root)
  {
    foreach (var row in SettingsRows)
    {
      var rect = FindDeep(root, row.name) as RectTransform;
      if (rect == null) continue;
      rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, row.y);
    }
  }

  // The backdrop darkens the running game behind the modal.
  private static void Dim(Transform root)
  {
    Transform background = FindDeep(root, "Background");
    if (background == null) return;

    var image = background.GetComponent<Image>();
    if (image == null) return;

    // Hoisted to the canvas root before stretching: the prefabs park this
    // inside SafeArea, so on a notched phone the scrim stopped at the safe-area
    // inset and left the board visible around the modal.
    Canvas canvas = background.GetComponentInParent<Canvas>();
    background.SetParent(canvas != null ? canvas.transform : root, false);
    background.SetAsFirstSibling();

    // Flat colour, no rounding: this one covers the whole screen
    image.sprite = null;
    image.type = Image.Type.Simple;
    image.color = UiSkin.Scrim;

    // BackgroundFill already cover-sizes the rect to the canvas every frame, so
    // stretching here would just fight it. Only take over when it is absent.
    if (background.GetComponent<BackgroundFill>() == null)
    {
      UiSkin.Stretch(image.rectTransform);
    }
  }

  // The buttons sit on a raised rounded card so the modal reads as a dialog
  // rather than as loose buttons floating over a dimmed screen.
  private static void Card(Transform root)
  {
    Transform panel = FindDeep(root, "ButtonsPanel");
    if (panel == null) return;

    var image = panel.GetComponent<Image>();
    if (image == null) image = panel.gameObject.AddComponent<Image>();
    UiSkin.Panel(image, UiSkin.PanelDark, UiSkin.RadiusPanel);
    image.raycastTarget = false;

    // The prefabs size this panel to most of the screen, so it read as a slab
    // with its rounded corners off-screen. Pin it as a compact card near the
    // bottom, leaving the upper area for the title and the victory stars.
    // Lower-middle, not pinned to the bottom: leaves the top for the title and
    // the band around 0.63 for the victory screen's star row.
    var rect = (RectTransform)panel;
    rect.anchorMin = new Vector2(0.5f, 0.40f);
    rect.anchorMax = new Vector2(0.5f, 0.40f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(620f, rect.sizeDelta.y);

    var layout = panel.GetComponent<VerticalLayoutGroup>();
    if (layout == null) layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
    layout.padding = new RectOffset(26, 26, 24, 24);
    layout.spacing = 16f;
    layout.childAlignment = TextAnchor.MiddleCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    // Height follows the buttons rather than the prefab's authored size
    var fitter = panel.GetComponent<ContentSizeFitter>();
    if (fitter == null) fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

    foreach (Button button in panel.GetComponentsInChildren<Button>(true))
    {
      var element = button.GetComponent<LayoutElement>();
      if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
      element.minHeight = 76f;
      element.preferredHeight = 84f;
    }

    UiSkin.AddBorder(rect, UiSkin.RadiusPanel);
  }

  // The shared look for the small navigation buttons that sit in a screen's
  // corners (Back, Home). One definition so every screen's corner controls are
  // identical — they were the last flat grey elements left once the headers and
  // cards picked up the dark-plate-and-edge treatment.
  public static void CornerButton(Button button)
  {
    if (button == null) return;

    var image = button.GetComponent<Image>();
    if (image == null) image = button.gameObject.AddComponent<Image>();
    UiSkin.Panel(image, new Color(0.07f, 0.08f, 0.13f, 0.86f), UiSkin.RadiusChip);
    button.targetGraphic = image;

    // Unity's default press tint is barely visible; make press and disable read.
    button.transition = Selectable.Transition.ColorTint;
    var colors = button.colors;
    colors.normalColor = Color.white;
    colors.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
    colors.pressedColor = new Color(0.78f, 0.78f, 0.84f, 1f);
    colors.selectedColor = Color.white;
    colors.disabledColor = new Color(0.55f, 0.55f, 0.60f, 0.6f);
    colors.fadeDuration = 0.08f;
    button.colors = colors;

    // The label has to be restyled here too. UiSkin.StyleButton used to do this
    // as a side effect, so dropping it left the prefab's own oversized font in
    // place and "BACK" spilled straight out of the plate.
    TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
    if (label != null)
    {
      UiSkin.Label(label, UiSkin.Role.ButtonLabel, UiSkin.TextPrimary);
      label.alignment = TextAlignmentOptions.Midline;
      label.textWrappingMode = TextWrappingModes.NoWrap;
      label.margin = new Vector4(14f, 2f, 14f, 2f);
      label.raycastTarget = false;
      UiSkin.Stretch(label.rectTransform);
    }

    Image border = UiSkin.AddBorder((RectTransform)button.transform, UiSkin.RadiusChip, 2.5f);
    if (border != null) border.color = new Color(0.62f, 0.70f, 0.88f, 0.55f);
  }

  // A header plate behind a screen title, edged and haloed in an accent colour.
  // Shared by the environment and level screens so both headers are identical —
  // the two screens are one flow and looked like two different games when each
  // styled its own title.
  //
  // The label is re-pointed at the CHIP'S OWN RECT and switched to Midline
  // alignment. TMP's plain Center aligns on the font's full line box, which
  // includes descender space the display font never uses, so all-caps text sat
  // visibly high inside the plate. Midline centres on the cap height instead,
  // which is what "looks centred" actually means here.
  public static RectTransform TitleChip(TMP_Text label, Color accent)
  {
    if (label == null) return null;

    var rect = label.rectTransform;

    var chipGo = new GameObject("TitleChip", typeof(RectTransform));
    // A backdrop has to be the sibling BEFORE its target, never a child of it:
    // UI draws parent-then-children, so a child would cover the text it backs.
    chipGo.transform.SetParent(rect.parent, false);
    chipGo.transform.SetSiblingIndex(rect.GetSiblingIndex());

    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.ForceMeshUpdate();

    // The canvas is match-HEIGHT (1280x720 reference), so its WIDTH is whatever
    // the device's aspect ratio makes it - 960 units on a 4:3 tablet, 1600 on a
    // 20:9 phone. A header sized against 1280 is therefore correct on exactly
    // one device; this measures the rect it is actually going into.
    float available = LayoutWidth(rect) - HeaderSideReserve * 2f;
    float width = Mathf.Clamp(label.preferredWidth + 96f, 300f, Mathf.Max(300f, available));
    const float height = 88f;

    // Shrink-to-fit rather than overflow: on the narrowest aspect ratio a long
    // biome name ("Volcanic Ashlands") is wider than the space between the two
    // corner buttons, and NoWrap text simply draws straight out of the plate.
    label.enableAutoSizing = true;
    label.fontSizeMin = 22f;
    label.fontSizeMax = label.fontSize;

    var chip = (RectTransform)chipGo.transform;
    chip.anchorMin = new Vector2(0.5f, 1f);
    chip.anchorMax = new Vector2(0.5f, 1f);
    chip.pivot = new Vector2(0.5f, 1f);
    chip.anchoredPosition = new Vector2(0f, -34f);
    chip.sizeDelta = new Vector2(width, height);

    // Neon halo behind the plate.
    var glowGo = new GameObject("Glow", typeof(RectTransform));
    glowGo.transform.SetParent(chipGo.transform, false);
    var glowRect = (RectTransform)glowGo.transform;
    UiSkin.Stretch(glowRect);
    glowRect.offsetMin = new Vector2(-26f, -26f);
    glowRect.offsetMax = new Vector2(26f, 26f);
    var glow = glowGo.AddComponent<Image>();
    glow.sprite = UiSprites.Glow();
    glow.type = Image.Type.Sliced;
    glow.pixelsPerUnitMultiplier = 1f;
    glow.color = new Color(accent.r, accent.g, accent.b, 0.34f);
    glow.raycastTarget = false;
    glowGo.AddComponent<LayoutElement>().ignoreLayout = true;
    glowGo.transform.SetAsFirstSibling();

    var image = chipGo.AddComponent<Image>();
    UiSkin.Panel(image, new Color(0.05f, 0.06f, 0.10f, 0.80f), UiSkin.RadiusChip);
    image.raycastTarget = false;

    Image border = UiSkin.AddBorder(chip, UiSkin.RadiusChip, 3f);
    if (border != null) border.color = new Color(accent.r, accent.g, accent.b, 0.95f);

    // Match the label to the plate exactly, then centre on the cap height.
    rect.anchorMin = chip.anchorMin;
    rect.anchorMax = chip.anchorMax;
    rect.pivot = chip.pivot;
    rect.anchoredPosition = chip.anchoredPosition;
    rect.sizeDelta = chip.sizeDelta;
    label.alignment = TextAlignmentOptions.Midline;
    label.margin = Vector4.zero;
    label.outlineWidth = 0f;

    return chip;
  }

  private static void Title(Transform root)
  {
    Transform title = FindDeep(root, "ScreenTitle");
    if (title == null) return;

    var label = title.GetComponent<TMP_Text>();
    if (label == null) return;

    UiSkin.Label(label, UiSkin.Role.Title);
    label.alignment = TextAlignmentOptions.Center;
    label.outlineWidth = 0.2f;
    label.outlineColor = new Color32(10, 12, 20, 220);

    // Pinned to the top so it clears the card and, on the victory screen, the
    // star row that sits between them
    var rect = label.rectTransform;
    rect.anchorMin = new Vector2(0.5f, 1f);
    rect.anchorMax = new Vector2(0.5f, 1f);
    rect.pivot = new Vector2(0.5f, 1f);
    rect.anchoredPosition = new Vector2(0f, -54f);
    rect.sizeDelta = new Vector2(900f, 120f);
  }

  // Wraps a runtime-built screen's own content in a SafeArea, mirroring
  // DisplaySetup.EnsureSafeArea for screens that never get that edit-time pass.
  //
  // DisplaySetup only wraps a Canvas it finds inside a scene or a listed
  // prefab. EnvironmentsScreen, LevelsScreen and WalletScreen are none of
  // those: they are instantiated as plain children of the MAIN MENU'S canvas,
  // so DisplaySetup's GetComponentsInChildren<Canvas> search finds nothing to
  // wrap and their content never got the notch/home-indicator inset — the
  // environment cards and level tiles could render right under a notch.
  //
  // Idempotent, and safe to call before the caller's own content exists:
  // BuildBackButton/BuildHomeButton already look for a "SafeArea" child and
  // parent themselves under it, which only ever found one on the two screens
  // that got EnsureSafeArea called via a scene-owned Canvas — never these.
  public static RectTransform EnsureSafeArea(Transform root)
  {
    var existing = root.Find("SafeArea") as RectTransform;
    if (existing != null) return existing;

    var go = new GameObject("SafeArea", typeof(RectTransform));
    var rect = (RectTransform)go.transform;
    rect.SetParent(root, false);
    UiSkin.Stretch(rect);

    // Backgrounds must fill the whole screen, so they stay outside the safe
    // area - same rule DisplaySetup.EnsureSafeArea applies at edit time.
    var children = new List<Transform>();
    foreach (Transform child in root)
    {
      if (child == rect || IsBackground(child)) continue;
      children.Add(child);
    }
    foreach (Transform child in children)
    {
      child.SetParent(rect, false);
    }

    go.AddComponent<SafeArea>();

    // Deliberately NOT SetAsFirstSibling(). SafeArea was appended as root's
    // LAST child above, before Background/Title/etc were touched; once the
    // non-background children are moved OUT of root and into it, root is left
    // as [Background, SafeArea] with SafeArea already last - i.e. already
    // drawn on top, exactly where it needs to be. DisplaySetup's edit-time
    // EnsureSafeArea calls SetAsFirstSibling() too, but it is paired with a
    // SEPARATE HoistBackgrounds pass that unconditionally puts Background back
    // in front afterwards. Without that second pass, forcing SafeArea to the
    // front instead puts it BEHIND Background - the fully-populated safe area
    // silently renders with only the background visible on top of it. Cost a
    // full debug cycle to chase down: every RectTransform involved measured
    // correct right up to the render call, because the bug was never a
    // sizing/anchor problem, only draw order.
    return rect;
  }

  // Mirrors DisplaySetup's edit-time rule of the same name: a screen's own
  // full-bleed background stays outside the safe area so it still covers the
  // notch/rounded-corner regions instead of being inset away from them.
  private static bool IsBackground(Transform t)
  {
    return t.name.ToLowerInvariant().Contains("background");
  }

  // The usable width, in canvas units, for anything laid out under `from`.
  //
  // This is THE number every adaptive layout in the UI is built on. The canvas
  // is match-HEIGHT against a 1280x720 reference, so it is always 720 units
  // tall and its WIDTH is whatever the device's aspect ratio makes it: 960
  // units on a 4:3 tablet, 1280 at 16:9, ~1600 on a 20:9 phone. Anything sized
  // against a literal 1280 is correct on exactly one shape of device and either
  // overflows or leaves a gap on every other.
  //
  // Derived from the canvas rect's ASPECT rather than read off its width, and
  // that is the whole point of this method: at the moment a screen's Start()
  // runs, the canvas exists but has not been through a layout pass, so its rect
  // still reports the raw pixel size rather than the scaled unit size. Reading
  // .width there returns 1920 on a 1080p phone instead of 1280, which is how
  // the title chip came out wide enough to run under the BACK button and the
  // level tiles came out too big for a 4:3 screen. The ratio is the same in
  // either unit, so multiplying it by the reference height gives the right
  // answer whether or not the rect has settled.
  public static float LayoutWidth(Transform from)
  {
    if (from == null) return 1280f;

    Canvas canvas = from.GetComponentInParent<Canvas>();
    if (canvas == null) return 1280f;

    var canvasRect = (RectTransform)(canvas.rootCanvas != null ? canvas.rootCanvas : canvas).transform;
    if (canvasRect.rect.height <= 1f || canvasRect.rect.width <= 1f) return 1280f;

    float referenceHeight = 720f;
    var scaler = canvasRect.GetComponent<CanvasScaler>();
    if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
        && scaler.referenceResolution.y > 1f)
    {
      referenceHeight = scaler.referenceResolution.y;
    }

    float width = referenceHeight * (canvasRect.rect.width / canvasRect.rect.height);

    // Take the notch out. Screen.safeArea is device state, not layout state, so
    // unlike the rect above it is correct from the first frame.
    Rect safe = Screen.safeArea;
    if (Screen.width > 0 && safe.width > 1f && safe.width < Screen.width)
    {
      width *= safe.width / Screen.width;
    }
    return width;
  }

  private static Transform FindDeep(Transform root, string name)
  {
    if (root.name == name) return root;
    for (int i = 0; i < root.childCount; i++)
    {
      Transform found = FindDeep(root.GetChild(i), name);
      if (found != null) return found;
    }
    return null;
  }
}
