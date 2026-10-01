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

      // The same shot with the cast standing on the path. Separate from the
      // clean plate deliberately: the plate is how the environment art is
      // judged, and eight enemies in front of it would get in the way.
      List<GameObject> cast = PlaceRealEnemies(grid);
      Capture(rig, cam, Devices[1], 0, label + "-enemies", false);
      foreach (GameObject e in cast) Object.DestroyImmediate(e);

      // The play camera barely sees the island's underside. Pose 1 is the low
      // intro orbit, which is where the cliff silhouette can actually be judged.
      if (env == "Environment 1") Capture(rig, cam, Devices[1], 1, label + "-cliff", false);
    }

    Object.DestroyImmediate(decorGo);
    foreach (GameObject t in towers)
    {
      Object.DestroyImmediate(t);
    }

    Debug.Log($"PREVIEW OK: wrote images to {OutputDir}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  // The real board of one level per biome, built the way the game builds it:
  // the level's own path through PathManager + PathVisualizer (the actual line
  // the player sees), the decorator, the theme, towers beside the path and the
  // cast standing on it. Render() only ever shows Environment 1-3 with no path,
  // which is how a map can look finished in preview and unfinished in play.
  public static void RenderBoards()
  {
    var ctx = BoardContext.Open();
    if (ctx == null) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }

    string only = System.Environment.GetEnvironmentVariable("BOARD_ENVS");
    string file = System.Environment.GetEnvironmentVariable("BOARD_LEVEL") ?? "Level05";
    for (int n = 1; n <= 7; n++)
    {
      if (!string.IsNullOrEmpty(only) && !only.Contains(n.ToString())) continue;
      List<GameObject> props = ctx.Build(n, file, withCast: true);
      if (props == null) continue;
      Capture(ctx.rig, ctx.cam, Devices[3], 0, $"board-env{n}", false);
      if (n == 1) Capture(ctx.rig, ctx.cam, Devices[0], 0, $"board-env{n}", false);
      foreach (GameObject go in props) Object.DestroyImmediate(go);
    }

    Debug.Log($"BOARDS OK: wrote images to {OutputDir}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  // A close-up of each biome's two landmarks - the base at the end of the path
  // and the nest at the start - standing in that biome's own light, fog and
  // ground. RenderBoards shows the whole island, where either is forty pixels
  // across in a far corner, and that is exactly how the first pass at the
  // volcanic and alien bases got as far as a render with a silhouette that
  // vanished into the ground. Judge a landmark here, then confirm it in
  // RenderBoards; the two answer different questions.
  public static void RenderLandmarks()
  {
    var ctx = BoardContext.Open();
    if (ctx == null) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }

    var shot = new Device { name = "base", width = 760, height = 760 };
    int written = 0;
    for (int n = 1; n <= 7; n++)
    {
      // No cast: eight enemies in front of the house is the one thing that
      // would get in the way of the only question this shot is asking.
      List<GameObject> props = ctx.Build(n, "Level05", withCast: false);
      if (props == null) continue;

      // The nest is a flat ring on the ground, so it wants a steeper, closer
      // look than a four-metre house does - at the base's angle a scenery
      // mushroom standing between it and the camera covers most of it.
      foreach ((string name, string label, Vector3 eye, float aim) in new[]
               { ("BaseStructure", "base", new Vector3(3.2f, 6.6f, -10.2f), 2.1f),
                 ("SpawnPortal", "nest", new Vector3(2.0f, 8.2f, -6.6f), 0.3f) })
      {
        GameObject landmark = GameObject.Find(name);
        if (landmark == null)
        {
          Debug.LogError($"LANDMARKS FAIL: environment {n} built no {name}");
          continue;
        }
        CaptureCloseUp(ctx.cam, landmark.transform.position, eye, aim, shot,
                       $"{OutputDir}/{label}-env{n}.png");
        written++;
      }
      foreach (GameObject go in props) Object.DestroyImmediate(go);
    }

    Debug.Log($"LANDMARKS OK: wrote {written} close-ups to {OutputDir}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  // Clones the gameplay camera rather than making a bare one, so the shot
  // keeps the scene's skybox, fog and post-processing - without the bloom the
  // emissive parts (windows, lava fissures, alien pores) are flat colours, and
  // those are most of what the dark biomes' bases are read by.
  private static void CaptureCloseUp(Camera source, Vector3 target, Vector3 eyeOffset,
                                     float aimHeight, Device device, string path)
  {
    var go = Object.Instantiate(source.gameObject);
    var strayRig = go.GetComponent<CameraRig>();
    if (strayRig != null) Object.DestroyImmediate(strayRig);
    var listener = go.GetComponent<AudioListener>();
    if (listener != null) Object.DestroyImmediate(listener);

    var cam = go.GetComponent<Camera>();
    cam.fieldOfView = 32f;
    // From the play camera's own side of the board, so a cap's overhang hides
    // what it hides in play.
    cam.transform.position = target + eyeOffset;
    cam.transform.LookAt(target + Vector3.up * aimHeight);

    float aspect = (float)device.width / device.height;
    var rt = new RenderTexture(device.width, device.height, 24, RenderTextureFormat.ARGB32)
    {
      antiAliasing = 2
    };
    RenderTexture previousActive = RenderTexture.active;
    cam.targetTexture = rt;
    cam.aspect = aspect;
    cam.Render();

    RenderTexture.active = rt;
    var texture = new Texture2D(device.width, device.height, TextureFormat.RGB24, false);
    texture.ReadPixels(new Rect(0, 0, device.width, device.height), 0, 0);
    texture.Apply();
    File.WriteAllBytes(path, texture.EncodeToPNG());

    RenderTexture.active = previousActive;
    cam.targetTexture = null;
    rt.Release();
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(texture);
    Object.DestroyImmediate(go);
  }

  // Photographs a spawn: the nest's mist and the enemy swelling out of it,
  // caught at six points across the half second it lasts and written as one
  // strip per biome.
  //
  // This exists because the effect is NOTHING BUT MOTION, and every other
  // render in this file is a single frame at t=0, where a spawn effect has by
  // definition not happened yet. SpawnEffect.Step and Enemy.EmergeScaleAt are
  // public so this can drive both by hand instead of waiting on an engine
  // Update that -executeMethod never runs.
  public static void RenderSpawn()
  {
    var ctx = BoardContext.Open();
    if (ctx == null) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }

    string only = System.Environment.GetEnvironmentVariable("SPAWN_ENVS");
    var frame = new Device { name = "spawn", width = 420, height = 420 };
    // Across SpawnEffect.Lifetime (0.62s). The first is deliberately BEFORE
    // the enemy is visible at all, because the question this shot answers is
    // whether the mist covers the moment it arrives.
    float[] ages = { 0.05f, 0.12f, 0.22f, 0.34f, 0.46f, 0.60f };
    int written = 0;

    for (int n = 1; n <= 7; n++)
    {
      if (!string.IsNullOrEmpty(only) && !only.Contains(n.ToString())) continue;
      List<GameObject> props = ctx.Build(n, "Level05", withCast: false);
      if (props == null) continue;

      GameObject nest = GameObject.Find("SpawnPortal");
      Vector3[] path = ctx.PathPoints();
      if (nest == null || path == null || path.Length < 2)
      {
        Debug.LogError($"SPAWN FAIL: environment {n} has no nest or no path");
        foreach (GameObject go in props) Object.DestroyImmediate(go);
        continue;
      }

      GameObject enemy = PlaceOneEnemy(path);
      Vector3 rest = enemy != null ? enemy.transform.localScale : Vector3.one;

      for (int i = 0; i < ages.Length; i++)
      {
        // A fresh effect per frame, stepped from zero to the age wanted, so
        // each shot is an honest replay rather than one effect photographed
        // while the stepping accumulates rounding.
        SpawnEffect effect = SpawnEffect.Spawn(path[0], 1.2f);
        for (float t = 0f; t < ages[i]; t += 1f / 60f) effect.Step(1f / 60f);

        if (enemy != null)
        {
          enemy.transform.localScale =
            rest * Enemy.EmergeScaleAt(ages[i] / 0.34f);   // Enemy.EmergeDuration
        }

        CaptureCloseUp(ctx.cam, path[0], new Vector3(2.0f, 5.4f, -7.0f), 0.8f,
                       frame, $"{OutputDir}/spawn-env{n}-{i}.png");
        written++;
        Object.DestroyImmediate(effect.gameObject);
      }

      if (enemy != null) Object.DestroyImmediate(enemy);
      foreach (GameObject go in props) Object.DestroyImmediate(go);
    }

    Debug.Log($"SPAWN OK: wrote {written} frames to {OutputDir}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  // One Basic enemy standing in the nest's mouth, posed and coloured the way
  // the game would have it.
  private static GameObject PlaceOneEnemy(Vector3[] path)
  {
    var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>(
      "Assets/Settings/Enemies/BasicEnemy.asset");
    if (cfg == null || cfg.prefab == null) return null;

    var go = (GameObject)PrefabUtility.InstantiatePrefab(cfg.prefab);
    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                      InteractionMode.AutomatedAction);
    go.transform.localScale *= UnitScale.Enemy * Mathf.Max(0.01f, cfg.scaleMultiplier);

    MeshRenderer body = Enemy.FindBodyRenderer(cfg.prefab);
    float y = body != null ? body.bounds.size.y * 0.5f : 0.5f;
    Vector3 dir = path[1] - path[0];
    dir.y = 0f;
    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
    go.transform.position = new Vector3(path[0].x, y, path[0].z);
    go.transform.rotation = Quaternion.LookRotation(dir.normalized);

    EnemyPreview.TintBody(go, cfg);
    EnemyPreview.TintTraits(go);
    return go;
  }

  // MainGame opened in edit mode with the singletons the path code reads wired
  // by hand (-executeMethod never runs Awake), so a level can be built the way
  // the game builds it.
  private class BoardContext
  {
    public CameraRig rig;
    public Camera cam;
    GridManager grid;
    PathManager pathManager;
    LevelDecorator decor;

    const System.Reflection.BindingFlags Any =
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;

    static readonly string[] TowerPaths =
    {
      "Assets/Prefabs/Towers/ArcherTower/ArcherTower.prefab",
      "Assets/Prefabs/Towers/IceTower/IceTower.prefab",
      "Assets/Prefabs/Towers/InfernoTower/InfernoTower.prefab",
      "Assets/Prefabs/Towers/SniperTower/SniperTower.prefab",
    };

    public static BoardContext Open()
    {
      EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
      var c = new BoardContext
      {
        rig = Object.FindFirstObjectByType<CameraRig>(),
        grid = Object.FindFirstObjectByType<GridManager>(),
        pathManager = Object.FindFirstObjectByType<PathManager>(),
      };
      if (c.rig == null || c.grid == null || c.pathManager == null)
      {
        Debug.LogError("BOARDS FAIL: MainGame is missing the rig, grid or path manager");
        return null;
      }
      c.cam = c.rig.GetComponent<Camera>();
      Directory.CreateDirectory(OutputDir);

      typeof(GridManager).GetProperty("Instance", Any).SetValue(null, c.grid);
      typeof(PathManager).GetProperty("Instance", Any).SetValue(null, c.pathManager);
      var visualizer = c.pathManager.GetComponent<PathVisualizer>();
      if (visualizer != null) typeof(PathVisualizer).GetMethod("Awake", Any).Invoke(visualizer, null);

      c.decor = Object.FindFirstObjectByType<LevelDecorator>();
      if (c.decor == null) c.decor = new GameObject("PreviewDecor").AddComponent<LevelDecorator>();
      return c;
    }

    // Returns the towers/enemies it placed, for the caller to destroy.
    public List<GameObject> Build(int env, string levelFile, bool withCast)
    {
      var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(
        $"Assets/Resources/Levels/Environment{env}/{levelFile}.asset");
      if (level == null) return null;

      GameSession.SelectedLevel = level;
      GameSession.SelectedEnvironment = level.environmentName;
      typeof(GridManager).GetMethod("InitializeGrid", Any).Invoke(grid, null);
      EnvironmentTheme.Apply(level.environmentName);
      Physics.SyncTransforms();
      typeof(PathManager).GetMethod("GeneratePath", Any).Invoke(pathManager, null);
      decor.BuildAt(pathManager.GetPathPoints());

      var created = PlaceRealTowers(level, TowerPaths);
      if (withCast) created.AddRange(PlaceRealEnemies(grid, level));
      return created;
    }

    public Vector3[] PathPoints() => pathManager.GetPathPoints();
  }

  // Renders each environment into Assets/Resources/EnvPreviews as a Sprite, so
  // the environment-selection cards show the actual in-game look instead of a
  // placeholder. EnvironmentsScreen falls back to these when no sprite has been
  // assigned in the inspector, so hand-made art can still override them later.
  public static void RenderEnvironmentCards()
  {
    const string dir = "Assets/Resources/EnvPreviews";
    Directory.CreateDirectory(dir);

    var ctx = BoardContext.Open();
    if (ctx == null) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }

    // Matches the art window on EnvironmentCard (about 1.7:1), at twice its
    // canvas size so it stays sharp on a 1.5x phone.
    var card = new Device { name = "card", width = 640, height = 376 };
    var written = new List<string>();

    for (int i = 1; i <= 7; i++)
    {
      // The biome's first level, with its real road, towers and cast, from the
      // closer three-quarter outro pose - a diorama of the place rather than
      // the whole board, which at thumbnail size is mostly clutter.
      List<GameObject> props = ctx.Build(i, "Level01", withCast: true);
      if (props == null) continue;
      string path = $"{dir}/Environment {i}.png";
      CaptureTo(ctx.rig, ctx.cam, card, path, pose: 2);
      written.Add(path);
      foreach (GameObject go in props) Object.DestroyImmediate(go);
    }

    AssetDatabase.Refresh();
    foreach (string path in written)
    {
      var importer = AssetImporter.GetAtPath(path) as TextureImporter;
      if (importer == null) continue;
      importer.textureType = TextureImporterType.Sprite;
      importer.spriteImportMode = SpriteImportMode.Single;
      importer.mipmapEnabled = false;
      importer.SaveAndReimport();
    }

    Debug.Log($"ENV CARDS OK: wrote {written.Count} previews to {dir}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  private static void CaptureTo(CameraRig rig, Camera cam, Device device, string path, int pose = 0)
  {
    float aspect = (float)device.width / device.height;
    rig.EditorPreview(pose, aspect);
    rig.enabled = false;

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
    texture.Apply();

    File.WriteAllBytes(path, texture.EncodeToPNG());

    cam.targetTexture = previousTarget;
    RenderTexture.active = previousActive;
    rt.Release();
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(texture);
    rig.enabled = true;
  }

  // Stands the whole enemy cast on the path, at gameplay scale, oriented along
  // it, lit by the scene's own lights and shot through the game camera.
  //
  // This exists because every judgement about the new enemy art until now came
  // from EnemyPreview's synthetic lineup: its own camera, its own single
  // directional light, flat ground, and a head-on angle. The real board is a
  // themed island seen from a steep three-quarter view with the environment's
  // own lighting, and "reads clearly" in one is not evidence for the other.
  private static List<GameObject> PlaceRealEnemies(GridManager grid, LevelConfig level = null)
  {
    var created = new List<GameObject>();
    Vector3[] pts = PreviewPathPoints(grid, level);
    if (pts == null || pts.Length < 3) return created;

    // Ordered so each variety type stands next to the base body it reuses.
    string[] names =
    {
      "BasicEnemy", "SplitterEnemy", "HealerEnemy", "FastEnemy",
      "SwarmEnemy", "ArmoredEnemy", "ShieldedEnemy", "BossEnemy",
    };

    for (int i = 0; i < names.Length; i++)
    {
      var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>(
        $"Assets/Settings/Enemies/{names[i]}.asset");
      if (cfg == null || cfg.prefab == null) continue;

      // Spread along the path, skipping the very ends so nothing sits on the
      // portal or the base.
      float t = (i + 0.5f) / names.Length;
      int index = Mathf.Clamp(Mathf.RoundToInt(t * (pts.Length - 1)), 1, pts.Length - 2);

      var go = (GameObject)PrefabUtility.InstantiatePrefab(cfg.prefab);
      PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                        InteractionMode.AutomatedAction);

      // Matches EnemyPool (UnitScale) and Enemy.ApplyAppearance
      // (scaleMultiplier), then poses the walk cycle with Enemy's own helper
      // so the silhouette is the one the game draws rather than the rest pose.
      Vector3 rest = go.transform.localScale *
                     (UnitScale.Enemy * Mathf.Max(0.01f, cfg.scaleMultiplier));
      float time = i * 0.37f;
      go.transform.localScale = Enemy.WaddleScale(rest, time, 0f);

      // Height matches EnemySpawner: half the BODY's height measured on the
      // prefab, which deliberately ignores UnitScale the same way the game
      // does - copying the quirk keeps the preview honest.
      MeshRenderer body = Enemy.FindBodyRenderer(cfg.prefab);
      float y = body != null ? body.bounds.size.y * 0.5f : 0.5f;

      Vector3 here = pts[index];
      Vector3 next = pts[Mathf.Min(index + 1, pts.Length - 1)];
      Vector3 dir = (next - here);
      dir.y = 0f;
      if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

      float yawOffset = 0f;
      var enemy = go.GetComponent<Enemy>();
      if (enemy != null)
      {
        // rotationOffset is private and serialized; the preview has to read it
        // the same way the game does or the cast faces the wrong way.
        var field = typeof(Enemy).GetField("rotationOffset",
          System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field != null) yawOffset = (float)field.GetValue(enemy);
      }

      go.transform.position = new Vector3(here.x, y, here.z);
      go.transform.rotation = Quaternion.LookRotation(dir.normalized) *
                              Quaternion.Euler(0f, yawOffset, 0f) *
                              Enemy.WaddleRoll(time, 0f);

      EnemyPreview.TintBody(go, cfg);
      EnemyPreview.TintTraits(go);
      foreach (EnemyTrait trait in go.GetComponentsInChildren<EnemyTrait>(true))
      {
        trait.Animate(time);
      }

      created.Add(go);
    }
    return created;
  }

  private static Vector3[] PreviewPathPoints(GridManager grid, LevelConfig level = null)
  {
    if (level == null) level = AssetDatabase.LoadAssetAtPath<LevelConfig>(
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
  private static List<GameObject> PlaceRealTowers(LevelConfig level = null, string[] towerPaths = null)
  {
    var created = new List<GameObject>();
    GridManager grid = Object.FindFirstObjectByType<GridManager>();
    if (level == null) level = AssetDatabase.LoadAssetAtPath<LevelConfig>(
      "Assets/Resources/Levels/Environment1/Level01.asset");
    if (grid == null || level == null || level.pathConfig == null) return created;

    if (towerPaths == null) towerPaths = new[]
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
        // Matches TowerFactory, which the game uses; instantiating the prefab
        // straight would otherwise preview them at the wrong size.
        tower.transform.localScale *= UnitScale.Tower;
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
