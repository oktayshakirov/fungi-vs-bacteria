using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The bar shown while a tower is armed for placement: what the tower is, what
// it does, what it costs, and a way to back out.
//
// It absorbed the tower info box rather than adding a second floating widget:
// the moment a player has picked a tower and is looking for a spot is exactly
// when "what does this one actually do?" matters, and one panel instead of two
// is one less thing that can overlap something else.
//
// Anchored bottom-LEFT and stacked ABOVE the Start Wave button rather than
// bottom-centre, which is where the bare Cancel button used to sit. Centre is
// not safe: the canvas is matched-height so its WIDTH shrinks on a 4:3 tablet,
// and a centred bar there runs into Start Wave on the left and the towers panel
// on the right. The strip above Start Wave is clear on every aspect ratio - the
// speed/camera buttons stop far higher up.
public class PlacementCancelButton : MonoBehaviour
{
  private const float BarWidth = 400f;
  private const float BarHeight = 158f;
  // Start Wave sits at y = 20 and is 75 tall, so this clears it with a margin.
  private const float BottomOffset = 110f;

  private static PlacementCancelButton instance;

  public static void Show(TowerConfig config, Action onCancel)
  {
    if (instance == null)
    {
      Canvas canvas = FindHudCanvas();
      if (canvas == null) return;

      var go = new GameObject("PlacementBar", typeof(RectTransform));
      go.transform.SetParent(canvas.transform, false);
      instance = go.AddComponent<PlacementCancelButton>();
      instance.Build();
    }

    instance.onCancel = onCancel;
    instance.SetTower(config);
    instance.gameObject.SetActive(true);
    instance.transform.SetAsLastSibling();
  }

  public static void Hide()
  {
    if (instance != null) instance.gameObject.SetActive(false);
  }

  private Action onCancel;
  private TMP_Text nameLabel;
  private TMP_Text costLabel;
  private TMP_Text descriptionLabel;
  private TMP_Text statsLabel;

  private void Build()
  {
    var rect = (RectTransform)transform;
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.zero;
    rect.pivot = Vector2.zero;
    rect.anchoredPosition = new Vector2(HudTheme.EdgeMargin, BottomOffset);
    rect.sizeDelta = new Vector2(BarWidth, BarHeight);

    var background = gameObject.AddComponent<Image>();
    UiSkin.Panel(background, UiSkin.PanelDark, UiSkin.RadiusPanel);
    UiSkin.AddBorder(rect, UiSkin.RadiusPanel).transform.SetAsFirstSibling();

    var layout = gameObject.AddComponent<VerticalLayoutGroup>();
    layout.padding = new RectOffset(14, 14, 10, 10);
    layout.spacing = 4f;
    layout.childAlignment = TextAnchor.UpperLeft;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    // --- name + cost on one row
    var headerGo = new GameObject("Header", typeof(RectTransform));
    headerGo.transform.SetParent(transform, false);
    var header = headerGo.AddComponent<HorizontalLayoutGroup>();
    header.spacing = 8f;
    header.childAlignment = TextAnchor.MiddleLeft;
    header.childControlWidth = true;
    header.childControlHeight = true;
    header.childForceExpandWidth = false;
    header.childForceExpandHeight = true;
    headerGo.AddComponent<LayoutElement>().preferredHeight = 34f;

    nameLabel = Label(headerGo.transform, "Name", UiSkin.Role.Heading, UiSkin.TextPrimary);
    nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
    nameLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

    Image coin = UiSkin.Icon(headerGo.transform, UiSprites.Coin(), UiSkin.Gold, 22f);
    var coinElement = coin.gameObject.AddComponent<LayoutElement>();
    coinElement.preferredWidth = 22f;
    coinElement.flexibleWidth = 0f;

    costLabel = Label(headerGo.transform, "Cost", UiSkin.Role.Value, UiSkin.Gold);
    costLabel.alignment = TextAlignmentOptions.MidlineRight;
    costLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 62f;

    // --- what it is for
    descriptionLabel = Label(transform, "Description", UiSkin.Role.Caption, UiSkin.TextPrimary);
    descriptionLabel.alignment = TextAlignmentOptions.TopLeft;
    descriptionLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

    // --- the numbers, which the description deliberately does not repeat
    statsLabel = Label(transform, "Stats", UiSkin.Role.Caption, UiSkin.TextMuted);
    statsLabel.alignment = TextAlignmentOptions.MidlineLeft;
    statsLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

    // --- cancel
    var cancelGo = new GameObject("Cancel", typeof(RectTransform));
    cancelGo.transform.SetParent(transform, false);
    cancelGo.AddComponent<Image>();
    var button = cancelGo.AddComponent<Button>();
    UiSkin.StyleButton(button, UiSkin.Danger, UiSkin.RadiusButton);
    cancelGo.AddComponent<LayoutElement>().preferredHeight = 42f;

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(cancelGo.transform, false);
    var cancelLabel = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(cancelLabel, UiSkin.Role.ButtonLabel);
    cancelLabel.text = "CANCEL";
    cancelLabel.alignment = TextAlignmentOptions.Midline;
    cancelLabel.raycastTarget = false;
    UiSkin.Stretch((RectTransform)labelGo.transform);

    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      onCancel?.Invoke();
    });
  }

  private void SetTower(TowerConfig config)
  {
    if (config == null) return;

    nameLabel.text = config.towerName;
    costLabel.text = config.cost.ToString();
    descriptionLabel.text = string.IsNullOrWhiteSpace(config.description)
      ? "Place it on any free tile."
      : config.description;
    statsLabel.text = StatLine(config);
  }

  // A support tower has no damage or fire rate to report, so listing them as
  // zeroes would read as a broken tower rather than a different KIND of tower.
  private static string StatLine(TowerConfig config)
  {
    if (config.isSupport)
    {
      string boost = config.damageBoost > 0f
        ? $"+{Mathf.RoundToInt(config.damageBoost * 100f)}% damage"
        : $"+{Mathf.RoundToInt(config.fireRateBoost * 100f)}% fire rate";
      return $"Range {config.range:0.#}   {boost}";
    }

    string line = $"Damage {config.damage}   Range {config.range:0.#}   {config.fireRate:0.#}/s";
    if (config.isAoE) line += "   Splash";
    if (config.slowsEnemies) line += "   Slow";
    return line;
  }

  private static TMP_Text Label(Transform parent, string name, UiSkin.Role role, Color color)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var label = go.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, role, color);
    label.raycastTarget = false;
    return label;
  }

  private static Canvas FindHudCanvas()
  {
    Canvas best = null;
    Canvas anyCanvas = null;

    foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
    {
      if (anyCanvas == null || canvas.sortingOrder < anyCanvas.sortingOrder) anyCanvas = canvas;
      if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
      // Prefer the lowest-sorting overlay canvas (the main HUD, not popups)
      if (best == null || canvas.sortingOrder < best.sortingOrder) best = canvas;
    }

    // Falling back to a non-overlay canvas rather than giving up: the HUD is
    // ScreenSpaceOverlay in the real game, but UiPreview renders through a
    // ScreenSpaceCamera canvas (an overlay canvas draws straight to the
    // backbuffer and never lands in a RenderTexture - see UiPreview's header),
    // so without this the bar could not be previewed at all.
    return best != null ? best : anyCanvas;
  }
}
