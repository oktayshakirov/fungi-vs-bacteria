using UnityEngine;

public class TowerPlacement : MonoBehaviour
{
  [SerializeField] private LayerMask placementLayer;    // Layer to raycast for placement (Ground)
  [SerializeField] private LayerMask blockingLayers;    // Layers that block placement (Tower)
  [SerializeField] private LayerMask ignoreLayers;      // Layers to ignore (Range Indicator)
  [SerializeField] private TowerFactory towerFactory;

  private Camera mainCamera;
  private TowerConfig currentTowerConfig;
  private Tower previewTower;

  private void Awake()
  {
    mainCamera = Camera.main;
  }

  private void Update()
  {
    if (currentTowerConfig == null) return;
    HandlePlacementInput();
  }

  public void StartPlacement(TowerConfig config)
  {
    if (config == null || !GameManager.Instance.CanAfford(config.cost)) return;
    AudioManager.Instance.PlaySound(AudioManager.SoundType.TowerDrag);
    currentTowerConfig = config;
    GameObject preview = towerFactory.CreateTowerPreview(config);
    previewTower = preview.GetComponent<Tower>();
  }

  public void CancelPlacement()
  {
    if (previewTower != null)
    {
      Destroy(previewTower.gameObject);
    }
    currentTowerConfig = null;
    previewTower = null;
  }

  private void HandlePlacementInput()
  {
    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
    if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placementLayer))
    {
      return;
    }

    Vector3 position = new Vector3(
        hit.point.x,
        GroundManager.Instance.GetGroundHeight(hit.point),
        hit.point.z
    );

    bool canPlace = CanPlaceAtPosition(position);

    // Update preview position and visual feedback
    if (previewTower != null)
    {
      previewTower.transform.position = position;
      previewTower.UpdateRangeIndicator(canPlace);
    }

    if (Input.GetMouseButtonDown(0) && canPlace)
    {
      PlaceTower(currentTowerConfig, position);
    }
    else if (Input.GetMouseButtonDown(1))
    {
      CancelPlacement();
    }
  }

  private bool CanPlaceAtPosition(Vector3 position)
  {
    Collider[] colliders = Physics.OverlapSphere(position, 0.5f);

    foreach (Collider collider in colliders)
    {
      int objectLayer = 1 << collider.gameObject.layer;

      if ((objectLayer & ignoreLayers.value) != 0)
        continue;

      if ((objectLayer & blockingLayers.value) != 0)
      {
        if (!collider.transform.IsChildOf(previewTower?.transform))
          return false;
      }
    }

    return true;
  }

  private void PlaceTower(TowerConfig config, Vector3 position)
  {
    if (!GameManager.Instance.TryPurchase(config.cost)) return;
    AudioManager.Instance.PlaySound(AudioManager.SoundType.TowerDrop);
    towerFactory.CreateTower(config, position);
    CancelPlacement();
  }
}