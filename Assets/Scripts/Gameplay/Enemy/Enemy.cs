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
  public uint SpawnVersion { get; private set; }
  public int damage { get; private set; } = 10;
  private int goldReward;
  private float armorDamageReduction = 0f;

  private Vector3[] waypoints;

  private int currentWaypointIndex = 0;
  private float[] distanceToExit;

  public bool IsTargetable => !isRemoved && health > 0 && gameObject.activeInHierarchy;
  public float RemainingStrength => health + shield;

  // Distance along the remaining route, not a shortcut across a winding path.
  // Cached suffix lengths make each tower's priority scan O(enemies).
  public float DistanceToExit
  {
    get
    {
      if (waypoints == null || distanceToExit == null || currentWaypointIndex >= waypoints.Length)
        return float.PositiveInfinity;
      Vector3 offset = waypoints[currentWaypointIndex] - transform.position;
      offset.y = 0;
      return offset.magnitude + distanceToExit[currentWaypointIndex];
    }
  }

  private float slowAmount = 0f;
  private float slowDuration = 2f;
  private float slowedUntil;
  private Color slowTint;
  private int statusAppearance;

  // Poison: the Poison tower's whole mechanic. See ApplyPoison.
  private float poisonPerSecond;
  // Seconds LEFT, counted down by the same dt that meters the dose, rather than
  // an absolute Time.time deadline. Both halves of the mechanic then run off
  // one clock, which is what makes it checkable at all - see CombatCheck.
  private float poisonRemaining;
  private Color poisonTint;
  private float poisonCarry;

  public bool IsPoisoned => poisonPerSecond > 0f && poisonRemaining > 0f;

  // Frozen outranks slowed outranks poisoned, because that is the order the
  // player needs to read: being stopped matters more than being slow, and
  // either matters more than taking damage they can already see in the numbers.
  private void RefreshStatusAppearance()
  {
    int state = (IsFrozen ? 3 : slowAmount > 0 ? 2 : IsPoisoned ? 1 : 0) + (int)Phase*4;
    if(state == statusAppearance) return;
    statusAppearance = state;
    if(bodyRenderer == null) return;
    propertyBlock ??= new MaterialPropertyBlock();
    bodyRenderer.GetPropertyBlock(propertyBlock);
    Color phaseColor=Phase==BossStage.Fortified?Color.Lerp(bodyColor,new Color(.64f,.49f,.27f),.14f)
      :Phase==BossStage.Rushing?Color.Lerp(bodyColor,new Color(.76f,.27f,.24f),.18f):bodyColor;
    Color color = state%4 == 3 ? Color.Lerp(phaseColor,new Color(.65f,.9f,1f),.7f)
      : state%4 == 2 ? Color.Lerp(phaseColor,slowTint,.55f)
      : state%4 == 1 ? Color.Lerp(phaseColor,poisonTint,.5f) : phaseColor;
    propertyBlock.SetColor(BaseColorId,color);
    propertyBlock.SetColor(ColorId,color);
    bodyRenderer.SetPropertyBlock(propertyBlock);
  }

  private float normalSpeed;
  private bool isRemoved = false;
  private int maxHealth;
  private int pendingDamage, pendingShield, pendingHeal;
  private float nextDamagePopup;
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
  public enum BossStage { Stable, Fortified, Rushing }
  public BossStage Phase { get; private set; }
  public bool IsBossWarning => pendingBossStage != Phase;
  private BossStage pendingBossStage;
  private float bossWarningLeft;
  private BossCue bossCue;
  private CombatPulse bossWarningPulse;
  private FloatingText bossWarningText;
  private uint warningPulseRevision,warningTextRevision;
  private float birthStartedAt = -1;
  public float BodyRadius => bodyRenderer!=null ? Mathf.Clamp(Mathf.Max(bodyRenderer.bounds.extents.x,bodyRenderer.bounds.extents.z),.35f,4f) : .6f;
  private SpawnOverride spawnOverride = SpawnOverride.Default;

  private MeshRenderer bodyRenderer;
  private EnemyTrait[] traits;
  private ShieldSkin shieldSkin;
  private bool shieldWasUp = true;
  // Yaw is tracked separately from transform.rotation because the body's waddle
  // writes a roll on top of it. Slerping towards the target FROM the rotation
  // that already carries the roll would let the two fight, and the roll would
  // be slowly absorbed into the facing.
  private Quaternion yawRotation = Quaternion.identity;
  private float motionPhase;
  private static MaterialPropertyBlock propertyBlock;
  private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
  private static readonly int ColorId = Shader.PropertyToID("_Color");

  // Public so EnemyPreview can pose an enemy at a chosen point in the walk
  // cycle using the SAME numbers the game runs - a still frame is the only way
  // to check the amplitude, and duplicating the constants in the preview is
  // how a preview ends up reassuring you about motion the game does not have.
  public const float WaddleSpeed = 7.5f;
  public const float WaddleRollDegrees = 3.5f;

  // The other half of the spawn illusion; SpawnEffect is the first half. An
  // enemy swells up out of nothing inside the nest's mist instead of appearing
  // at full size. Starts well above zero because a speck is as noticeable as a
  // pop, and runs on scaled time so it keeps pace at 2x and 3x.
  //
  // Scale only, never position: the pathing reads transform.position and
  // decides it has arrived when the distance to the waypoint drops under 0.1,
  // so lifting the root out of the ground would feed straight back into the
  // arrival test (the same trap the waddle avoids, see ApplyMotion).
  private const float EmergeDuration = 0.34f;
  private const float EmergeFrom = 0.10f;
  private float emergeStartedAt = -1f;

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
    // Awake runs inside Instantiate, before EnemyPool can change the scale.
    // Apply the shared visual size here once; Initialize always uses this
    // cached rest scale, including when a splitter child returns to the pool.
    baseScale = transform.localScale * UnitScale.Enemy;
    transform.localScale = baseScale;
    currentScale = baseScale;

    traits = GetComponentsInChildren<EnemyTrait>(true);
    shieldSkin = GetComponent<ShieldSkin>();
    bodyRenderer = FindBodyRenderer(gameObject);
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

  // The body is the first MeshRenderer that is NOT part of an EnemyTrait.
  //
  // Everything that wants "the body" used to just take
  // GetComponentInChildren<MeshRenderer>(), i.e. the first renderer in
  // depth-first order. Trait geometry is parented under the same root, so that
  // call can return a carapace or a spore crown instead - which would tint the
  // trait and leave the body its authored colour, and would park the health bar
  // at the trait's height. Shared so EnemyHealthBar and EnemySpawner agree with
  // Enemy about which renderer is the body; do not reintroduce the bare call.
  public static MeshRenderer FindBodyRenderer(GameObject root)
  {
    if (root == null) return null;
    MeshRenderer[] all = root.GetComponentsInChildren<MeshRenderer>(true);
    foreach (MeshRenderer r in all)
    {
      if (r != null && r.GetComponentInParent<EnemyTrait>(true) == null) return r;
    }
    return all.Length > 0 ? all[0] : null;
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
    unchecked { SpawnVersion++; }

    // Full reset: instances come back from the pool with stale state
    StopAllCoroutines();
    isRemoved = false;
    pendingDamage=pendingShield=pendingHeal=0; nextDamagePopup=0;
    ClearBossWarning();
    Phase=pendingBossStage=BossStage.Stable;bossWarningLeft=0;birthStartedAt=-1;
    slowAmount = 0f;
    slowedUntil = 0f;
    // Same trap as frozenUntil below: a poisoned enemy that died mid-dose would
    // otherwise hand the rest of that dose to the next enemy out of the pool,
    // which would then bleed to death on the way out of the nest.
    poisonPerSecond = 0f;
    poisonRemaining = 0f;
    poisonCarry = 0f;
    statusAppearance = 0;
    // Pooled instances come back with this still set: a Frost Wave that caught
    // an enemy just before it died would otherwise hand the next enemy out of
    // the pool the rest of that freeze, standing still on the path for no
    // visible reason. The comment above says "full reset" - this is part of it.
    frozenUntil = 0f;
    hitPunch = 0f;
    // Pooled instances come back mid-emergence otherwise, and the next enemy
    // out of the pool would start at a third of its size for no reason.
    emergeStartedAt = -1f;
    // A per-enemy offset, or a whole wave waddles in lockstep and reads as one
    // object. Seeded from the instance id so a pooled enemy is stable rather
    // than re-randomising every time it is reused.
    motionPhase = (GetInstanceID() & 1023) / 1023f * Mathf.PI * 2f;

    waypoints = path;
    distanceToExit = new float[waypoints.Length];
    for (int i = waypoints.Length - 2; i >= 0; i--)
    {
      Vector3 segment = waypoints[i + 1] - waypoints[i];
      segment.y = 0;
      distanceToExit[i] = distanceToExit[i + 1] + segment.magnitude;
    }
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
    ApplyTraitAppearance();

    if (healthBar == null)
    {
      healthBar = gameObject.GetComponent<EnemyHealthBar>();
      if (healthBar == null) healthBar = gameObject.AddComponent<EnemyHealthBar>();
    }
    UpdateHealthBar();
    if(enemyConfig.hasBossPhases && bossCue==null) bossCue=gameObject.AddComponent<BossCue>();
    bossCue?.Pose(BodyRadius*1.1f,Phase,false);

    transform.position = waypoints[currentWaypointIndex];

    // Set initial rotation to face the next waypoint
    int next = Mathf.Min(currentWaypointIndex + 1, waypoints.Length - 1);
    if (next != currentWaypointIndex)
    {
      Vector3 initialDirection = (waypoints[next] - waypoints[currentWaypointIndex]).normalized;
      SetTargetRotation(initialDirection);
      yawRotation = targetRotation;
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

  // Traits carry their own accent, so they are tinted separately from the body
  // rather than inheriting bodyColor - a shielded enemy has to stay readably
  // blue even in the biome that pushes every body towards blue.
  private void ApplyTraitAppearance()
  {
    Color tint = EnvironmentTheme.EnemyTint;
    bool up = shield > 0f;
    shieldWasUp = up;
    // Before the trait loop and outside its null guard: ShieldedEnemy carries a
    // ShieldSkin and NO traits at all, so a return above this would leave a
    // pooled enemy wearing whichever skin the last one it was died in.
    shieldSkin?.Apply(up);
    if (traits == null) return;
    foreach (EnemyTrait t in traits)
    {
      if (t == null) continue;
      t.ApplyTint(tint);
      t.SetShieldUp(up);
    }
  }

  // Called only when the shield crosses empty, not every frame: SetShieldUp
  // walks the trait's renderers.
  private void SyncShieldCue()
  {
    bool up = shield > 0f;
    if (up == shieldWasUp) return;
    shieldWasUp = up;
    if(up) CombatPulse.Emit(new Vector3(transform.position.x,.15f,transform.position.z),BodyRadius,
      ShieldColor,.35f,CombatPulse.Shape.Ring);
    shieldSkin?.Apply(up);
    if (traits == null) return;
    foreach (EnemyTrait t in traits)
    {
      if (t != null) t.SetShieldUp(up);
    }
  }

  // Called by EnemySpawner straight after Initialize, for enemies arriving out
  // of the nest. Splitter children deliberately do NOT get it: they are already
  // introduced by the parent's death burst, and swelling them as well read as
  // the burst stuttering.
  public void PlayEmergence() => emergeStartedAt = Time.time;

  private float EmergeScale()
  {
    if (emergeStartedAt < 0f) return 1f;
    float t = (Time.time - emergeStartedAt) / EmergeDuration;
    if (t >= 1f)
    {
      emergeStartedAt = -1f;
      return 1f;
    }
    return EmergeScaleAt(t);
  }

  // Public and static for the same reason WaddleScale is: it is the shape of
  // the motion, and CameraPreview poses it to photograph a spawn rather than
  // guessing at what a batch render cannot show. `t` runs 0 to 1.
  public static float EmergeScaleAt(float t)
  {
    t = Mathf.Clamp01(t);
    // Smoothstep, NOT an ease-out. An ease-out is fastest at the start, so the
    // enemy was already a third of its size in the first frame - visible
    // before the mist had built up to anything, which is the one thing this is
    // supposed to prevent. Smoothstep holds it small while the mist thickens
    // and does most of the growing underneath it.
    float eased = t * t * (3f - 2f * t);
    // Overshoots a little at the end, so it lands with a bounce rather than
    // easing silently into its final size.
    return Mathf.Lerp(EmergeFrom, 1.04f, eased) - 0.04f * eased * eased;
  }

  // The walk: a squash-and-stretch waddle and a small roll, plus whatever each
  // composed part does.
  //
  // Deliberately NOT a vertical bob on this transform. Pathing reads
  // transform.position, moves it with MoveTowards and decides it has arrived
  // when the distance to the waypoint drops under 0.1 - so lifting the root
  // would feed the bob straight back into the arrival test and an enemy could
  // hover next to a waypoint without ever reaching it. Squash and stretch buys
  // the same sense of a footfall without touching position at all.
  //
  // Runs on SCALED time so the 2x and 3x speed controls make the walk faster,
  // which is what the eye expects when the whole board speeds up.
  private void ApplyMotion(float time)
  {
    Vector3 scale = WaddleScale(currentScale, time, motionPhase);
    float birth=birthStartedAt<0?1:Mathf.Lerp(.8f,1,Mathf.Clamp01((time-birthStartedAt)/.22f));
    transform.localScale=Vector3.Scale(scale,new Vector3(1+hitPunch*.06f,1-hitPunch*.05f,1+hitPunch*.06f))*EmergeScale()*birth;
    bossCue?.Pose(BodyRadius*1.1f,Phase,IsBossWarning);

    transform.rotation = yawRotation * WaddleRoll(time, motionPhase);

    if (traits == null) return;
    foreach (EnemyTrait trait in traits)
    {
      if (trait != null) trait.Animate(time);
    }
  }

  // Volume-preserving-ish: tall and narrow, then short and wide. Amplitudes
  // are small on purpose - the models are detailed and organic, and anything
  // stronger reads as the mesh deforming rather than as the creature walking.
  public static Vector3 WaddleScale(Vector3 rest, float time, float phase)
  {
    float wave = Mathf.Sin(time * WaddleSpeed + phase);
    return new Vector3(
      rest.x * (1f - wave * 0.028f),
      rest.y * (1f + wave * 0.055f),
      rest.z * (1f - wave * 0.028f));
  }

  public static Quaternion WaddleRoll(float time, float phase)
  {
    return Quaternion.Euler(
      0f, 0f, Mathf.Sin((time * WaddleSpeed + phase) * 0.5f) * WaddleRollDegrees);
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
    if(Time.time>=nextDamagePopup) FlushDamageText();
    if(slowAmount > 0 && Time.time >= slowedUntil) { slowAmount=0; speed=normalSpeed; }
    TickPoison(Time.deltaTime);
    RefreshStatusAppearance();
    if (isRemoved || waypoints == null || currentWaypointIndex >= waypoints.Length) return;

    StepBoss(Time.deltaTime);

    // Move towards the next path point
    Vector3 targetPosition = waypoints[currentWaypointIndex];
    if (IsFrozen) return;
    transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

    // Look in the movement direction, turning smoothly instead of snapping
    Vector3 direction = (targetPosition - transform.position).normalized;
    SetTargetRotation(direction);
    yawRotation = Quaternion.Slerp(yawRotation, targetRotation, turnSpeed * Time.deltaTime);

    if (hitPunch > 0f)
    {
      hitPunch = Mathf.Max(0f, hitPunch - Time.deltaTime * 6f);
    }

    ApplyMotion(Time.time);

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
    SyncShieldCue();
    UpdateHealthBar();
  }

  private void TickHealer()
  {
    if (config == null || !config.isHealer || Time.time < nextHealAt) return;
    nextHealAt = Time.time + config.healInterval;

    bool healed=false;
    float radiusSqr = config.healRadius * config.healRadius;
    for (int i = 0; i < active.Count; i++)
    {
      Enemy other = active[i];
      if (other == null || other == this || other.isRemoved) continue;
      if (other.health >= other.maxHealth) continue;
      if ((other.transform.position - transform.position).sqrMagnitude > radiusSqr) continue;

      int amount = Mathf.Max(1,
        Mathf.RoundToInt(other.maxHealth * config.healShareOfMaxHealth));
      other.ReceiveHeal(amount);healed=true;
    }
    if(healed) CombatPulse.Emit(new Vector3(transform.position.x,.15f,transform.position.z),Mathf.Min(4,config.healRadius),
      new Color(.42f,.76f,.43f),.48f,CombatPulse.Shape.Ring,false,transform);
  }

  public void ReceiveHeal(int amount)
  {
    if (isRemoved || health >= maxHealth || amount <= 0) return;

    int before=health;
    health = Mathf.Min(maxHealth, health + amount);
    pendingHeal+=health-before;
    UpdateHealthBar();
  }

  private void DealDamageToBase()
  {
    if (GameManager.Instance != null)
    {
      GameManager.Instance.TakeDamage(damage, config, config != null && config.isSplitter && !spawnOverride.canSplit);
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.BaseDamage);
      Debug.Log($"Base took {damage} damage!");
    }
  }

  // Health and shield have separate fills so regeneration is unambiguous.
  private void UpdateHealthBar()
  {
    healthBar?.SetHealth(maxHealth > 0 ? (float)health / maxHealth : 0);
    healthBar?.SetShield(shieldMax > 0 ? shield / shieldMax : 0, shieldMax > 0);
  }

  private Vector3 PopupPosition => bodyRenderer!=null
    ? new Vector3(transform.position.x,bodyRenderer.bounds.max.y+.5f,transform.position.z)
    : transform.position+Vector3.up*(currentScale.y+.5f);

  private void FlushDamageText()
  {
    if(pendingDamage==0 && pendingShield==0 && pendingHeal==0) return;
    Vector3 position=PopupPosition;
    if(pendingDamage>0) FloatingText.Spawn(position,pendingDamage.ToString(),DamageColor);
    if(pendingShield>0) FloatingText.Spawn(position+Vector3.up*.35f,pendingShield.ToString(),ShieldColor,4f);
    if(pendingHeal>0) FloatingText.Spawn(position+Vector3.up*.25f,$"+{pendingHeal}",HealColor,4);
    pendingDamage=pendingShield=pendingHeal=0;
    nextDamagePopup=Time.time+.35f;
  }

  public void TakeDamage(int damageAmount) => TakeDamage(damageAmount, false);

  public void TakeDamage(int damageAmount, bool ignoreArmor)
  {
    if (isRemoved) return;

    // Apply armor damage reduction if any
    float reducedDamage = ignoreArmor
      ? damageAmount
      : damageAmount * (1f - armorDamageReduction);
    int dealt = Mathf.RoundToInt(reducedDamage);
    lastDamagedAt = Time.time;

    Vector3 popupPos = PopupPosition;

    // Shield absorbs first; only the overflow reaches health.
    if (shield > 0f)
    {
      float absorbed = Mathf.Min(shield, dealt);
      shield -= absorbed;
      dealt -= Mathf.RoundToInt(absorbed);
      pendingShield += Mathf.RoundToInt(absorbed);
      if(shield<=0)
      {
        FloatingText.Spawn(popupPos+Vector3.up*.7f,"SHIELD BROKEN",ShieldColor,8f,true);
        CombatPulse.Emit(new Vector3(transform.position.x,.18f,transform.position.z),BodyRadius*1.1f,
          ShieldColor,.45f,CombatPulse.Shape.BrokenShield,true);
      }
      SyncShieldCue();
    }

    if (dealt > 0)
    {
      health -= dealt;
      pendingDamage += dealt;
    }

    UpdateHealthBar();
    hitPunch = 1f;
    if(health>0) BeginBossWarning();

    if (health <= 0)
    {
      FlushDamageText();
      GameManager.Instance?.AddGold(goldReward);
      FloatingText.Spawn(popupPos + Vector3.up * 0.4f, $"+{goldReward}", GoldColor, 6f);
      DeathEffect.Spawn(bodyRenderer!=null?bodyRenderer.bounds.center:transform.position,bodyColor,BodyRadius*2);
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

    CombatPulse.Emit(new Vector3(transform.position.x,.15f,transform.position.z),BodyRadius*1.1f,
      new Color(.91f,.63f,.25f),.4f,CombatPulse.Shape.Ring,true);
    EnemySpawner.Instance.SpawnSplitChildren(config, waypoints, childOverride,
      Mathf.Max(0, config.splitCount), maxHealth, goldReward);
  }

  public void PlaySplitBirth() { birthStartedAt=Time.time;transform.localScale=currentScale*.8f; }

  private void ClearBossWarning()
  {
    if(bossWarningPulse!=null) bossWarningPulse.Cancel(warningPulseRevision);
    if(bossWarningText!=null) bossWarningText.Cancel(warningTextRevision);
    bossWarningPulse=null;bossWarningText=null;
  }

  private void BeginBossWarning()
  {
    if(config==null || !config.hasBossPhases || maxHealth<=0 || isRemoved) return;
    float remaining=health/(float)maxHealth;
    BossStage desired=remaining<=.33f?BossStage.Rushing:remaining<=.66f?BossStage.Fortified:BossStage.Stable;
    if((int)desired<=(int)Phase || desired==pendingBossStage) return;
    ClearBossWarning();
    pendingBossStage=desired;
    bossCue?.Pose(BodyRadius*1.1f,Phase,true);
    bossWarningLeft=Mathf.Max(.2f,config.bossWarningDuration);
    bossWarningText=FloatingText.Spawn(PopupPosition+Vector3.up*.2f,
      desired==BossStage.Fortified?"FORTIFYING":"RUSH INCOMING",new Color(.94f,.73f,.29f),12f,true,bossWarningLeft,transform);
    bossWarningPulse=CombatPulse.Emit(new Vector3(transform.position.x,.15f,transform.position.z),BodyRadius*1.25f,
      new Color(.93f,.71f,.28f),bossWarningLeft,CombatPulse.Shape.Warning,true,transform);
    warningPulseRevision=bossWarningPulse!=null?bossWarningPulse.Revision:0;
    warningTextRevision=bossWarningText!=null?bossWarningText.Revision:0;
  }

  // Scaled dt keeps telegraphs consistent with pause and speed controls. A
  // lethal burst cancels the transition; poison can skip a fortified phase.
  public void StepBoss(float dt)
  {
    if(!IsTargetable || config==null || !config.hasBossPhases) return;
    BeginBossWarning();
    if(!IsBossWarning) return;
    bossWarningLeft-=Mathf.Max(0,dt);
    if(bossWarningLeft>0) return;
    ClearBossWarning();
    Phase=pendingBossStage;
    bossCue?.Pose(BodyRadius*1.1f,Phase,false);
    armorDamageReduction=Phase==BossStage.Fortified?config.bossFortifiedArmor:config.bossRushArmor;
    normalSpeed=config.moveSpeed*spawnOverride.speedScale*(Phase==BossStage.Rushing?config.bossRushSpeed:1);
    speed=normalSpeed*(1-slowAmount);
    statusAppearance=-1;RefreshStatusAppearance();
    CombatPulse.Emit(new Vector3(transform.position.x,.15f,transform.position.z),BodyRadius*1.1f,
      Phase==BossStage.Fortified?new Color(.80f,.60f,.27f):new Color(.85f,.31f,.23f),.45f,CombatPulse.Shape.Ring,true,transform);
  }

  private void Remove()
  {
    if (isRemoved) return;
    isRemoved = true;
    ClearBossWarning();

    active.Remove(this);
    GameManager.Instance?.OnEnemyRemoved();
    EnemyPool.Release(gameObject);
  }

  // Killed by a Spore Bomb: no gold, no split children, no damage numbers.
  //
  // Deliberately NOT TakeDamage(health). That path pays the kill reward, and a
  // bomb that paid rewards would earn back its own price on a dense wave, which
  // turns the booster into a coin farm (see BoosterEffects.Detonate). Splitting
  // is skipped for the same reason a bomb is worth 600 coins: it clears the
  // board, not "clears the board and leaves you the children".
  public void Vaporize()
  {
    if (isRemoved) return;

    health = 0;
    DeathEffect.Spawn(bodyRenderer!=null?bodyRenderer.bounds.center:transform.position,bodyColor,BodyRadius*2);
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.EnemyDeath);
    Remove();
  }

  // Frozen solid: movement stops entirely until the timer runs out. Kept apart
  // from ApplySlow, which is the Ice tower's percentage slow on its own
  // timer; each status expires independently.
  public void ApplyFreeze(float seconds)
  {
    frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
    RefreshStatusAppearance();
  }

  public bool IsFrozen => Time.time < frozenUntil;
  private float frozenUntil;

  // Damage over time, and the Poison tower's reason to exist. Three decisions
  // worth not re-litigating:
  //
  // - It IGNORES ARMOUR. Without that, Poison was a worse Inferno against the
  //   one enemy type - Armored - whose whole point is shrugging off per-hit
  //   damage; a tower whose damage arrives in many small pieces is exactly what
  //   a flat percentage reduction punishes hardest. Shields still absorb it,
  //   so Shielded remains a counter to chip damage (phase 20's design).
  // - It REFRESHES rather than stacking. Two Poison towers on one enemy stack
  //   to double damage and a wall of towers would then stack to arbitrary
  //   damage; taking the stronger dose and resetting the clock keeps the
  //   ceiling at "one tower's worth, kept topped up".
  // - It cannot kill-by-rounding. The per-frame dose is a fraction of a point
  //   at any sane dps, so it is accumulated in poisonCarry and spent in whole
  //   points; rounding each frame to the nearest int would either deal nothing
  //   at all or 1 damage per frame, i.e. ~60 dps regardless of the setting.
  public void ApplyPoison(float damagePerSecond, float duration, Color? tint = null)
  {
    if (isRemoved || !gameObject.activeInHierarchy) return;
    if (damagePerSecond <= 0f || duration <= 0f) return;

    if (damagePerSecond >= poisonPerSecond)
    {
      poisonPerSecond = damagePerSecond;
      poisonTint = tint ?? new Color(.60f, .86f, .24f);
    }
    poisonRemaining = Mathf.Max(poisonRemaining, duration);
    statusAppearance = -1;
    RefreshStatusAppearance();
  }

  // dt is passed in rather than read from Time so CombatCheck can step a dose
  // by hand; edit-mode Time.deltaTime is zero and nothing would ever tick.
  public void StepPoison(float dt) => TickPoison(dt);

  private void TickPoison(float dt)
  {
    if (poisonPerSecond <= 0f) return;

    // Only the remaining slice counts, so a long dt at the end of a dose does
    // not deal a whole frame's worth of poison the enemy no longer owes.
    float slice = Mathf.Min(dt, poisonRemaining);
    poisonRemaining -= dt;
    poisonCarry += poisonPerSecond * slice;

    // Whole points while the dose runs, and the leftover fraction rounded and
    // spent when it ends. Without that last step a dose always delivers one
    // point less than it promises: ten tenth-second ticks of 10/s accumulate to
    // 9.999... in float, so the tenth point is never reached and is then thrown
    // away with the carry.
    bool finished = poisonRemaining <= 0f;
    int dose = finished ? Mathf.RoundToInt(poisonCarry) : (int)poisonCarry;
    poisonCarry -= dose;

    if (finished)
    {
      poisonPerSecond = 0f;
      poisonCarry = 0f;
    }

    if (dose > 0) TakeDamage(dose, true);
  }

  public void ApplySlow(float amount, Color? tint = null)
  {
    if(isRemoved || !gameObject.activeInHierarchy) return;
    amount = Mathf.Clamp01(amount);
    if(amount >= slowAmount) slowTint = tint ?? new Color(.42f,.82f,1f);
    slowAmount = Mathf.Max(slowAmount, amount);
    slowedUntil = Time.time + slowDuration;
    speed = normalSpeed * (1 - slowAmount);
    statusAppearance = -1;
    RefreshStatusAppearance();
  }
}
