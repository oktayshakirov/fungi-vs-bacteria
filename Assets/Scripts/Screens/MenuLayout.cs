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
  // How far ApplyLogo lifts the vs-battle art off the centre line, on top of
  // the 125 the scene authors. The art and Play used to be shifted DOWN by this
  // same amount together, which moved the pair without changing the gap between
  // them; the two now move apart instead - art up, Play down - because the
  // slack was never above or below the cluster, it was inside it.
  private const float VerticalRebalance = 37f;

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
    // Centre-anchored, so this is an offset from the middle of the screen. The
    // art is far taller than the 90-unit rect suggests; raising it takes back
    // the band Play just vacated instead of opening a hole between the two.
    logo.anchoredPosition = new Vector2(logo.anchoredPosition.x, 125f + VerticalRebalance);
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
