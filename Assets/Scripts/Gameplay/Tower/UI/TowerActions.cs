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
    private Button styleButton, sellButton;
    private bool choosingBranch;
    private Transform branchRow;
    private TMP_Text branchHint;
    private Transform priorityRow;
    private TMP_Text priorityHint;
    private readonly Button[] priorityButtons = new Button[3];

    private void Awake()
    {
      // Hide panel initially
      gameObject.SetActive(false);
    }

    public void ShowForTower(Tower tower)
    {
      if(currentTower!=tower) choosingBranch=false;
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

      // The refund is ON the Sell button. It used to sit alone in the header,
      // where the placement panel shows a build cost - but beside "Lv 2" a bare
      // "472" did not say whether it was a price, a refund or a stat.
      if (sellValueText != null)
      {
        sellValueText.gameObject.SetActive(false);
        Transform coin = sellValueText.transform.parent.Find("Icon");
        if (coin != null) coin.gameObject.SetActive(false);
      }
      sellButtonText.text = $"SELL  +{tower.SellValue}";
      descriptionText.text = string.IsNullOrWhiteSpace(config.description)
        ? string.Empty
        : config.description;
      if(tower.IsSupport && config.myceliumLinkBoost>0)
        descriptionText.text=$"Boosts nearby fungi. Adjacent attackers link for +{Mathf.RoundToInt(config.myceliumLinkBoost*100)}% extra " +
          (config.damageBoost>0?"damage.":"fire rate.");
      towerStatsText.text = StatLine(config, tower);

      if(tower.Specialization!=ArcherSpecialization.Balanced)
      {
        towerNameText.text=$"Archer: {tower.Specialization}  Lv {tower.Level}";
        descriptionText.text=ArcherBranches.Description(tower.Specialization);
      }
      if(choosingBranch) descriptionText.text="Choose a style once for this fungus. No extra cost.";
      RefreshSpecialization();
      RefreshUpgradeButton(tower);
      RefreshPriority();

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
      upgradeButton.gameObject.SetActive(available && !choosingBranch);
      if(sellButton!=null)sellButton.gameObject.SetActive(!choosingBranch);

      if (upgradePreviewText != null)
      {
        upgradePreviewText.gameObject.SetActive(available && !choosingBranch);
        if (available) upgradePreviewText.text = NextTierLine(tower);
      }

      // The panel is sized to its content by hand rather than by a
      // ContentSizeFitter: it is anchored into a corner with an explicit
      // sizeDelta, and a fitter would fight that every frame.
      TowerInfoPanel.Place((RectTransform)transform,
        TowerInfoPanel.BaseHeight + (available && !choosingBranch ? TowerInfoPanel.ExtraLineHeight : 0f)
          + (choosingBranch ? 108f : !tower.IsSupport ? 72f : 0f));

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

      string damage = config.Poisons
        ? $"Damage {tower.ProjectedDamageAt(next)}+{tower.ProjectedPoisonDamageAt(next)}"
        : $"Damage {tower.ProjectedDamageAt(next)}";
      return $"Next: {damage}   Range {tower.ProjectedRangeAt(next):0.#}" +
             $"   {tower.ProjectedFireRateAt(next):0.#}/s";
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
        return $"Range {tower.Range:0.#}   {boost}   {tower.MyceliumConnections} linked";
      }

      string damage = config.Poisons
        ? $"Damage {tower.EffectiveDamage}+{tower.ProjectedPoisonDamageAt(tower.Level)}"
        : $"Damage {tower.EffectiveDamage}";
      string line = $"{damage}   Range {tower.Range:0.#}" +
                    $"   {tower.EffectiveFireRate:0.#}/s";
      if (config.isAoE) line += "   Splash";
      if (config.Chains) line += $"   Chains {config.chainTargets + 1}";
      if (config.Poisons) line += "   Poison";
      if (config.slowsEnemies) line += "   Slow";

      // Compared against the tower's OWN tier, not against the config: an
      // upgraded tower is not "buffed", and labelling it so would make the
      // support towers look like they were doing something they are not.
      bool buffed = tower.EffectiveDamage != tower.UnbuffedDamageAt(tower.Level)
                 || !Mathf.Approximately(tower.EffectiveFireRate, tower.FireRate);
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
        if (isUpgrade) upgradeButton = button;else sellButton=button;

        button.transform.SetParent(actions, false);
        TowerInfoPanel.StyleAction(button, isUpgrade ? UiSkin.Primary : UiSkin.Neutral);
      }

      var styleRect=new GameObject("Specialize",typeof(RectTransform));styleRect.transform.SetParent(actions,false);
      styleRect.AddComponent<Image>();styleButton=styleRect.AddComponent<Button>();
      var styleText=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));styleText.transform.SetParent(styleRect.transform,false);
      UiSkin.Label(styleText.GetComponent<TMP_Text>(),UiSkin.Role.Caption);TowerInfoPanel.StyleAction(styleButton,UiSkin.Neutral);
      styleButton.onClick.AddListener(()=>{if(currentTower!=null && currentTower.CanSpecialize){choosingBranch=!choosingBranch;ShowForTower(currentTower);}});

      // Order: header, description, stats, preview, buttons. The scene's own
      // order puts the buttons in the middle.
      transform.Find("Header").SetSiblingIndex(1);
      descriptionText.transform.SetSiblingIndex(2);
      if (towerStatsText != null) towerStatsText.transform.SetSiblingIndex(3);
      upgradePreviewText.transform.SetSiblingIndex(4);
      actions.SetAsLastSibling();
      priorityRow = TowerInfoPanel.Actions(transform, "TargetPriority");
      priorityRow.name = "TargetPriority";
      priorityRow.GetComponent<LayoutElement>().preferredHeight = 38f;
      for (int i = 0; i < priorityButtons.Length; i++)
      {
        TargetPriority mode = (TargetPriority)i;
        var rect = new GameObject(mode.ToString(), typeof(RectTransform));
        rect.transform.SetParent(priorityRow, false);
        rect.AddComponent<Image>();
        var button = rect.AddComponent<Button>();
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(rect.transform, false);
        UiSkin.Stretch((RectTransform)text.transform);
        UiSkin.Label(text.GetComponent<TMP_Text>(), UiSkin.Role.Caption);
        text.GetComponent<TMP_Text>().text = mode.ToString().ToUpperInvariant();
        text.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        TowerInfoPanel.StyleAction(button, UiSkin.Neutral);
        button.onClick.AddListener(() => { currentTower?.SetPriority(mode); RefreshPriority(); });
        priorityButtons[i] = button;
      }
      priorityHint = TowerInfoPanel.Note(transform, "PriorityHint");
      branchRow=TowerInfoPanel.Actions(transform,"ArcherBranches");
      branchRow.GetComponent<LayoutElement>().preferredHeight=56f;
      foreach(var branch in new[]{ArcherSpecialization.Flurry,ArcherSpecialization.Longshot})
      {
        var rect=new GameObject(branch.ToString(),typeof(RectTransform));rect.transform.SetParent(branchRow,false);
        rect.AddComponent<Image>();var button=rect.AddComponent<Button>();
        var text=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(rect.transform,false);
        var label=text.GetComponent<TMP_Text>();UiSkin.Label(label,UiSkin.Role.Caption);
        label.text=branch==ArcherSpecialization.Flurry ? "<b>FLURRY</b>\n+40% rate, -15% reach" : "<b>LONGSHOT</b>\n+35% hit, +25% reach";
        TowerInfoPanel.StyleAction(button,UiSkin.Primary);UiFont.ApplyReadable(label);label.richText=true;label.fontSize=label.fontSizeMax=18;label.fontSizeMin=16;
        button.onClick.AddListener(()=>{if(currentTower!=null && currentTower.Specialize(branch)){choosingBranch=false;ShowForTower(currentTower);}});
      }
      branchHint=TowerInfoPanel.Note(transform,"BranchHint");branchHint.color=UiSkin.TextMuted;
    }

    private void RefreshSpecialization()
    {
      bool choose=currentTower!=null && currentTower.CanSpecialize && choosingBranch;
      bool eligible=currentTower!=null && currentTower.SupportsSpecialization && currentTower.Specialization==ArcherSpecialization.Balanced;
      styleButton.gameObject.SetActive(eligible);styleButton.interactable=eligible && currentTower.CanSpecialize;
      styleButton.GetComponentInChildren<TMP_Text>().text=choose?"BACK":currentTower.Level<2?"STYLE LV 2":"STYLE";
      // Emphasize Upgrade and an available Style choice; Sell/Back stay medium. Keep the three
      // actions compact without shrinking the letters below 16 canvas units.
      foreach(var button in new[]{sellButton,upgradeButton,styleButton})
      {
        if(button==null)continue;
        var label=button.GetComponentInChildren<TMP_Text>(true);if(label==null)continue;
        UiFont.ApplyControl(label,button==upgradeButton);
        if(button==styleButton && !choose && currentTower.CanSpecialize)UiFont.ApplyReadable(label,true);label.fontSize=label.fontSizeMax=eligible?18:26;label.fontSizeMin=16;
      }
      branchRow.gameObject.SetActive(choose);branchHint.gameObject.SetActive(choose);
      branchHint.GetComponent<LayoutElement>().preferredHeight=44;
      branchHint.text="Flurry: -20% hit. Longshot: -25% rate.\nChoose once; no extra cost.";
    }

    private void RefreshPriority()
    {
      bool show = currentTower != null && !currentTower.IsSupport && !choosingBranch;
      priorityRow.gameObject.SetActive(show);
      priorityHint.gameObject.SetActive(show);
      if (!show) return;
      for (int i = 0; i < priorityButtons.Length; i++)
      {
        bool selected=(int)currentTower.Priority == i;
        UiSkin.StyleButton(priorityButtons[i], selected ? UiSkin.Primary : UiSkin.Neutral);
        if(selected)UiFont.ApplyReadable(priorityButtons[i].GetComponentInChildren<TMP_Text>(true),true);
      }
      priorityHint.text = currentTower.Priority == TargetPriority.First ? "Target: closest to the exit along the path"
        : currentTower.Priority == TargetPriority.Strong ? "Target: most health + shield remaining"
        : "Target: closest to this fungus";
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
      currentTower = null;choosingBranch=false;
      gameObject.SetActive(false);
    }
  }
}
