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

    // Shares the bottom-left slot with the placement bar, at the same size and
    // in the same position - both are built out of TowerInfoPanel, which is
    // where all of that is decided. The two are mutually exclusive by
    // construction (see TowerPlacement.StartPlacement and
    // HUDManager.ShowTowerActions), so they can never be on screen together.

    private Tower currentTower;
    private TMP_Text sellValueText;
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

      // The sell value goes in the HEADER, where the placement panel puts the
      // build cost: same row, same coin, same place on screen, so the number
      // that answers "what is this tower worth to me right now" never moves.
      // The button below is then just the verb.
      if (sellValueText != null) sellValueText.text = tower.SellValue.ToString();
      sellButtonText.text = "SELL";
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
      // ContentSizeFitter: it is anchored into a corner with an explicit
      // sizeDelta, and a fitter would fight that every frame.
      TowerInfoPanel.Place((RectTransform)transform,
        TowerInfoPanel.BaseHeight + (available ? TowerInfoPanel.ExtraLineHeight : 0f));

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

      TowerInfoPanel.Place((RectTransform)transform, TowerInfoPanel.BaseHeight);
      TowerInfoPanel.Frame(gameObject);

      // The scene's own labels and buttons are REPARENTED into the shared rows
      // rather than recreated, so every reference wired up in the inspector
      // (and the onClick that calls SellTower/UpgradeTower) stays intact.
      TMP_Text name = towerNameText;
      TowerInfoPanel.Header(transform, ref name, out sellValueText);

      descriptionText = TowerInfoPanel.Description(transform);
      TowerInfoPanel.Stats(transform, towerStatsText);
      upgradePreviewText = TowerInfoPanel.Note(transform, "UpgradePreviewText");

      // The buttons go into a row of their own so Upgrade sits beside Sell
      // instead of stacking the panel taller.
      Transform actions = TowerInfoPanel.Actions(transform);

      foreach (Button button in GetComponentsInChildren<Button>(true))
      {
        bool isUpgrade = button.name.ToLowerInvariant().Contains("upgrade");
        if (isUpgrade) upgradeButton = button;

        button.transform.SetParent(actions, false);
        TowerInfoPanel.StyleAction(button, isUpgrade ? UiSkin.Primary : UiSkin.Neutral);
      }

      // Order: header, description, stats, preview, buttons. The scene's own
      // order puts the buttons in the middle.
      transform.Find("Header").SetSiblingIndex(1);
      descriptionText.transform.SetSiblingIndex(2);
      if (towerStatsText != null) towerStatsText.transform.SetSiblingIndex(3);
      upgradePreviewText.transform.SetSiblingIndex(4);
      actions.SetAsLastSibling();
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
