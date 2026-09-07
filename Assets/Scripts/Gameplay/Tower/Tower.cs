using UnityEngine;

[RequireComponent(typeof(TowerTargeting))]
public class Tower : MonoBehaviour
{
  [Header("Tower Components")]
  [SerializeField] private Transform tower;
  [SerializeField] private Transform projectileSpawnPoint;

  // The fungi models are authored facing local -X, not +Z. RotateTurret used to
  // aim +Z at the target, which left the mouth pointing 90 degrees away from
  // whatever the tower was shooting -- and because ProjectileSpawnPoint is a
  // child sitting at local -X, the projectile appeared to leave from the SIDE of
  // the head and to swing around it as the turret turned.
  // Aiming -X instead points the mouth at the target and carries the spawn point
  // around with it, so shots leave from the mouth. Mirrors Enemy.rotationOffset.
  [Tooltip("Yaw that maps the model's facing axis onto its aim direction. " +
           "90 = model faces local -X (all eight fungi prefabs).")]
  [SerializeField] private float modelYawOffset = 90f;

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

  // Multipliers contributed by nearby support towers; see TowerBuffs.
  private float damageMultiplier = 1f;
  private float fireRateMultiplier = 1f;

  // Upgrade tier, 1..config.maxLevel. Every stat below reads through it, so
  // nothing else in the codebase has to know upgrades exist.
  public int Level { get; private set; } = 1;

  private Vector3 baseScale = Vector3.one;
  private static MaterialPropertyBlock tierBlock;
  private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
  private static readonly int ColorId = Shader.PropertyToID("_Color");

  public float Range => config?.RangeAt(Level) ?? 0f;
  public float FireRate => config?.FireRateAt(Level) ?? 1f;

  public bool IsSupport => config != null && config.isSupport;
  public int EffectiveDamage =>
    config == null ? 0 : Mathf.RoundToInt(config.DamageAt(Level) * damageMultiplier);
  public float EffectiveFireRate =>
    config == null ? 1f : config.FireRateAt(Level) * fireRateMultiplier;

  // TowerBuffs reads these rather than the config directly, so an upgraded
  // support tower actually projects a stronger aura.
  public float EffectiveDamageBoost => config?.DamageBoostAt(Level) ?? 0f;
  public float EffectiveFireRateBoost => config?.FireRateBoostAt(Level) ?? 0f;

  public int MaxLevel => config?.MaxLevel ?? 1;
  public bool IsMaxLevel => config == null || Level >= config.MaxLevel;
  public int UpgradeCost => config?.UpgradeCostFrom(Level) ?? 0;
  public int SellValue => config?.SellValueAt(Level) ?? 0;

  public void SetBuffs(float damage, float fireRate)
  {
    damageMultiplier = damage;
    fireRateMultiplier = fireRate;
  }

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

