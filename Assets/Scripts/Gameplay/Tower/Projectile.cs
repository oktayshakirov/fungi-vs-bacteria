using UnityEngine;

public class Projectile : MonoBehaviour
{
  private ProjectileData data;
  private Transform target;

  [SerializeField] private float rotationSpeed = 20f;
  [SerializeField] private float maxLifetime = 5f;
  [SerializeField] private float collisionRadius = 0.5f;
  [SerializeField] private GameObject explosionPrefab;

  private float lifetime = 0f;

  public void Initialize(ProjectileData data)
  {
    this.data = data;
    AudioManager.Instance.PlaySound(AudioManager.SoundType.Projectile);
  }

  public void Seek(Transform target)
  {
    this.target = target;
  }

  private void Update()
  {
    lifetime += Time.deltaTime;

    if (target == null || lifetime > maxLifetime)
    {
      Destroy(gameObject);
      return;
    }
    Vector3 direction = (target.position - transform.position).normalized;
    float distanceToTarget = Vector3.Distance(transform.position, target.position);
    if (distanceToTarget <= collisionRadius)
    {
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
    AudioManager.Instance.PlaySound(AudioManager.SoundType.TargetHit);
    if (data.IsAoE)
    {
      Explode();
    }
    else
    {
      DamageEnemy(target);
    }

    Destroy(gameObject);
  }

  private void SpawnExplosionEffect()
  {
    if (explosionPrefab != null)
    {
      GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
      if (target != null)
      {
        explosion.transform.parent = target;
        explosion.transform.localScale = explosionPrefab.transform.localScale;
      }
      Destroy(explosion, 2f);
    }
  }



  private void Explode()
  {
    Collider[] colliders = Physics.OverlapSphere(transform.position, data.SplashRadius);
    foreach (Collider collider in colliders)
    {
      if (collider.CompareTag("Enemy"))
      {
        DamageEnemy(collider.transform);
      }
    }
  }

  private void DamageEnemy(Transform enemy)
  {
    if (enemy.TryGetComponent(out Enemy e))
    {
      e.TakeDamage(data.Damage);
      if (data.SlowsEnemies)
      {
        e.ApplySlow(data.SlowAmount);
      }
    }
  }
}