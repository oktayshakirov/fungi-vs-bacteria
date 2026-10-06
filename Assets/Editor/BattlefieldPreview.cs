using System;
using System.IO;
using System.Reflection;
using TMPro;
using TowerDefense.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Real board + real scene HUD + real tower-card prefabs in one render.
// This deliberately avoids UiPreview's cropped background and mock cards:
// fitting the camera to those would hide layout/framing regressions.
public static class BattlefieldPreview
{
  private const string Output = "Builds/BattlefieldPreview";
  private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

  [MenuItem("Tools/Display/Check Battlefield Readability")]
  public static void Render()
  {
    Directory.CreateDirectory(Output);
    bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
    ShaderUtil.allowAsyncCompilation = false;
    LevelConfig oldLevel = GameSession.SelectedLevel;
    string oldEnvironment = GameSession.SelectedEnvironment;
    try
    {
      foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(2340,1080),
        new Vector2Int(2400,1080), new Vector2Int(1440,1080) })
      {
        Shoot(1, size, 42f, false);
        Shoot(1, size, 42f, true);
      }
      Shoot(1, new Vector2Int(1920,1080), 34f, false);
      Shoot(1, new Vector2Int(1920,1080), 50f, false);
      Shoot(3, new Vector2Int(2400,1080), 42f, false);
      Shoot(4, new Vector2Int(1440,1080), 42f, false);
      Shoot(1,new Vector2Int(1920,1080),42f,false,true);
      Shoot(1,new Vector2Int(1440,1080),42f,false,true);
      Shoot(1,new Vector2Int(1920,1080),42f,false,false,true);
      Shoot(1,new Vector2Int(1440,1080),42f,false,true,true);
      TacticalShots();
      Debug.Log("BATTLEFIELD PREVIEW: 22 real-board/HUD renders (including complete-cast and dense waves); camera bounds, card visibility and disabled drag checks passed.");
    }
    finally
    {
      ShaderUtil.allowAsyncCompilation = asyncCompilation;
      GameSession.SelectedLevel = oldLevel;
      GameSession.SelectedEnvironment = oldEnvironment;
    }
  }

  public static void RunBatch()
  {
    try { Render(); EditorApplication.Exit(0); }
    catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
  }

  private static void PlaceCrowd(Vector3[] path,bool fullCast=false)
  {
    float length=0;for(int i=1;i<path.Length;i++)length+=Vector3.Distance(path[i-1],path[i]);
    string[] roles=fullCast?FullCastReview.Enemies:new[]{"BasicEnemy","FastEnemy","HealerEnemy","ArmoredEnemy"};
    for(int i=0;i<28;i++)
    {
      float remaining=length*(i+1f)/29f;int segment=1;
      while(segment<path.Length-1 && remaining>Vector3.Distance(path[segment-1],path[segment]))
      {remaining-=Vector3.Distance(path[segment-1],path[segment]);segment++;}
      Vector3 from=path[segment-1],to=path[segment];Vector3 dir=to-from;dir.y=0;
      Vector3 position=Vector3.Lerp(from,to,remaining/Vector3.Distance(from,to));
      var config=AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{roles[i%roles.Length]}.asset");
      position.y=UnitScale.EnemyGroundOffset(config.prefab,config.scaleMultiplier);
      var unit=(GameObject)PrefabUtility.InstantiatePrefab(config.prefab);unit.transform.localScale*=UnitScale.Enemy*config.scaleMultiplier;
      float yaw=new SerializedObject(config.prefab.GetComponent<Enemy>()).FindProperty("rotationOffset").floatValue;
      unit.transform.SetPositionAndRotation(position,Quaternion.LookRotation(dir.normalized)*Quaternion.Euler(0,yaw,0));
      EnemyPreview.TintBody(unit,config);
      EnemyPreview.TintTraits(unit);
      foreach(var script in unit.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
    }
  }

  private static void Shoot(int biome, Vector2Int size, float pitch, bool collapsed, bool dense=false, bool fullCast=false, int tactical=0, int feedback=0, int specialty=0, bool challenge=false, int campaignLevel=0)
  {
    var board = CameraPreview.BoardContext.Open();
    if (board == null) throw new InvalidOperationException("Missing board context.");
    var placed=board.Build(biome, campaignLevel>0 ? $"Level{campaignLevel:00}" : challenge ? "Level01" : feedback>0 ? "Level04" : tactical>0 ? "Level03" : "Level01", true,fullCast);
    if(dense)
    {
      foreach(var unit in placed) if(unit.GetComponent<Enemy>() != null)Object.DestroyImmediate(unit);
      PlaceCrowd(board.PathPoints(),fullCast);
    }
    Tower feedbackSelected=feedback>0?FeedbackPreview.Prepare(feedback,board.PathPoints()):null;
    if(specialty>0) feedbackSelected=Phase5Preview.PrepareArcher(placed,specialty);
    Camera cam = board.cam;
    var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
    cam.targetTexture = target;
    cam.aspect = size.x / (float)size.y;
    var rigProperties = new SerializedObject(board.rig);
    rigProperties.FindProperty("viewPresets").GetArrayElementAtIndex(0).vector3Value = new Vector3(pitch, 0, 1);
    rigProperties.ApplyModifiedPropertiesWithoutUndo();

    // Rehost the authored HUD children on a fresh camera canvas. A serialized
    // overlay canvas retains its old projection geometry when converted during
    // a batch render; runtime remains ScreenSpaceOverlay and has no such issue.
    Canvas authoredCanvas = Object.FindFirstObjectByType<Canvas>();
    var host = new GameObject("BattlefieldHud", typeof(RectTransform));
    Canvas canvas = host.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = cam;
    canvas.planeDistance = 10f;
    CanvasScaler scaler = host.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1280,720);
    scaler.matchWidthOrHeight = 1f;
    host.AddComponent<GraphicRaycaster>();
    while (authoredCanvas.transform.childCount > 0)
      authoredCanvas.transform.GetChild(0).SetParent(host.transform,false);
    Object.DestroyImmediate(authoredCanvas.gameObject);
    Canvas.ForceUpdateCanvases();
    HUDManager hud = Object.FindFirstObjectByType<HUDManager>();
    var fields = new SerializedObject(hud);
    TMP_Text gold = (TMP_Text)fields.FindProperty("goldText").objectReferenceValue;
    TMP_Text health = (TMP_Text)fields.FindProperty("healthText").objectReferenceValue;
    TMP_Text wave = (TMP_Text)fields.FindProperty("waveText").objectReferenceValue;
    TMP_Text timer = (TMP_Text)fields.FindProperty("timerText").objectReferenceValue;
    Button start = (Button)fields.FindProperty("startWaveButton").objectReferenceValue;
    Button pause = (Button)fields.FindProperty("pauseGameButton").objectReferenceValue;
    fields.FindProperty("towerActionsPanel").objectReferenceValue.AsGameObject().SetActive(false);
    gold.text = "515"; health.text = "100"; wave.text = challenge ? $"WAVE {GameSession.SelectedLevel.waveConfig.waves.Length-1}/{GameSession.SelectedLevel.waveConfig.waves.Length}" : feedback>0 ? (feedback>=3?"WAVE 3/3":"WAVE 2/3") : tactical>0 ? "WAVE 2/3" : $"WAVE 1/{GameSession.SelectedLevel.waveConfig.waves.Length}";
    RectTransform stats = gold.transform.parent as RectTransform;
    Transform safe = stats.parent;
    RectTransform panel = safe.Find("TowersPanel") as RectTransform;
    HudTheme.Apply(stats, gold, health, wave, timer, start, pause, panel);
    GameSpeedButton.Create(safe, stats, 0);
    CameraViewButton.Create(safe, stats, 1);

    var ui = new SerializedObject(Object.FindFirstObjectByType<TowerUI>());
    var database = (TowerDatabase)ui.FindProperty("towerDatabase").objectReferenceValue;
    var prefab = (TowerSelectionButton)ui.FindProperty("buttonPrefab").objectReferenceValue;
    for (int i = panel.childCount - 1; i >= 0; i--) Object.DestroyImmediate(panel.GetChild(i).gameObject);
    foreach (TowerConfig config in database.availableTowers)
    {
      TowerSelectionButton card = Object.Instantiate(prefab, panel);
      Invoke(card, "Awake");
      card.Initialize(config, _ => { });
      card.RefreshAffordability(config.cost <= 515);
    }
    var forecast = WavePreview.Create(safe, timer, hud);
    var next = GameSession.SelectedLevel.waveConfig.waves[challenge ? GameSession.SelectedLevel.waveConfig.waves.Length-1 : tactical>0 ? 2 : 1];
    if(tactical==3)
    {
      next = new WaveConfig.Wave { enemyGroups = Array.ConvertAll(FullCastReview.Enemies, name =>
        new WaveConfig.WaveEnemyGroup { enemyConfig=AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset"),count=3 }) };
    }
    forecast.Bind(next, challenge ? GameSession.SelectedLevel.waveConfig.waves.Length : tactical>0 ? 3 : 2, tactical==1 || tactical==3);
    forecast.SetStatus(tactical==1 || tactical==3 ? "NEXT IN 12s  /  SCOUT" : "SCOUT NEXT WAVE");
    start.GetComponentInChildren<TMP_Text>().text = tactical==1 || tactical==3 ? "SEND NEXT WAVE" : "DEFEAT THIS WAVE";
    if(tactical==2)
    {
      Tower unit = placed.Find(go=>go.GetComponent<Tower>()!=null).GetComponent<Tower>();
      Invoke(unit,"Awake");
      unit.Initialize(AssetDatabase.LoadAssetAtPath<TowerConfig>("Assets/Settings/Towers/ArcherTower.asset"),true);
      var actions = fields.FindProperty("towerActionsPanel").objectReferenceValue.AsGameObject().GetComponent<TowerActions>();
      Invoke(actions,"Awake");
      actions.ShowForTower(unit);
      unit.SetPriority(TargetPriority.Strong);
      Invoke(actions,"RefreshPriority");
      forecast.ApplyVisibility();
    }
    if(feedbackSelected!=null)
    {
      var actions=fields.FindProperty("towerActionsPanel").objectReferenceValue.AsGameObject().GetComponent<TowerActions>();
      Invoke(actions,"Awake");actions.ShowForTower(feedbackSelected);
      forecast.ApplyVisibility();
    }
    Canvas.ForceUpdateCanvases();
    if(specialty>0) Phase5Preview.ValidateLabels(canvas);
    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
    foreach (HudKeepClear keep in canvas.GetComponentsInChildren<HudKeepClear>()) keep.Apply();
    if (collapsed)
    {
      Find(canvas.transform, "TowersToggle").GetComponent<Button>().onClick.Invoke();
      Canvas.ForceUpdateCanvases();
    }

    board.rig.EditorPreview(0, cam.aspect);
    board.rig.enabled = false;
    // A render settles the ScreenSpaceCamera canvas geometry at this aspect.
    cam.Render(); Canvas.ForceUpdateCanvases();
    board.rig.EditorPreview(0, cam.aspect);
    Canvas.ForceUpdateCanvases();
    cam.Render(); Canvas.ForceUpdateCanvases();
    ValidateCanvas(canvas, cam);
    ValidateBounds(board.rig, cam);
    ValidateCards(panel, collapsed);
    if(tactical>0) ValidateTactical(canvas);
    CameraPreview.PoseSymbols(cam);
    if(feedback>0) FeedbackPreview.Pose(cam);
    cam.Render();
    RenderTexture previous = RenderTexture.active;
    RenderTexture.active = target;
    var shot = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
    shot.ReadPixels(new Rect(0,0,size.x,size.y),0,0); shot.Apply();
    RenderTexture.active = previous;
    File.WriteAllBytes($"{Output}/env{biome}-{size.x}x{size.y}-p{pitch:0}{(collapsed ? "-closed" : "")}{(dense ? "-dense" : "")}{(fullCast ? "-full-cast" : "")}{(tactical>0 ? "-tactical"+tactical : "")}{(feedback>0 ? "-feedback"+feedback : "")}{(specialty>0 ? "-specialty"+specialty : "")}{(challenge ? "-challenge" : "")}.png", shot.EncodeToPNG());
    Object.DestroyImmediate(shot);
    cam.targetTexture = null;
    target.Release(); Object.DestroyImmediate(target);
  }

  public static void RunFeedbackBatch()
  {
    ShaderUtil.allowAsyncCompilation=false;Directory.CreateDirectory(Output);
    try
    {
      foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1440,1080)})
        for(int mode=1;mode<=6;mode++) Shoot(1,size,42,false,mode==2,false,0,mode);
      Debug.Log("FEEDBACK PREVIEW: twelve phone/tablet frames of links, crowded feedback and both boss warnings/fortify/rush passed.");
      EditorApplication.Exit(0);
    }
    catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
  }

  private static void TacticalShots()
  {
    foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1440,1080)})
      for(int mode=1;mode<=3;mode++) Shoot(1,size,42,false,false,false,mode);
  }
  public static void RunTacticalBatch()
  {
    ShaderUtil.allowAsyncCompilation=false;
    Directory.CreateDirectory(Output);
    try { TacticalShots(); EditorApplication.Exit(0); }
    catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
  }
  private static void ValidateTactical(Canvas canvas)
  {
    foreach(var label in canvas.GetComponentsInChildren<TMP_Text>())
    {
      if(!label.transform.IsChildOf(Find(canvas.transform,"WavePreview")) && label.transform.parent.name!="TargetPriority") continue;
      label.ForceMeshUpdate();
      if(label.isTextOverflowing) throw new InvalidOperationException("Tactical label clips: "+label.name+" / "+label.text);
    }
    var forecast=Find(canvas.transform,"Forecast") as RectTransform;
    var priority=Find(canvas.transform,"TargetPriority") as RectTransform;
    if(priority!=null && priority.gameObject.activeInHierarchy && forecast.gameObject.activeInHierarchy)
      throw new InvalidOperationException("Forecast overlaps selected-tower panel.");
  }

  private static void ValidateCanvas(Canvas canvas, Camera cam)
  {
    var corners = new Vector3[4];
    ((RectTransform)canvas.transform).GetWorldCorners(corners);
    Vector3 min = cam.WorldToViewportPoint(corners[0]);
    Vector3 max = cam.WorldToViewportPoint(corners[2]);
    if (Mathf.Abs(min.x) > .005f || Mathf.Abs(min.y) > .005f
      || Mathf.Abs(max.x - 1f) > .005f || Mathf.Abs(max.y - 1f) > .005f)
      throw new InvalidOperationException($"Preview canvas does not match camera image: {min}, {max}.");
  }

  private static void ValidateBounds(CameraRig rig, Camera cam)
  {
    Bounds board = (Bounds)typeof(CameraRig).GetMethod("GetBoardBounds", Private).Invoke(rig, null);
    Rect viewport = rig.GameplayViewport;
    const float tolerance = .002f;
    for (int i = 0; i < 8; i++)
    {
      Vector3 e = board.extents;
      Vector3 corner = board.center + new Vector3((i&1)==0 ? -e.x:e.x, (i&2)==0 ? -e.y:e.y, (i&4)==0 ? -e.z:e.z);
      Vector3 point = cam.WorldToViewportPoint(corner);
      if (point.z <= 0 || point.x < viewport.xMin-tolerance || point.x > viewport.xMax+tolerance
        || point.y < viewport.yMin-tolerance || point.y > viewport.yMax+tolerance)
        throw new InvalidOperationException($"Camera clips corner {i}: {point} outside {viewport} at aspect {cam.aspect}.");
    }
  }

  private static void ValidateCards(RectTransform panel, bool collapsed)
  {
    TowerSelectionButton[] cards = panel.GetComponentsInChildren<TowerSelectionButton>(true);
    if (cards.Length != 8) throw new InvalidOperationException($"Expected eight tower cards, got {cards.Length}.");
    if (collapsed) return;
    var corners = new Vector3[4];
    var viewport = panel.parent as RectTransform;
    foreach (TowerSelectionButton card in cards)
    {
      ((RectTransform)card.transform).GetWorldCorners(corners);
      for (int i=0;i<4;i++)
      {
        Vector3 p = viewport.InverseTransformPoint(corners[i]);
        if (p.y < viewport.rect.yMin-.5f || p.y > viewport.rect.yMax+.5f)
          throw new InvalidOperationException($"Tower card clipped: {card.name}.");
      }
      // Unaffordable cards must neither click nor begin a tower drag.
      card.RefreshAffordability(false);
      if (card.GetComponent<Button>().interactable) throw new InvalidOperationException("Unaffordable card remains interactive.");
      card.RefreshAffordability(true);
    }
  }

  private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target,null);
  private static Transform Find(Transform root, string name)
  {
    if(root.name == name) return root;
    for(int i=0;i<root.childCount;i++) { Transform found=Find(root.GetChild(i),name); if(found != null) return found; }
    return null;
  }
  private static GameObject AsGameObject(this Object value) => value as GameObject;
}
