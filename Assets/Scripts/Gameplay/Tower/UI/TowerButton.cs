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

      button.onClick.AddListener(HandleClick);
      UpdateInteractability();
    }

    public void UpdateInteractability()
    {
      bool canAfford = GameManager.Instance.CanAfford(towerConfig.cost);
      Color iconColor = towerIcon.color;
      iconColor.a = canAfford ? 1f : 0.5f;
      towerIcon.color = iconColor;
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