using UnityEngine;
//TODO: Add Damage over time effect (PoisonTower)

[CreateAssetMenu(fileName = "NewTower", menuName = "Tower Defense/Tower")]
public class TowerConfig : ScriptableObject
{
  [Header("Basic Info")]
  public string towerName = "New Tower";
  public GameObject towerPrefab;
  public Sprite towerIcon;
  public int cost = 100;

  [Tooltip("One line, plain language, about what this tower is FOR - the role " +
           "it plays, not its numbers. The stats are shown next to it already. " +
           "Kept short: it has to fit a single bar on a phone in landscape.")]
  [TextArea(2, 3)]
  public string description = "";

  [Header("Attack Properties")]
  public float range = 5f;
  public float fireRate = 1f;
  public int damage = 20;

  [Header("Special Abilities")]
  public bool isAoE = false;
  public float splashRadius = 0f;
  public bool slowsEnemies = false;
  public float slowAmount = 0f;

  [Header("Support")]
  // A support tower never shoots. Instead it raises the damage and fire rate of
  // every attacking tower inside its `range`. Boosts are fractions (0.3 = +30%)
  // and stack additively across overlapping support towers, which is far easier
  // to reason about while balancing than a multiplicative stack.
  public bool isSupport = false;
  [Range(0f, 1f)] public float damageBoost = 0f;
  [Range(0f, 1f)] public float fireRateBoost = 0f;

  public int sellValue => Mathf.RoundToInt(cost * 0.7f);
}