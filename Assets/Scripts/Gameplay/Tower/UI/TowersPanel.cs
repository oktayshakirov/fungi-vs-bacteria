using UnityEngine;

namespace TowerDefense.UI
{
  public class TowerUI : MonoBehaviour
  {
    [SerializeField] private TowerDatabase towerDatabase;
    [SerializeField] private TowerPlacement towerPlacement;
    [SerializeField] private TowerSelectionButton buttonPrefab;
    [SerializeField] private Transform buttonContainer;

    private void Start()
    {
      if (towerDatabase == null)
      {
        Debug.LogError("Tower Database not assigned to TowerUI!");
        return;
      }

      CreateTowerButtons();
    }

    private void CreateTowerButtons()
    {
      // Clear existing buttons
      foreach (Transform child in buttonContainer)
      {
        Destroy(child.gameObject);
      }

      // Create new buttons
      foreach (TowerConfig towerConfig in towerDatabase.availableTowers)
      {
        TowerSelectionButton button = Instantiate(buttonPrefab, buttonContainer);
        button.Initialize(towerConfig, HandleTowerSelected);
      }
    }

    private void HandleTowerSelected(TowerConfig config)
    {
      towerPlacement.StartPlacement(config);
    }

    private void Update()
    {
      if (towerDatabase == null) return;

      // Update button interactability based on available currency
      var buttons = GetComponentsInChildren<TowerSelectionButton>();
      foreach (var button in buttons)
      {
        button.UpdateInteractability();
      }
    }
  }
}