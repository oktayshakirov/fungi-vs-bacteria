using UnityEditor;
using UnityEngine;

// Only this encounter opts into boss phases. Existing campaign bosses keep
// their authored behavior until the prototype has been played and tuned.
public static class Phase4Encounter
{
  public const string BossPath="Assets/Settings/Enemies/BossEncounterEnemy.asset";
  public static EnemyConfig BossConfig()
  {
    var config=AssetDatabase.LoadAssetAtPath<EnemyConfig>(BossPath);
    if(config!=null) return config;
    config=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/BossEnemy.asset"));
    config.enemyName="Colony Boss";config.hasBossPhases=true;config.portraitResourceName="BossEnemy";
    AssetDatabase.CreateAsset(config,BossPath);
    return config;
  }
  public static WaveConfig.Wave[] Waves(EnemyConfig basic,EnemyConfig fast,EnemyConfig armored)
  {
    WaveConfig.WaveEnemyGroup Group(EnemyConfig cfg,int count,float health=1)
      => new WaveConfig.WaveEnemyGroup {enemyConfig=cfg,count=count,healthMultiplier=health,rewardMultiplier=health};
    WaveConfig.Wave Make(float spacing,int reward,string hint,params WaveConfig.WaveEnemyGroup[] groups)
      => new WaveConfig.Wave {enemyGroups=groups,timeBetweenSpawns=spacing,timeToNextWave=12,waveGoldReward=reward,planningHint=hint};
    return new[] {
      Make(1.5f,35,"Place an Aura beside attacking fungi. Adjacent neighbors link for +10% extra damage.",Group(basic,6,.9f)),
      Make(1.1f,45,"Mycelium links reward close placement. Poison handles armor; leave Ice for the boss rush.",Group(basic,5),Group(armored,2)),
      Make(1.5f,65,"Boss warns before fortifying at 66% health, then rushes below 33%. Poison and Ice counter it.",Group(BossConfig(),1,.9f),Group(fast,3),Group(basic,4)) };
  }
  [MenuItem("Tools/Levels/Apply Phase 4 Encounter")]
  public static void Apply()
  {
    var level=AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Resources/Levels/Environment1/Level04.asset");
    EnemyConfig Load(string name)=>AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
    level.waveConfig.waves=Waves(Load("BasicEnemy"),Load("FastEnemy"),Load("ArmoredEnemy"));
    level.pathConfig.pathGridCoordinates=OpeningSequence.Path(2);
    level.pathConfig.description="Mycelium support links and a telegraphed boss encounter.";
    EditorUtility.SetDirty(level.waveConfig);EditorUtility.SetDirty(level.pathConfig);
    AssetDatabase.SaveAssets();
    Debug.Log("PHASE 4 ENCOUNTER: authored meadow level 4 and an opt-in original-model boss variant.");
  }
}
