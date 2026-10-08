using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Exercises the actual HUD controls after layout, including repeat updates.
public static class TabletUiChecks
{
  private static object Preview(string method, params object[] args) =>
    typeof(UiPreview).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

  [MenuItem("Tools/Display/Check Tablet HUD")]
  public static void Check()
  {
    if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
    var previous = EditorSceneManager.GetSceneManagerSetup();
    try
    {
      EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      var cam = new GameObject("TabletCheckCamera").AddComponent<Camera>();
      cam.transform.position = new Vector3(0, 0, -100);
      cam.orthographic = true;
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.backgroundColor = new Color(.36f, .50f, .22f);
      foreach (var size in new[] { new Vector2Int(1440,1080), new Vector2Int(1620,1080),
        new Vector2Int(1728,1080), new Vector2Int(2622,1206) })
      {
        var rt = new RenderTexture(size.x,size.y,24);
        cam.targetTexture = rt;
        var hud = (GameObject)Preview("BuildHud", cam, size.x, size.y);
        Canvas.ForceUpdateCanvases(); cam.Render(); Canvas.ForceUpdateCanvases();
        var toggle = Find(hud.transform,"TowersToggle").GetComponent<Button>();
        var icon = toggle.transform.GetChild(0) as RectTransform;
        for (int state=0; state<4; state++)
        {
          CheckIcon((RectTransform)toggle.transform, icon);
          toggle.onClick.Invoke();
          Canvas.ForceUpdateCanvases();
        }
        CheckKeepClear(hud, size);
        Object.DestroyImmediate(hud);
        CheckSettingsFooter(cam);
        cam.targetTexture=null; rt.Release(); Object.DestroyImmediate(rt);
        Debug.Log($"TABLET HUD PASS {size.x}x{size.y}: repeated open/close icon containment and stable wave spacing, separated settings footer.");
      }
    }
    finally
    {
      if (Array.Exists(previous, scene => scene.isLoaded && scene.isActive))
        EditorSceneManager.RestoreSceneManagerSetup(previous);
      else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
  }

  private static void CheckIcon(RectTransform button, RectTransform icon)
  {
    var corners=new Vector3[4]; icon.GetWorldCorners(corners);
    Vector3 center=Vector3.zero;
    foreach(var world in corners)
    {
      var p=button.InverseTransformPoint(world); center+=p/4;
      if(p.x<button.rect.xMin+6 || p.x>button.rect.xMax-6 ||
         p.y<button.rect.yMin+6 || p.y>button.rect.yMax-6)
        throw new InvalidOperationException("Towers toggle arrow escapes its padded button bounds.");
    }
    if(Mathf.Abs(center.y-button.rect.center.y)>.1f)
      throw new InvalidOperationException("Towers toggle arrow is not vertically centered.");
  }

  private static void CheckKeepClear(GameObject hud, Vector2Int size)
  {
    var gold=Find(hud.transform,"GoldText").GetComponent<TMP_Text>();
    var keep=hud.GetComponentInChildren<HudKeepClear>();
    var plate=(RectTransform)keep.transform;
    var wave=Find(hud.transform,"WaveText") as RectTransform;
    foreach(string amount in new[]{"515","15000","999999","515"})
    {
      gold.text=amount;
      Canvas.ForceUpdateCanvases();
      LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)hud.transform);
      keep.Apply();
      float x=plate.anchoredPosition.x, labelX=wave.anchoredPosition.x;
      for(int i=0;i<12;i++)
      {
        keep.Apply();
        if(Mathf.Abs(plate.anchoredPosition.x-x)>.1f || Mathf.Abs(wave.anchoredPosition.x-labelX)>.1f)
          throw new InvalidOperationException($"Wave badge oscillates at {size}, coins {amount}.");
      }
    }
    // A controlled blocker verifies growing AND shrinking, independently of font widths.
    var root=new GameObject("SpacingFixture",typeof(RectTransform));
    var parent=(RectTransform)root.transform; parent.sizeDelta=new Vector2(960,720);
    RectTransform Make(string name,float x,float width) {
      var child=new GameObject(name,typeof(RectTransform)); child.transform.SetParent(parent,false);
      var rect=(RectTransform)child.transform;rect.sizeDelta=new Vector2(width,40);rect.anchoredPosition=new Vector2(x,0);return rect;
    }
    var badge=Make("Badge",0,200);var label=Make("Label",0,180);var blocker=Make("Blocker",-200,300);
    HudKeepClear.Attach(badge,label,blocker,20);
    var component=badge.GetComponent<HudKeepClear>();
    for(int i=0;i<12;i++) { component.Apply(); if(Mathf.Abs(badge.anchoredPosition.x-70)>.1f || Mathf.Abs(label.anchoredPosition.x-70)>.1f) throw new InvalidOperationException("Wave spacing fails repeated overlap correction."); }
    blocker.sizeDelta=new Vector2(100,40);component.Apply();
    if(Mathf.Abs(badge.anchoredPosition.x)>.1f || Mathf.Abs(label.anchoredPosition.x)>.1f) throw new InvalidOperationException("Wave badge fails to return to center after stats shrink.");
    Object.DestroyImmediate(root);
  }

  private static void CheckSettingsFooter(Camera cam)
  {
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Screens/SettingsScreen.prefab");
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
    PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
    go.SetActive(true);
    var canvas=go.GetComponentInChildren<Canvas>(true);
    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;
    ScreenTheme.ApplySettingsScreen(go.transform,Find(go.transform,"Close").GetComponent<Button>());
    var privacy=SettingScreen.BuildPrivacyButton(go.transform,null);
    var policy=SettingScreen.BuildPolicyButton(go.transform,null);
    Canvas.ForceUpdateCanvases();cam.Render();Canvas.ForceUpdateCanvases();
    var version=Find(go.transform,"Version") as RectTransform;
    var space=version.parent as RectTransform;
    Rect Bounds(RectTransform rect) {
      var corners=new Vector3[4];rect.GetWorldCorners(corners);
      var min=new Vector2(float.MaxValue,float.MaxValue);var max=new Vector2(float.MinValue,float.MinValue);
      foreach(var point in corners){Vector2 local=space.InverseTransformPoint(point);min=Vector2.Min(min,local);max=Vector2.Max(max,local);}
      return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    var buildBounds=Bounds(version);
    foreach(var button in new[]{privacy,policy})
      if(buildBounds.Overlaps(Bounds((RectTransform)button.transform)))
        throw new InvalidOperationException("Settings version overlaps a privacy button.");
    Object.DestroyImmediate(go);
  }

  private static Transform Find(Transform root,string name)
  {
    if(root.name==name)return root;
    foreach(Transform child in root){var found=Find(child,name);if(found!=null)return found;}
    return null;
  }

  public static void RunBatch()
  {
    try { Check(); EditorApplication.Exit(0); }
    catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
  }
}
