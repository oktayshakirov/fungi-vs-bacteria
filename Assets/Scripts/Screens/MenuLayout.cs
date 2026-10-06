using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Placement and tint for the main menu's two buttons.
//
// This is the single source of truth, called from BOTH DisplaySetup (which
// bakes it into the scene) and ScreenTheme (which reapplies it at runtime).
// They used to disagree — the scene kept the authored placeholder layout while
// Start() repositioned everything — so the menu looked different before and
// after pressing play.
//
// The scene's own Play and gear artwork is kept; only position, size and tint
// are set here.
public static class MenuLayout
{
  // The title plate's height, and the band the vs-battle art is fitted into.
  // ApplyLogo measures the space actually left between the title and the PLAY
  // plate and sizes the art to it, so the three never collide on an aspect
  // nobody tested. The canvas scaler matches on HEIGHT (1280x720 reference),
  // so the band is ~424 units tall on a phone and the WIDTH is what runs out
  // first on a 4:3 tablet - both are checked.
  private const float TitleHeight = 76f;
  private const float LogoTopGap = 14f;
  private const float LogoBottomGap = 14f;
  private const float LogoSideMargin = 40f;

  // The art is one square 1024px illustration whose characters run right to
  // its edges, so it is fitted whole rather than cropped. The cap stops it
  // ballooning to fill a tablet's taller band; the floor stops it collapsing
  // to a stamp on a squat window, where clipping a little is the better trade.
  private const float LogoMaxHeight = 470f;
  private const float LogoMinHeight = 240f;

  // Fallback offset from the centre line, used only when the art's parent has
  // no usable rect yet (first frame, before SafeArea has run).
  //
  // The scene authors 125 and an earlier pass here raised it to 162, which put
  // the top of the art off the top of the screen on a real phone - the rect is
  // 90 units tall but the ARTWORK inside it is several times that, so the rect's
  // position says very little about where the art's edges land. Below centre is
  // where it has to sit for the whole cast to be on screen with Play at the
  // bottom - high enough to clear the button, low enough to keep the top of
  // the art on screen. Verified against a render, not the editor: the gap
  // between the two is only ~50 units and the rect gives no hint of it.
  private const float LogoOffset = 24f;

  // One inset for every corner control on the menu (gear, coin chip). Tighter
  // than the 28 the list screens use: those corners hold a labelled BACK plate
  // that needs breathing room, while the menu's corners hold single icons and
  // read as "not quite in the corner" at 28 - and the menu already sits inside
  // a SafeArea, so this is measured from the notch inset, never from the bezel.
  public const float CornerInset = 16f;

  // Play sits this far off the bottom of the safe area. It used to sit at 49
  // with a further ~130 units of dead grass below the art above it, so the
  // whole cluster read as floating in the middle of the screen; dropping the
  // button and raising the art splits the slack to the two ends instead of
  // leaving it in one block between them.
  // The caption strip below takes its room out of the button's HEIGHT, not out
  // of the art above it: inset + height still puts the top of the plate at 180,
  // exactly where a 150-tall button at inset 30 put it. Raising the inset alone
  // pushed the plate up into the characters.
  private const float PlayBottomInset = 64f;
  private const float PlayHeight = 116f;

  // The "next up" caption is its own strip along the bottom of the safe area,
  // under the button rather than inside it: at 100% opacity inside the plate it
  // competed with the word PLAY, and the button reads cleaner with one line.
  private const float NextUpBottomInset = 18f;
  private const float NextUpHeight = 32f;
  private const float NextUpAlpha = 0.72f;
  private const float NextUpSideInset = 222f;

