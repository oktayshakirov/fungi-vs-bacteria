using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
  // Per-spawn overrides. Only splitter children use anything but Default: they
  // inherit the parent's EnemyConfig but enter the world smaller, weaker, part
  // way down the path, and barred from splitting again.
  public struct SpawnOverride
  {
    public int startWaypoint;
    public float healthScale;
    public float sizeScale;
    public float speedScale;
    public bool canSplit;

    public static SpawnOverride Default => new SpawnOverride
    {
      startWaypoint = 0,
      healthScale = 1f,
      sizeScale = 1f,
      speedScale = 1f,
      canSplit = true,
    };
  }

  // Every enemy currently on the board. Healers need to find their neighbours
  // every tick and FindObjectsOfType allocates an array each call, which is the
  // exact per-frame garbage that was costing frames past ~25 enemies.
  private static readonly List<Enemy> active = new List<Enemy>();
  public static IReadOnlyList<Enemy> Active => active;

  public float speed { get; private set; } = 5f;
  public int health { get; private set; } = 100;
  public int damage { get; private set; } = 10;
  private int goldReward;
  private float armorDamageReduction = 0f;

  private Vector3[] waypoints;

  private int currentWaypointIndex = 0;

  private float slowAmount = 0f;
  private float slowDuration = 2f;
  private float normalSpeed;
  private bool isRemoved = false;
  private int maxHealth;
  private EnemyHealthBar healthBar;

  [SerializeField] private float rotationOffset = 0f;
  [SerializeField] private float turnSpeed = 12f;

  private Vector3 baseScale = Vector3.one;
  private Vector3 currentScale = Vector3.one;
  private float hitPunch = 0f;
  private Quaternion targetRotation;
  private Color bodyColor = Color.white;
  private Color defaultBodyColor = Color.white;

  // Variety state, all driven from EnemyConfig.
  private EnemyConfig config;
  private float shield;
  private float shieldMax;
  private float lastDamagedAt = -999f;
  private float nextHealAt;
  private SpawnOverride spawnOverride = SpawnOverride.Default;

  private MeshRenderer bodyRenderer;
  private static MaterialPropertyBlock propertyBlock;
  private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
  private static readonly int ColorId = Shader.PropertyToID("_Color");

  private static readonly Color DamageColor = new Color(1f, 0.85f, 0.2f);
  private static readonly Color GoldColor = new Color(1f, 0.9f, 0.35f);
  private static readonly Color ShieldColor = new Color(0.55f, 0.8f, 1f);
  private static readonly Color HealColor = new Color(0.45f, 0.95f, 0.5f);

  // Statics survive a scene change but the GameObjects they point at do not,
  // so the registry would fill with destroyed entries on the second level.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetStatics()
  {
    active.Clear();
  }

  private void Awake()
  {
    baseScale = transform.localScale;
    currentScale = baseScale;

    bodyRenderer = GetComponentInChildren<MeshRenderer>();
    if (bodyRenderer != null && bodyRenderer.sharedMaterial != null &&
        bodyRenderer.sharedMaterial.HasProperty("_BaseColor"))
    {
      defaultBodyColor = bodyRenderer.sharedMaterial.GetColor("_BaseColor");
    }
    else if (bodyRenderer != null && bodyRenderer.sharedMaterial != null &&
             bodyRenderer.sharedMaterial.HasProperty("_Color"))
    {
      defaultBodyColor = bodyRenderer.sharedMaterial.color;
    }
    bodyColor = defaultBodyColor;
  }

  private void Start()
  {
    normalSpeed = speed;
  }

  private void OnDisable()
  {
    active.Remove(this);
  }

  public void Initialize(Vector3[] path, EnemyConfig enemyConfig,
    float healthMultiplier = 1f, float rewardMultiplier = 1f,
    SpawnOverride? overrideOrNull = null)
  {
    SpawnOverride ov = overrideOrNull ?? SpawnOverride.Default;
    spawnOverride = ov;
    config = enemyConfig;

    // Full reset: instances come back from the pool with stale state
    StopAllCoroutines();
    isRemoved = false;
    slowAmount = 0f;
    hitPunch = 0f;

    waypoints = path;
    currentWaypointIndex = Mathf.Clamp(ov.startWaypoint, 0, waypoints.Length - 1);

    // Scaled per wave: the shared EnemyConfig assets are identical on level 1
    // and level 70, so this is what makes later levels actually harder.
    maxHealth = Mathf.Max(1,
      Mathf.RoundToInt(enemyConfig.maxHealth * healthMultiplier * ov.healthScale));
    health = maxHealth;
    speed = enemyConfig.moveSpeed
            * (enemyConfig.isFast ? enemyConfig.speedMultiplier : 1f)
            * ov.speedScale;
    normalSpeed = speed;
    damage = enemyConfig.baseDamage;
    goldReward = Mathf.Max(1,
      Mathf.RoundToInt(enemyConfig.goldReward * rewardMultiplier * ov.healthScale));
    armorDamageReduction = enemyConfig.isArmored ? enemyConfig.armorDamageReduction : 0f;

    // Shield is a share of maxHealth so it keeps pace with the per-wave ramp
    // instead of becoming a rounding error by level 70.
    shieldMax = enemyConfig.hasShield
      ? maxHealth * Mathf.Max(0f, enemyConfig.shieldShareOfHealth)
      : 0f;
    shield = shieldMax;
    lastDamagedAt = -999f;
    nextHealAt = Time.time + enemyConfig.healInterval;

    ApplyAppearance(enemyConfig, ov.sizeScale);

    if (healthBar == null)
    {
      healthBar = gameObject.GetComponent<EnemyHealthBar>();
      if (healthBar == null) healthBar = gameObject.AddComponent<EnemyHealthBar>();
    }
    healthBar.SetHealth(1f);

    transform.position = waypoints[currentWaypointIndex];

    // Set initial rotation to face the next waypoint
    int next = Mathf.Min(currentWaypointIndex + 1, waypoints.Length - 1);
    if (next != currentWaypointIndex)
    {
      Vector3 initialDirection = (waypoints[next] - waypoints[currentWaypointIndex]).normalized;
      SetTargetRotation(initialDirection);
      transform.rotation = targetRotation; // snap on spawn only
    }

    if (!active.Contains(this)) active.Add(this);
  }

  // Tint goes through a MaterialPropertyBlock, never the shared material:
  // several EnemyConfigs point at the same prefab, and the pool is keyed by
  // prefab, so writing the material would recolour every other type using it.
  private void ApplyAppearance(EnemyConfig cfg, float sizeScale)
  {
    currentScale = baseScale * Mathf.Max(0.01f, cfg.scaleMultiplier * sizeScale);
    transform.localScale = currentScale;

    // The type's own colour, shifted into the current biome. Multiplying keeps
    // the types telling themselves apart (shielded reads blue everywhere) while
    // the whole cast still belongs to the environment it is walking through.
    Color own = cfg.overrideBodyColor ? cfg.bodyColor : defaultBodyColor;
    Color tint = EnvironmentTheme.EnemyTint;
    bodyColor = new Color(own.r * tint.r, own.g * tint.g, own.b * tint.b, own.a);

    if (bodyRenderer == null) return;
    propertyBlock ??= new MaterialPropertyBlock();

    bodyRenderer.GetPropertyBlock(propertyBlock);
    Material shared = bodyRenderer.sharedMaterial;
    if (shared != null && shared.HasProperty("_BaseColor"))
    {
      propertyBlock.SetColor(BaseColorId, bodyColor);
    }
    if (shared != null && shared.HasProperty("_Color"))
    {
      propertyBlock.SetColor(ColorId, bodyColor);
    }
    bodyRenderer.SetPropertyBlock(propertyBlock);
  }

  private void SetTargetRotation(Vector3 direction)
  {
    if (direction != Vector3.zero)
    {
      // Keep the y-axis rotation only, maintain upright position
      direction.y = 0;
      targetRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, rotationOffset, 0);
    }
  }

  private void Update()
  {
    if (waypoints == null) return;

    // Move towards the next path point
    Vector3 targetPosition = waypoints[currentWaypointIndex];
    transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

    // Look in the movement direction, turning smoothly instead of snapping
    Vector3 direction = (targetPosition - transform.position).normalized;
    SetTargetRotation(direction);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

    // Decay the hit-punch scale back to normal
    if (hitPunch > 0f)
    {
      hitPunch = Mathf.Max(0f, hitPunch - Time.deltaTime * 6f);
      transform.localScale = currentScale * (1f + hitPunch * 0.18f);
    }

    TickShield();
    TickHealer();

    // Check if reached path point
    if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
    {
      currentWaypointIndex++;
      if (currentWaypointIndex >= waypoints.Length)
      {
        DealDamageToBase();
        Remove();
      }
    }
  }

  // Regenerates only after a quiet spell, so sustained fire keeps it down and
  // a tower that merely chips at it never gets through.
  private void TickShield()
  {
    if (shieldMax <= 0f || shield >= shieldMax || config == null) return;
    if (Time.time - lastDamagedAt < config.shieldRegenDelay) return;

    shield = Mathf.Min(shieldMax,
      shield + shieldMax * config.shieldRegenRate * Time.deltaTime);
    UpdateHealthBar();
  }

  private void TickHealer()
  {
    if (config == null || !config.isHealer || Time.time < nextHealAt) return;
    nextHealAt = Time.time + config.healInterval;

    float radiusSqr = config.healRadius * config.healRadius;
    for (int i = 0; i < active.Count; i++)
    {
      Enemy other = active[i];
      if (other == null || other == this || other.isRemoved) continue;
      if (other.health >= other.maxHealth) continue;
      if ((other.transform.position - transform.position).sqrMagnitude > radiusSqr) continue;

      int amount = Mathf.Max(1,
        Mathf.RoundToInt(other.maxHealth * config.healShareOfMaxHealth));
      other.ReceiveHeal(amount);
    }
  }

  public void ReceiveHeal(int amount)
  {
    if (isRemoved || health >= maxHealth) return;

    health = Mathf.Min(maxHealth, health + amount);
    UpdateHealthBar();
    FloatingText.Spawn(
      transform.position + Vector3.up * (currentScale.y + 0.5f), $"+{amount}", HealColor);
  }

  private void DealDamageToBase()
  {
    if (GameManager.Instance != null)
    {
      GameManager.Instance.TakeDamage(damage);
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.BaseDamage);
      Debug.Log($"Base took {damage} damage!");
    }
  }

  // Shield sits in front of health on the same bar, so the fill drains through
  // the shield and on into health without EnemyHealthBar needing a second bar.
  private void UpdateHealthBar()
  {
    float total = maxHealth + shieldMax;
    healthBar?.SetHealth(total > 0f ? (health + shield) / total : 0f);
  }

  public void TakeDamage(int damageAmount)
  {
    if (isRemoved) return;

    // Apply armor damage reduction if any
    float reducedDamage = damageAmount * (1f - armorDamageReduction);
    int dealt = Mathf.RoundToInt(reducedDamage);
    lastDamagedAt = Time.time;

    Vector3 popupPos = transform.position + Vector3.up * (currentScale.y + 0.5f);

    // Shield absorbs first; only the overflow reaches health.
    if (shield > 0f)
    {
      float absorbed = Mathf.Min(shield, dealt);
      shield -= absorbed;
      dealt -= Mathf.RoundToInt(absorbed);
      FloatingText.Spawn(popupPos, Mathf.RoundToInt(absorbed).ToString(), ShieldColor);
    }

    if (dealt > 0)
    {
      health -= dealt;
      FloatingText.Spawn(popupPos, dealt.ToString(), DamageColor);
    }

    UpdateHealthBar();
    hitPunch = 1f;

    if (health <= 0)
    {
      GameManager.Instance?.AddGold(goldReward);
      FloatingText.Spawn(popupPos + Vector3.up * 0.4f, $"+{goldReward}", GoldColor, 6f);
      DeathEffect.Spawn(transform.position + Vector3.up * currentScale.y * 0.5f, bodyColor, currentScale.y);
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.EnemyDeath);
      SpawnSplitChildren();
      Remove();
    }
  }

  // Children continue from where the parent fell rather than from the spawn,
  // so killing a splitter late is genuinely worse than killing it early.
  private void SpawnSplitChildren()
  {
    if (config == null || !config.isSplitter || !spawnOverride.canSplit) return;
    if (EnemySpawner.Instance == null || waypoints == null) return;

    var childOverride = new SpawnOverride
    {
      startWaypoint = currentWaypointIndex,
      healthScale = config.splitHealthShare,
      sizeScale = config.splitScaleShare,
      speedScale = config.splitSpeedMultiplier,
      canSplit = false,
    };

    EnemySpawner.Instance.SpawnSplitChildren(config, waypoints, childOverride,
      Mathf.Max(0, config.splitCount), maxHealth, goldReward);
  }

  private void Remove()
  {
    if (isRemoved) return;
    isRemoved = true;

    active.Remove(this);
    GameManager.Instance?.OnEnemyRemoved();
    EnemyPool.Release(gameObject);
  }

  public void ApplySlow(float amount)
  {
    slowAmount = Mathf.Max(slowAmount, amount);
    speed = normalSpeed * (1 - slowAmount);
    StartCoroutine(SlowWearOff());
  }

  private IEnumerator SlowWearOff()
  {
    yield return new WaitForSeconds(slowDuration);
    slowAmount = 0f;
    speed = normalSpeed;
  }
}
