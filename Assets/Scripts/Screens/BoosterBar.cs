using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TowerDefense.UI;

// The row of booster buttons along the bottom of the HUD, centred in the strip
// between the tower info panel (bottom-left) and the towers rail with Start
// Wave (right).
//
// It used to be a column down the left edge, under the speed/camera controls -
// which put it straight on top of the STORE button once that was added, and
// forced the buttons down to 46 units to fit the four other controls sharing
// that edge. The bottom strip is the one part of the HUD nothing else wants:
// the board is drawn above it, both thumbs reach it, and the buttons can be
// half as big again as they were.
//
// Only boosters the player OWNS get a button, decided once when the level
// loads. A player who has never bought one sees no bar at all rather than four
// dead buttons advertising the store, and a button that runs out mid-level
// stays put (disabled) so the column never reshuffles under a thumb.
public class BoosterBar : MonoBehaviour
{
  private const float MaxButtonSize = 92f;
  private const float MinButtonSize = 62f;
  private const float Gap = 12f;

  // Kept clear of the furniture at each end of the bottom strip.
  private const float SideGap = 20f;

  private float buttonSize = MaxButtonSize;

  // Where the free strip starts: past the tower info / sell panel, which owns
  // the bottom-left corner and is the panel these buttons themselves open.
  // Its width is fixed, so only its right edge matters here - the row sits
  // BESIDE it, not above it, and its height is irrelevant.
  private static float LeftBound =>
    TowerInfoPanel.BottomInset + TowerInfoPanel.Width + SideGap;

  private readonly Dictionary<BoosterKind, Button> buttons = new Dictionary<BoosterKind, Button>();
  private readonly Dictionary<BoosterKind, TMP_Text> counts = new Dictionary<BoosterKind, TMP_Text>();
  private readonly Dictionary<BoosterKind, Image> icons = new Dictionary<BoosterKind, Image>();
  private Transform panelHost;

  public static BoosterBar Create(Transform parent, RectTransform reference, int slot)
  {
    // `reference` and `slot` are kept for symmetry with the other two HUD
    // controls, but the bar is positioned from the BOTTOM - see Build.

    var owned = new List<BoosterKind>();
    foreach (BoosterKind kind in BoosterCatalog.All)
    {
      if (BoosterInventory.Has(kind)) owned.Add(kind);
    }
    if (owned.Count == 0) return null;

    var go = new GameObject("BoosterBar", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var bar = go.AddComponent<BoosterBar>();
    bar.panelHost = parent;
    bar.Build(owned, reference, slot);
    return bar;
  }

  private void Build(List<BoosterKind> owned, RectTransform reference, int slot)
  {
    // The strip between the info panel and the towers rail, and the row is
    // centred in THAT rather than on the screen: a screen-centred row overlaps
    // the info panel on a 4:3 canvas, where the two ends leave barely 400
    // units between them.
    float canvas = ScreenTheme.LayoutWidth(transform);
    float rightBound = canvas - HudTheme.RightRailSpan - SideGap;
    float band = Mathf.Max(0f, rightBound - LeftBound);
    float centre = (LeftBound + rightBound) * 0.5f;

    // Shrink only if the band cannot take the buttons at full size - a smaller
    // button is still tappable, an overlapping one is not.
    float gaps = Mathf.Max(0, owned.Count - 1) * Gap;
    buttonSize = Mathf.Clamp((band - gaps) / owned.Count, MinButtonSize, MaxButtonSize);
    float width = owned.Count * buttonSize + gaps;

    // If even the minimum size overflows the strip - five boosters on a 4:3
    // canvas, where the two ends leave ~320 units between them - the row keeps
    // its size and is kept on SCREEN instead. It then reaches under the info
    // panel's corner, which is the better failure: that panel is transient and
    // only open while a tower is selected, whereas an unreachably small or
    // half-off-screen booster button is broken all level.
    centre = Mathf.Clamp(centre,
      width * 0.5f + HudTheme.EdgeMargin,
      Mathf.Max(width * 0.5f + HudTheme.EdgeMargin, canvas - width * 0.5f - HudTheme.EdgeMargin));

    var rect = (RectTransform)transform;
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.zero;
    rect.pivot = new Vector2(0.5f, 0f);
    rect.anchoredPosition = new Vector2(centre, HudTheme.EdgeMargin);
    rect.sizeDelta = new Vector2(width, buttonSize);

    var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
    layout.spacing = Gap;
    layout.childAlignment = TextAnchor.MiddleCenter;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = true;

    foreach (BoosterKind kind in owned) BuildButton(kind);
    Refresh();
  }

  private void BuildButton(BoosterKind kind)
  {
    var go = new GameObject(kind.ToString(), typeof(RectTransform));
    go.transform.SetParent(transform, false);
    var element = go.AddComponent<LayoutElement>();
    element.preferredWidth = buttonSize;
    element.preferredHeight = buttonSize;
    element.flexibleWidth = 0f;

    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();
    UiSkin.StyleButton(button, UiSkin.PanelDark, UiSkin.RadiusChip);

    Image icon = UiSkin.Icon(go.transform, BoosterCatalog.Icon(kind),
      BoosterCatalog.Tint(kind), Mathf.Round(buttonSize * 0.52f));
    icon.raycastTarget = false;
    icon.rectTransform.anchoredPosition = new Vector2(0f, 4f);

    // The count rides the bottom-right corner of the button, the way a stack
    // size does in every inventory the player has ever seen.
    var countGo = new GameObject("Count", typeof(RectTransform));
    countGo.transform.SetParent(go.transform, false);
    var count = countGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(count, UiSkin.Role.Caption, UiSkin.TextPrimary);
    count.alignment = TextAlignmentOptions.BottomRight;
    count.raycastTarget = false;
    var countRect = count.rectTransform;
    UiSkin.Stretch(countRect);
    countRect.offsetMin = new Vector2(0f, 2f);
    countRect.offsetMax = new Vector2(-6f, 0f);

    UiSkin.AddBorder((RectTransform)go.transform, UiSkin.RadiusChip, 2.5f);

    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      BoosterPanel.Show(kind, panelHost);
    });

    buttons[kind] = button;
    counts[kind] = count;
    icons[kind] = icon;
  }

  private void Refresh()
  {
    foreach (KeyValuePair<BoosterKind, Button> entry in buttons)
    {
      BoosterKind kind = entry.Key;
      int owned = BoosterInventory.Count(kind);
      bool usable = BoosterEffects.CanUse(kind);

      entry.Value.interactable = usable;
      counts[kind].text = owned > 0 ? "x" + owned : string.Empty;

      // Dimmed rather than hidden when it cannot be used, so the column keeps
      // its shape and the player can still tap it to read WHY (the panel says
      // "Used this wave" on the button itself).
      Color tint = BoosterCatalog.Tint(kind);
      icons[kind].color = usable ? tint : new Color(tint.r, tint.g, tint.b, 0.4f);
    }
  }

  private void OnEnable()
  {
    BoosterInventory.OnChanged += Refresh;
    BoosterEffects.OnUsageChanged += Refresh;
  }

  private void OnDisable()
  {
    BoosterInventory.OnChanged -= Refresh;
    BoosterEffects.OnUsageChanged -= Refresh;
  }

  // The per-wave limits free up when a wave starts, and nothing else tells the
  // bar that. Cheap enough to poll: four dictionary reads on a handful of
  // buttons, and only while a level is running.
  private void Update() => Refresh();
}
