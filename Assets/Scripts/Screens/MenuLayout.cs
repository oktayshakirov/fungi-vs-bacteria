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
  public static void ApplyPlay(Button play)
  {
    if (play == null) return;

    // Bottom-anchored: the character art above is far taller than the 90px Logo
    // rect suggests, and a centre-relative button sat on top of it.
    var rect = (RectTransform)play.transform;
    rect.anchorMin = new Vector2(0.5f, 0f);
    rect.anchorMax = new Vector2(0.5f, 0f);
    rect.pivot = new Vector2(0.5f, 0f);
    rect.anchoredPosition = new Vector2(0f, 86f);
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
    var rect = (RectTransform)settings.transform;
    rect.anchorMin = new Vector2(1f, 1f);
    rect.anchorMax = new Vector2(1f, 1f);
    rect.pivot = new Vector2(1f, 1f);
    rect.anchoredPosition = new Vector2(-34f, -34f);
    rect.sizeDelta = new Vector2(150f, 150f);   // the gear fills the button
    Press(settings);
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
