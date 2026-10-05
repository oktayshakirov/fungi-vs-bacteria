using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The first three meadow levels are authored lessons, also retained when the
// procedural campaign is regenerated. Apply updates these assets in place.
public static class OpeningSequence
{
  public static WaveConfig.Wave[] Waves(int level, EnemyConfig basic, EnemyConfig fast, EnemyConfig armored)
  {
    WaveConfig.Wave Make(float spacing, int reward, string hint, params WaveConfig.WaveEnemyGroup[] groups)
      => new WaveConfig.Wave { enemyGroups = groups, timeBetweenSpawns = spacing,
        timeToNextWave = 12, waveGoldReward = reward, planningHint = hint };
    WaveConfig.WaveEnemyGroup Group(EnemyConfig type, int count, float health)
      => new WaveConfig.WaveEnemyGroup { enemyConfig = type, count = count,
        healthMultiplier = health, rewardMultiplier = health };
    if (level == 1) return new[] {
      Make(1.8f, 25, "Start with Archer. Cover the bend to get more shots per enemy.", Group(basic,4,.6f)),
      Make(1.5f, 30, "Tap a placed fungus. Compare its next upgrade with another Archer.", Group(basic,5,.8f)),
      Make(1.2f, 40, "Stronger bacteria ahead. Upgrade a good spot or extend your coverage.", Group(basic,6,1.1f)) };
    if (level == 2) return new[] {
      Make(1.6f, 25, "Build damage around the bends; runners arrive in the next wave.", Group(basic,5,.8f)),
      Make(1.1f, 35, "Fast bacteria overtake the pack. Ice slows them; FIRST protects the exit.", Group(basic,4,.9f), Group(fast,2,.9f)),
      Make(.8f, 45, "Combine Ice with damage, or spread Archers along the route.", Group(basic,5,1), Group(fast,4,1)) };
    return new[] {
      Make(1.4f, 30, "Prepare for armor next. Leave space near a bend for Poison or Sniper.", Group(basic,5,.9f)),
      Make(1.1f, 40, "Armor halves direct damage. Poison ignores armor; Ice buys time.", Group(basic,4,1), Group(armored,2,1)),
      Make(.75f, 50, "Mixed wave: FIRST catches runners; STRONG focuses armored bacteria.", Group(basic,6,1.1f), Group(fast,3,1), Group(armored,2,1.1f)) };
  }

  public static List<Vector2Int> Path(int level)
  {
    Vector2Int P(int x,int y) => new Vector2Int(x,y);
    if(level == 1) return new List<Vector2Int> {P(0,2),P(1,2),P(2,2),P(3,2),P(3,1),P(4,1),P(5,1),P(6,1),P(6,2),P(7,2),P(8,2),P(9,2)};
    if(level == 2) return new List<Vector2Int> {P(0,3),P(1,3),P(2,3),P(3,3),P(3,2),P(3,1),P(4,1),P(5,1),P(6,1),P(6,2),P(6,3),P(7,3),P(8,3),P(9,3)};
    return new List<Vector2Int> {P(0,2),P(1,2),P(2,2),P(2,3),P(3,3),P(4,3),P(4,2),P(4,1),P(5,1),P(6,1),P(6,2),P(7,2),P(8,2),P(9,2)};
  }

  [MenuItem("Tools/Levels/Apply Opening Sequence")]
  public static void Apply()
  {
    var basic = AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/BasicEnemy.asset");
    var fast = AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/FastEnemy.asset");
    var armored = AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/ArmoredEnemy.asset");
    for(int i=1;i<=3;i++)
    {
      var level = AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment1/Level{i:00}.asset");
      level.waveConfig.waves = Waves(i,basic,fast,armored);
      level.pathConfig.pathGridCoordinates = Path(i);
      level.pathConfig.description = i==1 ? "Fundamentals: coverage and upgrade inspection." : i==2 ? "Counter lesson: fast bacteria and Ice." : "Mixed test: speed, armor, and target priority.";
      EditorUtility.SetDirty(level.waveConfig);
      EditorUtility.SetDirty(level.pathConfig);
    }
    AssetDatabase.SaveAssets();
    Debug.Log("OPENING SEQUENCE: three authored meadow levels applied in place.");
  }
}
