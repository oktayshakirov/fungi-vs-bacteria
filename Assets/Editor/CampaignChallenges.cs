using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Focused campaign missions, retained when assets are regenerated in place.
// The ten previously authored openings keep their routes, waves and budgets.
public static class CampaignChallenges
{
  public static bool Owns(int biome,int number) => biome==1 ? number>=5 : number>=2;
  public static string Title(int biome,int number)
  {
    if(!Owns(biome,number))
    {
      if(biome==1)return new[]{"First Defense","Catch the Runners","Armor Answers","Mycelium Links"}[number-1];
      return new[]{"","","Bend Coverage","Poison and Armor","Small but Many","Sustained Fire","Linked Defense","Mixed Counters"}[biome];
    }
    switch(number)
    {
      case 2:return "Runner Relay";
      case 3:return "Armor Patrol";
      case 4:return biome>=2?"Packed Bends":"Hold the Bends";
      case 5:return biome>=3?"Shield Line":"Armored Front";
      case 6:return biome>=4?"Split Colony":biome==2?"Swarm Crossing":"Crossfire";
      case 7:return biome==3 || biome>=5?"Healer Escort":biome==4?"Shield Escort":"Exit Watch";
      case 8:return "Boss Advance";
      case 9:return "Counter Mix";
      default:return "Colony Finale";
    }
  }
  public static string Brief(int biome,int number)
  {
    if(!Owns(biome,number))
    {
      if(biome==1)return new[]{"Cover a bend, then compare an upgrade with another Archer.","Pair Ice with damage to catch runners.","Poison bypasses armor. FIRST catches runners.","Link adjacent fungi; save Ice for the boss rush."}[number-1];
      return new[]{"","","Cover both bends and try an Archer style.","Use Poison for armor and overlap damage against healers.","Use area damage for swarms and Splitter children.","Keep shields under fire; link your strongest attackers.","Build overlapping damage and prepare for the Colony Boss.","Combine counters and protect the exit during the finale."}[biome];
    }
    switch(number)
    {
      case 2:return "Catch runners with Ice and coverage near the exit.";
      case 3:return "Use Poison for armor; invest the clear reward in damage.";
      case 4:return "Overlap damage around bends to handle the whole pack.";
      case 5:return biome>=3?"Keep shields under continuous fire to stop regeneration.":"Poison bypasses armor; keep strong damage on the road.";
      case 6:return biome>=4?"Cover the exit with area damage for Splitter children.":"Combine control and damage across several road sections.";
      case 7:return biome==3 || biome>=5?"Overlap damage to finish enemies between healer pulses.":"Keep damage near the exit for enemies leaving the pack.";
      case 8:return "Build for the boss: Poison for armor, Ice for its rush.";
      case 9:return "Use clear rewards to strengthen gaps between counters.";
      default:return "Defend six waves, then counter the Colony Boss and escort.";
    }
  }
  public static List<Vector2Int> Path(int biome,int number)
  {
    var points=new List<Vector2Int>();
    void Leg(int x,int y)
    {
      var target=new Vector2Int(x,y);
      if(points.Count==0){points.Add(target);return;}
      var at=points[points.Count-1];
      while(at!=target){at += at.x!=x ? new Vector2Int(x>at.x?1:-1,0) : new Vector2Int(0,y>at.y?1:-1);points.Add(at);}
    }
    switch((biome+number)%4)
    {
      case 0:Leg(0,1);Leg(2,1);Leg(2,3);Leg(4,3);Leg(4,1);Leg(6,1);Leg(6,3);Leg(9,3);break;
      case 1:Leg(0,0);Leg(2,0);Leg(2,4);Leg(6,4);Leg(6,0);Leg(9,0);break;
      case 2:Leg(0,2);Leg(3,2);Leg(3,0);Leg(6,0);Leg(6,4);Leg(9,4);break;
      default:points=BiomeChallenges.Path(7);break;
    }
    if((biome+number/2)%2==0)for(int i=0;i<points.Count;i++)points[i]=new Vector2Int(points[i].x,4-points[i].y);
    return points;
  }
  private static string Featured(int biome,int number)
  {
    if(number==2)return "FastEnemy";
    if(number==3)return "ArmoredEnemy";
    if(number==4)return biome>=2?"SwarmEnemy":"BasicEnemy";
    if(number==5)return biome>=3?"ShieldedEnemy":"ArmoredEnemy";
    if(number==6)return biome>=4?"SplitterEnemy":biome==2?"SwarmEnemy":"ArmoredEnemy";
    if(number==7)return biome==3 || biome>=5?"HealerEnemy":biome==4?"ShieldedEnemy":"FastEnemy";
    return biome==1?"ArmoredEnemy":biome==2?"SwarmEnemy":biome==3 || biome==6?"HealerEnemy":biome==4 || biome==7?"SplitterEnemy":"ShieldedEnemy";
  }
  private static string Counter(string featured)
  {
    switch(featured)
    {
      case "FastEnemy":return "Ice slows runners. FIRST focuses enemies nearest the exit.";
      case "ArmoredEnemy":return "Poison bypasses armor. STRONG keeps damage on the toughest target.";
      case "SwarmEnemy":return "Shock chains and Inferno splash damage punish tightly spaced swarms.";
      case "ShieldedEnemy":return "Sustain hits on shields; a break in damage lets them regenerate.";
      case "SplitterEnemy":return "Splitters leave children. Keep area damage covering their route.";
      case "HealerEnemy":return "Healers mend nearby allies. Overlap damage to finish enemies between pulses.";
      default:return "Overlap damage around bends. Link adjacent attackers with Aura or Defense.";
    }
  }
  public static WaveConfig.Wave[] Waves(int biome,int number)
  {
    EnemyConfig Load(string name)=>AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
    int difficulty=(biome-1)*10+number;
    float peak=1f+.1133f*Mathf.Pow(difficulty-1,.61f);
    int count=number==10?6:number>=6?5:4;
    string featured=Featured(biome,number);
    bool bossMission=number==8 || number==10;
    var waves=new WaveConfig.Wave[count];
    for(int index=0;index<count;index++)
    {
      bool last=index==count-1;
      float health=peak*Mathf.Lerp(.68f,1f,index/(float)(count-1));
      var groups=new List<WaveConfig.WaveEnemyGroup>();
      void Add(string name,int amount,float share=1f)
      {
        var config=name=="BossEncounterEnemy"?Phase4Encounter.BossConfig():Load(name);
        var existing=groups.Find(g=>g.enemyConfig==config);
        if(existing!=null){existing.count+=amount;return;}
        groups.Add(new WaveConfig.WaveEnemyGroup{enemyConfig=config,count=amount,healthMultiplier=health*share,rewardMultiplier=Mathf.Max(1,health)});
      }
      if(index>0)
      {
        int amount=featured=="HealerEnemy"?1:featured=="SplitterEnemy"?1+(last?1:0):featured=="ArmoredEnemy" || featured=="ShieldedEnemy"?2+(last?1:0):featured=="SwarmEnemy"?5+index:3+index/2;
        Add(featured,amount);
        if(featured=="HealerEnemy")Add("ArmoredEnemy",2+(last?1:0));
      }
      Add("BasicEnemy",featured=="SwarmEnemy" && index>0?3:5);
      if(index==0 || index>=2)Add("FastEnemy",index==0?1:2+(last?1:0),.9f);
      if(last && number>=6 && featured!="ArmoredEnemy" && featured!="HealerEnemy")Add("ArmoredEnemy",2);
      if(last && bossMission)Add("BossEncounterEnemy",1,.9f);
      string hint=index==0 ? "Cover the bends. Use the first clear reward to prepare for the next threat."
        : last && bossMission ? "Boss warns before armor and rush phases. Poison ignores armor; Ice slows the rush."
        : index==1 ? Counter(featured)
        : last ? "Final pack: "+Counter(featured)
        : Counter(featured);
      waves[index]=new WaveConfig.Wave{enemyGroups=groups.ToArray(),timeBetweenSpawns=index==0?1.2f:featured=="SwarmEnemy"?.65f:featured=="FastEnemy"?.75f:1f,
        timeToNextWave=12,waveGoldReward=35+biome*5+index*10,planningHint=hint};
    }
    return waves;
  }
  public static void Describe(LevelConfig level,int biome,int number)
  {
    level.challengeTitle=Title(biome,number);level.challengeBrief=Brief(biome,number);EditorUtility.SetDirty(level);
  }
  [MenuItem("Tools/Levels/Apply Campaign Challenges")]
  public static void Apply()
  {
    int changed=0;
    for(int biome=1;biome<=7;biome++)for(int number=1;number<=10;number++)
    {
      var level=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment{biome}/Level{number:00}.asset");
      Describe(level,biome,number);
      if(!Owns(biome,number))continue;
      level.pathConfig.pathGridCoordinates=Path(biome,number);level.pathConfig.description=Title(biome,number)+": "+Brief(biome,number);
      level.waveConfig.waves=Waves(biome,number);EditorUtility.SetDirty(level.pathConfig);EditorUtility.SetDirty(level.waveConfig);changed++;
    }
    AssetDatabase.SaveAssets();Debug.Log($"CAMPAIGN CHALLENGES: {changed} focused missions applied; ten authored openings, budgets and save identities retained.");
  }
  public static void ApplyBatch(){Apply();EditorApplication.Exit(0);}
}
