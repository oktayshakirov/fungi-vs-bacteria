using UnityEngine;
using System.Collections.Generic;

public class Projectile : MonoBehaviour
{
  private ProjectileData data;
  private Transform target;
  private Enemy targetEnemy;
  private uint targetSpawn;
  private Collider[] hits = new Collider[32];
  private readonly HashSet<Enemy> damaged = new HashSet<Enemy>();

  [SerializeField] private float rotationSpeed = 20f;
  [SerializeField] private float maxLifetime = 5f;
  [SerializeField] private float collisionRadius = 0.5f;
  [SerializeField] private GameObject explosionPrefab;

  private float lifetime = 0f;

  public void Initialize(ProjectileData data)
  {
    this.data = data;
    lifetime = 0f;
    target=null;targetEnemy=null;damaged.Clear();
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.Projectile);
  }

  public void Seek(Transform target)
  {
    this.target = target;
    targetEnemy=target!=null?target.GetComponentInParent<Enemy>():null;
    targetSpawn=targetEnemy!=null?targetEnemy.SpawnVersion:0;
  }

  private void Update()
  {
    lifetime += Time.deltaTime;

    // Also drop the projectile if the target was pooled (deactivated) mid-flight,
    // otherwise it homes onto a dead/reused enemy's position.
    if (target == null || !target.gameObject.activeInHierarchy || TargetWasReused() || lifetime > maxLifetime)
    {
      CombatPool.Release(gameObject);
      return;
    }
    Vector3 direction = (target.position - transform.position).normalized;
    float distanceToTarget = Vector3.Distance(transform.position, target.position);
    if (distanceToTarget <= collisionRadius + data.Speed * Time.deltaTime)
    {
      transform.position = target.position;
      HitTarget();
      return;
    }
    float moveDistance = data.Speed * Time.deltaTime;
    transform.position += direction * moveDistance;
    Quaternion targetRotation = Quaternion.LookRotation(direction);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
  }

  private void HitTarget()
  {
    SpawnExplosionEffect();
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.TargetHit);
    ResolveImpact();
    CombatPool.Release(gameObject);
  }

  // Who this shot damages, and for how much - split out from HitTarget so a
  // check can drive it without the effect, the sound and the trip through the
  // pool. Public for the same reason Enemy.EmergeScaleAt and Enemy.WaddleScale
  // are: this is the part that has to be verified with the SAME numbers the
  // game runs, and a check that reimplements the branch proves nothing about
  // the branch. See CombatCheck.
  public void ResolveImpact()
  {
    // The shot's target may have died and come back out of the pool as a
    // different enemy since it was fired. Update drops a stale shot before it
    // can ever reach this, but the check belongs HERE as well: the guarantee is
    // about who gets damaged, so it has to hold for every caller rather than
    // depend on one particular path having looked first.
    if (TargetWasReused()) return;

    if (data.Chains)
    {
      Chain();
    }
    else if (data.IsAoE)
    {
      Explode();
    }
    else if (target != null)
    {
      DamageEnemy(target);
    }
  }

  // Also for CombatCheck: lets it set up a shot without a tower or a prefab.
  // Enemies are pooled, so a dead one's Transform stays non-null and is handed
  // out again as a new enemy. SpawnVersion is what tells the two apart.
  private bool TargetWasReused() =>
    targetEnemy != null && targetEnemy.SpawnVersion != targetSpawn;

  public void Aim(ProjectileData projectileData, Transform at, Vector3 position)
  {
    Initialize(projectileData);
    transform.position = position;
    Seek(at);
  }

  // The Shock tower's bolt: hit the target, then jump to the nearest enemy the
  // bolt has not already touched, up to ChainTargets times, losing a share of
  // the damage on each jump.
  //
  // Walks Enemy.Active rather than an OverlapSphere per jump. A chain of three
  // would be three physics queries per shot at two shots a second per tower,
  // and the registry exists precisely so the combat code never has to ask the
  // physics scene where the enemies are.
  //
  // Each jump measures from the enemy it is standing on, not from the impact
  // point, so a chain genuinely travels down a line of enemies instead of
  // being a splash in disguise - which is the difference the card promises.
  private void Chain()
  {
    damaged.Clear();

    Enemy current = targetEnemy;
    if (current == null && target != null) current = target.GetComponentInParent<Enemy>();
    if (current == null)
    {
      damaged.Clear();
      return;
    }

    float damage = data.Damage;
    DamageEnemy(current, Mathf.Max(1, Mathf.RoundToInt(damage)));
    damaged.Add(current);

    float radiusSqr = data.ChainRadius * data.ChainRadius;
    var enemies = Enemy.Active;

    for (int hop = 0; hop < data.ChainTargets; hop++)
    {
      // Each hop is worth less, but never less than 1 - a hop that reads as a
      // bolt and deals nothing is worse than no hop at all.
      damage *= 1f - data.ChainFalloff;
      int dealt = Mathf.Max(1, Mathf.RoundToInt(damage));

      Enemy next = null;
      float nearest = radiusSqr;
      Vector3 origin = current.transform.position;
      for (int i = 0; i < enemies.Count; i++)
      {
        Enemy candidate = enemies[i];
        if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
        if (damaged.Contains(candidate)) continue;
        float distance = (candidate.transform.position - origin).sqrMagnitude;
        if (distance <= nearest)
        {
          nearest = distance;
          next = candidate;
        }
      }

      if (next == null) break;

      ChainArc.Spawn(origin, next.transform.position, data.ChainTint);
      // Damage AFTER the arc is drawn: the hit can kill and pool the enemy, and
      // a pooled enemy is moved back to the nest on its next spawn, so reading
      // its position afterwards would draw the bolt to the wrong end of the path.
      DamageEnemy(next, dealt);
      damaged.Add(next);
      current = next;
    }

    damaged.Clear();
  }

  private void SpawnExplosionEffect()
  {
    if (explosionPrefab != null)
    {
      CombatPool.Spawn(explosionPrefab,transform.position,Quaternion.identity,true);
    }
  }



  private void Explode()
  {
    int count;
    while((count=Physics.OverlapSphereNonAlloc(transform.position,data.SplashRadius,hits))==hits.Length)
      System.Array.Resize(ref hits,hits.Length*2);
    damaged.Clear();
    for(int i=0;i<count;i++)
    {
      var collider=hits[i];hits[i]=null;
      var enemy=collider.GetComponentInParent<Enemy>();
      if(enemy!=null && enemy.gameObject.activeInHierarchy && damaged.Add(enemy))DamageEnemy(enemy.transform);
    }
    damaged.Clear();
  }

  private void DamageEnemy(Transform enemy)
  {
    if (enemy.TryGetComponent(out Enemy e)) DamageEnemy(e, data.Damage);
  }

  private void DamageEnemy(Enemy e, int amount)
  {
    // Poison and the slow go on BEFORE the damage: the damage can kill, and
    // applying a status to an enemy that has already gone back to the pool
    // would hand it to whichever enemy is handed out next.
    if (data.SlowsEnemies)
    {
      e.ApplySlow(data.SlowAmount, data.SlowTint);
    }
    if (data.Poisons)
    {
      e.ApplyPoison(data.PoisonPerSecond, data.PoisonDuration, data.PoisonTint);
    }
    e.TakeDamage(amount);
  }
}