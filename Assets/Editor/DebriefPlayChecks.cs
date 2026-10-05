using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class DebriefPlayChecks
{
  private const string Pending="DebriefPlayChecks.Pending";
  private static readonly List<string> Keys=SaveKeys();
  private static int stage,checks;
  private static double deadline;
  private static GameManager previous;
  private const BindingFlags InstancePrivate=BindingFlags.Instance|BindingFlags.NonPublic;
  static DebriefPlayChecks(){EditorApplication.playModeStateChanged+=Changed;}
  private static List<string> SaveKeys()
  {
    var keys=new List<string>{"Wallet_Coins","Wallet_LevelLoan","TutorialCompleted","Ads_LevelEndCount"};
    for(int b=1;b<=7;b++){keys.Add("HighestCompletedLevel_Environment "+b);for(int n=1;n<=10;n++)keys.Add("Stars_Environment "+b+"_"+n);}
    return keys;
  }
  public static void RunBatch()
  {
    foreach(string key in Keys){SessionState.SetBool(Pending+".Had."+key,PlayerPrefs.HasKey(key));SessionState.SetInt(Pending+".Value."+key,PlayerPrefs.GetInt(key,0));}
    SessionState.SetBool(Pending,true);EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");EditorApplication.EnterPlaymode();
  }
  private static void Changed(PlayModeStateChange state)
  {
    if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false))return;
    SessionState.SetBool(Pending,false);stage=0;checks=0;deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Step;
    PlayerPrefs.SetInt("Wallet_Coins",0);PlayerPrefs.SetInt("Wallet_LevelLoan",0);PlayerPrefs.SetInt("TutorialCompleted",1);PlayerPrefs.SetInt("Stars_Environment 1_3",3);
    GameSession.SelectedEnvironment="Environment 1";
    GameSession.SelectedLevel=AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Resources/Levels/Environment1/Level03.asset");
  }
  private static void Check(bool condition,string message){checks++;if(!condition)throw new InvalidOperationException(message);}
  private static EnemyConfig Config(string name)=>AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/"+name+".asset");
  private static void Set(object target,string name,object value)=>target.GetType().GetField(name,InstancePrivate).SetValue(target,value);
  private static void Leak(string name,bool child=false)
  {
    var manager=GameManager.Instance;var config=Config(name);var path=new[]{new Vector3(0,.15f,0)};
    var unit=EnemyPool.Get(config.prefab,path[0],Quaternion.identity).GetComponent<Enemy>();
    var ov=Enemy.SpawnOverride.Default;ov.canSplit=!child;
    unit.Initialize(path,config,1,1,ov);manager.OnEnemySpawned();
    // Exercise movement -> base damage -> removal, including the pooled prefab.
    typeof(Enemy).GetMethod("Update",InstancePrivate).Invoke(unit,null);
    Check(!unit.gameObject.activeSelf,"Escaped enemy did not return to pool");
  }
  private static void EndVictory()
  {
    var spawner=EnemySpawner.Instance;Set(spawner,"currentWave",spawner.TotalWaves);Set(spawner,"isSpawning",false);Set(spawner,"awaitingClear",false);
    HUDManager.Instance.UpdateWaveText(spawner.TotalWaves,spawner.TotalWaves);GameManager.Instance.CheckVictory();
    Check(GameManager.Instance.HasEnded && GameManager.Instance.currentHealth>0,"Victory flow did not end");
  }
  private static void Step()
  {
    try
    {
      if(EditorApplication.timeSinceStartup>deadline)throw new InvalidOperationException("Debrief real-scene flow timed out at stage "+stage);
      if(stage==0)
      {
        if(SceneController.Instance==null)return;SceneController.Instance.LoadScene(SceneController.GameScene.MainGame);stage=1;return;
      }
      var m=GameManager.Instance;
      if(stage==5)
      {
        if(SceneManager.GetActiveScene().name!="MainMenu" || m!=null)return;
        Check(Time.timeScale==1,"Menu exit left time paused");Debug.Log($"DEBRIEF PLAY CHECKS: {checks} assertions passed through real-scene fatal leak, pooling, shield, child escape, repair, repeated loss, continue, reward, replay, next level, retry and menu flows.");Finish(0);return;
      }
      if(m==null || m.Report==null || m==previous || EnemySpawner.Instance==null || HUDManager.Instance==null)return;
      if(stage==1)
      {
        Check(m.Report.PreviousBest==3,"Previous-best snapshot missing");Check(m.Report.ReachedColony==0,"New scene retained report");
        m.currentHealth=3;Leak("FastEnemy");
        Check(m.HasEnded && m.Report.ReachedColony==1 && m.Report.HealthLost==3,"Fatal escape missing/overkill inflated");
        var defeat=Object.FindFirstObjectByType<GameOverScreen>();Check(defeat!=null,"Defeat UI missing");
        Check(defeat.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="EnemyDamage" && t.text=="3 total health lost"),"Fatal enemy not shown synchronously");
        m.TakeDamage(10,Config("ArmoredEnemy"),false);Check(m.Report.ReachedColony==1,"Ended run accepted extra escapes");
        m.ContinueRun(50,false);Check(!m.HasEnded && m.Report.ReachedColony==1,"Continue cleared report");
        typeof(BoosterEffects).GetField("shieldEndsAt",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Time.time+10);
        Leak("FastEnemy");Check(m.currentHealth==50 && m.Report.ProtectedEscapes==1,"Shield escape counted as damage");
        typeof(BoosterEffects).GetField("shieldEndsAt",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,0f);
        Leak("SplitterEnemy",true);Check(m.Report.RankedEscapes().Any(e=>e.IsChild),"Splitter child misclassified");
        int recorded=m.Report.HealthLost;m.Repair(100);Check(m.currentHealth==m.Report.StartingHealth && m.Report.HealthLost==recorded,"Mend corrupted escape history");
        // A second loss must rebuild the reused UI with both episodes.
        m.TakeDamage(100,Config("ArmoredEnemy"),false);Check(m.HasEnded && m.Report.ReachedColony==4,"Second death lost history");
        Check(defeat.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="EnemyName" && t.text.Contains("Armored")),"Second death showed stale debrief");
        m.ContinueRun(70,false);EndVictory();
        var win=Object.FindFirstObjectByType<VictoryScreen>();Check(win!=null,"Victory UI missing");win.CompleteReveal();
        Check(LevelProgress.GetStars("Environment 1",3)==3,"Replay downgraded best stars");
        Check(!win.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="CoinPayout"),"Previously best replay promised payout");
        Check(win.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="ReplayGoal" && t.text.Contains("MATCH YOUR BEST")),"Existing best replay goal wrong");
        int coins=m.currentGold;m.CheckVictory();win.CompleteReveal();Check(m.currentGold==coins,"Repeated presentation paid twice");
        previous=m;win.GetComponentsInChildren<Button>().First(b=>b.name=="ReplayButton").onClick.Invoke();stage=2;return;
      }
      if(stage==2)
      {
        Check(GameSession.SelectedLevel.levelNumber==3 && m.Report.ReachedColony==0 && m.currentHealth==100,"Replay did not start a clean same-level run");
        EndVictory();var win=Object.FindFirstObjectByType<VictoryScreen>();win.CompleteReveal();previous=m;
        win.GetComponentsInChildren<Button>().First(b=>b.name=="NextLevelButton").onClick.Invoke();stage=3;return;
      }
      if(stage==3)
      {
        Check(GameSession.SelectedLevel.levelNumber==4 && m.Report.ReachedColony==0,"Next level reused old report");
        m.TakeDamage(100,Config("FastEnemy"),false);var defeat=Object.FindFirstObjectByType<GameOverScreen>();previous=m;
        defeat.GetComponentsInChildren<Button>().First(b=>b.name=="RestartNewGame").onClick.Invoke();stage=4;return;
      }
      if(stage==4)
      {
        Check(GameSession.SelectedLevel.levelNumber==4 && m.Report.ReachedColony==0 && m.currentHealth==100,"Retry did not reset run");
        m.TakeDamage(100,Config("FastEnemy"),false);var defeat=Object.FindFirstObjectByType<GameOverScreen>();
        defeat.GetComponentsInChildren<Button>().First(b=>b.name=="ReturnToMainMenu").onClick.Invoke();stage=5;
      }
    }
    catch(Exception e){Debug.LogException(e);Finish(1);}
  }
  private static void Finish(int code)
  {
    EditorApplication.update-=Step;if(GameManager.Instance!=null)Object.DestroyImmediate(GameManager.Instance.gameObject);
    foreach(string key in Keys){if(SessionState.GetBool(Pending+".Had."+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt(Pending+".Value."+key,0));else PlayerPrefs.DeleteKey(key);}
    PlayerPrefs.Save();Time.timeScale=1;EditorApplication.Exit(code);
  }
}
