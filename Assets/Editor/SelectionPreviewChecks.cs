using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Runs the real screens, never a second mock UI. No PlayerPrefs writes.
public static class SelectionPreviewChecks
{
  const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  const string Output = "Builds/SelectionPreview";
  [MenuItem("Tools/Selection/Render Screens")]
  public static void Render()
  {
    if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rendering previews.");
    for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
      if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
        throw new InvalidOperationException("Save modified scenes before rendering selection previews.");
    bool oldAsyncCompilation=ShaderUtil.allowAsyncCompilation;
    ShaderUtil.allowAsyncCompilation=false;
    var setup=EditorSceneManager.GetSceneManagerSetup();
    var oldLevel=GameSession.SelectedLevel;
    string oldEnvironment=GameSession.SelectedEnvironment;
    try
    {
      SelectionBoardSync.Sync();
      Directory.CreateDirectory(Output);
      EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
      foreach(var size in new[]{new Vector2Int(1440,1080),new Vector2Int(1920,1080),new Vector2Int(2400,1080)})
      {
        for(int biome=1;biome<=7;biome++)
        {
          GameSession.SelectedEnvironment=$"Environment {biome}";
          Shoot(size,true,biome);
          Shoot(size,false,biome);
        }
      }
      Debug.Log("SELECTION CHECKS OK: 42 screen renders; preview alpha, selection, paging, cleanup and GameSession isolation verified.");
    }
    finally
    {
      ShaderUtil.allowAsyncCompilation=oldAsyncCompilation;
      GameSession.SelectedEnvironment=oldEnvironment;
      GameSession.SelectedLevel=oldLevel;
      if (setup.Any(scene => scene.isLoaded && scene.isActive))
        EditorSceneManager.RestoreSceneManagerSetup(setup);
      else
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
  }
  public static void RunBatch()
  {
    try { Render(); EditorApplication.Exit(0); }
    catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
  }
  static void Invoke(object target,string name,params object[] args)
    => target.GetType().GetMethod(name,Private).Invoke(target,args);
  static void Shoot(Vector2Int size,bool environments,int biome)
  {
    var camera=new GameObject("SelectionShotCamera").AddComponent<Camera>();
    camera.transform.position=new Vector3(0,0,-100);
    camera.orthographic=true;
    camera.clearFlags=CameraClearFlags.SolidColor;
    camera.backgroundColor=Color.black;
    var output=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32);
    camera.targetTexture=output;
    var host=new GameObject("SelectionShotCanvas",typeof(RectTransform));
    var canvas=host.AddComponent<Canvas>();
    canvas.renderMode=RenderMode.ScreenSpaceCamera;
    canvas.worldCamera=camera;
    canvas.planeDistance=10;
    var scaler=host.AddComponent<CanvasScaler>();
    scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution=new Vector2(1280,720);
    scaler.matchWidthOrHeight=1;
    GameObject screen=null;
    try
    {
      string name=environments?"EnvironmentsScreen":"LevelsScreen";
      var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Screens/{name}.prefab");
      screen=Object.Instantiate(prefab,host.transform);
      UiSkin.Stretch((RectTransform)screen.transform);
      Canvas.ForceUpdateCanvases();
      var originalLevel=GameSession.SelectedLevel;
      string originalEnvironment=GameSession.SelectedEnvironment;
      MonoBehaviour controller=environments?(MonoBehaviour)screen.GetComponent<EnvironmentsScreen>():screen.GetComponent<LevelSelectionScreen>();
      Invoke(controller,"Start");
      if(environments)
      {
        controller.GetType().GetField("selected",Private).SetValue(controller,biome-1);
        Invoke(controller,"Refresh");
      }
      if(GameSession.SelectedLevel!=originalLevel || GameSession.SelectedEnvironment!=originalEnvironment)
        throw new InvalidOperationException("Browsing a preview changed GameSession.");
      foreach(var fade in host.GetComponentsInChildren<ScreenFade>()) Object.DestroyImmediate(fade.gameObject);
      Canvas.ForceUpdateCanvases();
      LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)host.transform);
      if(!environments) Invoke(controller,"Relayout");
      if(environments) ValidateIsland(screen.GetComponentInChildren<SelectionIslandPreview>());
      // Batch mode has no normal frame between construction and capture.
      Canvas.ForceUpdateCanvases();
      foreach(var label in screen.GetComponentsInChildren<TMPro.TMP_Text>())
      {
        label.ForceMeshUpdate();
        label.SetMaterialDirty();
        label.Rebuild(CanvasUpdate.PreRender);
      }
      Canvas.ForceUpdateCanvases();
      foreach(var trail in screen.GetComponentsInChildren<SelectionTrailGraphic>())
      {
        trail.Rebuild(CanvasUpdate.PreRender);
        var mesh=trail.canvasRenderer.GetMesh();
        if(mesh==null || mesh.vertexCount<4) throw new InvalidOperationException("Trail ribbon is empty.");
      }
      camera.Render();
      var shot=Read(output);
      File.WriteAllBytes($"{Output}/{(environments?"biomes":"levels")}-{biome}-{size.x}x{size.y}.png",shot.EncodeToPNG());
      Object.DestroyImmediate(shot);
      if(size.x==1440) ValidateBrowsing(controller, screen, environments);
      if(GameSession.SelectedLevel!=originalLevel || GameSession.SelectedEnvironment!=originalEnvironment)
        throw new InvalidOperationException("Changing selection changed GameSession.");
    }
    finally
    {
      camera.targetTexture=null;
      Object.DestroyImmediate(host);
      Object.DestroyImmediate(camera.gameObject);
      output.Release();Object.DestroyImmediate(output);
    }
  }
  static void ValidateBrowsing(MonoBehaviour controller, GameObject screen, bool environments)
  {
    var preview=screen.GetComponentInChildren<SelectionIslandPreview>();
    if(environments)
    {
      var originalTexture=preview.GetComponent<RawImage>().texture;
      Invoke(preview,"OnDisable");
      if(preview.GetComponent<RawImage>().texture!=null || originalTexture!=null)
        throw new InvalidOperationException("Preview texture was not released.");
      Invoke(controller,"Refresh");
      var field=controller.GetType().GetField("selected",Private);
      int original=(int)field.GetValue(controller);
      var swipe=new UnityEngine.EventSystems.PointerEventData(null);
      swipe.pressPosition=Vector2.zero;
      swipe.position=new Vector2(-Screen.width*.3f,0);
      ((EnvironmentsScreen)controller).OnEndDrag(swipe);
      if((int)field.GetValue(controller)!=Mathf.Min(6,original+1))
        throw new InvalidOperationException("Left swipe did not advance environment.");
      swipe.position=new Vector2(Screen.width*.3f,0);
      ((EnvironmentsScreen)controller).OnEndDrag(swipe);
      if((int)field.GetValue(controller)!=Mathf.Max(0,Mathf.Min(6,original+1)-1))
        throw new InvalidOperationException("Right swipe did not return environment.");
      field.SetValue(controller,original); Invoke(controller,"Refresh");
    }
    else
    {
      var levels=LevelRepository.GetLevelsForEnvironment(GameSession.SelectedEnvironment);
      for(int i=0;i<levels.Count;i++)
      {
        Invoke(controller,"Select",i);
        var play=(Button)controller.GetType().GetField("play",Private).GetValue(controller);
        if(play.interactable!=LevelProgress.IsLevelUnlocked(levels[i].environmentName,levels[i].levelNumber))
          throw new InvalidOperationException("Play button disagrees with saved progression.");
      }
      Invoke(controller,"Relayout");
      int expectedPage=SelectionRouteLayout.PageForIndex(levels.Count-1,((LevelSelectionScreen)controller).PageSize);
      int actualPage=(int)controller.GetType().GetField("page",Private).GetValue(controller);
      if(actualPage!=expectedPage) throw new InvalidOperationException("Selected level lost its page.");
      Invoke(controller,"ChangePage",1);
      if((int)controller.GetType().GetField("page",Private).GetValue(controller)!=expectedPage)
        throw new InvalidOperationException("Paging exceeded last page.");
      Invoke(controller,"ChangePage",-100);
      if((int)controller.GetType().GetField("page",Private).GetValue(controller)!=0)
        throw new InvalidOperationException("Paging exceeded first page.");
    }
    if(environments) ValidateIsland(preview);
    else if(screen.GetComponentsInChildren<SelectionTrailGraphic>().Length!=2)
      throw new InvalidOperationException("Level route was not built.");
    if(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="SelectionPreviewStage"))
      throw new InvalidOperationException("Temporary preview geometry was not released.");
  }
  static void ValidateIsland(SelectionIslandPreview preview)
  {
    var rt=preview.GetComponent<RawImage>().texture as RenderTexture;
    if(rt==null) throw new InvalidOperationException("Island has no render target.");
    var image=Read(rt);
    try
    {
      var pixels=image.GetPixels32();
      int solid=pixels.Count(c=>c.a>200),clear=pixels.Count(c=>c.a<10);
      if(solid<pixels.Length*.03f || clear<pixels.Length*.10f)
        throw new InvalidOperationException("Island render is blank or has lost its transparent background.");
    }
    finally { Object.DestroyImmediate(image); }
  }
  static Texture2D Read(RenderTexture rt)
  {
    var previous=RenderTexture.active;
    try
    {
      RenderTexture.active=rt;
      var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
      image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();return image;
    }
    finally { RenderTexture.active=previous; }
  }
}
