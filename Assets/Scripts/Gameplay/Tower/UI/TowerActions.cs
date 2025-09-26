using UnityEngine;
using TMPro;

namespace TowerDefense.UI
{
  public class TowerActions : MonoBehaviour
  {
    [SerializeField] private TextMeshProUGUI sellButtonText;
    [SerializeField] private TextMeshProUGUI towerNameText;
    [SerializeField] private TextMeshProUGUI towerStatsText;

    private Tower currentTower;

    private void Awake()
    {
      // Hide panel initially
      gameObject.SetActive(false);
    }

    public void ShowForTower(Tower tower)
    {
      Debug.Log($"ShowForTower called with tower: {tower?.name ?? "null"}");
      currentTower = tower;
      if (currentTower == null) return;

      var config = tower.GetTowerConfig();
      Debug.Log($"Tower config loaded: {config?.name ?? "null"}");

      // Update UI
      towerNameText.text = config.towerName;
      sellButtonText.text = $"Sell for {config.sellValue}g";
      towerStatsText.text = $"Damage: {config.damage}\n" +
                            $"Range: {config.range}\n" +
                            $"Fire Rate: {config.fireRate}/s";

      gameObject.SetActive(true);
    }

    public void SellTower()
    {
      Debug.Log($"SellTower button clicked. CurrentTower: {currentTower?.name ?? "null"}");
      if (currentTower != null)
      {
        Debug.Log("Calling Sell() on tower");
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
      Debug.Log("TowerActions Hide called");
      currentTower = null;
      gameObject.SetActive(false);
    }
  }
}