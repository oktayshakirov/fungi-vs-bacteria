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

    // Pulled inside first: WaveText's own position is nudged clear of
    // whichever left edge this button ends up with, so it has to be settled
    // before the readout can be placed relative to it.
    if (pauseButton != null)
    {
      UiSkin.StyleButton(pauseButton, UiSkin.Neutral, UiSkin.RadiusButton);
      PullInside((RectTransform)pauseButton.transform);
    }

    StyleWaveReadout(waveText, timerText, pauseButton != null ? (RectTransform)pauseButton.transform : null);

    if (startWaveButton != null)
    {
      UiSkin.StyleButton(startWaveButton, UiSkin.Primary, UiSkin.RadiusButton);
      MoveToBottomLeft((RectTransform)startWaveButton.transform);
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

  // Minimum clear space to leave between the wave readout and whatever sits
  // to its right (the pause button in the real HUD).
  private const float WaveReadoutClearance = 24f;

  private static void StyleWaveReadout(TMP_Text waveText, TMP_Text timerText, RectTransform pauseButton)
  {
    if (waveText != null)
    {
      // WaveText and the pause button are two independently-positioned scene
      // elements (-450 from the right edge vs a 330-wide button pulled to
      // -14) that landed 4 units into each other once the backdrop's own
      // padding was added on top. Nudged left of the button's ACTUAL edge
      // (post-PullInside) rather than a second hardcoded offset, so the two
      // cannot drift back out of sync the next time either one changes.
      if (pauseButton != null)
      {
        float pauseLeftEdge = pauseButton.anchoredPosition.x - pauseButton.rect.width;
        // +9 anticipates the Backdrop() padding added below, which extends
        // past waveText's own rect on every side.
        float waveRightEdge = waveText.rectTransform.anchoredPosition.x
                             + waveText.rectTransform.rect.width * 0.5f + 9f;
        float overlap = waveRightEdge - (pauseLeftEdge - WaveReadoutClearance);
        if (overlap > 0f)
        {
          Vector2 pos = waveText.rectTransform.anchoredPosition;
          waveText.rectTransform.anchoredPosition = new Vector2(pos.x - overlap, pos.y);
          if (timerText != null)
          {
            Vector2 timerPos = timerText.rectTransform.anchoredPosition;
            timerText.rectTransform.anchoredPosition = new Vector2(timerPos.x - overlap, timerPos.y);
          }
        }
      }

      // Narrow horizontal padding: even after the nudge above, a wide backdrop
      // would run back into the pause button.
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

  // Height of the collapse toggle bar at the top of the towers frame; doubles
  // as the frame's collapsed height.
  private const float TowersHeaderHeight = 44f;

  // Wraps the scene's TowersPanel (an authored, screen-anchored slot) in a
  // scrollable, collapsible frame instead of restyling it in place.
  //
  // The scene sizes TowersPanel to a fixed screen region and puts a
  // GridLayoutGroup directly on it with no Mask and no ScrollRect. With eight
  // towers in two fixed columns that grid is taller than the region — cards
  // simply rendered past the panel's own bottom edge, unclipped, straight over
  // the Start Wave button below it. Wrapping the SAME GridLayoutGroup object
  // in a Viewport+ScrollRect fixes the overlap for any number of towers, not
  // just the current eight, without touching TowerUI's serialized
  // buttonContainer reference (still this same RectTransform, just reparented).
  private static void StyleTowersPanel(RectTransform towersPanel)
  {
    if (towersPanel == null) return;

    var grid = towersPanel.GetComponent<GridLayoutGroup>();
    if (grid == null)
    {
      // No cards to scroll (e.g. a preview build with no grid yet) - back it
      // the way this always used to, and stop.
      PullInside(towersPanel);
      var plainBg = towersPanel.GetComponent<Image>();
      if (plainBg == null) plainBg = towersPanel.gameObject.AddComponent<Image>();
      UiSkin.Panel(plainBg, UiSkin.PanelDark, UiSkin.RadiusPanel);
      plainBg.raycastTarget = false;
      UiSkin.AddBorder(towersPanel, UiSkin.RadiusPanel).transform.SetAsFirstSibling();
      return;
    }

    Transform parent = towersPanel.parent;

    // The frame takes over the screen slot TowersPanel used to occupy. It is
    // given a real pixel height (rather than the scene's stretch-to-bottom
    // anchoring) because collapsing later just means swapping between two
    // known heights, not solving for an anchor fraction.
    float expandedHeight = towersPanel.rect.height;

    var frameGo = new GameObject("TowersFrame", typeof(RectTransform));
    var frame = (RectTransform)frameGo.transform;
    frame.SetParent(parent, false);
    frame.SetSiblingIndex(towersPanel.GetSiblingIndex());
    frame.anchorMin = new Vector2(1f, 1f);
    frame.anchorMax = new Vector2(1f, 1f);
    frame.pivot = new Vector2(1f, 1f);
    frame.anchoredPosition = towersPanel.anchoredPosition;

    // Measured from the grid's own settings rather than kept at the scene's
    // authored 330: two 160-wide columns plus padding already came to 340,
    // ten units wider than the panel that held them, so cards were already
    // clipping horizontally before a single card overflowed vertically.
    // Falls back to the authored width for any constraint mode other than the
    // fixed-column one the scene actually uses, where constraintCount is not
    // a column count.
    float width = towersPanel.rect.width;
    if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
    {
      width = Mathf.Max(width,
        grid.padding.left + grid.padding.right
        + grid.constraintCount * grid.cellSize.x
        + Mathf.Max(0, grid.constraintCount - 1) * grid.spacing.x);
    }
    frame.sizeDelta = new Vector2(width, expandedHeight);
    PullInside(frame);

    var frameBg = frameGo.AddComponent<Image>();
    UiSkin.Panel(frameBg, UiSkin.PanelDark, UiSkin.RadiusPanel);
    frameBg.raycastTarget = false;
    UiSkin.AddBorder(frame, UiSkin.RadiusPanel).transform.SetAsFirstSibling();

    // The panel's own background would now double up with the frame's.
    var oldBg = towersPanel.GetComponent<Image>();
    if (oldBg != null) oldBg.enabled = false;

    var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
    var scrollRect = (RectTransform)scrollGo.transform;
    scrollRect.SetParent(frame, false);
    scrollRect.anchorMin = Vector2.zero;
    scrollRect.anchorMax = Vector2.one;
    scrollRect.offsetMin = Vector2.zero;
    scrollRect.offsetMax = new Vector2(0f, -TowersHeaderHeight);

    var viewportGo = new GameObject("Viewport", typeof(RectTransform));
    viewportGo.transform.SetParent(scrollGo.transform, false);
    UiSkin.Stretch((RectTransform)viewportGo.transform);
    viewportGo.AddComponent<RectMask2D>();

    // TowerUI's buttonContainer field still points at this exact RectTransform
    // — reparenting it changes where it sits, not what it is, so the cards it
    // instantiates keep landing in the right place regardless of Start() order
    // between HUDManager and TowerUI.
    towersPanel.SetParent(viewportGo.transform, false);
    towersPanel.anchorMin = new Vector2(0f, 1f);
    towersPanel.anchorMax = new Vector2(1f, 1f);
    towersPanel.pivot = new Vector2(0.5f, 1f);
    towersPanel.anchoredPosition = Vector2.zero;
    // The rect was a POINT anchor on x before this (anchorMin.x == anchorMax.x),
    // where sizeDelta.x IS the width. Stretch-anchoring it here without
    // clearing that stale sizeDelta left the old 330 added ON TOP of the new
    // stretched width (340 * 1 + 330 = 670) - the grid had twice the width it
    // was supposed to and rendered one column with a matching dead gap beside
    // it, instead of the two it actually had room for.
    towersPanel.sizeDelta = new Vector2(0f, towersPanel.sizeDelta.y);

    var fitter = towersPanel.gameObject.AddComponent<ContentSizeFitter>();
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

    var scroll = scrollGo.AddComponent<ScrollRect>();
    scroll.horizontal = false;
    scroll.vertical = true;
    scroll.movementType = ScrollRect.MovementType.Clamped;
    scroll.viewport = (RectTransform)viewportGo.transform;
    scroll.content = towersPanel;

    // AutoHide (not AndExpandViewport - see HANDOFF) fades the bar via a
    // CanvasGroup and leaves the viewport alone. A hidden scrollbar reads as a
    // panel that is simply cropped, with nothing to say there is more below.
    Scrollbar bar = UiSkin.BuildScrollbar(scrollGo.transform);
    scroll.verticalScrollbar = bar;
    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

    BuildCollapseToggle(frame, scrollGo, expandedHeight);
  }

  // A full-width strip at the top of the frame that swaps the towers list for
  // bare board space - "how do I see the board" was one of the explicit asks,
  // and a fixed-height scroll panel otherwise always claims the same chunk of
  // screen even when the player just wants to watch a wave play out.
  private static void BuildCollapseToggle(RectTransform frame, GameObject scrollView, float expandedHeight)
  {
    var go = new GameObject("CollapseToggle", typeof(RectTransform));
    go.transform.SetParent(frame, false);
    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(1f, 1f);
    rect.pivot = new Vector2(0.5f, 1f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(0f, TowersHeaderHeight);
    // Above the scroll view so it stays clickable regardless of scroll offset.
    go.transform.SetAsLastSibling();

    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();
    UiSkin.StyleButton(button, UiSkin.Neutral, UiSkin.RadiusChip);

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    // Text, not a chevron glyph: the TMP atlas is ASCII-only (see HANDOFF), so
    // an arrow character would silently fail to render.
    UiSkin.Label(label, UiSkin.Role.Caption, UiSkin.TextPrimary);
    label.text = "HIDE TOWERS";
    label.alignment = TextAlignmentOptions.Midline;
    label.raycastTarget = false;
    UiSkin.Stretch((RectTransform)labelGo.transform);

    bool collapsed = false;
    button.onClick.AddListener(() =>
    {
      collapsed = !collapsed;
      scrollView.SetActive(!collapsed);
      frame.sizeDelta = new Vector2(frame.sizeDelta.x, collapsed ? TowersHeaderHeight : expandedHeight);
      label.text = collapsed ? "SHOW TOWERS" : "HIDE TOWERS";
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    });
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

  // Start Wave used to share the bottom-right corner with the towers panel,
  // with nothing coordinating the two - exactly what let the grid render over
  // it. The bottom-left corner is otherwise unused by the HUD.
  private static void MoveToBottomLeft(RectTransform rect)
  {
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.zero;
    rect.pivot = Vector2.zero;
    rect.anchoredPosition = new Vector2(EdgeMargin, EdgeMargin);
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
