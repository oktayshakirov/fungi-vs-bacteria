using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TowerDefense.UI
{
  public class TowerSelectionButton : MonoBehaviour
  {
    [SerializeField] private Image towerIcon;
    [SerializeField] private Image goldIcon;
    [SerializeField] private Image lockIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI costText;

    private Button button;
    private TowerConfig towerConfig;
    private System.Action<TowerConfig> onSelected;

    private void Awake()
    {
      button = GetComponent<Button>();
    }

    public void Initialize(TowerConfig config, System.Action<TowerConfig> onSelectedCallback)
    {
      towerConfig = config;
      onSelected = onSelectedCallback;

      nameText.text = config.towerName;
      costText.text = config.cost.ToString();

      if (config.towerIcon != null)
      {
        towerIcon.sprite = config.towerIcon;
      }

      Style();
      button.onClick.AddListener(HandleClick);
      UpdateInteractability();
    }

    // The card was a plain white box with black text. Restyled here rather than
    // in the prefab so it stays in step with the rest of the skin.
    private void Style()
    {
      var background = GetComponent<Image>();
      if (background != null)
      {
        UiSkin.Panel(background, UiSkin.PanelRaised, UiSkin.RadiusButton);
        button.targetGraphic = background;
      }

      var colors = button.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
      colors.pressedColor = new Color(0.78f, 0.80f, 0.86f, 1f);
      colors.disabledColor = new Color(0.6f, 0.6f, 0.65f, 0.7f);
      colors.fadeDuration = 0.08f;
      button.colors = colors;

      UiSkin.Label(nameText, UiSkin.Role.Caption);
      UiSkin.Label(costText, UiSkin.Role.Value, UiSkin.Gold);

      // The affordability markers were tiny sprites; a coin and a dimmed coin
      // read better at card size and need no extra art.
      if (goldIcon != null)
      {
        goldIcon.sprite = UiSprites.Coin();
        goldIcon.color = UiSkin.Gold;
        goldIcon.preserveAspect = true;
      }
      if (lockIcon != null)
      {
        lockIcon.sprite = UiSprites.Coin();
        lockIcon.color = UiSkin.Danger;
        lockIcon.preserveAspect = true;
      }
    }

    public void UpdateInteractability()
    {
      bool canAfford = GameManager.Instance.CanAfford(towerConfig.cost);

      Color iconColor = towerIcon.color;
      iconColor.a = canAfford ? 1f : 0.45f;
      towerIcon.color = iconColor;

      // Whole card dims when unaffordable, not just the tower icon
      var background = GetComponent<Image>();
      if (background != null)
      {
        background.color = canAfford ? UiSkin.PanelRaised
          : new Color(UiSkin.PanelDark.r, UiSkin.PanelDark.g, UiSkin.PanelDark.b, 0.75f);
      }
      if (costText != null) costText.color = canAfford ? UiSkin.Gold : UiSkin.Danger;

      goldIcon.gameObject.SetActive(canAfford);
      lockIcon.gameObject.SetActive(!canAfford);
    }

    private void HandleClick()
    {
      onSelected?.Invoke(towerConfig);
    }

    private void OnDestroy()
    {
      button.onClick.RemoveListener(HandleClick);
    }
  }
}