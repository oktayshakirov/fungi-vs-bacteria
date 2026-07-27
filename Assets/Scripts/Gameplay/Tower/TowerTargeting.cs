using UnityEngine;

public class TowerTargeting : MonoBehaviour
{
  public Transform CurrentTarget { get; private set; }
  private float range;

  public void Initialize(float range)
  {
    this.range = range;
  }

  private float GetDistanceToTarget(Transform target)
  {
    float distanceToEnemy = Vector3.Distance(transform.position, target.position);

    return distanceToEnemy;
  }

  public void UpdateTarget()
  {
    if (CurrentTarget != null)
    {
      // A pooled enemy is deactivated, not destroyed, so the reference stays
      // non-null. Without this check the tower keeps firing at a dead (or
      // reused) enemy's position — shooting at "nothing".
      if (!CurrentTarget.gameObject.activeInHierarchy)
      {
        CurrentTarget = null;
      }
      else if (GetDistanceToTarget(CurrentTarget) > range)
      {
        CurrentTarget = null;
      }
    }

    if (CurrentTarget == null)
    {
      FindNewTarget();
    }
  }

  private void FindNewTarget()
  {
    GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
    float shortestDistance = Mathf.Infinity;
    GameObject nearestEnemy = null;

    foreach (GameObject enemy in enemies)
    {
      float distanceToEnemy = GetDistanceToTarget(enemy.transform);

      if (distanceToEnemy < shortestDistance && distanceToEnemy <= range)
      {
        shortestDistance = distanceToEnemy;
        nearestEnemy = enemy;
      }
    }

    CurrentTarget = nearestEnemy?.transform;
  }

  public void DrawRangeGizmo()
  {
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(transform.position, range);
  }
}