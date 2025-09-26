using UnityEngine;

[CreateAssetMenu(fileName = "NewWave", menuName = "Tower Defense/Wave")]
public class WaveConfig : ScriptableObject
{
  [System.Serializable]
  public class WaveEnemyGroup
  {
    public EnemyConfig enemyConfig;
    public int count = 5;
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