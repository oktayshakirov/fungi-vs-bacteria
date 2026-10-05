using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Regenerate the authored campaign in place. Existing budgets, health values,
// GUIDs, progression keys and references survive repeated generation.
public static class LevelGenerator
{
  [MenuItem("Tools/Level Generator/Generate All Levels")]
  public static void Generate()
  {
    EnemyConfig Load(string name)=>AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
    var basic=Load("BasicEnemy");var fast=Load("FastEnemy");var armor=Load("ArmoredEnemy");
    if(basic==null || fast==null || armor==null)throw new InvalidOperationException("Missing campaign enemy assets.");
    Directory.CreateDirectory("Assets/Settings/Generated/Paths");Directory.CreateDirectory("Assets/Settings/Generated/Waves");
    for(int biome=1;biome<=7;biome++)Directory.CreateDirectory($"Assets/Resources/Levels/Environment{biome}");
    AssetDatabase.Refresh();
    for(int biome=1;biome<=7;biome++)for(int number=1;number<=10;number++)
    {
      string levelPath=$"Assets/Resources/Levels/Environment{biome}/Level{number:00}.asset";
      bool newLevel=AssetDatabase.LoadAssetAtPath<LevelConfig>(levelPath)==null;
      var level=LoadOrCreate<LevelConfig>(levelPath);
      var path=LoadOrCreate<PathConfig>($"Assets/Settings/Generated/Paths/Env{biome}-Level{number:00}-Path.asset");
      var waves=LoadOrCreate<WaveConfig>($"Assets/Settings/Generated/Waves/Env{biome}-Level{number:00}-Waves.asset");
      bool campaign=CampaignChallenges.Owns(biome,number);
      path.pathName=$"Env{biome} Level{number} Path";
      path.pathGridCoordinates=campaign?CampaignChallenges.Path(biome,number):biome>=2?BiomeChallenges.Path(biome):OpeningSequence.Path(number==4?2:number);
      waves.waves=campaign?CampaignChallenges.Waves(biome,number):biome>=2?BiomeChallenges.Waves(biome):number==4?Phase4Encounter.Waves(basic,fast,armor):OpeningSequence.Waves(number,basic,fast,armor);
      level.levelNumber=number;level.environmentName="Environment "+biome;level.pathConfig=path;level.waveConfig=waves;
      if(newLevel){level.startingGold=500+((biome-1)*10+number-1)*24;level.startingHealth=100;}
      CampaignChallenges.Describe(level,biome,number);
      if(campaign)path.description=level.challengeTitle+": "+level.challengeBrief;
      EditorUtility.SetDirty(path);EditorUtility.SetDirty(waves);EditorUtility.SetDirty(level);
    }
    AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("LevelGenerator: 70 authored levels regenerated in place; existing budgets and save identities retained.");
  }
  public static void GenerateBatch()
  {
    try{Generate();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  private static T LoadOrCreate<T>(string path) where T:ScriptableObject
  {
    var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null)return existing;
    var asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;
  }
}