    // A support tower has no weapon. Without this it would still run the
    // targeting scan and, because HandleShooting falls back to a 1/sec cadence
    // when fireRate is 0, fire an invisible zero-damage projectile every second
    // — complete with the firing sound.
    if (config.isSupport)
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
    TowerBuffs.Unregister(this);
  }

  // `preview` must be set here rather than left to the later SetPreviewMode
  // call: a preview tower follows the cursor around the board and must never
  // join the buff graph, or it would hand out aura bonuses while being dragged.
  public void Initialize(TowerConfig towerConfig, bool preview = false)
  {
    this.config = towerConfig;
    this.isPreviewMode = preview;

    if (config != null)
    {
      // Captured here, not in Awake: TowerFactory scales the instantiated
      // object by UnitScale.Tower AFTER Instantiate but BEFORE Initialize, so
      // Awake would bank the prefab's authored scale and the tier cue would
      // shrink every tower back down on its first upgrade.
      baseScale = transform.localScale;

      if (targeting != null)
      {
        targeting.Initialize(Range);
      }
      fireCountdown = 1f / (FireRate > 0 ? FireRate : 1f);

      if (!preview) TowerBuffs.Register(this);
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

    // The lightest tick there is, on the longest limit of any gameplay haptic:
    // a full board fires many times a second and this has to read as "the
    // defences are working", not as a continuous vibration.
    Haptics.PlayThrottled(Haptics.Style.Selection, 0.4f);

    GameObject projectileGO = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
    Vector3 directionToTarget = (targeting.CurrentTarget.position - projectileSpawnPoint.position).normalized;
    projectileGO.transform.forward = directionToTarget;

    if (projectileGO.TryGetComponent(out Projectile projectile))
    {
      var projectileData = new ProjectileData(
          EffectiveDamage,
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

    Quaternion lookRotation =
      Quaternion.LookRotation(direction) * Quaternion.Euler(0f, modelYawOffset, 0f);
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
      float rate = EffectiveFireRate;
      fireCountdown = 1f / (rate > 0 ? rate : 1f);
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

    GameManager.Instance?.AddGold(SellValue);
    GridManager.Instance?.SetCellBuildable(GridPosition, true);
    Deselect();
    Destroy(gameObject);
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.Sell);
  }

  // Returns false when the tower is maxed or the player cannot pay, so the UI
  // can stay quiet rather than pretending something happened.
  public bool Upgrade()
  {
    if (config == null || isPreviewMode || IsMaxLevel) return false;

    int price = UpgradeCost;
    if (GameManager.Instance == null || !GameManager.Instance.TryPurchase(price))
    {
      return false;
    }

    Level++;

    // Range grows with the tier, so the targeting radius has to be re-armed;
    // without this an upgraded tower reports a longer range in the panel and
    // still refuses to shoot anything past its old one.
    targeting?.Initialize(Range);

    // Support auras change with the tier, and an upgraded attacker changes what
    // a nearby support is worth, so the whole graph is re-derived.
    TowerBuffs.Recalculate();

    ApplyTierVisuals();

    // No Haptics call here: AudioManager.PlaySound already fires a Medium
    // impact for TowerDrop. Adding one would double it up, the same trap
    // TowerActions.SellTower documents.
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.TowerDrop);
    return true;
  }

  // There is no upgrade art (see HANDOFF section 8), so the tier reads as a
  // size step plus a warmer body colour. Both are cheap and both survive at
  // phone size, which a decal or a badge on the model would not.
  //
  // The tint goes through a MaterialPropertyBlock for the same reason enemies'
  // does: several TowerConfigs can point at one prefab, and writing
  // sharedMaterial would re-tier every other tower using it.
  private void ApplyTierVisuals()
  {
    if (config == null) return;

    int steps = Mathf.Max(0, Level - 1);
    transform.localScale = baseScale * (1f + 0.08f * steps);

    Transform model = tower != null ? tower : transform;

    // Multiplied INTO the material's own colour, never assigned over it: the
    // eight fungi are told apart by their body colour, and writing a flat tier
    // colour would make every level-2 tower on the board identical.
    Color tint = Color.Lerp(Color.white, new Color(1f, 0.86f, 0.55f),
                            Mathf.Min(1f, 0.5f * steps));

    tierBlock ??= new MaterialPropertyBlock();
    foreach (Renderer rend in model.GetComponentsInChildren<Renderer>(true))
    {
      if (tileIndicator != null && rend.transform.IsChildOf(tileIndicator.transform)) continue;

      Material shared = rend.sharedMaterial;
      if (shared == null) continue;

      rend.GetPropertyBlock(tierBlock);
      ApplyTint(tierBlock, shared, BaseColorId, tint);
      ApplyTint(tierBlock, shared, ColorId, tint);
      rend.SetPropertyBlock(tierBlock);
    }
  }

  private static void ApplyTint(MaterialPropertyBlock block, Material shared,
    int property, Color tint)
  {
    if (!shared.HasProperty(property)) return;

    // The block is read back each time rather than kept per-tower, so the base
    // colour has to come from the shared material: the block's own value is
    // already tinted from the previous tier and compounding it would drive the
    // model to white by level 3.
    Color baseColor = shared.GetColor(property);
    block.SetColor(property, new Color(baseColor.r * tint.r, baseColor.g * tint.g,
                                       baseColor.b * tint.b, baseColor.a));
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