using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class DebriefChecks
{
  private static int checks, frames;
  private const BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;
  private static void Check(bool condition,string message) { checks++;if(!condition)throw new InvalidOperationException(message); }
  public static void RunBatch()
  {
    const string key="Ads_LevelEndCount";bool had=PlayerPrefs.HasKey(key);int saved=PlayerPrefs.GetInt(key,0);int code=0;
    try
    {
      checks=frames=0;Models();ShaderUtil.allowAsyncCompilation=false;
      Directory.CreateDirectory("Builds/Debrief");
      foreach(int width in new[]{1440,1920,2340,2400})
      {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("DebriefPreviewCamera").AddComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-100);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.36f,.50f,.22f);
        foreach(string variant in new[]{"victory-clean","victory-two","victory-one","victory-best","victory-finale","defeat-runners","defeat-mixed","defeat-children","defeat-shield"}) Shoot(camera,width,variant);
      }
      SelectionGoals();
      Debug.Log($"DEBRIEF CHECKS: {checks} model/layout checks passed; {frames} real-prefab result screens across four aspect ratios.");
    }
    catch(Exception e){Debug.LogException(e);code=1;}
    finally {if(had)PlayerPrefs.SetInt(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
    EditorApplication.Exit(code);
  }
  private static EnemyConfig Config(string name)=>AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Settings/Enemies/"+name+".asset");
  private static void Models()
  {
    for(int health=1;health<=500;health++)for(int remaining=0;remaining<=health;remaining++)
    {
      float ratio=remaining/(float)health;int old=ratio>=.9f?3:ratio>=.5f?2:1;
      Check(LevelProgress.StarsForHealth(remaining,health)==old,$"Star scoring changed for {remaining}/{health}");
      if(remaining>0) {int stars=LevelProgress.StarsForHealth(remaining,health);Check(remaining>=LevelProgress.HealthForStars(stars,health),"Goal disagrees with scoring");}
    }
    var report=new BattleReport(91,2);
    report.RecordEscape(Config("FastEnemy"),false,10);report.RecordEscape(Config("FastEnemy"),false,4);
    report.RecordEscape(Config("SplitterEnemy"),true,6);report.RecordEscape(Config("ShieldedEnemy"),false,0);
    Check(report.ReachedColony==4 && report.HealthLost==20 && report.ProtectedEscapes==1,"Escape accounting failed");
    Check(report.RankedEscapes().Count==3 && report.RankedEscapes()[0].Count==2,"Aggregation/sorting failed");
    Check(report.Lesson().Contains("Ice"),"Runner counter was lost");
    report.RecordEscape(Config("SplitterEnemy"),true,30);Check(report.RankedEscapes()[0].Name.Contains("child") && report.Lesson().Contains("children"),"Split child evidence missing");
    Check(BattleReport.Goal(2,2,91,true,4).Contains("82 / 91"),"Odd-health 3-star target wrong");
    Check(BattleReport.Goal(1,3,100,true,4).Contains("MATCH YOUR BEST"),"Prior best must not promise another reward");
    Check(BattleReport.Goal(0,0,100,false,4).Contains("Clear all 4 waves"),"First-win objective wrong");
    Check(new BattleReport(100,0).ReachedColony==0,"New run inherited escape data");
    foreach(string type in new[]{"ArmoredEnemy","HealerEnemy","ShieldedEnemy","BossEncounterEnemy","SwarmEnemy","BasicEnemy"})
    { var r=new BattleReport(100,0);r.RecordEscape(Config(type),false,10);Check(!string.IsNullOrEmpty(r.Lesson()),"Missing lesson: "+type); }
  }
  private static void Shoot(Camera camera,int width,string variant)
  {
    typeof(UiPreview).GetMethod("ClearCanvases",Private).Invoke(null,null);
    bool victory=variant.StartsWith("victory");
    GameSession.SelectedEnvironment=variant=="victory-finale"?"Environment 7":"Environment 1";
    GameSession.SelectedLevel=AssetDatabase.LoadAssetAtPath<LevelConfig>(variant=="victory-finale"?"Assets/Resources/Levels/Environment7/Level10.asset":"Assets/Resources/Levels/Environment1/Level03.asset");
    string path=victory?"Assets/Resources/Screens/VictoryScreen.prefab":"Assets/Prefabs/Screens/GameOverScreen.prefab";
    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);go.SetActive(true);
    var target=new RenderTexture(width,1080,24);camera.targetTexture=target;
    var canvas=go.GetComponentInChildren<Canvas>(true);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
    ((RectTransform)canvas.transform).sizeDelta=new Vector2(width,1080);Canvas.ForceUpdateCanvases();
    int stars=variant=="victory-clean" || variant=="victory-finale"?3:variant=="victory-one" || variant=="victory-best"?1:2;
    if(victory)go.GetComponent<VictoryScreen>().Initialize(stars,variant=="victory-best"?0:150);else go.GetComponent<GameOverScreen>().Initialize();
    var actions=go.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="ButtonsPanel");
    var report=new BattleReport(100,variant=="victory-best"?3:variant=="victory-two"?2:0);
    int health=victory?(stars==3?100:stars==2?70:40):0;
    if(variant.Contains("mixed"))
    {
      foreach(string type in new[]{"ArmoredEnemy","HealerEnemy","ShieldedEnemy","BossEncounterEnemy","SplitterEnemy","FastEnemy","SwarmEnemy","BasicEnemy"})report.RecordEscape(Config(type),false,type=="ArmoredEnemy"?30:10);
    }
    else if(variant.Contains("children"))for(int i=0;i<10;i++)report.RecordEscape(Config("SplitterEnemy"),true,10);
    else if(variant.Contains("shield")){report.RecordEscape(Config("ShieldedEnemy"),false,0);report.RecordEscape(Config("ArmoredEnemy"),false,100);}
    else if(variant!="victory-clean" && variant!="victory-finale")for(int i=0;i<(100-health)/10;i++)report.RecordEscape(Config("FastEnemy"),false,10);
    BattleDebrief.Show(go.transform,actions,victory,stars,report,health,victory?4:3);
    if(victory)go.GetComponent<VictoryScreen>().CompleteReveal();
    foreach(var fill in go.GetComponentsInChildren<BackgroundFill>(true)){fill.enabled=false;fill.enabled=true;}
    Canvas.ForceUpdateCanvases();camera.Render();Canvas.ForceUpdateCanvases();camera.Render();
    Audit(go,actions,variant);
    var shot=new Texture2D(width,1080,TextureFormat.RGB24,false);var previous=RenderTexture.active;RenderTexture.active=target;
    shot.ReadPixels(new Rect(0,0,width,1080),0,0);shot.Apply();RenderTexture.active=previous;
    File.WriteAllBytes($"Builds/Debrief/{variant}-{width}.png",shot.EncodeToPNG());frames++;
    // Rebuild the actual screen to catch stale panels, payout and star rows.
    if(victory)go.GetComponent<VictoryScreen>().Initialize(stars,0);else go.SendMessage("PrepareForShow",SendMessageOptions.DontRequireReceiver);
    Canvas.ForceUpdateCanvases();camera.Render();
    Check(go.GetComponentsInChildren<RectTransform>().Count(r=>r.name=="BattleDebrief")==1,"Duplicate debrief after second show");
    if(victory)Check(go.GetComponentsInChildren<RectTransform>().Count(r=>r.name=="StarsRow")==1,"Duplicate star row after second show");
    camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(shot);Object.DestroyImmediate(go);
  }
  private static void SelectionGoals()
  {
    const string key="Stars_Environment 7_10";bool had=PlayerPrefs.HasKey(key);int saved=PlayerPrefs.GetInt(key,0);
    try
    {
      PlayerPrefs.SetInt(key,2);
      foreach(int width in new[]{1440,1920,2340,2400})
      {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("ReplayGoalCamera").AddComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-100);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.36f,.50f,.22f);
        GameSession.SelectedEnvironment="Environment 7";
        GameSession.SelectedLevel=AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Resources/Levels/Environment7/Level10.asset");
        typeof(UiPreview).GetMethod("ShootLive",Private).Invoke(null,new object[]{camera,"Assets/Prefabs/Screens/LevelsScreen.prefab","replay-goal-"+width,width,1080,10});
        File.Copy("Builds/UiPreview/replay-goal-"+width+".png","Builds/Debrief/replay-goal-"+width+".png",true);frames++;
      }
    }
    finally{if(had)PlayerPrefs.SetInt(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
  }
  private static void Audit(GameObject go,RectTransform actions,string variant)
  {
    var view=go.GetComponentsInChildren<RectTransform>().First(r=>r.name=="BattleDebrief");
    var safe=actions.parent as RectTransform;var corners=new Vector3[4];
    foreach(var rect in new[]{view,actions})
    {
      rect.GetWorldCorners(corners);
      foreach(var corner in corners) {Vector2 local=safe.InverseTransformPoint(corner);Check(safe.rect.Contains(local),"Result card outside safe area: "+variant);}
    }
    Check(!RectTransformUtility.CalculateRelativeRectTransformBounds(safe,view).Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(safe,actions)),"Result/action cards overlap");
    foreach(var label in go.GetComponentsInChildren<TMP_Text>())
    {
      label.ForceMeshUpdate();
      Check(!label.isTextOverflowing,"Text overflow: "+variant+" / "+label.name+" / "+label.text);
      Check(label.textBounds.size.x<=label.rectTransform.rect.width+3 && label.textBounds.size.y<=label.rectTransform.rect.height+3,"Text bounds exceed box: "+variant+" / "+label.name+" / "+label.text);
      if(label.transform.IsChildOf(view))Check(label.fontSize>=16,"Debrief unreadable below floor: "+label.name);
    }
  }
}
