using UnityEngine;

[RequireComponent(typeof(TowerTargeting))]
public class Tower : MonoBehaviour
{
  [Header("Tower Components")]
  [SerializeField] private Transform tower;
  [SerializeField] private Transform projectileSpawnPoint;

  [Header("Prefabs")]
  [SerializeField] private GameObject projectilePrefab;
  [SerializeField] private GameObject tileIndicatorPrefab;

  private TowerConfig config;
  private TowerTargeting targeting;
  private float fireCountdown = 0f;
  private GameObject tileIndicator;
  private bool isSelected = false;
  private bool isPreviewMode = false;
  public Vector2Int GridPosition { get; private set; }

  public float Range => config?.range ?? 0f;
  public float FireRate => config?.fireRate ?? 1f;

  private void Awake()
  {
    targeting = GetComponent<TowerTargeting>();
    if (targeting == null)
    {
      Debug.LogError("Tower requires a TowerTargeting component!", this);
    }
  }

  private void Update()
  {
    if (isPreviewMode || config == null)
    {
      return;
    }

    UpdateTarget();
    if (targeting != null && targeting.CurrentTarget != null)
    {
      RotateTurret();
      HandleShooting();
    }
  }

  private void OnDestroy()
  {
    HideTileIndicator();
  }

  public void Initialize(TowerConfig towerConfig)
  {
    this.config = towerConfig;
    if (config != null)
    {
      if (targeting != null)
      {
        targeting.Initialize(config.range);
      }
      fireCountdown = 1f / (FireRate > 0 ? FireRate : 1f);
    }
    else
    {
      Debug.LogError("Tower initialized with a null TowerConfig!", this);
    }
  }

  public void SetGridPosition(Vector2Int gridPos)
  {
    GridPosition = gridPos;
  }

  public void UpdateTarget()
  {
    targeting?.UpdateTarget();
  }

  public void Attack()
  {
    if (config == null || projectilePrefab == null || targeting == null || targeting.CurrentTarget == null || projectileSpawnPoint == null)
    {
      return;
    }

    GameObject projectileGO = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
    Vector3 directionToTarget = (targeting.CurrentTarget.position - projectileSpawnPoint.position).normalized;
    projectileGO.transform.forward = directionToTarget;

    if (projectileGO.TryGetComponent(out Projectile projectile))
    {
      var projectileData = new ProjectileData(
          config.damage,
          config.isAoE,
          config.splashRadius,
          config.slowsEnemies,
          config.slowAmount,
          20f
      );
      projectile.Initialize(projectileData);
      projectile.Seek(targeting.CurrentTarget);
    }
    else
    {
      Debug.LogWarning($"Projectile prefab '{projectilePrefab.name}' is missing the Projectile component.", projectilePrefab);
      Destroy(projectileGO);
    }
  }

  private void RotateTurret()
  {
    if (tower == null || targeting == null || targeting.CurrentTarget == null) return;

    Vector3 targetPosition = targeting.CurrentTarget.position;
    Vector3 direction = targetPosition - tower.position;
    direction.y = 0f;

    if (direction == Vector3.zero) return;

    Quaternion lookRotation = Quaternion.LookRotation(direction);
    float speed = 10f;
    tower.rotation = Quaternion.Slerp(tower.rotation, lookRotation, Time.deltaTime * speed);
  }

  private void HandleShooting()
  {
    if (config == null) return;

    fireCountdown -= Time.deltaTime;
    if (fireCountdown <= 0f)
    {
      Attack();
      fireCountdown = 1f / (FireRate > 0 ? FireRate : 1f);
    }
  }

  public TowerConfig GetTowerConfig()
  {
    return config;
  }

  public void Select()
  {
    if (isSelected || isPreviewMode) return;
    isSelected = true;
    HUDManager.Instance?.ShowTowerActions(this);
  }

  public void Deselect()
  {
    if (!isSelected || isPreviewMode) return;
    isSelected = false;
    HUDManager.Instance?.HideTowerActions();
  }

