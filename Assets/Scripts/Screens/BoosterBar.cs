using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TowerDefense.UI;

// The column of booster buttons down the left edge of the HUD, between the
// speed/camera controls above and the info panel below.
//
// Left, not right: the right edge is the towers rail and Start Wave, and the
// left column already holds the two other in-level controls, so this continues
// a stack the eye already knows rather than opening a third cluster.
//
// Only boosters the player OWNS get a button, decided once when the level
// loads. A player who has never bought one sees no bar at all rather than four
// dead buttons advertising the store, and a button that runs out mid-level
// stays put (disabled) so the column never reshuffles under a thumb.
public class BoosterBar : MonoBehaviour
{
  private const float MaxButtonSize = 62f;
  private const float MinButtonSize = 46f;
  private const float Gap = 8f;

  // Height the HUD already spends above this bar: the stats chips plus the
  // speed and camera buttons, and a gap. The bar is squeezed into whatever is
  // left between that and the info panel below it.
  private const float TopReserve = 300f;

  private float buttonSize = MaxButtonSize;

  // How far above the bottom the column starts: clear of the info panel that
  // shares this corner, at its tallest (TowerActions grows by one line when it
  // previews an upgrade), plus a gap.
  private static float PanelClearance =>
    TowerInfoPanel.BottomInset + TowerInfoPanel.BaseHeight
    + TowerInfoPanel.ExtraLineHeight + 12f;

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
    // Anchored to the BOTTOM-left and stacked upward, starting just above the
    // slot the tower/booster info panels occupy.
    //
    // It started under the speed and camera buttons, continuing that column
    // downward, and with four boosters owned the last button ran straight into
    // the info panel - which is the panel the bar itself opens. Growing up from
    // a fixed floor means the two can never meet however many boosters are
    // owned, and it puts the buttons nearer the thumb.
    // Sized to the space that is actually free, not to a fixed button size.
    // The left edge of a 720-unit canvas is fully spoken for once a player owns
    // all four: chips, two controls, four boosters and the info panel come to
    // more than the screen, and at a fixed 62 the top button sat on the camera
    // control. Shrinking is the right trade - a smaller button is still
    // tappable, an overlapping one is not.
    float free = ScreenTheme.LayoutHeight(transform) - PanelClearance - TopReserve;
    float gaps = Mathf.Max(0, owned.Count - 1) * Gap;
    buttonSize = Mathf.Clamp((free - gaps) / owned.Count, MinButtonSize, MaxButtonSize);

    var rect = (RectTransform)transform;
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.zero;
    rect.pivot = Vector2.zero;
    rect.anchoredPosition = new Vector2(HudTheme.EdgeMargin, PanelClearance);
    rect.sizeDelta = new Vector2(buttonSize, owned.Count * buttonSize + gaps);

    var layout = gameObject.AddComponent<VerticalLayoutGroup>();
    layout.spacing = Gap;
    layout.childAlignment = TextAnchor.UpperLeft;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    foreach (BoosterKind kind in owned) BuildButton(kind);
    Refresh();
  }

  private void BuildButton(BoosterKind kind)
  {
    var go = new GameObject(kind.ToString(), typeof(RectTransform));
    go.transform.SetParent(transform, false);
    go.AddComponent<LayoutElement>().preferredHeight = buttonSize;

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
