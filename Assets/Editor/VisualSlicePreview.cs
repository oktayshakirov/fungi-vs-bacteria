using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class VisualSlicePreview
{
  const string Output="Builds/VisualSlice";
  public static void RunBatch()
  {
    bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
    try {Render();EditorApplication.Exit(0);}
    catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    finally {ShaderUtil.allowAsyncCompilation=async;}
  }

  [MenuItem("Tools/Art/Render Meadow Cast")]
  public static void Render()
  {
    Directory.CreateDirectory(Output);
    var tiles=new Color[8][];
    string[] names={"ArcherTower","IceTower","PoisonTower","ColonyMeadow","BasicEnemy","FastEnemy","HealerEnemy","ArmoredEnemy"};
    string[] labels={"ARCHER · DAMAGE","ICE · CONTROL","POISON · DAMAGE OVER TIME","MEADOW COLONY","BASIC · GRUNT","FAST · RUNNER","HEALER · SUPPORT","ARMORED · TANK"};
    for(int i=0;i<names.Length;i++)
    {
      var tile=Portrait(names[i],labels[i],i<3,i==3);
      tiles[i]=tile.GetPixels();Object.DestroyImmediate(tile);
    }
    var gallery=new Texture2D(2560,1280,TextureFormat.RGB24,false);
    for(int i=0;i<8;i++)gallery.SetPixels((i%4)*640,i<4?640:0,640,640,tiles[i]);
    gallery.Apply();File.WriteAllBytes(Output+"/cast-gallery.png",gallery.EncodeToPNG());Object.DestroyImmediate(gallery);
    Debug.Log("ORIGINAL STYLE GALLERY: authored models rendered with their refined materials.");
  }

  public static Texture2D Portrait(string name,string label,bool tower,bool colony,float viewYaw=0,bool shieldDown=false)
  {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.15f;
    light.color=new Color(1,.96f,.88f);light.transform.rotation=Quaternion.Euler(38,-30,0);
    var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.color=new Color(.68f,.82f,1);fill.transform.rotation=Quaternion.Euler(25,150,0);
    RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.47f,.52f);RenderSettings.reflectionIntensity=.4f;
    GameObject prefab;
    float scale;
    float enemyYawOffset=0;
    EnemyConfig enemyConfig=null;
    if(tower){prefab=AssetDatabase.LoadAssetAtPath<TowerConfig>($"Assets/Settings/Towers/{name}.asset").towerPrefab;scale=UnitScale.Tower;}
    else if(colony){prefab=null;scale=1;}
    else
    {
      enemyConfig=AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
      prefab=enemyConfig.prefab;scale=UnitScale.Enemy*enemyConfig.scaleMultiplier;
      var settings=new SerializedObject(prefab.GetComponent<Enemy>());enemyYawOffset=settings.FindProperty("rotationOffset").floatValue;
    }
    GameObject go;
    if(colony)
    {
      go=new GameObject("OriginalMeadowColony");
      go.AddComponent<LevelDecorator>().BuildBasePreview(AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Resources/Levels/Environment1/Level01.asset"));
    }
    else go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
    go.transform.localScale*=scale;
    go.transform.rotation=Quaternion.Euler(0,(tower?-64:colony?24:204+enemyYawOffset)+viewYaw,0);
    if(shieldDown)go.GetComponent<ShieldSkin>()?.Apply(false);
    foreach(var script in go.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
    if(enemyConfig != null && enemyConfig.overrideBodyColor)
    {
      var body=Enemy.FindBodyRenderer(go);var block=new MaterialPropertyBlock();
      block.SetColor("_BaseColor",enemyConfig.bodyColor);body.SetPropertyBlock(block);
    }
    Bounds bounds=go.GetComponentInChildren<Renderer>().bounds;
    foreach(var r in go.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
    var cam=new GameObject("PortraitCam").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.10f,.16f,.20f);cam.orthographic=true;
    cam.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x,bounds.size.z)*.68f;
    Vector3 centre=bounds.center+Vector3.down*(cam.orthographicSize*.04f);
    cam.transform.position=centre+Quaternion.Euler(24,0,0)*Vector3.back*30;cam.transform.LookAt(centre);cam.farClipPlane=100;
    foreach(var trait in go.GetComponentsInChildren<EnemyTrait>(true))trait.Animate(0,cam);
    var rt=new RenderTexture(640,640,24,RenderTextureFormat.ARGB32){antiAliasing=4};cam.targetTexture=rt;cam.aspect=1;
    var canvas=new GameObject("Caption",typeof(RectTransform)).AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;
    var text=new GameObject("Role",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();text.transform.SetParent(canvas.transform,false);
    var rect=text.rectTransform;rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,0);rect.sizeDelta=new Vector2(0,70);rect.anchoredPosition=Vector2.zero;
    UiSkin.Label(text,UiSkin.Role.Caption);text.enableAutoSizing=false;text.fontSize=25;text.color=new Color(.91f,.94f,.91f);text.alignment=TextAlignmentOptions.Center;text.text=label;
    Canvas.ForceUpdateCanvases();cam.Render();cam.Render();RenderTexture.active=rt;
    var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
    RenderTexture.active=null;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);return image;
  }
}
