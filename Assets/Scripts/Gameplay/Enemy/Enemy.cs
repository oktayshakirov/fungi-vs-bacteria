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

  [SerializeField] private float rotationOffset = 0f;

  private void Start()
  {
    normalSpeed = speed;
  }

  public void Initialize(Vector3[] path, EnemyConfig enemyConfig)
  {
    waypoints = path;
    health = enemyConfig.maxHealth;
    speed = enemyConfig.moveSpeed * (enemyConfig.isFast ? enemyConfig.speedMultiplier : 1f);
    normalSpeed = speed;
    damage = enemyConfig.baseDamage;
    goldReward = enemyConfig.goldReward;
    armorDamageReduction = enemyConfig.isArmored ? enemyConfig.armorDamageReduction : 0f;

    transform.position = waypoints[0];

    // Set initial rotation to face the first waypoint
    if (waypoints.Length > 1)
    {
      Vector3 initialDirection = (waypoints[1] - waypoints[0]).normalized;
      UpdateRotation(initialDirection);
    }
  }

  private void UpdateRotation(Vector3 direction)
  {
    if (direction != Vector3.zero)
    {
      // Keep the y-axis rotation only, maintain upright position
      direction.y = 0;
      Quaternion targetRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, rotationOffset, 0);
      transform.rotation = targetRotation;
    }
  }

  private void Update()
  {
    if (waypoints == null) return;

    // Move towards the next path point
    Vector3 targetPosition = waypoints[currentWaypointIndex];
    transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

    // Look in the movement direction
    Vector3 direction = (targetPosition - transform.position).normalized;
    UpdateRotation(direction);

    // Check if reached path point
    if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
    {
      currentWaypointIndex++;
      if (currentWaypointIndex >= waypoints.Length)
      {
        DealDamageToBase();
        Destroy(gameObject);
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
    // Apply armor damage reduction if any
    float reducedDamage = damageAmount * (1f - armorDamageReduction);
    health -= Mathf.RoundToInt(reducedDamage);
    if (health <= 0)
    {
      GameManager.Instance?.AddGold(goldReward);
      AudioManager.Instance.PlaySound(AudioManager.SoundType.EnemyDeath);
      Destroy(gameObject);
    }
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