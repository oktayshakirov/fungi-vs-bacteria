using UnityEngine;

public enum TargetPriority { First, Strong, Nearest }

public class TowerTargeting : MonoBehaviour
{
  public Transform CurrentTarget { get; private set; }
  public TargetPriority Priority { get; private set; } = TargetPriority.First;
  private float range;
  private float nextSearch;
  private Enemy targetEnemy;
  private uint targetSpawn;

  // Reinitializing the range on upgrade keeps the player's chosen priority.
  public void Initialize(float range)
  {
    this.range = range;
    CurrentTarget = null;
    targetEnemy = null;
    nextSearch = Time.time + (GetInstanceID() & 15) * .006f;
  }

  public void SetPriority(TargetPriority priority)
  {
    Priority = priority;
    nextSearch = Time.time;
    FindNewTarget();
  }

  private float GroundDistanceSquared(Transform target)
  {
    Vector3 offset = target.position - transform.position;
    return offset.x * offset.x + offset.z * offset.z;
  }

  public void UpdateTarget()
  {
    if (CurrentTarget != null && (targetEnemy == null || !targetEnemy.IsTargetable
      || targetEnemy.SpawnVersion != targetSpawn || GroundDistanceSquared(CurrentTarget) > range * range))
      CurrentTarget = null;

    // Reconsider the best target even while the old one is valid: a fast
    // bacterium can overtake it. Staggered scans avoid a full-board frame spike.
    if (Time.time >= nextSearch)
    {
      nextSearch = Time.time + .10f;
      FindNewTarget();
    }
  }

  private void FindNewTarget()
  {
    Enemy best = null;
    float bestScore = float.PositiveInfinity;
    float bestExit = float.PositiveInfinity;
    var enemies = Enemy.Active;
    for (int i = 0; i < enemies.Count; i++)
    {
      Enemy enemy = enemies[i];
      if (enemy == null || !enemy.IsTargetable) continue;
      float distance = GroundDistanceSquared(enemy.transform);
      if (distance > range * range) continue;
      float exit = enemy.DistanceToExit;
      float score = Priority == TargetPriority.Nearest ? distance
        : Priority == TargetPriority.Strong ? -enemy.RemainingStrength : exit;
      if (best == null || score < bestScore || (Mathf.Approximately(score, bestScore) && exit < bestExit))
      {
        best = enemy;
        bestScore = score;
        bestExit = exit;
      }
    }
    targetEnemy = best;
    targetSpawn = best != null ? best.SpawnVersion : 0;
    CurrentTarget = best != null ? best.transform : null;
  }

  public void DrawRangeGizmo()
  {
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(transform.position, range);
  }
}
