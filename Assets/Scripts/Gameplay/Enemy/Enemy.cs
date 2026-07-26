using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour
{
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
  private float hitPunch = 0f;
  private Quaternion targetRotation;

  private static readonly Color DamageColor = new Color(1f, 0.85f, 0.2f);
  private static readonly Color GoldColor = new Color(1f, 0.9f, 0.35f);

  private void Awake()
  {
    baseScale = transform.localScale;
  }

  private void Start()
  {
    normalSpeed = speed;
  }

  public void Initialize(Vector3[] path, EnemyConfig enemyConfig)
  {
    // Full reset: instances come back from the pool with stale state
    StopAllCoroutines();
    currentWaypointIndex = 0;
    isRemoved = false;
    slowAmount = 0f;
    hitPunch = 0f;
    transform.localScale = baseScale;

    waypoints = path;
    health = enemyConfig.maxHealth;
    maxHealth = enemyConfig.maxHealth;
    speed = enemyConfig.moveSpeed * (enemyConfig.isFast ? enemyConfig.speedMultiplier : 1f);
    normalSpeed = speed;
    damage = enemyConfig.baseDamage;
    goldReward = enemyConfig.goldReward;
    armorDamageReduction = enemyConfig.isArmored ? enemyConfig.armorDamageReduction : 0f;

    if (healthBar == null)
    {
      healthBar = gameObject.GetComponent<EnemyHealthBar>();
      if (healthBar == null) healthBar = gameObject.AddComponent<EnemyHealthBar>();
    }
    healthBar.SetHealth(1f);

    transform.position = waypoints[0];

    // Set initial rotation to face the first waypoint
    if (waypoints.Length > 1)
    {
      Vector3 initialDirection = (waypoints[1] - waypoints[0]).normalized;
      SetTargetRotation(initialDirection);
      transform.rotation = targetRotation; // snap on spawn only
    }
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
      transform.localScale = baseScale * (1f + hitPunch * 0.18f);
    }

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

  private void DealDamageToBase()
  {
    if (GameManager.Instance != null)
    {
      GameManager.Instance.TakeDamage(damage);
      AudioManager.Instance.PlaySound(AudioManager.SoundType.BaseDamage);
      Debug.Log($"Base took {damage} damage!");
    }
  }

  public void TakeDamage(int damageAmount)
  {
    if (isRemoved) return;

    // Apply armor damage reduction if any
    float reducedDamage = damageAmount * (1f - armorDamageReduction);
    int dealt = Mathf.RoundToInt(reducedDamage);
    health -= dealt;
    healthBar?.SetHealth((float)health / maxHealth);

    // Juice: damage number + a small scale punch toward the camera
    Vector3 popupPos = transform.position + Vector3.up * (baseScale.y + 0.5f);
    FloatingText.Spawn(popupPos, dealt.ToString(), DamageColor);
    hitPunch = 1f;

    if (health <= 0)
    {
      GameManager.Instance?.AddGold(goldReward);
      FloatingText.Spawn(popupPos + Vector3.up * 0.4f, $"+{goldReward}", GoldColor, 6f);
      AudioManager.Instance.PlaySound(AudioManager.SoundType.EnemyDeath);
      Remove();
    }
  }

  private void Remove()
  {
    if (isRemoved) return;
    isRemoved = true;

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