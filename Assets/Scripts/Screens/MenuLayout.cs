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
  // Where ApplyLogo puts the vs-battle art, as an offset from the centre line.
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
  private const float PlayBottomInset = 30f;

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
    rect.sizeDelta = new Vector2(620f, 150f);

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
    UiSkin.Label(label, UiSkin.Role.ButtonLabel, UiSkin.TextPrimary);
    label.fontSizeMin = 48f;
    label.fontSizeMax = 84f;
    label.fontSize = 84f;
    label.characterSpacing = 4f;
    label.outlineWidth = 0.18f;
    label.outlineColor = new Color32(18, 40, 8, 200);

    ApplyNextUp(play, label);
  }

  // "NEXT: VERDANT MEADOW - LEVEL 3" across the bottom of the Play button, so a
  // returning player sees where they are without opening two screens. PLAY
  // moves up to make room; with nothing left to play the caption says so.
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

    Transform existing = play.transform.Find("NextUp");
    GameObject go = existing != null ? existing.gameObject : new GameObject("NextUp", typeof(RectTransform));
    go.transform.SetParent(play.transform, false);
    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0f, 0f);
    rect.anchorMax = new Vector2(1f, 0f);
    rect.pivot = new Vector2(0.5f, 0f);
    rect.anchoredPosition = new Vector2(0f, 16f);
    rect.sizeDelta = new Vector2(-60f, 34f);

    var caption = go.GetComponent<TextMeshProUGUI>();
    if (caption == null) caption = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(caption, UiSkin.Role.ButtonLabel, UiSkin.TextPrimary);
    caption.fontSizeMin = 14f;
    caption.fontSizeMax = 24f;
    caption.text = text;
    caption.alignment = TextAlignmentOptions.Midline;
    caption.outlineWidth = 0.18f;
    caption.outlineColor = new Color32(18, 40, 8, 200);
    caption.raycastTarget = false;

    var labelRect = label.rectTransform;
    labelRect.anchorMin = Vector2.zero;
    labelRect.anchorMax = Vector2.one;
    labelRect.offsetMin = new Vector2(0f, 36f);
    labelRect.offsetMax = new Vector2(0f, -4f);
    label.fontSizeMax = 76f;
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
    // Centre-anchored, so this is an offset from the middle of the screen.
    logo.anchoredPosition = new Vector2(logo.anchoredPosition.x, LogoOffset);
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
    rect.sizeDelta = new Vector2(560f, 76f);

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
