using UnityEngine;
using System.Collections.Generic;

public class TowerPlacement : MonoBehaviour
{
  [SerializeField] private LayerMask placementLayer;
  [SerializeField] private TowerFactory towerFactory;
  [SerializeField] private GridVisualizer gridLineVisualizer;
  [SerializeField] private GridTileVisualizer gridTileVisualizer;

  private Camera mainCamera;
  private TowerConfig currentTowerConfig;
  private Tower previewTower;
  private GridManager gridManager;
  private bool groundManagerChecked = false;
  private bool groundManagerAvailable = false;

  private void Awake()
  {
    mainCamera = Camera.main;
    gridManager = GridManager.Instance;

    if (gridManager == null)
    {
      Debug.LogError("GridManager instance not found in TowerPlacement! Disabling component.");
      enabled = false;
      return;
    }
    if (gridLineVisualizer == null)
    {
      Debug.LogWarning("GridLineVisualizer not assigned in TowerPlacement!");
    }
    if (gridTileVisualizer == null)
    {
      Debug.LogError("GridTileVisualizer not assigned in TowerPlacement! Disabling component.");
      enabled = false;
      return;
    }
    if (towerFactory == null)
    {
      Debug.LogError("TowerFactory not assigned in TowerPlacement! Disabling component.");
      enabled = false;
      return;
    }

    groundManagerAvailable = GroundManager.Instance != null;
    if (!groundManagerAvailable)
    {
      Debug.LogWarning("GroundManager instance not found in TowerPlacement. Placement Y position may be inaccurate.");
    }
    groundManagerChecked = true;
  }

  private void Update()
  {
    if (currentTowerConfig == null || !enabled) return;

    if (Input.GetMouseButtonDown(1))
    {
      CancelPlacement();
      return;
    }

    HandlePlacementInput();
  }


  public void StartPlacement(TowerConfig config)
  {
    if (currentTowerConfig != null) CancelPlacement();

    if (config == null || GameManager.Instance == null || !GameManager.Instance.CanAfford(config.cost))
    {
      if (GameManager.Instance != null && config != null)
        Debug.LogWarning($"Cannot start placement: Not enough gold for {config.towerName}. Need {config.cost}, have {GameManager.Instance.currentGold}");
      return;
    }

    AudioManager.Instance?.PlaySound(AudioManager.SoundType.TowerDrag);
    currentTowerConfig = config;
    GameObject preview = towerFactory.CreateTowerPreview(config);
    if (preview == null)
    {
      Debug.LogError($"Failed to create preview for {config.towerName}");
      currentTowerConfig = null;
      return;
    }
    previewTower = preview.GetComponent<Tower>();
    if (previewTower == null)
    {
      Debug.LogError($"Preview GameObject for {config.towerName} is missing Tower component");
      Destroy(preview);
      currentTowerConfig = null;
      return;
    }

    gridLineVisualizer?.ShowGrid();
    gridTileVisualizer?.ShowVisualization();
  }

  public void CancelPlacement()
  {
    if (previewTower != null)
    {
      Destroy(previewTower.gameObject);
    }
    currentTowerConfig = null;
    previewTower = null;

    gridLineVisualizer?.HideGrid();
    gridTileVisualizer?.HideVisualization();
  }

  private void HandlePlacementInput()
  {
    if (previewTower == null || gridManager == null || mainCamera == null) return;

    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
    bool hitValidSurface = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placementLayer);

    if (!hitValidSurface)
    {
      if (previewTower.gameObject.activeSelf)
      {
        previewTower.gameObject.SetActive(false);
        gridTileVisualizer?.ShowTemporarilyHiddenIndicator();
      }
      return;
    }

    if (!previewTower.gameObject.activeSelf)
    {
      previewTower.gameObject.SetActive(true);
    }

    Vector2Int centerGridPos = gridManager.WorldToGrid(hit.point);
    Vector3 snappedPosition = gridManager.GridToWorld(centerGridPos);

    float groundY = snappedPosition.y;
    if (groundManagerAvailable)
    {
      groundY = GroundManager.Instance.GetGroundHeight(snappedPosition);
    }
    else if (!groundManagerChecked)
    {
      groundManagerAvailable = GroundManager.Instance != null;
      if (groundManagerAvailable) groundY = GroundManager.Instance.GetGroundHeight(snappedPosition);
      groundManagerChecked = true;
    }
    snappedPosition.y = groundY;

    previewTower.transform.position = snappedPosition;
    gridTileVisualizer?.SetPreviewPosition(centerGridPos);

    bool canPlaceCenter = gridManager.IsCellBuildable(centerGridPos);
    previewTower.UpdatePlacementIndicatorVisuals(canPlaceCenter);

    if (Input.GetMouseButtonDown(0))
    {
      if (canPlaceCenter)
      {
        PlaceTower(currentTowerConfig, snappedPosition, centerGridPos);
      }
      else
      {
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        Debug.LogWarning("Cannot place tower here: Tile is blocked.");
      }
    }
  }

  private void PlaceTower(TowerConfig config, Vector3 worldPosition, Vector2Int gridPosition)
  {
    if (GameManager.Instance == null)
    {
      Debug.LogError("GameManager not found, cannot process purchase!");
      return;
    }
    if (towerFactory == null)
    {
      Debug.LogError("TowerFactory not found, cannot create tower!");
      return;
    }


    if (!GameManager.Instance.TryPurchase(config.cost))
    {
      Debug.LogWarning($"Cannot place tower: Not enough gold. Need {config.cost}, have {GameManager.Instance.currentGold}");
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      return;
    }

    AudioManager.Instance?.PlaySound(AudioManager.SoundType.TowerDrop);
    Tower createdTower = towerFactory.CreateTower(config, worldPosition);

    if (createdTower != null)
    {
      createdTower.SetGridPosition(gridPosition);
      gridManager?.SetCellBuildable(gridPosition, false);
    }
    else
    {
      Debug.LogError($"Failed to create tower '{config.towerName}' at {worldPosition} after purchase!");
      GameManager.Instance.AddGold(config.cost);
    }

    CancelPlacement();
  }
}