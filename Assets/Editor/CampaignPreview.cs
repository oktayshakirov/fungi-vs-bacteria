using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CampaignPreview
{
  private const BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;
  public static void RunBatch()
  {
    try
    {
      ShaderUtil.allowAsyncCompilation=false;Directory.CreateDirectory("Builds/CampaignPreview");
      var live=typeof(UiPreview).GetMethod("ShootLive",Private);var shoot=typeof(BattlefieldPreview).GetMethod("Shoot",Private);
      foreach(int width in new[]{1440,1920,2340,2400})
      {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cam=new GameObject("MissionPreviewCamera").AddComponent<Camera>();cam.orthographic=true;cam.transform.position=new Vector3(0,0,-100);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.36f,.50f,.22f);
        for(int biome=1;biome<=7;biome++)
        {
          GameSession.SelectedEnvironment="Environment "+biome;
          GameSession.SelectedLevel=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment{biome}/Level10.asset");
          string name=$"env{biome}-{width}-missions";
          live.Invoke(null,new object[]{cam,"Assets/Prefabs/Screens/LevelsScreen.prefab",name,width,1080,10});Copy(name);
          name=$"env{biome}-{width}-briefing";
          live.Invoke(null,new object[]{cam,"Assets/Prefabs/Screens/LoadingScreen.prefab",name,width,1080,10});Copy(name);
        }
      }
      foreach(int width in new[]{1440,1920})for(int biome=1;biome<=7;biome++)
      {
        shoot.Invoke(null,new object[]{biome,new Vector2Int(width,1080),42f,false,false,true,1,0,0,true,10});
        string source=$"env{biome}-{width}x1080-p42-full-cast-tactical1-challenge.png";
        File.Copy("Builds/BattlefieldPreview/"+source,$"Builds/CampaignPreview/env{biome}-{width}-finale.png",true);
      }
      ValidateEveryForecast();Debug.Log("CAMPAIGN PREVIEW: 56 mission/briefing screens across four aspect ratios, 14 real-board finale views and every campaign wave's forecast passed.");EditorApplication.Exit(0);
    }
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  public static void RunForecastBatch()
  {
    try{ShaderUtil.allowAsyncCompilation=false;ValidateEveryForecast();EditorApplication.Exit(0);}
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  private static void Copy(string name){File.Copy("Builds/UiPreview/"+name+".png","Builds/CampaignPreview/"+name+".png",true);}
  private static void ValidateEveryForecast()
  {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    var cam=new GameObject("ForecastAuditCamera").AddComponent<Camera>();cam.orthographic=true;
    var target=new RenderTexture(1440,1080,24);cam.targetTexture=target;
    var host=(GameObject)typeof(UiPreview).GetMethod("BuildHud",Private).Invoke(null,new object[]{cam,1440,1080});
    var timer=Array.Find(host.GetComponentsInChildren<TMP_Text>(),t=>t.name=="TimerText");var forecast=WavePreview.Create(host.transform,timer,null);
    int total=0;
    for(int b=1;b<=7;b++)for(int n=1;n<=10;n++)
    {
      var level=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment{b}/Level{n:00}.asset");
      for(int w=0;w<level.waveConfig.waves.Length;w++)
      {
        // Edit-mode review owns these temporary tiles; remove them before Bind
        // calls the runtime deferred-destruction path on the previous wave.
        var entries=forecast.transform.Find("Forecast/Enemies");
        for(int i=entries.childCount-1;i>=0;i--)Object.DestroyImmediate(entries.GetChild(i).gameObject);
        forecast.Bind(level.waveConfig.waves[w],w+1,true);Canvas.ForceUpdateCanvases();cam.Render();Canvas.ForceUpdateCanvases();
        foreach(var label in forecast.GetComponentsInChildren<TMP_Text>())
        {
          label.ForceMeshUpdate();
          if(label.isTextOverflowing || label.textBounds.size.x>label.rectTransform.rect.width+3 || label.textBounds.size.y>label.rectTransform.rect.height+3 || label.fontSize<14)
            throw new InvalidOperationException($"Forecast unreadable at Env{b}/Level{n}/Wave{w+1}: {label.name} / {label.text} / {label.fontSize}");
        }
        total++;
      }
    }
    Debug.Log($"CAMPAIGN FORECAST AUDIT: {total} waves passed at a minimum of 14 canvas units.");
    cam.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(host);
  }
}
