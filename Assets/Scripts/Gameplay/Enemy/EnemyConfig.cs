using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Tower Defense/Enemy")]
public class EnemyConfig : ScriptableObject
{
  [Header("Basic Properties")]
  public string enemyName = "New Enemy";
  public GameObject prefab;

  [Header("Stats")]
  public int maxHealth = 100;
  public float moveSpeed = 5f;
  public int baseDamage = 10;
  public int goldReward = 10;

  [Header("Special Properties")]
  public bool isArmored = false;
  public float armorDamageReduction = 0f;
  public bool isFast = false;
  public float speedMultiplier = 1f;

  // --- variety behaviours ---------------------------------------------------
  // Difficulty is capped by a structural ceiling: player power is bounded by
  // buildable cells (~33), so peak enemy health cannot exceed ~3x before a full
  // board starts losing (see HANDOFF section 6). Everything below scales the
  // game by changing what an enemy DOES rather than by raising its numbers.
  //
  // All four are driven from this asset and handled inside Enemy, deliberately:
  // the eight enemy prefabs are authored assets and adding a component to each
  // one per behaviour would mean editing them.

  [Header("Shield")]
  [Tooltip("Absorbs damage before health, and regenerates if left alone. " +
           "Punishes slow chip damage and rewards burst.")]
  public bool hasShield = false;
  // Fraction of maxHealth, so it scales with the per-wave health multiplier
  // instead of becoming irrelevant by level 70.
  public float shieldShareOfHealth = 0.5f;
  public float shieldRegenDelay = 4f;
  // Fraction of the shield pool restored per second once regen kicks in.
  public float shieldRegenRate = 0.25f;

  [Header("Healer")]
  [Tooltip("Periodically heals nearby enemies. Punishes towers spread thin " +
           "and rewards focusing the pack.")]
  public bool isHealer = false;
  public float healRadius = 6f;
  public float healInterval = 1.5f;
  // Fraction of the TARGET's maxHealth, so a healer stays relevant late.
  public float healShareOfMaxHealth = 0.08f;

  [Header("Splitter")]
  [Tooltip("Spawns smaller children where it died. Punishes single-target " +
           "boards and turns a late kill into a leak.")]
  public bool isSplitter = false;
  public int splitCount = 2;
  // Children spawn from this config, scaled down, and are barred from
  // splitting again so a chain cannot run away.
  public float splitHealthShare = 0.35f;
  public float splitScaleShare = 0.65f;
  public float splitSpeedMultiplier = 1.15f;

  [Header("Presentation")]
  [Tooltip("Tints the body so a type is readable at a glance. Applied through " +
           "a MaterialPropertyBlock - configs share prefabs, so writing to the " +
           "shared material would recolour every other type using it.")]
  public bool overrideBodyColor = false;
  public Color bodyColor = Color.white;
  public float scaleMultiplier = 1f;
}
