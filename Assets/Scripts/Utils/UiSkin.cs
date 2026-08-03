using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The one place that decides what the interface looks like: palette, corner
// radii, text sizes, and the helpers that apply them. Everything else asks for
// a role ("this is a primary button", "this is a stat value") rather than
// setting colours itself, so the UI stays consistent.
//
// The palette is dark and slightly translucent on purpose: the board underneath
// is bright, saturated green/sand, and light UI panels disappeared into it.
public static class UiSkin
{
  // ---------------------------------------------------------------- palette

  public static readonly Color PanelDark = new Color(0.086f, 0.098f, 0.157f, 0.90f);
  public static readonly Color PanelRaised = new Color(0.145f, 0.165f, 0.243f, 0.95f);
  public static readonly Color PanelBorder = new Color(0.35f, 0.40f, 0.56f, 0.55f);
  public static readonly Color Scrim = new Color(0.04f, 0.05f, 0.09f, 0.78f);

  public static readonly Color Gold = new Color(1f, 0.79f, 0.29f);
  public static readonly Color Health = new Color(1f, 0.42f, 0.44f);
  // Taken from the main menu's Play button so every call-to-action in the game
  // — Play, Start Wave, Resume, Next Level, the loading fill — is one green.
  public static readonly Color Primary = new Color(0.745f, 0.851f, 0f);
  public static readonly Color Danger = new Color(0.87f, 0.33f, 0.33f);
  public static readonly Color Neutral = new Color(0.27f, 0.31f, 0.44f);
  public static readonly Color Accent = new Color(0.38f, 0.72f, 1f);

  public static readonly Color TextPrimary = new Color(0.95f, 0.96f, 0.99f);
  public static readonly Color TextMuted = new Color(0.64f, 0.68f, 0.80f);
  public static readonly Color TextDark = new Color(0.08f, 0.09f, 0.14f);

  public const int RadiusPanel = 20;
  public const int RadiusButton = 16;
  public const int RadiusChip = 24;

  public enum Role { Title, Heading, Body, Value, Caption, ButtonLabel }

  // ---------------------------------------------------------------- panels

  // Turns a plain Image into a rounded panel. Returns it for chaining.
  public static Image Panel(Image image, Color color, int radius = RadiusPanel)
  {
    if (image == null) return null;
    image.sprite = UiSprites.Panel(radius);
    image.type = Image.Type.Sliced;
    image.pixelsPerUnitMultiplier = 1f;
    image.color = color;
    return image;
  }

