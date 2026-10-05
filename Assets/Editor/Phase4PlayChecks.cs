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
public static class Phase4PlayChecks
{
  private const string Pending="Phase4PlayChecks.Pending";
  private static readonly string[] Keys={"Wallet_Coins","Wallet_LevelLoan","TutorialCompleted","HighestCompletedLevel_Environment 1","Stars_Environment 1_1","Stars_Environment 1_2","Stars_Environment 1_3","Stars_Environment 1_4"};
  private static int scenario;
  private static bool built;
  private static double loadedAt,deadline;
  private static readonly List<Tower> towers=new List<Tower>();
  static Phase4PlayChecks() { EditorApplication.playModeStateChanged+=Changed; }
  public static void RunBatch()
  {
    foreach(string key in Keys)
    {
      SessionState.SetBool("Phase4PlayChecks.Had."+key,PlayerPrefs.HasKey(key));
      SessionState.SetInt("Phase4PlayChecks.Value."+key,PlayerPrefs.GetInt(key,0));
    }
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    SessionState.SetBool(Pending,true);
    EditorApplication.EnterPlaymode();
  }
  private static void Changed(PlayModeStateChange state)
  {
    if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
    SessionState.SetBool(Pending,false);
    scenario=0; deadline=EditorApplication.timeSinceStartup+180;
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
    int level=scenario<6?scenario%3+1:4;
    GameSession.SelectedLevel=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment1/Level{level:00}.asset");
    GameSession.SelectedEnvironment="Environment 1";
    Time.timeScale=1;
    EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainGame.unity",new LoadSceneParameters(LoadSceneMode.Single));
    loadedAt=EditorApplication.timeSinceStartup;
  }
  private static void Step()
  {
    try
    {
      if(EditorApplication.timeSinceStartup>deadline) throw new InvalidOperationException("Opening play scenarios timed out.");
      if(EditorApplication.timeSinceStartup-loadedAt<.5) return;
      var manager=GameManager.Instance;
      var spawner=EnemySpawner.Instance;
      if(manager==null || spawner==null) throw new InvalidOperationException("MainGame managers are missing.");
      if(!built)
      {
        int expected=GameSession.SelectedLevel.startingGold;
        if(manager.currentGold!=expected) throw new InvalidOperationException("Starting wallet/floor mismatch.");
        bool control=(scenario/3)%2==0;
        string[] plan=control ? new[]{"IceTower","ArcherTower","ArcherTower","ArcherTower"}
          : new[]{"PoisonTower","SniperTower","ArcherTower"};
        if(scenario==6) plan=new[]{"AuraTower","ArcherTower","ArcherTower","ArcherTower"};
        if(scenario==7) plan=new[]{"PoisonTower","SniperTower","IceTower"};
        var factory=Object.FindFirstObjectByType<TowerFactory>();
        foreach(string name in plan)
        {
          var config=AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset");
          Vector2Int cell=ChooseCell(config);
          if(scenario==6) cell=new[]{new Vector2Int(4,2),new Vector2Int(4,3),new Vector2Int(5,2),new Vector2Int(5,3)}[towers.Count];
          if(!GridManager.Instance.IsCellBuildable(cell)) throw new InvalidOperationException("Encounter plan uses an invalid cell.");
          if(!manager.TryPurchase(config.cost)) throw new InvalidOperationException("Plan exceeds the legal opening budget.");
          towers.Add(factory.CreateTower(config,GridManager.Instance.GridToWorld(cell)));
          GridManager.Instance.SetCellBuildable(cell,false);
          if(name=="PoisonTower" || name=="SniperTower") towers[towers.Count-1].SetPriority(TargetPriority.Strong);
        }
        if(scenario==6 && towers[0].MyceliumConnections!=2) throw new InvalidOperationException("Encounter plan did not form two links.");
        built=true;
        Time.timeScale=8;
        spawner.StartGame();
      }
      if(!manager.HasEnded) return;
      int health=manager.currentHealth;
      Debug.Log($"PHASE 4 PLAY: scenario {scenario+1}, level {GameSession.SelectedLevel.levelNumber}, health {health}, waves {spawner.WavesStarted}/{spawner.TotalWaves}.");
      if(health<=0 || !spawner.AreWavesComplete()) throw new InvalidOperationException("Opening plan did not complete the level.");
      scenario++;
      if(scenario>=8)
      {
        Debug.Log("PHASE 4 PLAY CHECKS: first three levels retained two winning plans; linked and poison/control plans completed the boss encounter (8 real-scene runs).");
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
      if(SessionState.GetBool("Phase4PlayChecks.Had."+key,false)) PlayerPrefs.SetInt(key,SessionState.GetInt("Phase4PlayChecks.Value."+key,0));
      else PlayerPrefs.DeleteKey(key);
    }
    PlayerPrefs.Save(); Time.timeScale=1;
    EditorApplication.Exit(code);
  }
}
