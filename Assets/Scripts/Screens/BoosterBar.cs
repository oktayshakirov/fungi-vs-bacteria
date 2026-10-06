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
// EVERY booster gets a button, whether it is owned or not. The bar used to be
// built from the owned ones only, which meant the row changed shape between
// levels and a player who had spent everything saw no bar at all - and so had
// no way back in. A booster you do not own is drawn dimmed with no count, and
// tapping it opens the same panel with BUY on it instead of USE. The row is
// therefore the same five buttons in the same places all game.
public class BoosterBar : MonoBehaviour
{
  private const float MaxButtonSize = 92f;
  // Low enough that all FIVE buttons fit the free strip on a 4:3 tablet, where
  // the canvas is only 960 units wide. The bar shows every booster now, so this
  // is no longer the rare five-owned case it was sized for - it is every level.
  private const float MinButtonSize = 48f;
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

  // The countdown dial over a timed booster's button, and its backing disc.
  // Only the three timed boosters have one.
  private readonly Dictionary<BoosterKind, Image> timers = new Dictionary<BoosterKind, Image>();
  private readonly Dictionary<BoosterKind, GameObject> timerHosts =
    new Dictionary<BoosterKind, GameObject>();
  private Transform panelHost;

  public static BoosterBar Create(Transform parent, RectTransform reference, int slot)
  {
    // `reference` and `slot` are kept for symmetry with the other two HUD
    // controls, but the bar is positioned from the BOTTOM - see Build.

    var all = new List<BoosterKind>(BoosterCatalog.All);

    var go = new GameObject("BoosterBar", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var bar = go.AddComponent<BoosterBar>();
    bar.panelHost = parent;
    bar.Build(all, reference, slot);
    return bar;
  }

  private void Build(List<BoosterKind> kinds, RectTransform reference, int slot)
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
    float gaps = Mathf.Max(0, kinds.Count - 1) * Gap;
    buttonSize = Mathf.Clamp((band - gaps) / kinds.Count, MinButtonSize, MaxButtonSize);
    float width = kinds.Count * buttonSize + gaps;

    // If even the minimum size overflows the strip, the row keeps its size and
    // grows LEFT, under the tower info panel's corner. That panel is transient
    // and only open while a tower is selected; Start Wave and the towers rail
    // on the right are there all level, and the row used to be clamped to the
    // SCREEN instead, which parked the last booster underneath Start Wave.
    float minCentre = width * 0.5f + HudTheme.EdgeMargin;
    float maxCentre = rightBound - width * 0.5f;
    centre = Mathf.Clamp(centre, minCentre, Mathf.Max(minCentre, maxCentre));

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

    foreach (BoosterKind kind in kinds) BuildButton(kind);
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
    BuildTimer(kind, go.transform);

    button.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      BoosterPanel.Show(kind, panelHost);
    });

    buttons[kind] = button;
    counts[kind] = count;
    icons[kind] = icon;
  }

  // A small dial above the button that empties as the effect runs out. No
  // numbers on purpose: at five to fifteen seconds the shrinking wedge answers
  // "how much longer" faster than a digit does, and it costs no glyphs on a
  // HUD that is already tight.
  //
  // Hidden whenever nothing is running, so a button with no active effect looks
  // exactly as it did before.
  private void BuildTimer(BoosterKind kind, Transform button)
  {
    bool timed = kind == BoosterKind.FrostWave
                 || kind == BoosterKind.Overclock
                 || kind == BoosterKind.Shield;
    if (!timed) return;

    float size = Mathf.Round(buttonSize * 0.42f);

    var host = new GameObject("Timer", typeof(RectTransform));
    host.transform.SetParent(button, false);
    var hostRect = (RectTransform)host.transform;
    // Sitting ON the top edge of the button rather than clear above it: the bar
    // is already at the bottom margin of the screen, so anything floating free
    // above it would collide with the board.
    hostRect.anchorMin = hostRect.anchorMax = new Vector2(0.5f, 1f);
    hostRect.pivot = new Vector2(0.5f, 0.5f);
    hostRect.anchoredPosition = new Vector2(0f, 2f);
    hostRect.sizeDelta = new Vector2(size, size);

    var disc = host.AddComponent<Image>();
    disc.sprite = UiSprites.Circle();
    disc.color = new Color(0.04f, 0.05f, 0.09f, 0.92f);
    disc.raycastTarget = false;

    var dialGo = new GameObject("Dial", typeof(RectTransform));
    dialGo.transform.SetParent(host.transform, false);
    var dialRect = UiSkin.Stretch((RectTransform)dialGo.transform);
    dialRect.offsetMin = new Vector2(3f, 3f);
    dialRect.offsetMax = new Vector2(-3f, -3f);

    var dial = dialGo.AddComponent<Image>();
    dial.sprite = UiSprites.Circle();
    dial.color = BoosterCatalog.Tint(kind);
    dial.raycastTarget = false;
    // A filled circle sprite wound as a pie: full at the moment of use,
    // emptying clockwise from the top.
    dial.type = Image.Type.Filled;
    dial.fillMethod = Image.FillMethod.Radial360;
    dial.fillOrigin = (int)Image.Origin360.Top;
    dial.fillClockwise = true;
    dial.fillAmount = 1f;

    timers[kind] = dial;
    timerHosts[kind] = host;
    host.SetActive(false);
  }

  private void RefreshTimers()
  {
    foreach (KeyValuePair<BoosterKind, Image> entry in timers)
    {
      float fraction = Mathf.Clamp01(BoosterEffects.ActiveFraction(entry.Key));
      bool running = fraction > 0f;

      if (timerHosts.TryGetValue(entry.Key, out GameObject host)
          && host.activeSelf != running)
      {
        host.SetActive(running);
      }
      if (running) entry.Value.fillAmount = fraction;
    }
  }

  private void Refresh()
  {
    foreach (KeyValuePair<BoosterKind, Button> entry in buttons)
    {
      BoosterKind kind = entry.Key;
      int owned = BoosterInventory.Count(kind);
      bool usable = BoosterEffects.CanUse(kind);

      // Always tappable, even at zero: the panel behind it is the only place
      // that says what the booster does and what it costs, and an owned-but-
      // blocked one still has to be able to say "used this wave". The panel
      // itself decides whether it offers USE or BUY.
      entry.Value.interactable = true;
      // "x0" rather than blank, so an empty slot reads as empty, not unknown.
      counts[kind].text = "x" + owned;

      // Dimmed when it cannot be pressed into service right now - either none
      // are owned, or the per-level/per-wave limit is spent.
      Color tint = BoosterCatalog.Tint(kind);
      icons[kind].color = usable ? tint : new Color(tint.r, tint.g, tint.b, 0.35f);
    }

    RefreshTimers();
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