  // Adds a hairline border as a child, giving panels a defined edge instead of
  // fading into the board behind them.
  public static Image AddBorder(RectTransform parent, int radius = RadiusPanel, float width = 2.5f)
  {
    var go = new GameObject("Border", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rect = (RectTransform)go.transform;
    Stretch(rect);

    var image = go.AddComponent<Image>();
    image.sprite = UiSprites.Outline(radius, width);
    image.type = Image.Type.Sliced;
    image.pixelsPerUnitMultiplier = 1f;
    image.color = PanelBorder;
    image.raycastTarget = false;

    // Without this a border on a layout-group parent is treated as content and
    // eats a slot — on the tower grid it consumed the first card's cell.
    go.AddComponent<LayoutElement>().ignoreLayout = true;
    return image;
  }

  // ---------------------------------------------------------------- buttons

  // Styles an existing Button and its label. `tint` is the fill colour; the
  // sprite's baked shading gives it depth.
  public static void StyleButton(Button button, Color tint, int radius = RadiusButton)
  {
    if (button == null) return;

    var image = button.GetComponent<Image>();
    if (image != null)
    {
      image.sprite = UiSprites.Button(radius);
      image.type = Image.Type.Sliced;
      image.pixelsPerUnitMultiplier = 1f;
      image.color = tint;
      button.targetGraphic = image;
    }

    // Unity's default is a barely-visible grey tint; make press and disable read
    button.transition = Selectable.Transition.ColorTint;
    var colors = button.colors;
    colors.normalColor = Color.white;
    colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
    colors.pressedColor = new Color(0.82f, 0.82f, 0.86f, 1f);
    colors.selectedColor = Color.white;
    colors.disabledColor = new Color(0.55f, 0.55f, 0.60f, 0.6f);
    colors.fadeDuration = 0.08f;
    button.colors = colors;

    TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
    if (label != null)
    {
      Label(label, Role.ButtonLabel, LabelColorFor(tint));
      // Keep text off the rounded corners
      label.margin = new Vector4(14f, 4f, 14f, 4f);
    }
  }

  // Dark text on bright fills, light text on dark ones.
  private static Color LabelColorFor(Color fill)
  {
    float luma = fill.r * 0.299f + fill.g * 0.587f + fill.b * 0.114f;
    return luma > 0.62f ? TextDark : TextPrimary;
  }

  // ---------------------------------------------------------------- text

  public static TMP_Text Label(TMP_Text label, Role role, Color? color = null)
  {
    if (label == null) return null;

    // The display face ("Groovy") carries the game's character; Lato is a plain
    // sans that made most of the UI look like a placeholder. Groovy is used for
    // everything with presence — titles, headings, button labels, stat values —
    // and Lato is kept only for small running text, where a display face costs
    // legibility.
    bool display = role == Role.Title || role == Role.Heading
                || role == Role.ButtonLabel || role == Role.Value;
    UiFont.Apply(label, display);
    label.color = color ?? ColorFor(role);
    label.fontStyle = role == Role.Body || role == Role.Caption ? FontStyles.Normal : FontStyles.Bold;

    // Rendered uppercase rather than rewritten, so the authored copy is
    // untouched. The prefabs mix "Resume game" with "END GAME".
    if (role == Role.ButtonLabel) label.fontStyle |= FontStyles.UpperCase;
    label.characterSpacing = role == Role.Title ? 6f : 0f;

    // Auto-sizing keeps text inside its box across the aspect ratios this game
    // ships on, rather than overflowing on narrow phones. Short labels must not
    // wrap, or auto-sizing splits them onto two lines instead of shrinking to
    // fit the width — which is how "SPEED 1x" spilled out of its button.
    if (role == Role.ButtonLabel || role == Role.Value)
    {
      label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    label.enableAutoSizing = true;
    Vector2 size = SizeFor(role);
    label.fontSizeMin = size.x;
    label.fontSizeMax = size.y;
    label.fontSize = size.y;
    return label;
  }

  private static Color ColorFor(Role role)
  {
    switch (role)
    {
      case Role.Caption: return TextMuted;
      case Role.Value: return Gold;
      default: return TextPrimary;
    }
  }

  private static Vector2 SizeFor(Role role)
  {
    switch (role)
    {
      case Role.Title: return new Vector2(34f, 76f);
      case Role.Heading: return new Vector2(24f, 48f);
      case Role.Value: return new Vector2(20f, 40f);
      case Role.Caption: return new Vector2(14f, 24f);
      case Role.ButtonLabel: return new Vector2(18f, 36f);
      default: return new Vector2(16f, 30f);
    }
  }

  // ---------------------------------------------------------------- layout

  public static RectTransform Stretch(RectTransform rect)
  {
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.offsetMin = Vector2.zero;
    rect.offsetMax = Vector2.zero;
    return rect;
  }

  // Fills an existing GameObject out as an icon + label button. Used by the HUD
  // controls that are built at runtime.
  public static Button IconButton(GameObject host, Sprite icon, Color tint, out TMP_Text label,
    int radius = RadiusChip)
  {
    host.AddComponent<Image>();
    var button = host.AddComponent<Button>();

    var layout = host.AddComponent<HorizontalLayoutGroup>();
    layout.padding = new RectOffset(14, 16, 8, 8);
    layout.spacing = 8;
    layout.childAlignment = TextAnchor.MiddleCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = true;

    Image glyph = Icon(host.transform, icon, TextPrimary, 26f);
    var glyphElement = glyph.gameObject.AddComponent<LayoutElement>();
    glyphElement.preferredWidth = 26f;
    glyphElement.preferredHeight = 26f;
    glyphElement.flexibleWidth = 0f;

    var textGo = new GameObject("Label", typeof(RectTransform));
    textGo.transform.SetParent(host.transform, false);
    label = textGo.AddComponent<TextMeshProUGUI>();
    label.alignment = TextAlignmentOptions.MidlineLeft;
    label.raycastTarget = false;
    textGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

    StyleButton(button, tint, radius);
    glyph.color = LabelColorFor(tint);
    return button;
  }

  // Creates a child RectTransform with an Image, e.g. for icons.
  public static Image Icon(Transform parent, Sprite sprite, Color color, float size)
  {
    var go = new GameObject("Icon", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rect = (RectTransform)go.transform;
    rect.sizeDelta = new Vector2(size, size);

    var image = go.AddComponent<Image>();
    image.sprite = sprite;
    image.color = color;
    image.raycastTarget = false;
    image.preserveAspect = true;
    return image;
  }
}
