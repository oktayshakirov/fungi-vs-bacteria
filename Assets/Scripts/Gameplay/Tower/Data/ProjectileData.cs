using UnityEngine;

public struct ProjectileData
{
  public int Damage { get; }
  public bool IsAoE { get; }
  public float SplashRadius { get; }
  public bool SlowsEnemies { get; }
  public float SlowAmount { get; }
  public float Speed { get; }

  public ProjectileData(int damage, bool isAoE, float splashRadius, bool slowsEnemies, float slowAmount, float speed)
  {
    Damage = damage;
    IsAoE = isAoE;
    SplashRadius = splashRadius;
    SlowsEnemies = slowsEnemies;
    SlowAmount = slowAmount;
    Speed = speed;
  }
}