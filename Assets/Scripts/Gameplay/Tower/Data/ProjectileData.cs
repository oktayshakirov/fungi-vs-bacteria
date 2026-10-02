using UnityEngine;

public struct ProjectileData
{
  public int Damage { get; }
  public bool IsAoE { get; }
  public float SplashRadius { get; }
  public bool SlowsEnemies { get; }
  public float SlowAmount { get; }
  public float Speed { get; }
  public Color SlowTint { get; }

  // Chain (Shock): the bolt jumps from the enemy it hit to ChainTargets more,
  // each within ChainRadius of the last, losing ChainFalloff of the damage on
  // every jump. Separate from IsAoE deliberately - a chain follows the enemies
  // and a splash does not care where they are, which is the whole difference
  // between the two towers.
  public int ChainTargets { get; }
  public float ChainRadius { get; }
  public float ChainFalloff { get; }
  public Color ChainTint { get; }

  // Poison: damage over time on the enemy itself, so it keeps working between
  // shots. Ignores armour (see Enemy.ApplyPoison).
  public float PoisonPerSecond { get; }
  public float PoisonDuration { get; }
  public Color PoisonTint { get; }

  public static readonly Color DefaultSlowTint = new Color(.42f, .82f, 1f);

  public ProjectileData(int damage, bool isAoE, float splashRadius, bool slowsEnemies,
    float slowAmount, float speed, Color? slowTint = null,
    int chainTargets = 0, float chainRadius = 0f, float chainFalloff = 0f,
    Color? chainTint = null,
    float poisonPerSecond = 0f, float poisonDuration = 0f, Color? poisonTint = null)
  {
    Damage = damage;
    IsAoE = isAoE;
    SplashRadius = splashRadius;
    SlowsEnemies = slowsEnemies;
    SlowAmount = slowAmount;
    Speed = speed;
    SlowTint = slowTint ?? DefaultSlowTint;
    ChainTargets = chainTargets;
    ChainRadius = chainRadius;
    ChainFalloff = chainFalloff;
    ChainTint = chainTint ?? new Color(.77f, .55f, 1f);
    PoisonPerSecond = poisonPerSecond;
    PoisonDuration = poisonDuration;
    PoisonTint = poisonTint ?? new Color(.60f, .86f, .24f);
  }

  public bool Chains => ChainTargets > 0 && ChainRadius > 0f;
  public bool Poisons => PoisonPerSecond > 0f && PoisonDuration > 0f;
}