  public static void ApplyPlay(Button play)
  {
    if (play == null) return;

    // Bottom-anchored: the character art above is far taller than the 90px Logo
    // rect suggests, and a centre-relative button sat on top of it.
    var rect = (RectTransform)play.transform;
    rect.anchorMin = new Vector2(0.5f, 0f);
    rect.anchorMax = new Vector2(0.5f, 0f);
    rect.pivot = new Vector2(0.5f, 0f);
    rect.anchoredPosition = new Vector2(0f, PlayBottomInset);
    rect.sizeDelta = new Vector2(620f, PlayHeight);

    var image = play.GetComponent<Image>();
    if (image != null)
    {
      image.color = UiSkin.Primary;
      play.targetGraphic = image;
    }
    Press(play);

    TMP_Text label = play.GetComponentInChildren<TMP_Text>(true);
    if (label == null) return;

    // White, with a dark outline so it still reads on the bright green
    UiSkin.Label(label, UiSkin.Role.DisplayButton, UiSkin.TextPrimary);
    label.fontSizeMin = 48f;
    label.fontSizeMax = 72f;
    label.fontSize = 72f;
    label.characterSpacing = 4f;
    label.outlineWidth = 0.18f;
    label.outlineColor = new Color32(18, 40, 8, 200);

    ApplyNextUp(play, label);
  }

  // "NEXT: VERDANT MEADOW - LEVEL 3" on a dimmed strip along the bottom of the
  // screen, under PLAY, with a play triangle in front of it - so a returning
  // player sees where they are without opening two screens, and without a
  // second full-strength line fighting the word PLAY on the plate itself.
  // With nothing left to play the caption says so.
  private static void ApplyNextUp(Button play, TMP_Text label)
  {
    string text;
    if (LevelProgress.TryGetNextUp(out string env, out int level))
    {
      text = $"NEXT: {EnvironmentInfo.DisplayName(env)} - LEVEL {level}";
    }
    else
    {
      text = "EVERY BIOME CLEARED!";
    }

    // Earlier builds parented this to the button; clear that one out so the
    // caption never renders twice on a scene carried over from them.
    Transform stale = play.transform.Find("NextUp");
    if (stale != null)
    {
      if (Application.isPlaying) Object.Destroy(stale.gameObject);
      else Object.DestroyImmediate(stale.gameObject);
    }

    Transform parent = play.transform.parent;
    if (parent == null) return;

    Transform existing = parent.Find("NextUp");
    GameObject go = existing != null ? existing.gameObject : new GameObject("NextUp", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0f, 0f);
    rect.anchorMax = new Vector2(1f, 0f);
    rect.pivot = new Vector2(0.5f, 0f);
    // Reserve the bottom-left corner for the independent Remove Ads action,
    // and the same width on the right, so the strip stays centred under PLAY.
    // A lopsided inset put the caption's centre ~100 units right of the button.
    rect.offsetMin = new Vector2(NextUpSideInset, NextUpBottomInset);
    rect.offsetMax = new Vector2(-NextUpSideInset, NextUpBottomInset + NextUpHeight);

    // Icon and caption sit on one centred row, so the pair stays centred
    // whatever the biome name's length.
    var row = go.GetComponent<HorizontalLayoutGroup>();
    if (row == null) row = go.AddComponent<HorizontalLayoutGroup>();
    row.childAlignment = TextAnchor.MiddleCenter;
    row.spacing = 10f;
    row.childControlWidth = true;
    row.childControlHeight = true;
    row.childForceExpandWidth = false;
    row.childForceExpandHeight = false;

    var tint = new Color(UiSkin.TextPrimary.r, UiSkin.TextPrimary.g, UiSkin.TextPrimary.b, NextUpAlpha);

    Transform iconTransform = go.transform.Find("Icon");
    Image icon = iconTransform != null
      ? iconTransform.GetComponent<Image>()
      : UiSkin.Icon(go.transform, UiSprites.Play(), tint, NextUpHeight * 0.62f);
    icon.sprite = UiSprites.Play();
    icon.color = tint;
    var iconLayout = icon.GetComponent<LayoutElement>();
    if (iconLayout == null) iconLayout = icon.gameObject.AddComponent<LayoutElement>();
    iconLayout.preferredWidth = NextUpHeight * 0.62f;
    iconLayout.preferredHeight = NextUpHeight * 0.62f;

    Transform captionTransform = go.transform.Find("Caption");
    GameObject captionGo = captionTransform != null
      ? captionTransform.gameObject
      : new GameObject("Caption", typeof(RectTransform));
    captionGo.transform.SetParent(go.transform, false);
    var caption = captionGo.GetComponent<TextMeshProUGUI>();
    if (caption == null) caption = captionGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(caption, UiSkin.Role.ButtonLabel, tint);
    caption.color = tint;
    caption.enableAutoSizing = true;
    caption.fontSizeMin = 14f;
    caption.fontSizeMax = 22f;
    caption.fontSize = 22f;
    caption.text = text;
    caption.alignment = TextAlignmentOptions.Midline;
    caption.outlineWidth = 0.16f;
    caption.outlineColor = new Color32(10, 18, 6, 170);
    caption.raycastTarget = false;

    // The plate now holds one line again, so PLAY fills it.
    var labelRect = label.rectTransform;
    labelRect.anchorMin = Vector2.zero;
    labelRect.anchorMax = Vector2.one;
    labelRect.offsetMin = Vector2.zero;
    labelRect.offsetMax = Vector2.zero;
    label.fontSizeMax = 72f;
    label.alignment = TextAlignmentOptions.Midline;
  }

