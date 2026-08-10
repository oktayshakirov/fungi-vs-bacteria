using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Restyles the in-game HUD at load, using the elements HUDManager already holds
// references to rather than searching by name.
//
// Done at runtime instead of as scene surgery so it also covers the panels that
// only exist at runtime (the tower cards), and so restyling never needs the
// scene re-saved. The scene keeps owning layout — anchors and positions — while
// this owns appearance.
public static class HudTheme
{
  public static void Apply(
    RectTransform statsPanel, TMP_Text goldText, TMP_Text healthText,
    TMP_Text waveText, TMP_Text timerText,
    Button startWaveButton, Button pauseButton,
    RectTransform towersPanel)
  {
    StyleStats(statsPanel, goldText, healthText);
    StyleWaveReadout(waveText, timerText);

    if (startWaveButton != null)
    {
      UiSkin.StyleButton(startWaveButton, UiSkin.Primary, UiSkin.RadiusButton);
      PullInside((RectTransform)startWaveButton.transform);
    }
    if (pauseButton != null)
    {
      UiSkin.StyleButton(pauseButton, UiSkin.Neutral, UiSkin.RadiusButton);
      PullInside((RectTransform)pauseButton.transform);
    }

    StyleTowersPanel(towersPanel);
  }

  // Gold and health become icon chips. The texts themselves are reparented
  // rather than replaced, so every existing UpdateStats call keeps working.
  private static void StyleStats(RectTransform statsPanel, TMP_Text goldText, TMP_Text healthText)
  {
    if (statsPanel != null)
    {
      // The old flat grey box; the chips carry the background now
      var panelImage = statsPanel.GetComponent<Image>();
      if (panelImage != null) panelImage.enabled = false;

      var row = statsPanel.GetComponent<HorizontalLayoutGroup>();
      if (row == null) row = statsPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
      row.spacing = 10f;
      row.childAlignment = TextAnchor.MiddleLeft;
      row.childControlWidth = false;
      row.childControlHeight = false;
      row.childForceExpandWidth = false;
      row.childForceExpandHeight = false;
      // The scene anchors StatsPanel at x=0, so without this the chips sit hard
      // against the screen edge on any device without a notch inset
      row.padding = new RectOffset((int)EdgeMargin, 0, (int)EdgeMargin, 0);
    }

    Chip(goldText, UiSprites.Coin(), UiSkin.Gold, UiSkin.Gold);
    Chip(healthText, UiSprites.Heart(), UiSkin.Health, UiSkin.TextPrimary);
  }

  private static void Chip(TMP_Text text, Sprite icon, Color iconColor, Color textColor)
  {
    if (text == null) return;

    Transform parent = text.transform.parent;
    int index = text.transform.GetSiblingIndex();

    var chip = new GameObject(text.name + "Chip", typeof(RectTransform));
    chip.transform.SetParent(parent, false);
    chip.transform.SetSiblingIndex(index);

    var chipRect = (RectTransform)chip.transform;
    chipRect.sizeDelta = new Vector2(168f, 62f);

    var bg = chip.AddComponent<Image>();
    UiSkin.Panel(bg, UiSkin.PanelDark, UiSkin.RadiusChip);
    bg.raycastTarget = false;

    var element = chip.AddComponent<LayoutElement>();
    element.preferredWidth = 168f;
    element.preferredHeight = 62f;

    var layout = chip.AddComponent<HorizontalLayoutGroup>();
    layout.padding = new RectOffset(14, 16, 8, 8);
    layout.spacing = 9;
    layout.childAlignment = TextAnchor.MiddleLeft;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = true;

    Image iconImage = UiSkin.Icon(chip.transform, icon, iconColor, 30f);
    var iconElement = iconImage.gameObject.AddComponent<LayoutElement>();
    iconElement.preferredWidth = 30f;
    iconElement.preferredHeight = 30f;
    iconElement.flexibleWidth = 0f;

    text.transform.SetParent(chip.transform, false);
    UiSkin.Label(text, UiSkin.Role.Value, textColor);
    text.alignment = TextAlignmentOptions.MidlineLeft;

    var textElement = text.gameObject.GetComponent<LayoutElement>();
    if (textElement == null) textElement = text.gameObject.AddComponent<LayoutElement>();
    textElement.flexibleWidth = 1f;
    textElement.preferredHeight = 36f;
  }

