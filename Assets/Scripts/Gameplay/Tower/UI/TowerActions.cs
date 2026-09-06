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

    private Tower currentTower;
    private TMP_Text descriptionText;
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

      towerNameText.text = config.towerName;
      sellButtonText.text = $"SELL  +{config.sellValue}";
      descriptionText.text = string.IsNullOrWhiteSpace(config.description)
        ? string.Empty
        : config.description;
      towerStatsText.text = StatLine(config, tower);

      // Upgrades do not exist yet (Tower.Upgrade only logs), so the button is
      // hidden rather than shown dead - a control that visibly does nothing is
      // worse than one that isn't there. It comes back on its own the moment
      // Tower gains a real upgrade level.
      if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);

      gameObject.SetActive(true);
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
          ? $"+{Mathf.RoundToInt(config.damageBoost * 100f)}% damage"
          : $"+{Mathf.RoundToInt(config.fireRateBoost * 100f)}% fire rate";
        return $"Range {config.range:0.#}   {boost} to nearby towers";
      }

      string line = $"Damage {tower.EffectiveDamage}   Range {config.range:0.#}" +
                    $"   {tower.EffectiveFireRate:0.#}/s";
      if (config.isAoE) line += "   Splash";
      if (config.slowsEnemies) line += "   Slow";

      bool buffed = tower.EffectiveDamage != config.damage
                 || !Mathf.Approximately(tower.EffectiveFireRate, config.fireRate);
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

      // The buttons go into a row of their own so a future Upgrade button sits
      // beside Sell instead of stacking the panel taller.
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
      if (currentTower != null)
      {
        currentTower.Upgrade();
      }
    }

    public void Hide()
    {
      currentTower = null;
      gameObject.SetActive(false);
    }
  }
}
