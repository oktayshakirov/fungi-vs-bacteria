using UnityEngine;

[CreateAssetMenu(fileName = "NewWave", menuName = "Tower Defense/Wave")]
public class WaveConfig : ScriptableObject
{
  [System.Serializable]
  public class WaveEnemyGroup
  {
    public EnemyConfig enemyConfig;
    public int count = 5;

    // EnemyConfig assets are shared by all 70 levels, so the only way an enemy
    // can get tougher deeper into the game is per-wave scaling. Without these
    // the generator can only add MORE enemies, which makes levels longer rather
    // than harder — the whole difficulty curve was flat because of it.
    // Reward scales alongside health so income keeps pace with what a kill costs.
    public float healthMultiplier = 1f;
    public float rewardMultiplier = 1f;
  }

  [System.Serializable]
  public class Wave
  {
    public WaveEnemyGroup[] enemyGroups;
    public float timeBetweenSpawns = 1f;
    public float timeToNextWave = 5f;
    public int waveGoldReward = 50;
  }

  public Wave[] waves;
}