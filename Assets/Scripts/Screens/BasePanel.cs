using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TowerDefense.UI;

// The panel shown when the base is tapped: its health, and two ways to spend
// coins on it - HEAL back to the ceiling, or REINFORCE to raise the ceiling
// (100 -> 150 -> 200), the way a tower's UPGRADE raises its stats. Prices and
// steps live in BaseUpgrades.
//
// Built out of TowerInfoPanel like the tower and booster panels, so it is the
// same plate in the same bottom-left slot, and mutually exclusive with them:
// opening any one of the three closes the other two.
//
// Neither button is ever disabled for being unaffordable - a disabled button
// swallowed the tap and read as a dead screen. Short on coins, a press shakes
// the panel and plays the locked thud; the price is shown in red beforehand.
public class BasePanel : MonoBehaviour
{
  private static BasePanel instance;

  public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

  public static void Show(Transform host)
  {
    if (instance == null)
    {
      if (host == null) return;
      var go = new GameObject("BasePanel", typeof(RectTransform));
      go.transform.SetParent(host, false);
      instance = go.AddComponent<BasePanel>();
      instance.Build();
    }

    instance.gameObject.SetActive(true);
    instance.transform.SetAsLastSibling();
    instance.Refresh();
  }

  public static void Hide()
  {
    if (instance != null) instance.gameObject.SetActive(false);
  }

  private TMP_Text nameLabel;
  private TMP_Text healthLabel;
  private TMP_Text statsLabel;
  private Button healButton;
  private TMP_Text healLabel;
  private Button reinforceButton;
  private TMP_Text reinforceLabel;

  private const float ActionRowHeight = 54f;

  private void Build()
  {
    TowerInfoPanel.Place((RectTransform)transform, TowerInfoPanel.BaseHeight + (ActionRowHeight - 46f));
    TowerInfoPanel.Frame(gameObject);

    TMP_Text name = null;
    TowerInfoPanel.Header(transform, ref name, out healthLabel);
    nameLabel = name;

    // The header's value is health here, not coins: swap its coin for a heart.
    Transform header = transform.Find("Header");
    Image icon = header != null ? header.Find("Icon")?.GetComponent<Image>() : null;
    if (icon != null)
    {
      icon.sprite = UiSprites.Heart();
      icon.color = UiSkin.Health;
    }
    healthLabel.color = UiSkin.Health;
    healthLabel.GetComponent<LayoutElement>().preferredWidth = 96f;

    TMP_Text description = TowerInfoPanel.Description(transform);
    description.text = "Your base. Heal it back to full, or reinforce it to hold more health.";
    statsLabel = TowerInfoPanel.Stats(transform);

    Transform actions = TowerInfoPanel.Actions(transform);
    actions.GetComponent<LayoutElement>().preferredHeight = ActionRowHeight;
    healButton = MakeAction(actions, "Heal", UiSkin.Primary, out healLabel);
    healButton.onClick.AddListener(OnHeal);
    reinforceButton = MakeAction(actions, "Reinforce", UiSkin.Gold, out reinforceLabel);
    reinforceButton.onClick.AddListener(OnReinforce);
  }

  private static Button MakeAction(Transform parent, string name, Color tint, out TMP_Text label)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.ButtonLabel);
    label.raycastTarget = false;
    label.richText = true;

    TowerInfoPanel.StyleAction(button, tint);
    UiFont.ApplyControl(label, true);
    return button;
  }

  private void Refresh()
  {
    GameManager game = GameManager.Instance;
    if (game == null) return;

    int level = game.BaseLevel;
    nameLabel.text = level > 0 ? $"Base   Lv {level + 1}" : "Base";
    healthLabel.text = $"{game.currentHealth}/{game.MaxHealth}";

    statsLabel.text = game.CanReinforce
      ? $"Reinforce: +{game.ReinforceStep} max health ({level}/{BaseUpgrades.ReinforceSteps})"
      : $"Fully reinforced ({BaseUpgrades.ReinforceSteps}/{BaseUpgrades.ReinforceSteps})";

    bool full = game.currentHealth >= game.MaxHealth;
    healLabel.text = full ? "FULL HEALTH" : PriceText("HEAL", game.HealCost);
    healButton.interactable = !full;

    reinforceButton.gameObject.SetActive(game.CanReinforce);
    if (game.CanReinforce) reinforceLabel.text = PriceText("REINFORCE", game.ReinforceCost);
  }

  // The price in gold when affordable, in red when not - read before pressing.
  private static string PriceText(string verb, int price)
  {
    bool affordable = GameManager.Instance != null && GameManager.Instance.CanAfford(price);
    string colour = affordable ? "#1A1D28" : "#B02A2A";
    return $"{verb}  <color={colour}>{price:N0}</color>";
  }

  private void OnHeal()
  {
    GameManager game = GameManager.Instance;
    if (game == null) return;
    if (!game.CanAfford(game.HealCost)) { Refuse(); return; }
    if (game.TryHealBase()) Bought();
  }

  private void OnReinforce()
  {
    GameManager game = GameManager.Instance;
    if (game == null || !game.CanReinforce) return;
    if (!game.CanAfford(game.ReinforceCost)) { Refuse(); return; }
    if (game.TryReinforceBase()) Bought();
  }

  private void Bought()
  {
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    Haptics.Play(Haptics.Style.Success);
    Refresh();
  }

  private void Refuse()
  {
    AudioManager.Instance?.PlayLocked();
    UiShake.Nudge((RectTransform)transform, 10f);
  }

  // Health and coins both move while the panel is open - a leak, a kill
  // payout - so the labels follow them. Cheap: only rewritten on a change.
  private int shownHealth = -1;
  private int shownCoins = -1;

  private void Update()
  {
    GameManager game = GameManager.Instance;
    if (game == null) return;
    if (game.HasEnded) { Hide(); return; }
    if (game.currentHealth == shownHealth && game.currentGold == shownCoins) return;
    shownHealth = game.currentHealth;
    shownCoins = game.currentGold;
    Refresh();
  }
}