  public void Sell()
  {
    if (config == null)
    {
      Debug.LogError("Cannot sell tower: Configuration is missing!", this);
      Destroy(gameObject);
      return;
    }

    GameManager.Instance?.AddGold(config.sellValue);
    GridManager.Instance?.SetCellBuildable(GridPosition, true);
    Deselect();
    Destroy(gameObject);
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.Sell);
  }

  public void Upgrade()
  {
    Debug.Log($"[Not Implemented] Attempting to upgrade tower: {gameObject.name}", this);
  }

  public void SetPreviewMode(bool preview)
  {
    isPreviewMode = preview;
    if (targeting != null) targeting.enabled = !preview;

    if (preview)
    {
      ShowTileIndicator();
    }
    else
    {
      HideTileIndicator();
    }

    foreach (Collider col in GetComponentsInChildren<Collider>(true))
    {
      col.enabled = !preview;
    }
  }

  public void UpdatePlacementIndicatorVisuals(bool validPlacement)
  {
    if (!isPreviewMode || tileIndicator == null) return;

    Renderer indicatorRenderer = tileIndicator.GetComponent<Renderer>();
    if (indicatorRenderer == null || indicatorRenderer.material == null) return;

    Material mat = indicatorRenderer.material;
    Color color = validPlacement ? Color.blue : Color.red;
    color.a = 0.5f;
    mat.color = color;
  }

  private void ShowTileIndicator()
  {
    if (tileIndicator != null) return;
    if (tileIndicatorPrefab == null) { Debug.LogError("TileIndicatorPrefab is not assigned!", this); return; }
    if (GridManager.Instance == null) { Debug.LogError("GridManager instance not found!", this); return; }

    tileIndicator = Instantiate(tileIndicatorPrefab, transform);
    UpdateTileIndicatorPositionAndScale();

    Renderer rend = tileIndicator.GetComponent<Renderer>();
    if (rend != null && rend.material != null)
    {
      rend.material.color = new Color(0f, 1f, 0f, 0.5f);
    }
  }

  private void HideTileIndicator()
  {
    if (tileIndicator != null)
    {
      Destroy(tileIndicator);
      tileIndicator = null;
    }
  }

  private void UpdateTileIndicatorPositionAndScale()
  {
    if (tileIndicator == null || GridManager.Instance == null) return;

    tileIndicator.transform.localPosition = Vector3.up * 0.01f;
    tileIndicator.transform.localRotation = Quaternion.identity;

    float cellSize = GridManager.Instance.cellSize;
    Vector3 parentScale = transform.lossyScale;

    float requiredLocalScaleX = (Mathf.Abs(parentScale.x) < 0.001f) ? 0 : cellSize / parentScale.x;
    float requiredLocalScaleY = (Mathf.Abs(parentScale.y) < 0.001f) ? 0.01f : 0.01f / parentScale.y;
    float requiredLocalScaleZ = (Mathf.Abs(parentScale.z) < 0.001f) ? 0 : cellSize / parentScale.z;

    tileIndicator.transform.localScale = new Vector3(requiredLocalScaleX, requiredLocalScaleY, requiredLocalScaleZ);
  }

  private void OnDrawGizmosSelected()
  {
    if (targeting != null && config != null)
    {
      targeting.DrawRangeGizmo();
    }
    else
    {
      Gizmos.color = Color.grey;
      Gizmos.DrawWireSphere(transform.position, config?.range ?? 5f);
    }

    if (GridManager.Instance != null && !isPreviewMode && GridPosition != default(Vector2Int))
    {
      Gizmos.color = Color.cyan;
      Vector3 cellCenter = GridManager.Instance.GridToWorld(GridPosition);
      float yPos = GroundManager.Instance != null ? GroundManager.Instance.GetGroundHeight(cellCenter) : transform.position.y;
      cellCenter.y = yPos + 0.05f;
      Gizmos.DrawWireCube(cellCenter, new Vector3(GridManager.Instance.cellSize, 0.1f, GridManager.Instance.cellSize));
    }
  }
}