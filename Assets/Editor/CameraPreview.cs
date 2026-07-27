using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Renders the gameplay camera at several device aspect ratios so the framing
// can be checked without launching the game on hardware.
public static class CameraPreview
{
  private struct Device
  {
    public string name;
    public int width;
    public int height;
  }

  private static readonly Device[] Devices =
  {
    new Device { name = "ipad-4x3", width = 1024, height = 768 },
    new Device { name = "laptop-16x9", width = 1024, height = 576 },
    new Device { name = "iphone-19.5x9", width = 1040, height = 480 },
    new Device { name = "android-20x9", width = 1067, height = 480 }
  };

  private const string OutputDir = "Builds/CameraPreview";

  // Diagnostic: writes the raw generated ground textures to disk to check the
  // generation independent of scene lighting.
  public static void DumpTextures()
  {
    Directory.CreateDirectory(OutputDir);
    System.IO.File.WriteAllBytes($"{OutputDir}/tex_sand.png", GroundTextureFactory.Sand().EncodeToPNG());
    System.IO.File.WriteAllBytes($"{OutputDir}/tex_toxic.png", GroundTextureFactory.Toxic().EncodeToPNG());
    Debug.Log("DUMP OK");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  // Renders the meadow at several pitch/FOV combos so a lower, closer, more
  // cinematic camera can be chosen by comparison.
  public static void RenderCameraExperiment()
  {
    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
    CameraRig rig = Object.FindFirstObjectByType<CameraRig>();
    if (rig == null) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }
    Camera cam = rig.GetComponent<Camera>();

    Directory.CreateDirectory(OutputDir);
    List<GameObject> towers = PlaceRealTowers();
    EnvironmentTheme.Apply("Environment 1");

    // (pitch, fov)
    (float pitch, float fov)[] combos =
    {
      (34f, 45f), (30f, 50f), (27f, 55f), (24f, 60f),
    };

    var so = new SerializedObject(rig);
    so.FindProperty("adaptPitchToAspect").boolValue = false;
    so.ApplyModifiedPropertiesWithoutUndo();

    foreach (var combo in combos)
    {
      so.Update();
      so.FindProperty("playPitch").floatValue = combo.pitch;
      so.FindProperty("fieldOfView").floatValue = combo.fov;
      so.ApplyModifiedPropertiesWithoutUndo();
      Capture(rig, cam, Devices[1], 0, $"cam-p{combo.pitch:00}-f{combo.fov:00}", false);
    }

    foreach (GameObject t in towers) Object.DestroyImmediate(t);
    Debug.Log("CAM EXPERIMENT OK");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  public static void Render()
  {
    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);

    CameraRig rig = Object.FindFirstObjectByType<CameraRig>();
    if (rig == null)
    {
      Debug.LogError("PREVIEW FAIL: no CameraRig in MainGame scene");
      if (Application.isBatchMode) EditorApplication.Exit(1);
      return;
    }

    Camera cam = rig.GetComponent<Camera>();

    Directory.CreateDirectory(OutputDir);

    // Framing shots use placeholder markers + shaded HUD bands
    List<GameObject> temporaries = BuildTemporaryVisuals();
    foreach (Device device in Devices)
    {
      Capture(rig, cam, device, 0, "play", true);
    }
    Capture(rig, cam, Devices[1], 1, "intro", true);
    Capture(rig, cam, Devices[1], 2, "outro", true);
    foreach (GameObject temp in temporaries)
    {
      Object.DestroyImmediate(temp);
    }

    // Theme shots use real tower prefabs, the level decorator (props + base +
    // portal), and each environment's palette, no HUD shading
    List<GameObject> towers = PlaceRealTowers();
    GridManager grid = Object.FindFirstObjectByType<GridManager>();
    Vector3[] pathPts = PreviewPathPoints(grid);

    var decorGo = new GameObject("PreviewDecor");
    var decor = decorGo.AddComponent<LevelDecorator>();

    foreach (string env in new[] { "Environment 1", "Environment 2", "Environment 3" })
    {
      EnvironmentTheme.Apply(env);
      decor.BuildAt(pathPts);
      string label = env.Replace(" ", "").ToLower(); // environment1, ...
      Capture(rig, cam, Devices[1], 0, label, false);
    }

    Object.DestroyImmediate(decorGo);
    foreach (GameObject t in towers)
    {
      Object.DestroyImmediate(t);
    }

    Debug.Log($"PREVIEW OK: wrote images to {OutputDir}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  private static Vector3[] PreviewPathPoints(GridManager grid)
  {
    LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(
      "Assets/Resources/Levels/Environment1/Level01.asset");
    if (grid == null || level == null || level.pathConfig == null) return null;

    var cells = level.pathConfig.pathGridCoordinates;
    var pts = new Vector3[cells.Count];
    for (int i = 0; i < cells.Count; i++)
    {
      pts[i] = grid.GridToWorld(cells[i]);
      pts[i].y = 0f;
    }
    return pts;
  }

  private static void Capture(CameraRig rig, Camera cam, Device device, int poseIndex, string label, bool shadeHud)
  {
    float aspect = (float)device.width / device.height;

    rig.EditorPreview(poseIndex, aspect);
    rig.enabled = false; // stop LateUpdate from re-framing with the editor aspect

    var rt = new RenderTexture(device.width, device.height, 24, RenderTextureFormat.ARGB32)
    {
      antiAliasing = 2
    };

    RenderTexture previousTarget = cam.targetTexture;
    RenderTexture previousActive = RenderTexture.active;

    cam.targetTexture = rt;
    cam.aspect = aspect;
    cam.Render();

    RenderTexture.active = rt;
    var texture = new Texture2D(device.width, device.height, TextureFormat.RGB24, false);
    texture.ReadPixels(new Rect(0, 0, device.width, device.height), 0, 0);

    if (shadeHud) ShadeHudBands(texture, rig.TopReserve, rig.BottomReserve);
    texture.Apply();

    File.WriteAllBytes($"{OutputDir}/{device.name}-{label}.png", texture.EncodeToPNG());

    cam.targetTexture = previousTarget;
    RenderTexture.active = previousActive;
    rt.Release();
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(texture);

    rig.enabled = true;
  }

  // Darkens the strips the HUD will occupy, so it is obvious whether the board
  // is hidden behind the stats bar or the towers panel.
  private static void ShadeHudBands(Texture2D texture, float topReserve, float bottomReserve)
  {
    int topRows = Mathf.RoundToInt(texture.height * topReserve);
    int bottomRows = Mathf.RoundToInt(texture.height * bottomReserve);

    for (int y = 0; y < texture.height; y++)
    {
      bool inBand = y < bottomRows || y >= texture.height - topRows;
      if (!inBand) continue;

      for (int x = 0; x < texture.width; x++)
      {
        Color c = texture.GetPixel(x, y);
        texture.SetPixel(x, y, Color.Lerp(c, new Color(0.9f, 0.2f, 0.4f), 0.35f));
      }
    }
  }

  // Places real tower prefabs at the same Y a placement would use (grass level),
  // so both the visual theme and whether towers sit on the ground can be judged.
  private static List<GameObject> PlaceRealTowers()
  {
    var created = new List<GameObject>();
    GridManager grid = Object.FindFirstObjectByType<GridManager>();
    LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(
      "Assets/Resources/Levels/Environment1/Level01.asset");
    if (grid == null || level == null || level.pathConfig == null) return created;

    string[] towerPaths =
    {
      "Assets/Prefabs/Towers/ArcherTower/ArcherTower.prefab",
      "Assets/Prefabs/Towers/SniperTower/SniperTower.prefab",
      "Assets/Prefabs/Towers/IceTower/IceTower.prefab",
    };

    var pathCells = new HashSet<Vector2Int>(level.pathConfig.pathGridCoordinates);
    int placed = 0;
    for (int x = 0; x < grid.gridSize.x && placed < towerPaths.Length; x++)
    {
      for (int y = 0; y < grid.gridSize.y && placed < towerPaths.Length; y++)
      {
        var cell = new Vector2Int(x, y);
        if (pathCells.Contains(cell)) continue;
        if (!pathCells.Contains(cell + Vector2Int.up) && !pathCells.Contains(cell + Vector2Int.down)) continue;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(towerPaths[placed]);
        if (prefab == null) { placed++; continue; }

        GameObject tower = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Vector3 pos = grid.GridToWorld(cell);
        pos.y = 0f; // grass surface, matching how placement snaps
        tower.transform.position = pos;
        created.Add(tower);
        placed++;
      }
    }
    return created;
  }

  // The path and towers only exist at runtime, so stand-ins are spawned to make
  // the framing legible.
  private static List<GameObject> BuildTemporaryVisuals()
  {
    var created = new List<GameObject>();

    GridManager grid = Object.FindFirstObjectByType<GridManager>();
    LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(
      "Assets/Resources/Levels/Environment1/Level01.asset");
    if (grid == null || level == null || level.pathConfig == null) return created;

    var pathMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
    {
      color = new Color(0.35f, 0.22f, 0.12f)
    };
    var towerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
    {
      color = new Color(0.55f, 0.25f, 0.7f)
    };

    var pathCells = new HashSet<Vector2Int>(level.pathConfig.pathGridCoordinates);
    foreach (Vector2Int cell in pathCells)
    {
      GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
      marker.name = "TEMP_Path";
      marker.transform.position = grid.GridToWorld(cell) + Vector3.up * 0.05f;
      marker.transform.localScale = new Vector3(grid.cellSize, 0.1f, grid.cellSize);
      marker.GetComponent<MeshRenderer>().sharedMaterial = pathMaterial;
      created.Add(marker);
    }

    // A few stand-in towers next to the path, to show scale and perspective
    int placed = 0;
    for (int x = 0; x < grid.gridSize.x && placed < 6; x++)
    {
      for (int y = 0; y < grid.gridSize.y && placed < 6; y++)
      {
        var cell = new Vector2Int(x, y);
        if (pathCells.Contains(cell)) continue;
        if (!pathCells.Contains(cell + Vector2Int.up) && !pathCells.Contains(cell + Vector2Int.down)) continue;

        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        tower.name = "TEMP_Tower";
        tower.transform.position = grid.GridToWorld(cell) + Vector3.up * 2f;
        tower.transform.localScale = new Vector3(2.4f, 2f, 2.4f);
        tower.GetComponent<MeshRenderer>().sharedMaterial = towerMaterial;
        created.Add(tower);
        placed++;
      }
    }

    return created;
  }
}
