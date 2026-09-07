using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TowerDefense.UI
{
  // The panel shown when a placed tower is tapped: what it is, what it does,
  // and what you can do with it.
  //
  // The scene authors this rect at 1400x200 anchored to the bottom-left corner
  // - wider than the 1280 canvas and covering the entire bottom strip, right
  // over Start Wave and everything else down there. Rather than re-author the
  // scene, the panel is restyled and re-anchored here at runtime, the same way
  // HudTheme owns the rest of the HUD's appearance. The scene's own children
  // (and their onClick wiring) are reparented, not recreated, so nothing that
  // was hooked up in the inspector comes loose.
  public class TowerActions : MonoBehaviour
  {
    [SerializeField] private TextMeshProUGUI sellButtonText;
    [SerializeField] private TextMeshProUGUI towerNameText;
    [SerializeField] private TextMeshProUGUI towerStatsText;

    // Shares the bottom-left slot with the placement bar. The two are mutually
    // exclusive by construction (see TowerPlacement.StartPlacement and
    // HUDManager.ShowTowerActions), so they can never be on screen together.
    private const float PanelWidth = 400f;
    private const float PanelHeight = 176f;
    private const float BottomOffset = 110f;

    // Grown by exactly one line when the next tier is previewed. Without that
    // preview the panel offers, say, "UPGRADE 788" on a tower that cost 150 and
    // sells for 472, and the price reads as nonsense - the upgrade curve is
    // deliberately steep (see TowerConfig) and the only thing that makes it
    // legible is showing what the money actually buys.
    private const float PreviewLineHeight = 26f;

    private Tower currentTower;
    private TMP_Text descriptionText;
    private TMP_Text upgradePreviewText;
    private Button upgradeButton;
    private bool built;

    private void Awake()
    {
      // Hide panel initially
      gameObject.SetActive(false);
    }

    public void ShowForTower(Tower tower)
    {
      currentTower = tower;
      if (currentTower == null) return;

      var config = tower.GetTowerConfig();
      if (config == null) return;

      Build();

      // The tier is spelled out rather than shown as pips: the TMP atlases are
      // static and ASCII-only, so there are no star or dot glyphs to use.
      towerNameText.text = tower.MaxLevel > 1
        ? $"{config.towerName}   Lv {tower.Level}"
        : config.towerName;
      sellButtonText.text = $"SELL  +{tower.SellValue}";
      descriptionText.text = string.IsNullOrWhiteSpace(config.description)
        ? string.Empty
        : config.description;
      towerStatsText.text = StatLine(config, tower);

      RefreshUpgradeButton(tower);

      gameObject.SetActive(true);
    }

    // Gold arrives continuously while the panel is open - every kill pays out -
    // so affordability is re-checked each frame rather than only on selection,
    // or the button stays dimmed after the player can plainly afford it.
    // Only the interactable flag is touched here; rebuilding the label text
    // every frame would allocate a string per frame for no visible change.
    private void Update()
    {
      if (currentTower == null || upgradeButton == null) return;
      if (!upgradeButton.gameObject.activeSelf) return;

      bool affordable = GameManager.Instance != null &&
                        GameManager.Instance.CanAfford(currentTower.UpgradeCost);
      if (upgradeButton.interactable != affordable) upgradeButton.interactable = affordable;
    }

    // Hidden entirely on a tower that cannot be upgraded at all (maxLevel 1) or
    // has run out of tiers; dimmed but visible when the player simply cannot
    // afford it, so the cost still reads as a goal rather than vanishing.
    private void RefreshUpgradeButton(Tower tower)
    {
      if (upgradeButton == null) return;

      bool available = tower.MaxLevel > 1 && !tower.IsMaxLevel;
      upgradeButton.gameObject.SetActive(available);

      if (upgradePreviewText != null)
      {
        upgradePreviewText.gameObject.SetActive(available);
        if (available) upgradePreviewText.text = NextTierLine(tower);
      }

      // The panel is sized to its content by hand rather than by a
      // ContentSizeFitter: Build() anchors it to the bottom-left corner with an
      // explicit sizeDelta, and a fitter would fight that every frame.
      var rect = (RectTransform)transform;
      rect.sizeDelta = new Vector2(PanelWidth,
        PanelHeight + (available ? PreviewLineHeight : 0f));

      if (!available) return;

      int price = tower.UpgradeCost;
      bool affordable = GameManager.Instance != null && GameManager.Instance.CanAfford(price);
      upgradeButton.interactable = affordable;

      TMP_Text label = upgradeButton.GetComponentInChildren<TMP_Text>(true);
      if (label != null) label.text = $"UPGRADE  {price}";
    }

    // What the next tier actually buys, in the same shape as the stat line
    // above it so the two can be read against each other at a glance.
    private static string NextTierLine(Tower tower)
    {
      TowerConfig config = tower.GetTowerConfig();
      if (config == null) return string.Empty;

      int next = tower.Level + 1;
      if (config.isSupport)
      {
        return config.damageBoost > 0f
          ? $"Next: +{Mathf.RoundToInt(config.DamageBoostAt(next) * 100f)}% damage" +
            $"   Range {config.RangeAt(next):0.#}"
          : $"Next: +{Mathf.RoundToInt(config.FireRateBoostAt(next) * 100f)}% fire rate" +
            $"   Range {config.RangeAt(next):0.#}";
      }

      return $"Next: Damage {config.DamageAt(next)}   Range {config.RangeAt(next):0.#}" +
             $"   {config.FireRateAt(next):0.#}/s";
    }

    // Reports the tower's EFFECTIVE numbers, not its authored ones: a tower
    // standing inside an Aura or Defense tower's radius really is hitting
    // harder than its config says, and the panel claiming otherwise is how a
    // player concludes support towers do nothing.
    private static string StatLine(TowerConfig config, Tower tower)
    {
      if (config.isSupport)
      {
        string boost = config.damageBoost > 0f
          ? $"+{Mathf.RoundToInt(tower.EffectiveDamageBoost * 100f)}% damage"
          : $"+{Mathf.RoundToInt(tower.EffectiveFireRateBoost * 100f)}% fire rate";
        return $"Range {tower.Range:0.#}   {boost} to nearby towers";
      }

      string line = $"Damage {tower.EffectiveDamage}   Range {tower.Range:0.#}" +
                    $"   {tower.EffectiveFireRate:0.#}/s";
      if (config.isAoE) line += "   Splash";
      if (config.slowsEnemies) line += "   Slow";

      // Compared against the tower's OWN tier, not against the config: an
      // upgraded tower is not "buffed", and labelling it so would make the
      // support towers look like they were doing something they are not.
      bool buffed = tower.EffectiveDamage != config.DamageAt(tower.Level)
                 || !Mathf.Approximately(tower.EffectiveFireRate, config.FireRateAt(tower.Level));
      if (buffed) line += "   (buffed)";
      return line;
    }

    // Runs once, on the first tower selected. Deferred rather than done in
    // Awake because the panel starts inactive and a disabled GameObject cannot
    // have its layout rebuilt.
    private void Build()
    {
      if (built) return;
      built = true;

      var rect = (RectTransform)transform;
      rect.anchorMin = Vector2.zero;
      rect.anchorMax = Vector2.zero;
      rect.pivot = Vector2.zero;
      rect.anchoredPosition = new Vector2(HudTheme.EdgeMargin, BottomOffset);
      rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

      var background = GetComponent<Image>();
      if (background == null) background = gameObject.AddComponent<Image>();
      UiSkin.Panel(background, UiSkin.PanelDark, UiSkin.RadiusPanel);
      background.raycastTarget = true;   // don't let taps fall through to the board
      UiSkin.AddBorder(rect, UiSkin.RadiusPanel).transform.SetAsFirstSibling();

      var layout = GetComponent<VerticalLayoutGroup>();
      if (layout == null) layout = gameObject.AddComponent<VerticalLayoutGroup>();
      layout.padding = new RectOffset(14, 14, 10, 10);
      layout.spacing = 4f;
      layout.childAlignment = TextAnchor.UpperLeft;
      layout.childControlWidth = true;
      layout.childControlHeight = true;
      layout.childForceExpandWidth = true;
      layout.childForceExpandHeight = false;

      StyleLabel(towerNameText, UiSkin.Role.Heading, UiSkin.TextPrimary,
        TextAlignmentOptions.MidlineLeft, 32f);

      // Built here rather than in the scene: the scene has no object for it,
      // and adding one by hand would drift from this layout.
      var descGo = new GameObject("TowerDescriptionText", typeof(RectTransform));
      descGo.transform.SetParent(transform, false);
      descriptionText = descGo.AddComponent<TextMeshProUGUI>();
      UiSkin.Label(descriptionText, UiSkin.Role.Caption, UiSkin.TextPrimary);
      descriptionText.alignment = TextAlignmentOptions.TopLeft;
      descriptionText.raycastTarget = false;
      descGo.AddComponent<LayoutElement>().preferredHeight = 44f;

      StyleLabel(towerStatsText, UiSkin.Role.Caption, UiSkin.TextMuted,
        TextAlignmentOptions.MidlineLeft, 22f);

      var previewGo = new GameObject("UpgradePreviewText", typeof(RectTransform));
      previewGo.transform.SetParent(transform, false);
      upgradePreviewText = previewGo.AddComponent<TextMeshProUGUI>();
      UiSkin.Label(upgradePreviewText, UiSkin.Role.Caption, UiSkin.Primary);
      upgradePreviewText.alignment = TextAlignmentOptions.MidlineLeft;
      upgradePreviewText.raycastTarget = false;
      previewGo.AddComponent<LayoutElement>().preferredHeight = PreviewLineHeight - 4f;

      // The buttons go into a row of their own so Upgrade sits beside Sell
      // instead of stacking the panel taller.
      var rowGo = new GameObject("Actions", typeof(RectTransform));
      rowGo.transform.SetParent(transform, false);
      var row = rowGo.AddComponent<HorizontalLayoutGroup>();
      row.spacing = 8f;
      row.childAlignment = TextAnchor.MiddleCenter;
      row.childControlWidth = true;
      row.childControlHeight = true;
      row.childForceExpandWidth = true;
      row.childForceExpandHeight = true;
      rowGo.AddComponent<LayoutElement>().preferredHeight = 46f;

      foreach (Button button in GetComponentsInChildren<Button>(true))
      {
        bool isUpgrade = button.name.ToLowerInvariant().Contains("upgrade");
        if (isUpgrade) upgradeButton = button;

        button.transform.SetParent(rowGo.transform, false);
        UiSkin.StyleButton(button, isUpgrade ? UiSkin.Primary : UiSkin.Neutral,
          UiSkin.RadiusButton);

        var element = button.GetComponent<LayoutElement>();
        if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
          label.alignment = TextAlignmentOptions.Midline;
          label.textWrappingMode = TextWrappingModes.NoWrap;
          UiSkin.Stretch(label.rectTransform);
        }
      }

      // Order: name, description, stats, buttons. The scene's own order puts
      // the buttons in the middle.
      if (towerNameText != null) towerNameText.transform.SetSiblingIndex(1);
      descGo.transform.SetSiblingIndex(2);
      if (towerStatsText != null) towerStatsText.transform.SetSiblingIndex(3);
      previewGo.transform.SetSiblingIndex(4);
      rowGo.transform.SetAsLastSibling();
    }

    private static void StyleLabel(TMP_Text label, UiSkin.Role role, Color color,
      TextAlignmentOptions alignment, float height)
    {
      if (label == null) return;
      UiSkin.Label(label, role, color);
      label.alignment = alignment;
      label.raycastTarget = false;

      var element = label.GetComponent<LayoutElement>();
      if (element == null) element = label.gameObject.AddComponent<LayoutElement>();
      element.preferredHeight = height;
    }

    public void SellTower()
    {
      if (currentTower != null)
      {
        // No Haptics call here: Tower.Sell plays SoundType.Sell, which already
        // fires one through AudioManager. Adding one would double up.
        currentTower.Sell();
      }
      else
      {
        Debug.LogError("Attempted to sell with no currentTower reference!");
      }
    }

    public void UpgradeTower()
    {
      if (currentTower == null) return;
      if (!currentTower.Upgrade()) return;

      // Re-read the whole panel rather than patching the label: the tier, the
      // stat line, the sell value and the next upgrade's price all moved.
      ShowForTower(currentTower);
    }

    public void Hide()
    {
      currentTower = null;
      gameObject.SetActive(false);
    }
  }
}
