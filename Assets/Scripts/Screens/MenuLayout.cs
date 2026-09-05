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
  // Verified against an actual render (Tools/UI Preview -> screen-mainmenu.png),
  // not eyeballed in the editor: the vs-battle art plus Play used to leave only
  // ~27 units of clearance above the art and ~100 empty below Play, out of 720
  // - the whole cluster read as pinned to the top with the ground below it
  // unused. Both this and ApplyLogo shift down by the same 37 units, splitting
  // that slack roughly evenly, while keeping the gap between them (and between
  // Play and the bottom edge) exactly as it was - nothing here was placed to
  // clear the art below it, so nothing needs reproving.
  private const float VerticalRebalance = 37f;

  public static void ApplyPlay(Button play)
  {
    if (play == null) return;

    // Bottom-anchored: the character art above is far taller than the 90px Logo
    // rect suggests, and a centre-relative button sat on top of it.
    var rect = (RectTransform)play.transform;
    rect.anchorMin = new Vector2(0.5f, 0f);
    rect.anchorMax = new Vector2(0.5f, 0f);
    rect.pivot = new Vector2(0.5f, 0f);
    rect.anchoredPosition = new Vector2(0f, 86f - VerticalRebalance);
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
    rect.anchoredPosition = new Vector2(-28f, -28f);
    rect.sizeDelta = new Vector2(150f, 150f);   // the gear fills the button
    Press(settings);
  }

  // The vs-battle illustration. Not a button, so DisplaySetup/ScreenTheme find
  // it by name rather than iterating buttons the way Play/Settings are found.
  public static void ApplyLogo(RectTransform logo)
  {
    if (logo == null) return;
    logo.anchoredPosition = new Vector2(logo.anchoredPosition.x, 125f - VerticalRebalance);
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
