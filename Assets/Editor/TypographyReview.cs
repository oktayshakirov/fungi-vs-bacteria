using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Review the real runtime-built screens at the narrow/wide layout limits.
public static class TypographyReview
{
  private const BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;
  public static void Audit(string name)
  {
    int readable=0,display=0;
    foreach(var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
    {
      if(!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text))continue;
      text.ForceMeshUpdate();
      if((text.fontStyle & FontStyles.Bold)!=0)
        Debug.LogWarning($"TYPOGRAPHY SYNTHETIC BOLD {name}: {text.name} / {text.text}");
      if(text.font==UiFont.Title){display++;continue;}
      if(text.font!=UiFont.Body && text.font!=UiFont.Medium && text.font!=UiFont.Emphasis && text.font!=UiFont.Action)
        Debug.LogWarning($"TYPOGRAPHY UNSTYLED {name}: {text.name} / {text.text} / {text.font?.name}");
      else readable++;
      if(text.isTextOverflowing || text.textBounds.size.x>text.rectTransform.rect.width+3 || text.textBounds.size.y>text.rectTransform.rect.height+3)
        Debug.LogWarning($"TYPOGRAPHY LAYOUT {name}: {text.name} / {text.text} bounds={text.textBounds.size} rect={text.rectTransform.rect.size}");
    }
    Debug.Log($"TYPOGRAPHY AUDIT {name}: {readable} readable labels, {display} display labels.");
  }
  public static void RunExtraBatch()
  {
    try
    {
      System.IO.Directory.CreateDirectory("Builds/UiPreview");ShaderUtil.allowAsyncCompilation=false;
      foreach(int width in new[]{1440,2400})
      {
        string suffix=width==1440?"tablet":"wide";
        typeof(UiPreview).GetMethod("ShootMainMenu",Private).Invoke(null,new object[]{width,1080,"mainmenu-"+suffix});
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cam=new GameObject("TypographyCamera").AddComponent<Camera>();cam.orthographic=true;cam.transform.position=new Vector3(0,0,-100);
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.36f,.50f,.22f);
        var screen=typeof(UiPreview).GetMethod("ShootScreen",Private);
        screen.Invoke(null,new object[]{cam,"Assets/Prefabs/Screens/PauseGameScreen.prefab","ResumeGame","pause-"+suffix,false,width,1080});
        screen.Invoke(null,new object[]{cam,"Assets/Resources/Screens/SettingsScreen.prefab",null,"settings-"+suffix,true,width,1080});
        screen.Invoke(null,new object[]{cam,"Assets/Resources/Screens/VictoryScreen.prefab","NextLevelButton","victory-"+suffix,false,width,1080});
        screen.Invoke(null,new object[]{cam,"Assets/Prefabs/Screens/GameOverScreen.prefab","RestartNewGame","gameover-"+suffix,false,width,1080});
        var wallet=typeof(UiPreview).GetMethod("ShootWallet",Private);
        wallet.Invoke(null,new object[]{cam,"store-"+suffix,false,WalletScreen.TabFree,width,1080});
        wallet.Invoke(null,new object[]{cam,"coins-"+suffix,true,WalletScreen.TabFree,width,1080});
        wallet.Invoke(null,new object[]{cam,"packs-"+suffix,true,WalletScreen.TabPacks,width,1080});
        Forecast(cam,width,suffix);
      }
      Debug.Log("TYPOGRAPHY EXTRA: narrow/wide menus, modals, stores, currency punctuation, and eight-role forecasts rendered.");EditorApplication.Exit(0);
    }
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  private static void Forecast(Camera cam,int width,string suffix)
  {
    typeof(UiPreview).GetMethod("ClearCanvases",Private).Invoke(null,null);
    var rt=new RenderTexture(width,1080,24);cam.targetTexture=rt;
    var host=(GameObject)typeof(UiPreview).GetMethod("BuildHud",Private).Invoke(null,new object[]{cam,width,1080});
    var timer=host.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="TimerText");
    var forecast=WavePreview.Create(host.transform,timer,null);
    var wave=new WaveConfig.Wave();
    wave.enemyGroups=new[]{"BasicEnemy","FastEnemy","ArmoredEnemy","BossEncounterEnemy","SwarmEnemy","ShieldedEnemy","SplitterEnemy","HealerEnemy"}
      .Select(name=>new WaveConfig.WaveEnemyGroup{enemyConfig=AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset"),count=12,healthMultiplier=1,rewardMultiplier=1}).ToArray();
    wave.planningHint="Poison counters armor. Keep sustained fire on shields and area damage on every Splitter child.";
    forecast.Bind(wave,9,true);forecast.SetStatus("NEXT IN 12s / SCOUT");
    Canvas.ForceUpdateCanvases();cam.Render();Canvas.ForceUpdateCanvases();cam.Render();
    Audit("forecast-eight-"+suffix);
    // Detailed labels may never silently shrink below the readable floor.
    foreach(var label in forecast.GetComponentsInChildren<TMP_Text>())
    {label.ForceMeshUpdate();if(label.isTextOverflowing)throw new InvalidOperationException("Eight-role forecast overflow: "+label.text);}
    typeof(UiPreview).GetMethod("SavePng",Private).Invoke(null,new object[]{rt,width,1080,"forecast-eight-"+suffix});
    cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(host);
  }
}
