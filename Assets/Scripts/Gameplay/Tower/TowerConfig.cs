using UnityEngine;
//TODO: Add Damage over time effect (PoisonTower)
//TODO: Add passive effect ex: shield, health, damage boost, etc (AuraTower)

[CreateAssetMenu(fileName = "NewTower", menuName = "Tower Defense/Tower")]
public class TowerConfig : ScriptableObject
{
  [Header("Basic Info")]
  public string towerName = "New Tower";
  public GameObject towerPrefab;
  public Sprite towerIcon;
  public int cost = 100;

  [Header("Attack Properties")]
  public float range = 5f;
  public float fireRate = 1f;
  public int damage = 20;
  public bool canTargetAir = true;

  [Header("Special Abilities")]
  public bool isAoE = false;
  public float splashRadius = 0f;
  public bool slowsEnemies = false;
  public float slowAmount = 0f;

  public int sellValue => Mathf.RoundToInt(cost * 0.7f);
}