  public static void ApplySettings(Button settings)
  {
    if (settings == null) return;

    // The scene already has gear artwork on this button; it was only ever
    // mispositioned (x=1969, off the right edge of a 1920 canvas).
    // Inset matches every other corner button in the game (28, see
    // ScreenTheme.CornerButton / ApplyListScreen) - this one used to sit at 34,
    // visibly further from the corner than everywhere else, which read as
    // "not quite in the corner" once the wallet dialog's own close button
    // (at the standard 28) sat right next to it.
    var rect = (RectTransform)settings.transform;
    rect.anchorMin = new Vector2(1f, 1f);
    rect.anchorMax = new Vector2(1f, 1f);
    rect.pivot = new Vector2(1f, 1f);
    rect.anchoredPosition = new Vector2(-CornerInset, -CornerInset);
    rect.sizeDelta = new Vector2(120f, 120f);   // the gear fills the button
    Press(settings);
  }

  // The vs-battle illustration. Not a button, so DisplaySetup/ScreenTheme find
  // it by name rather than iterating buttons the way Play/Settings are found.
  public static void ApplyLogo(RectTransform logo)
  {
    if (logo == null) return;

    // The scene authored this as a 90x90 rect at localScale 5 - it rendered at
    // 450 units while every layout calculation that looked at the rect saw 90,
    // which is exactly how the art and the PLAY plate ended up overlapping.
    // Real size on the rect, scale back to 1, and the numbers mean something.
    logo.localScale = Vector3.one;
    logo.anchorMin = new Vector2(0.5f, 0.5f);
    logo.anchorMax = new Vector2(0.5f, 0.5f);
    logo.pivot = new Vector2(0.5f, 0.5f);

    var image = logo.GetComponent<Image>();
    if (image != null)
    {
      image.preserveAspect = true;   // the fit below is exact, but be explicit
      image.raycastTarget = false;   // it sat over PLAY and ate taps at 450px
    }

    FitLogo(logo);

    // The safe area is resolved over the first frames and again on rotation, so
    // one measurement at Start is not enough - this re-runs the fit whenever
    // the band actually changes size.
    if (logo.GetComponent<MenuLogoFit>() == null) logo.gameObject.AddComponent<MenuLogoFit>();
  }

