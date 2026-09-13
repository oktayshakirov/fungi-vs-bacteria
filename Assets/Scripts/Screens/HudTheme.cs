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

    // The rail has to be measured before Start Wave can be placed under it, so
    // the two cannot drift apart the way they did when each carried its own
    // hardcoded corner.
    Rail rail = StyleTowersPanel(towersPanel);

    if (startWaveButton != null)
    {
      UiSkin.StyleButton(startWaveButton, UiSkin.Primary, UiSkin.RadiusButton);
      PlaceStartWave((RectTransform)startWaveButton.transform, rail);
    }
  }

  // Where the towers rail ended up, so Start Wave can sit directly under it at
  // exactly its width. `valid` is false when there is no towers grid to build a
  // rail out of (a preview with no cards), in which case Start Wave falls back
  // to the bottom-left corner it used to own.
  private struct Rail
  {
    public bool valid;
    public Transform host;   // what the rail was parented to; Start Wave joins it
    public float width;
    public float right;    // x of the rail's right edge, from the right edge
    public float bottom;   // y of the rail's bottom edge, from the bottom edge
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

  // --------------------------------------------------------- the towers rail
  //
  // The right-hand rail: a single scrolling column of tower cards with an
  // icon-only collapse toggle above it and Start Wave directly beneath it.
  //
  // It was a TWO-column grid pinned 14 units off the right edge with Start Wave
  // parked in the opposite corner. Two columns of 160 claim 340 units - better
  // than a quarter of the board on a 16:9 phone - for a list that is read one
  // card at a time, and splitting the two controls across opposite corners
  // meant the eye had to cross the whole screen between "which tower" and
  // "go". One column, hard against the right edge, hands that width back to
  // the board and puts both controls in one vertical run.

  // How far the rail sits off the right edge of the SCREEN - not off the safe
  // area. The rail is hoisted out of the SafeArea (see StyleTowersPanel) so it
  // can use the strip the landscape safe-area inset was reserving down the
  // right, which on a notched phone is 40-odd units of empty board that nothing
  // else was ever going to occupy. That inset exists for the notch, which in
  // landscape is on the OTHER side; what is left on this side is the rounded
  // corner, which this margin clears on its own.
  private const float RailInset = 18f;

  // The icon-only collapse toggle, to the LEFT of the rail.
  private const float ToggleSize = 46f;
  private const float ToggleGap = 8f;

  // Width reserved down the right of the rail for the scrollbar.
  private const int ScrollbarGutter = 14;

  // Start Wave, under the rail, at the rail's own width.
  private const float StartWaveHeight = 66f;
  private const float StartWaveGap = 10f;

  // Bigger than it would need to be inside the safe area: the rail is hoisted
  // out of it, so this is the only thing keeping Start Wave off the bottom edge
  // and clear of a home indicator.
  private const float RailBottomMargin = 24f;

  // Wraps the scene's TowersPanel (an authored, screen-anchored slot) in a
  // scrollable frame instead of restyling it in place.
  //
  // The scene sizes TowersPanel to a fixed screen region and puts a
  // GridLayoutGroup directly on it with no Mask and no ScrollRect. With eight
  // towers that grid is taller than the region — cards simply rendered past the
  // panel's own bottom edge, unclipped, over whatever was below. Wrapping the
  // SAME GridLayoutGroup object in a Viewport+ScrollRect fixes the overlap for
  // any number of towers without touching TowerUI's serialized buttonContainer
  // reference (still this same RectTransform, just reparented).
  private static Rail StyleTowersPanel(RectTransform towersPanel)
  {
    var rail = new Rail { valid = false };
    if (towersPanel == null) return rail;

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
      return rail;
    }

    Transform parent = towersPanel.parent;

    // ONE column. The scene authors two; the count is overridden here rather
    // than in the scene so the width below is always derived from what the
    // grid is actually going to lay out.
    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
    grid.constraintCount = 1;
    grid.startAxis = GridLayoutGroup.Axis.Horizontal;
    grid.spacing = new Vector2(0f, 8f);

    // A gutter on the right for the permanent scrollbar, which would otherwise
    // sit on top of the cards.
    grid.padding = new RectOffset(10, 10 + ScrollbarGutter, 10, 10);

    // Squarer cells would only fit three towers in the rail's height. The card
    // lays its name, art and cost out on anchors, so it takes the shorter cell
    // without anything inside it moving.
    grid.cellSize = new Vector2(grid.cellSize.x, Mathf.Min(grid.cellSize.y, 132f));

    float width = grid.padding.left + grid.padding.right + grid.cellSize.x;

    float top = -towersPanel.anchoredPosition.y;          // gap below the pause row
    float frameTop = top;
    float frameBottom = StartWaveHeight + StartWaveGap + RailBottomMargin;

    // Hoisted OUT of the SafeArea, onto the canvas root. Same exception
    // ScreenTheme.Dim makes for full-bleed backgrounds, and for a related
    // reason: the safe area is a rule about where CONTENT can be read, and in
    // landscape it reserves a strip down the right that the notch is not even
    // on. RailInset clears the rounded corner by itself. Everything else in the
    // HUD stays inside the safe area.
    SafeArea safeArea = parent.GetComponentInParent<SafeArea>();
    Transform railHost = safeArea != null && safeArea.transform.parent != null
      ? safeArea.transform.parent
      : parent;

    var frameGo = new GameObject("TowersFrame", typeof(RectTransform));
    var frame = (RectTransform)frameGo.transform;
    frame.SetParent(railHost, false);

    // Stretched top-to-bottom between the toggle and Start Wave, NOT given a
    // measured pixel height. Measuring the canvas here is what the first
    // version did, and it is wrong for a reason worth keeping written down: at
    // the moment HudTheme runs, the canvas has been created but not yet driven
    // through a layout pass, so its rect still reports the raw render size
    // rather than the scaled 720 units - the rail came out hundreds of units
    // too tall and ran off the bottom of the screen, taking the scrollbar with
    // it. Anchors are resolved BY the layout system instead of before it, so
    // they are right on every aspect ratio and at every safe-area inset with
    // nothing to measure.
    frame.anchorMin = new Vector2(1f, 0f);
    frame.anchorMax = new Vector2(1f, 1f);
    frame.pivot = new Vector2(1f, 0.5f);
    frame.offsetMin = new Vector2(-RailInset - width, frameBottom);
    frame.offsetMax = new Vector2(-RailInset, -frameTop);

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
    scrollRect.offsetMax = Vector2.zero;

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
    // clearing that stale sizeDelta left the old width added ON TOP of the new
    // stretched one - the grid had twice the width it was supposed to.
    // Height zeroed as well as width: the scene authors this as a stretched
    // rect with a NEGATIVE sizeDelta.y, which the ContentSizeFitter below then
    // has to overwrite - and until it does, the content rect is inside out.
    towersPanel.sizeDelta = Vector2.zero;

    var fitter = towersPanel.gameObject.AddComponent<ContentSizeFitter>();
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

    var scroll = scrollGo.AddComponent<ScrollRect>();
    scroll.horizontal = false;
    scroll.vertical = true;
    scroll.movementType = ScrollRect.MovementType.Clamped;
    scroll.viewport = (RectTransform)viewportGo.transform;
    scroll.content = towersPanel;

    // PERMANENT, not AutoHide. Dragging on a card starts a tower drag rather
    // than a scroll (see TowerSelectionButton), so the bar is the one control
    // that always scrolls the list - and a bar that only appears once you have
    // already scrolled cannot tell you that you can. Widened for the same
    // reason: it is a handle here, not just an indicator.
    Scrollbar bar = UiSkin.BuildScrollbar(scrollGo.transform);
    var barRect = (RectTransform)bar.transform;
    barRect.sizeDelta = new Vector2(12f, 0f);
    barRect.anchoredPosition = new Vector2(-6f, 0f);
    scroll.verticalScrollbar = bar;
    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

    BuildCollapseToggle(railHost, frame, top, width);

    rail.valid = true;
    rail.host = railHost;
    rail.width = width;
    rail.right = RailInset;
    rail.bottom = RailBottomMargin;
    return rail;
  }

  // A single icon button beside the rail that swaps the towers list for bare
  // board space - "how do I see the board" was one of the explicit asks, and a
  // fixed-height scroll panel otherwise always claims the same chunk of screen
  // even when the player just wants to watch a wave play out.
  //
  // It started as a full-width "HIDE TOWERS" strip across the top of the frame,
  // which spent a whole row of the rail on a label the player reads once, and
  // then sat ABOVE the rail, which cost the list that row anyway. To the LEFT
  // of the rail it costs the list nothing: the board it overlaps is board the
  // rail was already next to. It sits outside the frame so it survives the
  // frame being switched off, and the chevron points the way the rail moves -
  // right to push it away, left to bring it back.
  private static void BuildCollapseToggle(Transform parent, RectTransform frame,
    float top, float railWidth)
  {
    var go = new GameObject("TowersToggle", typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(1f, 1f);
    rect.anchorMax = new Vector2(1f, 1f);
    rect.pivot = new Vector2(1f, 1f);
    rect.anchoredPosition = new Vector2(-(RailInset + railWidth + ToggleGap), -top);
    rect.sizeDelta = new Vector2(ToggleSize, ToggleSize);
    go.transform.SetAsLastSibling();

    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();
    UiSkin.StyleButton(button, UiSkin.Neutral, UiSkin.RadiusChip);

    // A sprite, never a glyph: the TMP atlases in this project are static and
    // ASCII-only, so an arrow character silently renders as a blank box. The
    // sprite is drawn pointing down, so +90 points it right and 270 left.
    Image chevron = UiSkin.Icon(go.transform, UiSprites.Chevron(), UiSkin.TextPrimary, 24f);
    chevron.raycastTarget = false;
    chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);   // push it away

    bool collapsed = false;
    button.onClick.AddListener(() =>
    {
      collapsed = !collapsed;
      frame.gameObject.SetActive(!collapsed);
      chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, collapsed ? 270f : 90f);
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    });
  }

  // Start Wave, directly under the towers rail and exactly as wide as it. It
  // used to sit in the bottom-LEFT corner, which is the slot the tower info and
  // sell panels need; moving it here frees that corner and puts "pick a tower"
  // and "start the wave" in one column instead of one at each end of the HUD.
  private static void PlaceStartWave(RectTransform rect, Rail rail)
  {
    if (!rail.valid)
    {
      rect.anchorMin = Vector2.zero;
      rect.anchorMax = Vector2.zero;
      rect.pivot = Vector2.zero;
      rect.anchoredPosition = new Vector2(EdgeMargin, EdgeMargin);
      return;
    }

    // Follows the rail out of the SafeArea so the two line up on the same edge;
    // a button left inside it would sit visibly short of the rail above it.
    if (rail.host != null && rect.parent != rail.host) rect.SetParent(rail.host, false);

    // Bottom-anchored for the same reason the rail is stretch-anchored: nothing
    // here is allowed to depend on knowing the canvas height.
    rect.anchorMin = new Vector2(1f, 0f);
    rect.anchorMax = new Vector2(1f, 0f);
    rect.pivot = new Vector2(1f, 0f);
    rect.anchoredPosition = new Vector2(-rail.right, rail.bottom);
    rect.sizeDelta = new Vector2(rail.width, StartWaveHeight);

    // The rail is narrow, so "START WAVE" has to shrink to fit rather than
    // spill out of the plate or wrap to a second line inside a 66-unit button.
    TMP_Text label = rect.GetComponentInChildren<TMP_Text>(true);
    if (label != null)
    {
      label.textWrappingMode = TextWrappingModes.NoWrap;
      label.enableAutoSizing = true;
      label.fontSizeMin = 16f;
      label.fontSizeMax = 30f;
      label.margin = new Vector4(8f, 0f, 8f, 0f);
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