  private static void StyleWaveReadout(TMP_Text waveText, TMP_Text timerText)
  {
    if (waveText != null)
    {
      // Narrow horizontal padding: the scene puts WaveText only 20px clear of
      // the pause button, so a wide backdrop runs into it
      Backdrop(waveText.rectTransform, UiSkin.PanelDark, UiSkin.RadiusChip, new Vector2(9f, 8f));
      UiSkin.Label(waveText, UiSkin.Role.Heading);
      waveText.alignment = TextAlignmentOptions.Center;
    }

    if (timerText != null)
    {
      // Sits directly over the board with no panel behind it, so muted grey was
      // not readable against the grass
      UiSkin.Label(timerText, UiSkin.Role.Body, UiSkin.TextPrimary);
      timerText.alignment = TextAlignmentOptions.Center;
      // The scene authors this as the placeholder "Timer", which showed on the
      // HUD until the first countdown tick replaced it
      timerText.text = string.Empty;
      timerText.fontStyle = FontStyles.Bold;
      timerText.outlineWidth = 0.18f;
      timerText.outlineColor = new Color32(12, 14, 24, 210);
    }
  }

  private static void StyleTowersPanel(RectTransform towersPanel)
  {
    if (towersPanel == null) return;

    // A parent Image draws behind its children, so this backs the cards without
    // needing an extra object
    PullInside(towersPanel);

    var bg = towersPanel.GetComponent<Image>();
    if (bg == null) bg = towersPanel.gameObject.AddComponent<Image>();
    UiSkin.Panel(bg, UiSkin.PanelDark, UiSkin.RadiusPanel);
    bg.raycastTarget = false;
    UiSkin.AddBorder(towersPanel, UiSkin.RadiusPanel).transform.SetAsFirstSibling();

    // The scene stretches this panel the full height of the screen, leaving a
    // tall empty slab under the last card. Anchor it to the top instead and let
    // the grid drive the height, so the panel ends where the cards do.
    if (towersPanel.GetComponent<GridLayoutGroup>() != null)
    {
      towersPanel.anchorMin = new Vector2(1f, 1f);
      towersPanel.anchorMax = new Vector2(1f, 1f);
      towersPanel.pivot = new Vector2(1f, 1f);

      var fitter = towersPanel.GetComponent<ContentSizeFitter>();
      if (fitter == null) fitter = towersPanel.gameObject.AddComponent<ContentSizeFitter>();
      fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
      fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }
  }

  public const float StackedButtonWidth = 132f;
  public const float StackedButtonHeight = 58f;
  public const float EdgeMargin = 20f;

  // The scene anchors the right-hand HUD at x = +10, i.e. ten units past the
  // screen edge. That was survivable at the old canvas scale; once the UI was
  // scaled up it clipped the panel and both buttons.
  private static void PullInside(RectTransform rect)
  {
    if (rect == null || rect.anchoredPosition.x <= 0f) return;
    rect.anchoredPosition = new Vector2(-14f, rect.anchoredPosition.y);
  }

  // Stacks a runtime HUD button under a reference panel, left edges aligned.
  // These used to sit at hardcoded offsets, which overlapped the stats chips as
  // soon as the scene's StatsPanel was a different height than assumed.
  public static void PlaceUnder(RectTransform rect, RectTransform reference, int slot)
  {
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    rect.sizeDelta = new Vector2(StackedButtonWidth, StackedButtonHeight);

    const float gap = 8f;
    float left = EdgeMargin;
    float top = -EdgeMargin;

    if (reference != null)
    {
      // Convert the reference's top-left into the same anchor space. The extra
      // EdgeMargin matches the padding the stat chips get, so the stack lines
      // up with them rather than with the panel's invisible bounds.
      Vector2 refPivotOffset = new Vector2(
        reference.pivot.x * reference.rect.width,
        (1f - reference.pivot.y) * reference.rect.height);
      left = reference.anchoredPosition.x - refPivotOffset.x + EdgeMargin;
      top = reference.anchoredPosition.y + refPivotOffset.y - reference.rect.height - gap;
    }

    rect.anchoredPosition = new Vector2(left, top - slot * (StackedButtonHeight + gap));
  }

  // A rounded panel placed as the sibling *before* a element, so it draws behind
  // it. A child would draw on top and hide it.
  public static Image Backdrop(RectTransform target, Color color, int radius, Vector2 padding)
  {
    var go = new GameObject(target.name + "Backdrop", typeof(RectTransform));
    go.transform.SetParent(target.parent, false);
    go.transform.SetSiblingIndex(target.GetSiblingIndex());

    var rect = (RectTransform)go.transform;
    rect.anchorMin = target.anchorMin;
    rect.anchorMax = target.anchorMax;
    rect.pivot = target.pivot;
    rect.anchoredPosition = target.anchoredPosition;
    rect.sizeDelta = target.sizeDelta + padding * 2f;

    var image = go.AddComponent<Image>();
    UiSkin.Panel(image, color, radius);
    image.raycastTarget = false;
    return image;
  }
}
