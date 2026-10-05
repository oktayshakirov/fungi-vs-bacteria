using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TacticalChecks
{
  private const string Pending = "TacticalChecks.Pending";
  private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  static TacticalChecks() { EditorApplication.playModeStateChanged += Changed; }
  public static void RunBatch()
  {
    foreach (string key in new[] { "Wallet_Coins", "Wallet_LevelLoan" })
    {
      SessionState.SetBool("TacticalChecks.Had." + key, PlayerPrefs.HasKey(key));
      SessionState.SetInt("TacticalChecks.Value." + key, PlayerPrefs.GetInt(key,0));
    }
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    SessionState.SetBool(Pending, true);
    EditorApplication.EnterPlaymode();
  }
  private static void Changed(PlayModeStateChange state)
  {
    if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
    SessionState.SetBool(Pending,false);
    // Restore persistence even when an assertion fails.

    try
    {
      new GameObject("Camera").AddComponent<Camera>().tag = "MainCamera";
      TargetPriorities();
      WaveLifecycle();
      OpeningAssets();
      Debug.Log("TACTICAL CHECKS: route priorities, overtake, pooled reuse, shield strength, upgrade retention, no-overlap waves, clear-only reward, manual/auto/pause guards, forecasts, and opening progression passed.");
      EditorApplication.Exit(0);
    }
    catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    finally
    {
      foreach (var manager in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(manager.gameObject);
      foreach (string key in new[] { "Wallet_Coins", "Wallet_LevelLoan" })
      {
        if (SessionState.GetBool("TacticalChecks.Had." + key,false)) PlayerPrefs.SetInt(key,SessionState.GetInt("TacticalChecks.Value." + key,0));
        else PlayerPrefs.DeleteKey(key);
      }
      PlayerPrefs.Save();
    }
  }
  private static void Require(bool condition, string message)
  { if (!condition) throw new InvalidOperationException(message); }
  private static void Set(object owner,string field,object value)
  { owner.GetType().GetField(field,Private).SetValue(owner,value); }
  private static void Invoke(object owner,string method)
  { owner.GetType().GetMethod(method,Private).Invoke(owner,null); }
  private static Enemy Spawn(EnemyConfig config,Vector3[] path,int waypoint)
  {
    var unit = EnemyPool.Get(config.prefab,path[waypoint],Quaternion.identity).GetComponent<Enemy>();
    var ov = Enemy.SpawnOverride.Default; ov.startWaypoint=waypoint;
    unit.Initialize(path,config,1,1,ov);
    return unit;
  }
  private static void TargetPriorities()
  {
    var basic = AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/BasicEnemy.asset");
    // Near the exit in straight-line distance, but still behind a long detour.
    Vector3[] path = { new Vector3(0,0,0),new Vector3(20,0,0),new Vector3(20,0,20),new Vector3(0,0,20),new Vector3(0,0,1) };
    Enemy early=Spawn(basic,path,0), late=Spawn(basic,path,3);
    var targeting = new GameObject("Targeting").AddComponent<TowerTargeting>();
    targeting.Initialize(30);
    targeting.SetPriority(TargetPriority.First);
    Require(targeting.CurrentTarget==late.transform,"FIRST took a shortcut across the path.");
    targeting.SetPriority(TargetPriority.Nearest);
    Require(targeting.CurrentTarget==early.transform,"NEAREST ignored ground distance.");
    var shield = Object.Instantiate(basic); shield.hasShield=true; shield.shieldShareOfHealth=1;
    Enemy strong=Spawn(shield,path,1);
    targeting.SetPriority(TargetPriority.Strong);
    Require(targeting.CurrentTarget==strong.transform,"STRONG did not include shield strength.");
    targeting.Initialize(30);
    Require(targeting.Priority==TargetPriority.Strong,"Upgrade reset the selected priority.");
    targeting.SetPriority(TargetPriority.First);
    var ov=Enemy.SpawnOverride.Default; ov.startWaypoint=4;
    early.Initialize(path,basic,1,1,ov);
    Set(targeting,"nextSearch",0f); targeting.UpdateTarget();
    Require(targeting.CurrentTarget==early.transform,"FIRST did not reconsider an overtaking enemy.");
    uint old=early.SpawnVersion;
    EnemyPool.Release(early.gameObject);
    var reused=EnemyPool.Get(basic.prefab,path[0],Quaternion.identity).GetComponent<Enemy>();
    reused.Initialize(path,basic);
    Require(reused==early && reused.SpawnVersion!=old,"Test did not reuse a pooled enemy.");
    Set(targeting,"nextSearch",float.MaxValue); targeting.UpdateTarget();
    Require(targeting.CurrentTarget==null,"Stale target survived a pooled respawn.");
    Require(reused.DistanceToExit>late.DistanceToExit,"Route suffix was stale after reuse.");
    EnemyPool.Release(reused.gameObject); EnemyPool.Release(late.gameObject); EnemyPool.Release(strong.gameObject);
    Object.DestroyImmediate(targeting.gameObject); Object.DestroyImmediate(shield);
  }
  private static void WaveLifecycle()
  {
    var hud=new GameObject("HUD", typeof(RectTransform)).AddComponent<HUDManager>(); hud.enabled=false;
    var manager=new GameObject("GameManager").AddComponent<GameManager>(); manager.enabled=false;
    manager.currentHealth=100;
    var spawner=new GameObject("Spawner").AddComponent<EnemySpawner>(); spawner.enabled=false;
    var config=ScriptableObject.CreateInstance<WaveConfig>();
    config.waves=new[] {
      new WaveConfig.Wave {enemyGroups=new WaveConfig.WaveEnemyGroup[0],waveGoldReward=11,timeToNextWave=12},
      new WaveConfig.Wave {enemyGroups=new WaveConfig.WaveEnemyGroup[0],waveGoldReward=17,timeToNextWave=12},
      new WaveConfig.Wave {enemyGroups=new WaveConfig.WaveEnemyGroup[0],waveGoldReward=23,timeToNextWave=12} };
    Set(spawner,"waveConfig",config);
    int before=Wallet.Coins;
    manager.OnEnemySpawned();
    spawner.StartGame();
    Require(spawner.WavesStarted==1 && spawner.IsWaveInProgress,"First wave did not start.");
    Require(Wallet.Coins==before,"Reward arrived before enemies were defeated.");
    spawner.StartGame(); spawner.StartNextWave();
    Require(spawner.WavesStarted==1,"Repeated start overlapped waves.");
    // Simulate two registered split children before their parent's removal.
    manager.OnEnemySpawned(); manager.OnEnemySpawned(); manager.OnEnemyRemoved(); spawner.CheckWaveClear();
    Require(!spawner.CanStartWave && Wallet.Coins==before,"Splitter children did not hold the clear gate.");
    manager.OnEnemyRemoved();
    Set(manager,"gameEnded",true); manager.currentHealth=0;
    manager.OnEnemyRemoved(); spawner.CheckWaveClear();
    Require(Wallet.Coins==before && !spawner.CanStartWave,"Lost wave cleared before continue.");
    Set(manager,"spawner",spawner);
    manager.ContinueRun(50,false);
    Set(manager,"spawner",null);
    Require(Wallet.Coins==before+11 && spawner.CanStartWave && spawner.PreparationRemaining==12,"Clear did not reward and prepare.");
    spawner.CheckWaveClear(); Require(Wallet.Coins==before+11,"Clear rewarded twice.");
    Time.timeScale=0; spawner.StartNextWave();
    Require(spawner.WavesStarted==1,"Paused manual start was accepted.");
    Time.timeScale=1;
    manager.OnEnemySpawned(); spawner.StartGame();
    Require(spawner.WavesStarted==2,"Manual early send failed.");
    Set(spawner,"waveTimer",-1f); Invoke(spawner,"Update");
    Require(spawner.WavesStarted==2,"Old timer sent an extra wave.");
    manager.OnEnemyRemoved(); spawner.CheckWaveClear();
    Require(Wallet.Coins==before+28,"Second clear reward was incorrect.");
    manager.OnEnemySpawned(); Set(spawner,"waveTimer",0f); Invoke(spawner,"Update");
    Require(spawner.WavesStarted==3 && !spawner.AreWavesComplete(),"Auto send or last-wave gate failed.");
    manager.OnEnemyRemoved(); spawner.CheckWaveClear();
    Require(spawner.AreWavesComplete() && Wallet.Coins==before+51,"Final wave completion or payout failed.");
    spawner.StartGame(); Require(spawner.WavesStarted==3,"Start after final wave was accepted.");
    Object.DestroyImmediate(spawner.gameObject); Object.DestroyImmediate(manager.gameObject); Object.DestroyImmediate(hud.gameObject); Object.DestroyImmediate(config);
  }
  private static void OpeningAssets()
  {
    var basic=AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/BasicEnemy.asset");
    var repeated=new WaveConfig.Wave { enemyGroups=new[] {
      new WaveConfig.WaveEnemyGroup { enemyConfig=basic,count=3 },
      new WaveConfig.WaveEnemyGroup { enemyConfig=basic,count=2 },
      new WaveConfig.WaveEnemyGroup { enemyConfig=basic,count=0 } } };
    Require(WaveIntel.Entries(repeated).Count==1 && WaveIntel.Count(repeated)==5,"Forecast did not aggregate repeated groups.");
    for(int i=1;i<=3;i++)
    {
      var level=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment1/Level{i:00}.asset");
      Require(level.waveConfig.waves.Length==3,"Opening level is not three short waves.");
      var path=level.pathConfig.pathGridCoordinates;
      var visited=new System.Collections.Generic.HashSet<Vector2Int>();
      foreach(var cell in path) Require(cell.x>=0 && cell.x<10 && cell.y>=0 && cell.y<5 && visited.Add(cell),"Path leaves the board or intersects itself.");
      for(int j=1;j<path.Count;j++) Require(Mathf.Abs(path[j].x-path[j-1].x)+Mathf.Abs(path[j].y-path[j-1].y)==1,"Path is not contiguous.");
      foreach(var wave in level.waveConfig.waves)
      {
        Require(WaveIntel.Count(wave)>0 && !string.IsNullOrWhiteSpace(WaveIntel.Hint(wave)),"Opening wave lacks enemies or a teaching hint.");
        foreach(var entry in WaveIntel.Entries(wave))
        {
          Require(Resources.Load<Sprite>("EnemyPortraits/"+entry.config.name)!=null,"Forecast portrait is missing.");
          Require(i>1 || (!entry.config.isFast && !entry.config.isArmored),"Fundamentals introduces a counter too early.");
          Require(i!=2 || !entry.config.isArmored,"Fast lesson introduces armor early.");
        }
      }
      var final=WaveIntel.Entries(level.waveConfig.waves[2]);
      Require(i!=3 || (final.Exists(e=>e.config.isFast) && final.Exists(e=>e.config.isArmored)),"Mixed test lacks both taught counters.");
    }
  }
}
