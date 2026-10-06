using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Runs the real MainGame scene with two different legal opening plans. This
// verifies playability and saved-wallet compatibility, not human difficulty.
[InitializeOnLoad]
public static class BiomePlayChecks
{
  private const string Pending="BiomePlayChecks.Pending";
  private static readonly List<string> Keys=SaveKeys();
  private static List<string> SaveKeys()
  {
    var keys=new List<string>{"Wallet_Coins","Wallet_LevelLoan","TutorialCompleted"};
    for(int biome=2;biome<=7;biome++){keys.Add("HighestCompletedLevel_Environment "+biome);keys.Add("Stars_Environment "+biome+"_1");}
    return keys;
  }
  private static int scenario;
  private static bool built;
  private static double loadedAt,deadline;
  private static readonly List<Tower> towers=new List<Tower>();
  static BiomePlayChecks() { EditorApplication.playModeStateChanged+=Changed; }
  public static void RunBatch()
  {
    foreach(string key in Keys)
    {
      SessionState.SetBool("BiomePlayChecks.Had."+key,PlayerPrefs.HasKey(key));
      SessionState.SetInt("BiomePlayChecks.Value."+key,PlayerPrefs.GetInt(key,0));
    }
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    SessionState.SetBool(Pending,true);
    EditorApplication.EnterPlaymode();
  }
  private static void Changed(PlayModeStateChange state)
  {
    if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
    SessionState.SetBool(Pending,false);
    scenario=0; deadline=EditorApplication.timeSinceStartup+300;
    EditorApplication.update+=Step;
    Next();
  }
  private static void Next()
  {
    // Unload the old manager before assigning this scenario's persisted data.
    if (GameManager.Instance != null) Object.DestroyImmediate(GameManager.Instance.gameObject);
    towers.Clear(); built=false;
    PlayerPrefs.SetInt("Wallet_Coins",0);
    PlayerPrefs.SetInt("Wallet_LevelLoan",0);
    PlayerPrefs.SetInt("TutorialCompleted",1);
    int level=1;
    GameSession.SelectedLevel=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment{scenario%6+2}/Level{level:00}.asset");
    GameSession.SelectedEnvironment=GameSession.SelectedLevel.environmentName;
    Time.timeScale=1;
    EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainGame.unity",new LoadSceneParameters(LoadSceneMode.Single));
    loadedAt=EditorApplication.timeSinceStartup;
  }
  private static void Step()
  {
    try
    {
      if(EditorApplication.timeSinceStartup>deadline) throw new InvalidOperationException("Biome play scenarios timed out.");
      if(EditorApplication.timeSinceStartup-loadedAt<.5) return;
      var manager=GameManager.Instance;
      var spawner=EnemySpawner.Instance;
      if(manager==null || spawner==null) throw new InvalidOperationException("MainGame managers are missing.");
      if(!built)
      {
        int expected=GameSession.SelectedLevel.startingGold;
        if(manager.currentGold!=expected) throw new InvalidOperationException("Starting wallet/floor mismatch.");
        bool branches=scenario<6;int biome=scenario%6+2;
        var factory=Object.FindFirstObjectByType<TowerFactory>();
        void Build(string name,bool upgraded=false)
        {
          var cfg=AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");
          var cell=ChooseCell(cfg);if(!manager.TryPurchase(cfg.cost))throw new InvalidOperationException("Plan exceeds budget");
          var unit=factory.CreateTower(cfg,GridManager.Instance.GridToWorld(cell));towers.Add(unit);GridManager.Instance.SetCellBuildable(cell,false);
          if(name=="PoisonTower" || name=="SniperTower")unit.SetPriority(TargetPriority.Strong);
          if(upgraded)
          {
            if(!unit.Upgrade())throw new InvalidOperationException("Legal upgrade opening failed");
          }
        }
        if(branches)
        {
          Build("ArcherTower",true);Build("IceTower");Build("ArcherTower");
          foreach(string name in new[]{"PoisonTower","ShockTower","AuraTower","DefenseTower","InfernoTower","ArcherTower"})
          {
            var cfg=AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");if(manager.CanAfford(cfg.cost))Build(name);
          }
        }
        else
        {
          foreach(string name in new[]{"ShockTower","InfernoTower","IceTower","ArcherTower","PoisonTower","AuraTower","SniperTower","DefenseTower","ArcherTower"})
          {
            var cfg=AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");if(manager.CanAfford(cfg.cost))Build(name);
          }
        }
        built=true;
        Time.timeScale=8;
        spawner.StartGame();
      }
      if(!manager.HasEnded) return;
      int health=manager.currentHealth;
      Debug.Log($"BIOME PLAY: scenario {scenario+1}, {GameSession.SelectedEnvironment}, {(scenario<6?"branch":"mixed")} plan, towers {towers.Count}, health {health}, waves {spawner.WavesStarted}/{spawner.TotalWaves}.");
      if(health<=0 || !spawner.AreWavesComplete()) throw new InvalidOperationException("Biome plan did not complete the level.");
      scenario++;
      if(scenario>=12)
      {
        Debug.Log("BIOME PLAY CHECKS: all six biome challenges completed with branch and mixed plans using legal opening budgets (12 real-scene runs).");
        Finish(0);
      }
      else Next();
    }
    catch(Exception e) { Debug.LogException(e); Finish(1); }
  }
  private static Vector2Int ChooseCell(TowerConfig config)
  {
    var grid=GridManager.Instance;
    Vector3[] path=PathManager.Instance.GetPathPoints();
    float best=-1; Vector2Int cell=default;
    for(int x=0;x<grid.gridSize.x;x++) for(int y=0;y<grid.gridSize.y;y++)
    {
      var candidate=new Vector2Int(x,y);
      if(!grid.IsCellBuildable(candidate)) continue;
      Vector3 position=grid.GridToWorld(candidate);
      float score=0;
      for(int segment=1;segment<path.Length;segment++) for(int step=0;step<4;step++)
      {
        Vector3 sample=Vector3.Lerp(path[segment-1],path[segment],step/4f); sample.y=0;
        if(Vector3.Distance(sample,position)>config.range) continue;
        float covered=0;
        foreach(var tower in towers) if(Vector3.Distance(sample,tower.transform.position)<tower.Range) covered+=.35f;
        score+=1f/(1f+covered);
      }
      if(score>best) { best=score;cell=candidate; }
    }
    return cell;
  }
  private static void Finish(int code)
  {
    EditorApplication.update-=Step;
    if (GameManager.Instance != null) Object.DestroyImmediate(GameManager.Instance.gameObject);
    foreach(string key in Keys)
    {
      if(SessionState.GetBool("BiomePlayChecks.Had."+key,false)) PlayerPrefs.SetInt(key,SessionState.GetInt("BiomePlayChecks.Value."+key,0));
      else PlayerPrefs.DeleteKey(key);
    }
    PlayerPrefs.Save(); Time.timeScale=1;
    EditorApplication.Exit(code);
  }
}
