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
      rect.anchoredPosition = new Vector2(28f, -28f);
      rect.sizeDelta = new Vector2(210f, 74f);
      UiSkin.StyleButton(back, UiSkin.Neutral, UiSkin.RadiusChip);
    }
  }

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
