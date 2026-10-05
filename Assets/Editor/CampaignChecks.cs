using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CampaignChecks
{
  private static void Require(bool okay,string reason){if(!okay)throw new InvalidOperationException(reason);}
  public static void RunBatch()
  {
    try
    {
      var assets=new Dictionary<string,string>();var openings=new Dictionary<Object,string>();
      var budgets=new Dictionary<LevelConfig,Vector2Int>();
      var levels=new List<LevelConfig>();
      for(int biome=1;biome<=7;biome++)for(int number=1;number<=10;number++)
      {
        var level=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment{biome}/Level{number:00}.asset");
        Require(level!=null,"Missing campaign level");levels.Add(level);budgets[level]=new Vector2Int(level.startingGold,level.startingHealth);
        foreach(var asset in new Object[]{level,level.pathConfig,level.waveConfig}){string path=AssetDatabase.GetAssetPath(asset);assets[path]=AssetDatabase.AssetPathToGUID(path);}
        if(!CampaignChallenges.Owns(biome,number))foreach(var asset in new Object[]{level.pathConfig,level.waveConfig})openings[asset]=EditorJsonUtility.ToJson(asset);
      }
      CampaignChallenges.Apply();Validate(levels);
      var authored=levels.ToDictionary(level=>level,level=>EditorJsonUtility.ToJson(level.waveConfig)+EditorJsonUtility.ToJson(level.pathConfig)+level.challengeTitle+level.challengeBrief);
      for(int pass=0;pass<2;pass++)
      {
        LevelGenerator.Generate();Validate(levels);
        foreach(var entry in assets)Require(AssetDatabase.AssetPathToGUID(entry.Key)==entry.Value,"GUID changed: "+entry.Key);
        foreach(var entry in budgets)Require(entry.Key.startingGold==entry.Value.x && entry.Key.startingHealth==entry.Value.y,"Budget/health changed");
        foreach(var entry in openings)Require(EditorJsonUtility.ToJson(entry.Key)==entry.Value,"Authored opening changed: "+entry.Key.name);
        foreach(var level in levels)Require(authored[level]==EditorJsonUtility.ToJson(level.waveConfig)+EditorJsonUtility.ToJson(level.pathConfig)+level.challengeTitle+level.challengeBrief,"Regeneration changed mission content");
      }
      // A designer-adjusted budget must also survive regeneration.
      var probe=levels[4];int gold=probe.startingGold,health=probe.startingHealth;
      try{probe.startingGold=gold+37;probe.startingHealth=91;LevelGenerator.Generate();Require(probe.startingGold==gold+37 && probe.startingHealth==91,"Custom level tuning was overwritten");}
      finally{probe.startingGold=gold;probe.startingHealth=health;EditorUtility.SetDirty(probe);AssetDatabase.SaveAssets();}
      var report=new StringBuilder("biome,level,mission,waves,path_cells,starting_gold,enemies,bosses\n");
      foreach(var level in levels)report.AppendLine($"{level.environmentName},{level.levelNumber},{level.challengeTitle},{level.waveConfig.waves.Length},{level.pathConfig.pathGridCoordinates.Count},{level.startingGold},{level.waveConfig.waves.Sum(w=>w.enemyGroups.Sum(g=>g.count))},{level.waveConfig.waves.Sum(w=>w.enemyGroups.Where(g=>g.enemyConfig.hasBossPhases).Sum(g=>g.count))}");
      Directory.CreateDirectory("Builds/Campaign");File.WriteAllText("Builds/Campaign/mission-audit.csv",report.ToString());
      Debug.Log("CAMPAIGN CHECKS: 70 mission briefs, 60 wave/route profiles, legal paths/threats/hints, ten unchanged openings, 210 stable GUIDs, deterministic regeneration and custom-budget preservation passed.");EditorApplication.Exit(0);
    }
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  private static void Validate(List<LevelConfig> levels)
  {
    var keys=new HashSet<string>();int owned=0;
    foreach(var level in levels)
    {
      Require(keys.Add(level.environmentName+"/"+level.levelNumber),"Duplicate save identity");
      Require(!string.IsNullOrWhiteSpace(level.challengeTitle) && !string.IsNullOrWhiteSpace(level.challengeBrief),"Missing mission briefing");
      int biome=int.Parse(level.environmentName.Substring("Environment ".Length));
      if(!CampaignChallenges.Owns(biome,level.levelNumber))continue;owned++;
      var cells=level.pathConfig.pathGridCoordinates;var used=new HashSet<Vector2Int>();
      Require(cells.Count>=14 && cells.Count<=20 && cells[0].x==0 && cells[cells.Count-1].x==9,"Invalid route length/endpoints");
      for(int i=0;i<cells.Count;i++)
      {
        Require(cells[i].x>=0 && cells[i].x<10 && cells[i].y>=0 && cells[i].y<5 && used.Add(cells[i]),"Invalid or repeated route cell");
        for(int j=0;j<i;j++){int distance=Mathf.Abs(cells[i].x-cells[j].x)+Mathf.Abs(cells[i].y-cells[j].y);Require(j==i-1?distance==1:distance>1,"Disconnected or touching route branches");}
      }
      var waves=level.waveConfig.waves;Require(waves.Length>=4 && waves.Length<=6,"Campaign duration changed outside intended band");
      foreach(var wave in waves)
      {
        Require(wave.timeToNextWave==12 && !string.IsNullOrWhiteSpace(wave.planningHint) && wave.planningHint.Length<=125,"Missing/overlong hint or incorrect preparation");
        var kinds=new HashSet<EnemyConfig>();Require(wave.enemyGroups.Length<=5,"Too many simultaneous roles");
        foreach(var group in wave.enemyGroups)Require(group.enemyConfig!=null && kinds.Add(group.enemyConfig) && group.count>0 && group.healthMultiplier>0 && group.healthMultiplier<=2.6f && group.rewardMultiplier>=1 && WaveIntel.Portrait(group.enemyConfig)!=null,"Invalid campaign threat/scaling/portrait");
      }
      int bosses=waves.Sum(w=>w.enemyGroups.Where(g=>g.enemyConfig.hasBossPhases).Sum(g=>g.count));
      Require(bosses==(level.levelNumber==8 || level.levelNumber==10?1:0),"Unexpected phased boss count");
    }
    Require(owned==60,"Campaign profile count changed");
  }
}
