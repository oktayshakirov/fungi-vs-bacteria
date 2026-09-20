using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TowerDefense.UI;

// The panel shown when a booster in the HUD bar is tapped: what it does, how
// many are left, and a USE button.
//
// A booster is NOT used by tapping it in the bar. The bomb costs 600 coins and
// there is no undo, so a single stray tap while dragging a tower past the bar
// would be the most expensive misclick in the game. Arming it first also gives
// somewhere to say what the thing actually does, which the bar's icon cannot.
//
// Built out of TowerInfoPanel, so it is the same plate in the same corner as
// the tower panels - and mutually exclusive with them by the same rule: each
// one cancels the other two when it opens.
public class BoosterPanel : MonoBehaviour
{
  private static BoosterPanel instance;

  public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

  public static void Show(BoosterKind kind, Transform host)
  {
    if (instance == null)
    {
      if (host == null) return;

      var go = new GameObject("BoosterPanel", typeof(RectTransform));
      go.transform.SetParent(host, false);
      instance = go.AddComponent<BoosterPanel>();
      instance.Build();
    }

    instance.SetBooster(kind);
    instance.gameObject.SetActive(true);
    instance.transform.SetAsLastSibling();
  }

  public static void Hide()
  {
    if (instance != null) instance.gameObject.SetActive(false);
  }

  private BoosterKind current;
  private TMP_Text nameLabel;
  private TMP_Text countLabel;
  private TMP_Text descriptionLabel;
  private TMP_Text statsLabel;
  private TMP_Text useLabel;
  private Button useButton;

  private void Build()
  {
    TowerInfoPanel.Place((RectTransform)transform, TowerInfoPanel.BaseHeight);
    TowerInfoPanel.Frame(gameObject);

    TMP_Text name = null;
    TowerInfoPanel.Header(transform, ref name, out countLabel);
    nameLabel = name;

    descriptionLabel = TowerInfoPanel.Description(transform);
    statsLabel = TowerInfoPanel.Stats(transform);

    Transform actions = TowerInfoPanel.Actions(transform);

    useButton = MakeAction(actions, "Use", "USE", UiSkin.Primary, out useLabel);
    useButton.onClick.AddListener(OnUse);

    MakeAction(actions, "Cancel", "CANCEL", UiSkin.Neutral, out _)
      .onClick.AddListener(() =>
      {
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        Hide();
      });
  }

  private static Button MakeAction(Transform parent, string name, string text, Color tint,
    out TMP_Text label)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    go.AddComponent<Image>();
    var button = go.AddComponent<Button>();

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(go.transform, false);
    label = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(label, UiSkin.Role.ButtonLabel);
    label.text = text;
    label.raycastTarget = false;

    TowerInfoPanel.StyleAction(button, tint);
    return button;
  }

  private void SetBooster(BoosterKind kind)
  {
    current = kind;

    nameLabel.text = BoosterCatalog.Name(kind);
    countLabel.text = "x" + BoosterInventory.Count(kind);
    descriptionLabel.text = BoosterCatalog.Description(kind);

    statsLabel.text = BoosterCatalog.Limit(kind) == BoosterLimit.OncePerLevel
      ? "Once per level"
      : "Once per wave";

    Refresh();
  }

  private void Refresh()
  {
    string blocked = BoosterEffects.BlockedReason(current);
    useButton.interactable = blocked == null;
    useLabel.text = blocked ?? "USE";
  }

  private void OnUse()
  {
    if (!BoosterEffects.Use(current)) { Refresh(); return; }
    Hide();
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
}
