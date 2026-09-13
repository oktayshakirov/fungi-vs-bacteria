using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
  // The shared chrome for the two panels that explain a tower:
  //
  //   * PlacementCancelButton - shown while a tower is ARMED, before it is
  //     placed. Header value is what it costs; the action is CANCEL.
  //   * TowerActions          - shown when a tower already on the board is
  //     tapped. Header value is what it sells for; the actions are SELL and
  //     UPGRADE.
  //
  // They are two different components because they answer to different parts of
  // the game, but to the player they are ONE panel that says "here is a tower,
  // here is what it does". They used to be built independently and drifted:
  // different heights, different vertical offsets, one with a cost row and one
  // without - so picking a tower and then tapping a placed one made the box
  // under your thumb jump and change shape. Everything that decides how that
  // box looks and where it sits now lives here, once.
  //
  // The two are mutually exclusive by construction (TowerPlacement.StartPlacement
  // cancels a selection, HUDManager.ShowTowerActions cancels a placement), so
  // they can share the slot without coordinating.
  public static class TowerInfoPanel
  {
    public const float Width = 400f;

    // Name row + description + stat line + action row, at the paddings below.
    public const float BaseHeight = 176f;

    // One extra line, for the upgrade preview on the selected-tower panel.
    public const float ExtraLineHeight = 26f;

    // Bottom-left. That corner was Start Wave's until the towers rail took it
    // over (see HudTheme.PlaceStartWave), which is what freed the whole strip
    // for a panel this size.
    public const float BottomInset = HudTheme.EdgeMargin;

    private const float HeaderHeight = 34f;
    private const float DescriptionHeight = 44f;
    private const float StatsHeight = 22f;
    private const float ActionsHeight = 46f;

    // Anchors the panel in its corner at a given content height. Called on
    // every refresh, not only at build time: the selected-tower panel grows and
    // shrinks by one line as the upgrade preview appears and disappears.
    public static void Place(RectTransform rect, float height)
    {
      rect.anchorMin = Vector2.zero;
      rect.anchorMax = Vector2.zero;
      rect.pivot = Vector2.zero;
      rect.anchoredPosition = new Vector2(HudTheme.EdgeMargin, BottomInset);
      rect.sizeDelta = new Vector2(Width, height);
    }

    // The plate itself: background, border and the vertical stack every row
    // below is laid into.
    public static void Frame(GameObject panel)
    {
      var rect = (RectTransform)panel.transform;

      var background = panel.GetComponent<Image>();
      if (background == null) background = panel.AddComponent<Image>();
      UiSkin.Panel(background, UiSkin.PanelDark, UiSkin.RadiusPanel);
      background.raycastTarget = true;   // don't let taps fall through to the board
      UiSkin.AddBorder(rect, UiSkin.RadiusPanel).transform.SetAsFirstSibling();

      var layout = panel.GetComponent<VerticalLayoutGroup>();
      if (layout == null) layout = panel.AddComponent<VerticalLayoutGroup>();
      layout.padding = new RectOffset(14, 14, 10, 10);
      layout.spacing = 4f;
      layout.childAlignment = TextAnchor.UpperLeft;
      layout.childControlWidth = true;
      layout.childControlHeight = true;
      layout.childForceExpandWidth = true;
      layout.childForceExpandHeight = false;
    }

    // The top row: the tower's name on the left, a coin and a number on the
    // right. `name` is an existing label where the caller already has one (the
    // selected-tower panel's is authored in the scene and wired by reference);
    // pass null to have one built.
    public static void Header(Transform panel, ref TMP_Text name, out TMP_Text value)
    {
      var rowGo = new GameObject("Header", typeof(RectTransform));
      rowGo.transform.SetParent(panel, false);
      var row = rowGo.AddComponent<HorizontalLayoutGroup>();
      row.spacing = 8f;
      row.childAlignment = TextAnchor.MiddleLeft;
      row.childControlWidth = true;
      row.childControlHeight = true;
      row.childForceExpandWidth = false;
      row.childForceExpandHeight = true;
      rowGo.AddComponent<LayoutElement>().preferredHeight = HeaderHeight;

      if (name == null) name = Label(rowGo.transform, "Name", UiSkin.Role.Heading, UiSkin.TextPrimary);
      else name.transform.SetParent(rowGo.transform, false);

      UiSkin.Label(name, UiSkin.Role.Heading, UiSkin.TextPrimary);
      name.alignment = TextAlignmentOptions.MidlineLeft;
      name.textWrappingMode = TextWrappingModes.NoWrap;
      name.raycastTarget = false;
      // Shrinks rather than running into the coin: the selected-tower panel
      // appends a tier ("Ice Tower   Lv 2"), which is the longest this row
      // ever gets.
      name.enableAutoSizing = true;
      name.fontSizeMin = 20f;
      name.fontSizeMax = name.fontSize;
      Element(name.gameObject).flexibleWidth = 1f;

      Image coin = UiSkin.Icon(rowGo.transform, UiSprites.Coin(), UiSkin.Gold, 22f);
      var coinElement = coin.gameObject.AddComponent<LayoutElement>();
      coinElement.preferredWidth = 22f;
      coinElement.flexibleWidth = 0f;

      value = Label(rowGo.transform, "Value", UiSkin.Role.Value, UiSkin.Gold);
      value.alignment = TextAlignmentOptions.MidlineRight;
      value.gameObject.AddComponent<LayoutElement>().preferredWidth = 74f;
    }

    // What the tower is for, in words. Deliberately does not repeat the stat
    // line under it.
    public static TMP_Text Description(Transform panel)
    {
      TMP_Text label = Label(panel, "Description", UiSkin.Role.Caption, UiSkin.TextPrimary);
      label.alignment = TextAlignmentOptions.TopLeft;
      Element(label.gameObject).preferredHeight = DescriptionHeight;
      return label;
    }

    // The numbers. `existing` lets the selected-tower panel keep the label the
    // scene wired up instead of building a second one.
    public static TMP_Text Stats(Transform panel, TMP_Text existing = null)
    {
      TMP_Text label = existing;
      if (label == null) label = Label(panel, "Stats", UiSkin.Role.Caption, UiSkin.TextMuted);
      else label.transform.SetParent(panel, false);

      UiSkin.Label(label, UiSkin.Role.Caption, UiSkin.TextMuted);
      label.alignment = TextAlignmentOptions.MidlineLeft;
      label.raycastTarget = false;
      Element(label.gameObject).preferredHeight = StatsHeight;
      return label;
    }

    // A single extra line, in the call-to-action colour.
    public static TMP_Text Note(Transform panel, string name)
    {
      TMP_Text label = Label(panel, name, UiSkin.Role.Caption, UiSkin.Primary);
      label.alignment = TextAlignmentOptions.MidlineLeft;
      Element(label.gameObject).preferredHeight = ExtraLineHeight - 4f;
      return label;
    }

    // The button row at the foot of the panel. Buttons are added to the
    // returned transform; they share the width evenly.
    public static Transform Actions(Transform panel)
    {
      var rowGo = new GameObject("Actions", typeof(RectTransform));
      rowGo.transform.SetParent(panel, false);
      var row = rowGo.AddComponent<HorizontalLayoutGroup>();
      row.spacing = 8f;
      row.childAlignment = TextAnchor.MiddleCenter;
      row.childControlWidth = true;
      row.childControlHeight = true;
      row.childForceExpandWidth = true;
      row.childForceExpandHeight = true;
      rowGo.AddComponent<LayoutElement>().preferredHeight = ActionsHeight;
      return rowGo.transform;
    }

    // One button in that row, styled the same way in both panels.
    public static void StyleAction(Button button, Color tint)
    {
      UiSkin.StyleButton(button, tint, UiSkin.RadiusButton);
      Element(button.gameObject).flexibleWidth = 1f;

      TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
      if (label == null) return;
      label.alignment = TextAlignmentOptions.Midline;
      label.textWrappingMode = TextWrappingModes.NoWrap;
      label.enableAutoSizing = true;
      label.fontSizeMin = 16f;
      label.fontSizeMax = label.fontSize;
      UiSkin.Stretch(label.rectTransform);
    }

    // The stat line for a tower that has not been built yet, so there is no
    // Tower instance to read effective (buffed, upgraded) numbers off.
    //
    // A support tower has no damage or fire rate to report, so listing them as
    // zeroes would read as a broken tower rather than a different KIND of tower.
    public static string StatLine(TowerConfig config)
    {
      if (config == null) return string.Empty;

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

    private static LayoutElement Element(GameObject go)
    {
      var element = go.GetComponent<LayoutElement>();
      if (element == null) element = go.AddComponent<LayoutElement>();
      return element;
    }
  }
}
