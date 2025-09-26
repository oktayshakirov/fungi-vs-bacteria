using UnityEngine;

[RequireComponent(typeof(TowerTargeting))]
public class Tower : MonoBehaviour
{
  [SerializeField] private Transform tower;
  [SerializeField] private Transform projectileSpawnPoint;
  [SerializeField] private GameObject projectilePrefab;
  [SerializeField] private GameObject rangeIndicatorPrefab;

  private TowerConfig config;
  private TowerTargeting targeting;
  private float fireCountdown;
  private GameObject rangeIndicator;
  private bool isSelected;
  private bool isPreviewMode;

  public float Range => config.range;
  public float FireRate => config.fireRate;

  private void Awake()
  {
    targeting = GetComponent<TowerTargeting>();
  }

  public void Initialize(TowerConfig config)
  {
    this.config = config;
    targeting.Initialize(config.range);
  }

  private void Update()
  {
    if (isPreviewMode)
    {
      UpdateRangeIndicatorPosition();
      return;
    }

    UpdateTarget();
    if (targeting.CurrentTarget != null)
    {
      RotateTurret();
      HandleShooting();
    }
  }

  public void UpdateTarget()
  {
    targeting.UpdateTarget();
  }

  public void Attack()
  {
    if (projectilePrefab == null || targeting.CurrentTarget == null) return;
    Vector3 directionToTarget = (targeting.CurrentTarget.position - projectileSpawnPoint.position).normalized;
    GameObject projectileGO = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
    projectileGO.transform.forward = directionToTarget;

    if (projectileGO.TryGetComponent(out Projectile projectile))
    {
      var projectileData = new ProjectileData(
          config.damage,
          config.isAoE,
          config.splashRadius,
          config.slowsEnemies,
          config.slowAmount,
          20f // Default speed, consider moving to config
      );

      projectile.Initialize(projectileData);
      projectile.Seek(targeting.CurrentTarget);
    }
  }

  private void RotateTurret()
  {
    if (tower == null) return;
    Vector3 targetPosition = targeting.CurrentTarget.position;
    Vector3 directionToTarget = targetPosition - tower.position;
    directionToTarget.y = 0f;
    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
    tower.rotation = Quaternion.Lerp(tower.rotation, targetRotation, Time.deltaTime * 10f);
  }

  private void HandleShooting()
  {
    if (fireCountdown <= 0f)
    {
      Attack();
      fireCountdown = 1f / FireRate;
    }

    fireCountdown -= Time.deltaTime;
  }

  private void OnDrawGizmosSelected()
  {
    if (targeting != null)
    {
      targeting.DrawRangeGizmo();
    }
  }

  public TowerConfig GetTowerConfig()
  {
    return config;
  }

  public void Select()
  {
    if (isSelected) return;

    isSelected = true;
    ShowRangeIndicator();
    HUDManager.Instance.ShowTowerActions(this);
  }

  public void Deselect()
  {
    if (!isSelected) return;

    isSelected = false;
    HideRangeIndicator();
    HUDManager.Instance.HideTowerActions();
  }

  public void Sell()
  {
    Debug.Log("3. Tower.Sell() called");
    Debug.Log($"4. Adding {config.sellValue} gold");
    GameManager.Instance.AddGold(config.sellValue);
    Deselect();
    Debug.Log("5. Tower destroyed");
    Destroy(gameObject);
    AudioManager.Instance.PlaySound(AudioManager.SoundType.Sell);
  }

  public void Upgrade()
  {
    Debug.Log("[Not implemented] Upgrading tower:", gameObject);
  }

  private void OnDestroy()
  {
    HideRangeIndicator();
  }

  public void SetPreviewMode(bool preview)
  {
    isPreviewMode = preview;

    // Disable functional components in preview mode
    targeting.enabled = !preview;

    // Show range indicator
    if (preview)
    {
      ShowRangeIndicator();
    }

    // Disable colliders except for range indicator
    foreach (Collider col in GetComponentsInChildren<Collider>())
    {
      col.enabled = !preview;
    }
  }

  public void UpdateRangeIndicator(bool validPlacement)
  {
    if (rangeIndicator == null) return;

    Material material = rangeIndicator.GetComponent<MeshRenderer>().material;
    Color color = validPlacement ? Color.blue : Color.red;
    color.a = 0.3f;
    material.color = color;
  }

  private void UpdateRangeIndicatorPosition()
  {
    if (rangeIndicator == null) return;

    Vector3 towerPosition = transform.position;
    rangeIndicator.transform.position = new Vector3(
        towerPosition.x,
        0.01f,
        towerPosition.z
    );
  }

  private void ShowRangeIndicator()
  {
    if (rangeIndicator != null) return;

    rangeIndicator = Instantiate(rangeIndicatorPrefab);
    rangeIndicator.transform.parent = transform;
    UpdateRangeIndicatorPosition();

    float parentScale = transform.lossyScale.x;
    float compensatedRange = config.range / parentScale * 2;

    rangeIndicator.transform.localScale = new Vector3(
        compensatedRange,
        0.01f,
        compensatedRange
    );

    Material material = rangeIndicator.GetComponent<MeshRenderer>().material;
    Color color = Color.blue;
    color.a = 0.3f;
    material.color = color;
  }

  private void HideRangeIndicator()
  {
    if (rangeIndicator != null)
    {
      Destroy(rangeIndicator);
      rangeIndicator = null;
    }
  }
}