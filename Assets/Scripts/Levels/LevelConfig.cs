using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelConfig", menuName = "Tower Defense/Level Config")]
public class LevelConfig : ScriptableObject
{
  [Header("Identity")]
  public int levelNumber = 1;
  public string environmentName = "Environment 1";

  [Header("Gameplay")]
  public PathConfig pathConfig;
  public WaveConfig waveConfig;
  public int startingGold = 500;
  public int startingHealth = 100;
}