  // Sizes the art to the space left between the title plate and the PLAY
  // plate, and centres it in that band. Public because MenuLogoFit calls it
  // every time the band changes.
  public static void FitLogo(RectTransform logo)
  {
    if (logo == null) return;
    var parent = logo.parent as RectTransform;
    if (parent == null) return;

    Rect area = parent.rect;
    if (area.height <= 1f || area.width <= 1f)
    {
      // No usable rect yet: keep the authored placement rather than fitting to
      // a degenerate band, and let MenuLogoFit try again next frame.
      logo.anchoredPosition = new Vector2(logo.anchoredPosition.x, LogoOffset);
      return;
    }

    float fromTop = CornerInset + TitleHeight + LogoTopGap;
    float fromBottom = PlayBottomInset + PlayHeight + LogoBottomGap;
    float bandHeight = area.height - fromTop - fromBottom;
    float bandWidth = area.width - LogoSideMargin * 2f;
    if (bandHeight <= 0f || bandWidth <= 0f) return;

    var image = logo.GetComponent<Image>();
    Sprite sprite = image != null ? image.sprite : null;
    float aspect = sprite != null && sprite.rect.height > 0f
      ? sprite.rect.width / sprite.rect.height
      : 1f;

    // Whichever of the two runs out first wins, then the cap and the floor.
    float height = Mathf.Min(bandHeight, bandWidth / aspect, LogoMaxHeight);
    height = Mathf.Max(height, LogoMinHeight);
    logo.sizeDelta = new Vector2(height * aspect, height);

    // anchoredPosition is an offset from the parent's centre here, so the
    // band's centre has to be re-expressed relative to that centre.
    float bandCentreFromBottom = fromBottom + bandHeight * 0.5f;
    logo.anchoredPosition = new Vector2(0f, bandCentreFromBottom - area.height * 0.5f);
  }

  // The game's name across the top, between the coin chip and the gear. The
  // menu showed the vs-battle art and a PLAY button and nothing else: nowhere
  // did it say what the game was called, which is the first thing a store
  // screenshot or a first launch needs to say. Both words share UiSkin's own
  // Primary green - the same colour as PLAY - with "VS" in Gold between them,
  // so the title reads as part of the same system as the rest of the menu
  // instead of the character art's own red/violet, which clashed with it.
  public static void ApplyTitle(Transform root)
  {
    if (root == null) return;
    Transform host = root.Find("SafeArea") ?? root;
    Transform existing = host.Find("GameTitle");
    GameObject go = existing != null ? existing.gameObject : new GameObject("GameTitle", typeof(RectTransform));
    go.transform.SetParent(host, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0.5f, 1f);
    rect.anchorMax = new Vector2(0.5f, 1f);
    rect.pivot = new Vector2(0.5f, 1f);
    rect.anchoredPosition = new Vector2(0f, -CornerInset);
    // Clear of the coin chip (left) and the gear (right) on the narrowest
    // canvas, a 4:3 tablet at 960 units wide.
    rect.sizeDelta = new Vector2(560f, TitleHeight);

    var label = go.GetComponent<TextMeshProUGUI>();
    if (label == null) label = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.Title, UiSkin.TextPrimary);
    label.fontSizeMin = 30f;
    label.fontSizeMax = 64f;
    label.characterSpacing = 2f;
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.alignment = TextAlignmentOptions.Midline;
    label.richText = true;
    // Hex, not the Color structs directly: TMP rich text only takes string tags.
    string green = "#" + ColorUtility.ToHtmlStringRGB(UiSkin.Primary);
    string vs = "#" + ColorUtility.ToHtmlStringRGB(UiSkin.Gold);
    label.text = $"<color={green}>FUNGI</color> <color={vs}><size=70%>VS</size></color> <color={green}>BACTERIA</color>";
    label.outlineWidth = 0.22f;
    label.outlineColor = new Color32(10, 14, 24, 230);
    label.raycastTarget = false;
    go.transform.SetAsLastSibling();
  }

  // Press/disable feedback without touching the button's artwork.
  private static void Press(Button button)
  {
    button.transition = Selectable.Transition.ColorTint;
    var colors = button.colors;
    colors.normalColor = Color.white;
    colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
    colors.pressedColor = new Color(0.82f, 0.82f, 0.86f, 1f);
    colors.selectedColor = Color.white;
    colors.disabledColor = new Color(0.55f, 0.55f, 0.60f, 0.6f);
    colors.fadeDuration = 0.08f;
    button.colors = colors;
  }
}
