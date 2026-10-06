using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Legal scripted player plans over the real scene, enemies and projectiles.
// Spend only the starting wallet floor and earned rewards; no boosters/continues.
[InitializeOnLoad]
public static class CampaignPlayChecks
{
  private const string Pending="CampaignPlayChecks.Pending";
  private static readonly List<string> Keys=SaveKeys();
  private static readonly List<Vector3Int> Cases=Scenarios();
  private static readonly List<Tower> towers=new List<Tower>();
  private static int scenario,first,last,handledWave,upgrades;
  private static bool built;
  private static double loadedAt,deadline;
  private static string counter;
  private static StringBuilder report;
  static CampaignPlayChecks(){EditorApplication.playModeStateChanged+=Changed;}
  private static List<string> SaveKeys()
  {
    var keys=new List<string>{"Wallet_Coins","Wallet_LevelLoan","TutorialCompleted"};
    for(int biome=1;biome<=7;biome++){keys.Add("HighestCompletedLevel_Environment "+biome);for(int n=1;n<=10;n++)keys.Add("Stars_Environment "+biome+"_"+n);}
    return keys;
  }
  private static List<Vector3Int> Scenarios()
  {
    var cases=new List<Vector3Int>();
    for(int b=1;b<=7;b++)for(int n=1;n<=10;n++)if(CampaignChallenges.Owns(b,n))cases.Add(new Vector3Int(b,n,0));
    for(int b=2;b<=7;b++)foreach(int n in new[]{5,10})cases.Add(new Vector3Int(b,n,1));
    return cases;
  }
  private static int Arg(string name,int fallback)
  {
    var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]==name && int.TryParse(args[i+1],out int value))return value;return fallback;
  }
  public static void RunBatch()
  {
    foreach(string key in Keys){SessionState.SetBool(Pending+".Had."+key,PlayerPrefs.HasKey(key));SessionState.SetInt(Pending+".Value."+key,PlayerPrefs.GetInt(key,0));}
    SessionState.SetInt(Pending+".First",Arg("-campaignStart",0));SessionState.SetInt(Pending+".Count",Arg("-campaignCount",72));
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
  }
  private static void Changed(PlayModeStateChange state)
  {
    if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false))return;
    SessionState.SetBool(Pending,false);first=SessionState.GetInt(Pending+".First",0);last=Mathf.Min(Cases.Count,first+SessionState.GetInt(Pending+".Count",72));scenario=first;
    report=new StringBuilder("scenario,biome,level,plan,towers,upgrades,health,waves,coins\n");
    deadline=EditorApplication.timeSinceStartup+1800;EditorApplication.update+=Step;Next();
  }
  private static void Next()
  {
    if(GameManager.Instance!=null)Object.DestroyImmediate(GameManager.Instance.gameObject);
    towers.Clear();built=false;handledWave=0;upgrades=0;
    PlayerPrefs.SetInt("Wallet_Coins",0);PlayerPrefs.SetInt("Wallet_LevelLoan",0);PlayerPrefs.SetInt("TutorialCompleted",1);
    var item=Cases[scenario];GameSession.SelectedEnvironment="Environment "+item.x;
    GameSession.SelectedLevel=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment{item.x}/Level{item.y:00}.asset");
    Time.timeScale=1;EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainGame.unity",new LoadSceneParameters(LoadSceneMode.Single));loadedAt=EditorApplication.timeSinceStartup;
  }
  private static void Step()
  {
    try
    {
      if(EditorApplication.timeSinceStartup>deadline || EditorApplication.timeSinceStartup-loadedAt>90)throw new InvalidOperationException("Campaign run timed out at scenario "+scenario);
      if(EditorApplication.timeSinceStartup-loadedAt<.5)return;
      var manager=GameManager.Instance;var spawner=EnemySpawner.Instance;
      if(manager==null || spawner==null)throw new InvalidOperationException("Missing scene managers");
      var item=Cases[scenario];bool branch=item.z==1;
      if(!built)
      {
        if(manager.currentGold!=GameSession.SelectedLevel.startingGold)throw new InvalidOperationException("Clean wallet did not match legal starting budget");
        var threat=GameSession.SelectedLevel.waveConfig.waves[1].enemyGroups[0].enemyConfig;
        counter=threat.hasShield?"ArcherTower":threat.isSplitter?"InfernoTower":threat.isHealer?"PoisonTower":threat.isArmored?"PoisonTower":threat.isFast?"IceTower":threat.name=="SwarmEnemy"?"ShockTower":"ArcherTower";
        if(branch)
        {
          // Reserve the counter before paying for an upgrade: an early upgrade
          // alone leaves the Wetland armor mission without its Poison answer.
          if(counter!="ArcherTower")Build(counter);
          var archer=Build("ArcherTower");if(!archer.Upgrade())throw new InvalidOperationException("Legal Archer upgrade failed");upgrades++;
          if(manager.CanAfford(Config("IceTower").cost))Build("IceTower");
        }
        Spend(false);built=true;Time.timeScale=8;spawner.StartGame();
      }
      if(manager.HasEnded)
      {
        int health=manager.currentHealth;
        string row=$"{scenario},{item.x},{item.y},{(branch?"archer":"mixed")},{towers.Count},{upgrades},{health},{spawner.WavesStarted},{manager.currentGold}";
        report.AppendLine(row);Debug.Log("CAMPAIGN PLAY: "+row);
        if(health<=0 || !spawner.AreWavesComplete())throw new InvalidOperationException("Campaign plan failed: "+row);
        scenario++;if(scenario>=last){Debug.Log($"CAMPAIGN PLAY CHECKS: {last-first} legal real-scene completions passed (scenarios {first}..{last-1}); no external gold, boosters or continues.");Finish(0);}else Next();
        return;
      }
      if(spawner.CanStartWave && spawner.WavesStarted>handledWave)
      {
        handledWave=spawner.WavesStarted;Spend(true);spawner.StartNextWave();
      }
    }
    catch(Exception e){Debug.LogException(e);Finish(1);}
  }
  private static TowerConfig Config(string name)=>AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");
  private static Tower Build(string name)
  {
    var config=Config(name);Vector2Int cell=ChooseCell(config);var manager=GameManager.Instance;
    if(!manager.TryPurchase(config.cost))throw new InvalidOperationException("Purchase exceeded actual wallet");
    var factory=Object.FindFirstObjectByType<TowerFactory>();var unit=factory.CreateTower(config,GridManager.Instance.GridToWorld(cell));
    towers.Add(unit);GridManager.Instance.SetCellBuildable(cell,false);
    if(name=="PoisonTower" || name=="SniperTower")unit.SetPriority(TargetPriority.Strong);else unit.SetPriority(TargetPriority.First);
    return unit;
  }
  private static void Spend(bool reinvest)
  {
    var manager=GameManager.Instance;
    if(!towers.Exists(t=>t.GetTowerConfig()==Config(counter)) && manager.CanAfford(Config(counter).cost))Build(counter);
    if(reinvest)
    {
      Tower choice=null;float best=0;
      foreach(var tower in towers)
      {
        if(tower.IsSupport || tower.IsMaxLevel || !manager.CanAfford(tower.UpgradeCost))continue;
        float score=tower.EffectiveDamage*tower.EffectiveFireRate/Mathf.Max(1,tower.UpgradeCost);
        if(score>best){best=score;choice=tower;}
      }
      if(choice!=null){if(!choice.Upgrade())throw new InvalidOperationException("Affordable upgrade failed");upgrades++;}
    }
    string[] order={counter,"IceTower","ArcherTower","ShockTower","PoisonTower","InfernoTower","AuraTower","DefenseTower","SniperTower","ArcherTower","ShockTower","ArcherTower"};
    for(int pass=0;pass<12 && towers.Count<12;pass++)
    {
      bool added=false;
      for(int offset=0;offset<order.Length;offset++)
      {
        string name=order[(towers.Count+offset)%order.Length];
        if((name=="AuraTower" || name=="DefenseTower") && towers.Exists(t=>t.GetTowerConfig()==Config(name)))continue;
        if(!manager.CanAfford(Config(name).cost))continue;
        Build(name);added=true;break;
      }
      if(!added)break;
    }
  }
  private static Vector2Int ChooseCell(TowerConfig config)
  {
    var grid=GridManager.Instance;var path=PathManager.Instance.GetPathPoints();float best=float.NegativeInfinity;Vector2Int result=default;
    for(int x=0;x<grid.gridSize.x;x++)for(int y=0;y<grid.gridSize.y;y++)
    {
      var cell=new Vector2Int(x,y);if(!grid.IsCellBuildable(cell))continue;
      Vector3 position=grid.GridToWorld(cell);float score=0;
      if(config.isSupport)
      {
        foreach(var tower in towers)if(!tower.IsSupport){float d=Vector3.Distance(position,tower.transform.position);if(d<=config.range)score+=d<=5.1f?12:3;}
      }
      else for(int segment=1;segment<path.Length;segment++)for(int step=0;step<4;step++)
      {
        Vector3 sample=Vector3.Lerp(path[segment-1],path[segment],step/4f);sample.y=0;
        if(Vector3.Distance(sample,position)>config.range)continue;
        float coverage=0;foreach(var tower in towers)if(!tower.IsSupport && Vector3.Distance(sample,tower.transform.position)<tower.Range)coverage+=.25f;
        score+=1/(1+coverage);
      }
      if(score>best){best=score;result=cell;}
    }
    if(float.IsNegativeInfinity(best))throw new InvalidOperationException("No legal build cell");return result;
  }
  private static void Finish(int code)
  {
    EditorApplication.update-=Step;if(GameManager.Instance!=null)Object.DestroyImmediate(GameManager.Instance.gameObject);
    foreach(string key in Keys){if(SessionState.GetBool(Pending+".Had."+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt(Pending+".Value."+key,0));else PlayerPrefs.DeleteKey(key);}
    PlayerPrefs.Save();Time.timeScale=1;Directory.CreateDirectory("Builds/Campaign");File.WriteAllText($"Builds/Campaign/play-{first}-{last-1}.csv",report.ToString());EditorApplication.Exit(code);
  }
}
