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
}
