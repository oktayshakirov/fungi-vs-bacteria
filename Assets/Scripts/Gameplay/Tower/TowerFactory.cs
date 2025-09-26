using UnityEngine;

public class TowerFactory : MonoBehaviour
{
  private Vector3 GetPositionWithHeightOffset(Vector3 position)
  {
    return position;
  }

  private GameObject InstantiateTower(TowerConfig config, Vector3 position)
  {
    // Validate prefab before instantiation
    if (!config.towerPrefab.GetComponent<TowerTargeting>())
    {
      Debug.LogError($"Tower prefab '{config.towerPrefab.name}' in TowerConfig '{config.name}' is missing required TowerTargeting component!", config);
      return null;
    }

    return Instantiate(config.towerPrefab, position, Quaternion.identity);
  }

  public Tower CreateTower(TowerConfig config, Vector3 position)
  {
    Vector3 adjustedPosition = GetPositionWithHeightOffset(position);
    GameObject towerObject = InstantiateTower(config, adjustedPosition);

    Tower tower = towerObject.GetComponent<Tower>();
    tower.Initialize(config);
    return tower;
  }

  public GameObject CreateTowerPreview(TowerConfig config)
  {
    Vector3 previewPosition = GetPositionWithHeightOffset(Vector3.zero);
    GameObject preview = InstantiateTower(config, previewPosition);

    // Get the tower component and initialize it (for range indicator setup)
    Tower tower = preview.GetComponent<Tower>();
    tower.Initialize(config);

    // Put preview in preview mode
    tower.SetPreviewMode(true);

    return preview;
  }
}