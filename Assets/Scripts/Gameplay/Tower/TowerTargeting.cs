using UnityEngine;

public class TowerTargeting : MonoBehaviour
{
  public Transform CurrentTarget { get; private set; }
  private float range;
  private float nextSearch;
  private Enemy targetEnemy;
  private uint targetSpawn;

  public void Initialize(float range)
  {
    this.range = range;
    CurrentTarget=null; targetEnemy=null;
    nextSearch=Time.time+(GetInstanceID()&15)*.006f;
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
      if (!CurrentTarget.gameObject.activeInHierarchy || targetEnemy == null || targetEnemy.SpawnVersion != targetSpawn)
      {
        CurrentTarget = null;
      }
      else if (GetDistanceToTarget(CurrentTarget) > range)
      {
        CurrentTarget = null;
      }
    }

    if (CurrentTarget == null && Time.time >= nextSearch)
    {
      nextSearch = Time.time + .10f;
      FindNewTarget();
    }
  }

  private void FindNewTarget()
  {
    float shortestDistance = range*range;
    Enemy nearest = null;
    var enemies = Enemy.Active;
    for(int i=0;i<enemies.Count;i++)
    {
      Enemy enemy=enemies[i];
      if(enemy==null || !enemy.gameObject.activeInHierarchy) continue;
      float distance=(transform.position-enemy.transform.position).sqrMagnitude;
      if(distance<=shortestDistance){shortestDistance=distance;nearest=enemy;}
    }
    targetEnemy=nearest;
    targetSpawn=nearest!=null?nearest.SpawnVersion:0;
    CurrentTarget=nearest!=null?nearest.transform:null;
  }

  public void DrawRangeGizmo()
  {
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(transform.position, range);
  }
}