using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-shot project surgery: installs the camera rig, wraps every screen in a
// SafeArea, and makes canvas scaling consistent for a landscape phone game.
public static class DisplaySetup
{
  private static readonly string[] ScenePaths =
  {
    "Assets/Scenes/MainGame.unity",
    "Assets/Scenes/MainMenu.unity"
  };

  private static readonly string[] PrefabPaths =
  {
    "Assets/Prefabs/Screens/GameOverScreen.prefab",
    "Assets/Prefabs/Screens/PauseGameScreen.prefab",
    "Assets/Resources/Screens/SettingsScreen.prefab",
    "Assets/Prefabs/Screens/LoadingScreen.prefab",
    "Assets/Prefabs/Screens/EnvironmentsScreen.prefab",
    "Assets/Prefabs/Screens/LevelsScreen.prefab",
    "Assets/Resources/Screens/VictoryScreen.prefab"
  };

  // A wide, shallow board fills a landscape phone like the PvZ lawn and keeps
  // towers/enemies large and readable; too many rows makes units tiny and
  // forces a flat top-down angle.
  // 10x5 rather than 13x6: at 13 wide the camera had to frame 75 world units,
  // leaving a tower about 64px on a 1080p phone. Fewer, larger tiles make the
  // board readable and touchable.
  public const int BoardWidth = 10;
  public const int BoardHeight = 5;

  [MenuItem("Tools/Display/Apply Camera + Safe Area Setup")]
  public static void Apply()
  {
    int cameras = 0, safeAreas = 0, scalers = 0;

    foreach (string scenePath in ScenePaths)
    {
      Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

      foreach (GameObject root in scene.GetRootGameObjects())
      {
        foreach (GridManager grid in root.GetComponentsInChildren<GridManager>(true))
        {
          ConfigureBoard(grid);
        }

        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
        {
          if (!camera.CompareTag("MainCamera") || !scenePath.Contains("MainGame")) continue;

          CameraRig rig = camera.GetComponent<CameraRig>();
          if (rig == null)
          {
            rig = camera.gameObject.AddComponent<CameraRig>();
            cameras++;
          }
          ConfigureRig(rig);
          ConfigureCameraLook(camera);
        }

        foreach (Light light in root.GetComponentsInChildren<Light>(true))
        {
          ConfigureKeyLight(light);
        }

        if (scenePath.Contains("MainGame"))
        {
          AddBoardBase(root);
          EnsureLevelDecorator(root);
        }

        foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
        {
          if (ConfigureScaler(canvas)) scalers++;
          if (EnsureSafeArea(canvas)) safeAreas++;
          HoistBackgrounds(canvas);
        }
      }

      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
    }

    foreach (string prefabPath in PrefabPaths)
    {
      GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
      if (root == null) continue;

      bool changed = false;
      foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
      {
        if (ConfigureScaler(canvas)) { scalers++; changed = true; }
        if (EnsureSafeArea(canvas)) { safeAreas++; changed = true; }
        if (HoistBackgrounds(canvas)) changed = true;
      }

      if (prefabPath.Contains("PauseGameScreen") && AddPauseSettingsButton(root)) changed = true;

      if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
      PrefabUtility.UnloadPrefabContents(root);
    }

    AssetDatabase.SaveAssets();
    Debug.Log($"DISPLAY SETUP: camera rigs added={cameras}, safe areas added={safeAreas}, scalers updated={scalers}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  // Values already serialized in the scene win over the C# defaults, so the
  // tuned framing is written explicitly here.
  private static void ConfigureRig(CameraRig rig)
  {
    var so = new SerializedObject(rig);
    // Low, wide-lens, close: a cinematic 3/4 view with real perspective depth.
    // A touch lower reveals more of the cliff/sky around the floating island.
    so.FindProperty("fieldOfView").floatValue = 50f;
    so.FindProperty("playPitch").floatValue = 34f;
    so.FindProperty("playYaw").floatValue = 0f;
    // CameraRig.ResolvedPose() reads viewPresets whenever the array is
    // non-empty, so playPitch alone is ignored. Preset 0 is the play view.
    SetPresets(so, new[]
    {
      new Vector3(34f, 0f, 1f),    // play: fills the screen, tiles read clearly
      new Vector3(52f, 0f, 1f),    // near-isometric
      new Vector3(26f, 26f, 1f),   // low cinematic three-quarter
    });
    so.FindProperty("adaptPitchToAspect").boolValue = false;
    so.FindProperty("minPitch").floatValue = 24f;
    so.FindProperty("maxPitch").floatValue = 40f;
    so.FindProperty("edgePadding").floatValue = 0.02f;
    so.FindProperty("hudTopReserve").floatValue = 0.09f;
    so.FindProperty("hudBottomReserve").floatValue = 0.15f;
    so.FindProperty("towerHeadroom").floatValue = 5f;
    so.FindProperty("playIntroOnStart").boolValue = true;
    so.FindProperty("introDuration").floatValue = 3.8f;
    SetPose(so, "introPose", pitch: 11f, yaw: -62f, zoom: 0.48f, height: 1.5f);
    so.FindProperty("outroDuration").floatValue = 2.5f;
    SetPose(so, "outroPose", pitch: 26f, yaw: 18f, zoom: 0.78f, height: 1.5f);
    so.ApplyModifiedPropertiesWithoutUndo();
  }

  private static void SetPresets(SerializedObject so, Vector3[] presets)
  {
    SerializedProperty array = so.FindProperty("viewPresets");
    array.arraySize = presets.Length;
    for (int i = 0; i < presets.Length; i++)
    {
      array.GetArrayElementAtIndex(i).vector3Value = presets[i];
    }
  }

  private static void SetPose(SerializedObject so, string name, float pitch, float yaw, float zoom, float height)
  {
    SerializedProperty pose = so.FindProperty(name);
    pose.FindPropertyRelative("pitch").floatValue = pitch;
    pose.FindPropertyRelative("yaw").floatValue = yaw;
    pose.FindPropertyRelative("zoom").floatValue = zoom;
    pose.FindPropertyRelative("height").floatValue = height;
  }

  // A dark, cool backdrop makes the board read as a lit stage instead of
  // floating in grey.
  private static void ConfigureCameraLook(Camera camera)
  {
    camera.clearFlags = CameraClearFlags.SolidColor;
    camera.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 1f);
    camera.allowHDR = true;
    EditorUtility.SetDirty(camera);
  }

  // Shadows are the main depth cue for the angled view: without them towers
  // look pasted onto the ground.
  private static void ConfigureKeyLight(Light light)
  {
    if (light.type != LightType.Directional) return;

    // Keyed from the front-left so shadows fall toward the camera and read as depth
    light.transform.rotation = Quaternion.Euler(50f, 145f, 0f);
    light.intensity = 0.95f;
    light.color = new Color(1f, 0.97f, 0.9f);
    light.shadows = LightShadows.Soft;
    light.shadowStrength = 0.55f;
    EditorUtility.SetDirty(light);
  }

  // Resizes the play field and the ground plane under it.
  private static void ConfigureBoard(GridManager grid)
  {
    grid.gridSize = new Vector2Int(BoardWidth, BoardHeight);
    float worldWidth = BoardWidth * grid.cellSize;
    float worldDepth = BoardHeight * grid.cellSize;
    grid.originPosition = new Vector3(-worldWidth * 0.5f, 0f, -worldDepth * 0.5f);
    EditorUtility.SetDirty(grid);

    // Visual ground/slab extend past the grid by a margin, giving a border ring
    // of grass for decorative props (the play grid stays centred and unchanged).
    float groundW = worldWidth + 2f * BoardDecor.Margin;
    float groundD = worldDepth + 2f * BoardDecor.Margin;

    // The ground is a built-in 10x10 plane, so scale is world size / 10
    GroundManager ground = Object.FindFirstObjectByType<GroundManager>();
    if (ground != null)
    {
      ground.transform.position = new Vector3(0f, ground.transform.position.y, 0f);
      ground.transform.localScale = new Vector3(groundW / 10f, 1f, groundD / 10f);
      EditorUtility.SetDirty(ground);
    }
  }

  // Landscape game: match the screen height so UI keeps a constant size
  // regardless of how wide (or notched) the device is.
  private static bool ConfigureScaler(Canvas canvas)
  {
    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
    if (scaler == null) return false;
    if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return false;
    if (Mathf.Approximately(scaler.matchWidthOrHeight, 1f)) return false;

    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
    scaler.matchWidthOrHeight = 1f;
    EditorUtility.SetDirty(scaler);
    return true;
  }

  // Wrap a canvas's children in a full-stretch SafeArea object so nothing
  // lands under a notch or a home indicator.
  private static bool EnsureSafeArea(Canvas canvas)
  {
    if (canvas.GetComponentInChildren<SafeArea>(true) != null) return false;
    if (canvas.transform.childCount == 0) return false;

    var safeAreaGo = new GameObject("SafeArea", typeof(RectTransform));
    var rect = safeAreaGo.GetComponent<RectTransform>();
    rect.SetParent(canvas.transform, false);
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.offsetMin = Vector2.zero;
    rect.offsetMax = Vector2.zero;

    var children = new List<Transform>();
    foreach (Transform child in canvas.transform)
    {
      // Backgrounds must fill the whole screen, so they stay outside the safe area
      if (child != rect && !IsBackground(child)) children.Add(child);
    }
    foreach (Transform child in children)
    {
      child.SetParent(rect, false);
    }

    safeAreaGo.AddComponent<SafeArea>();
    rect.SetAsFirstSibling();
    EditorUtility.SetDirty(canvas);
    return true;
  }

  private static bool IsBackground(Transform t)
  {
    return t.name.ToLowerInvariant().Contains("background");
  }

  // Moves the top-level screen background out of the SafeArea (so it fills the
  // notch/rounded-corner regions) and makes it cover the screen at any aspect.
  // Only the background that is a direct child of the canvas or the SafeArea is
  // affected — backgrounds nested inside widgets (e.g. a progress bar's fill)
  // are left alone.
  private static bool HoistBackgrounds(Canvas canvas)
  {
    var candidates = new List<Transform>();
    foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
    {
      if (!IsBackground(t) || !(t is RectTransform)) continue;
      if (t.GetComponent<Image>() == null && t.GetComponent<RawImage>() == null) continue;

      // Skip backgrounds that belong to a widget (slider fill, button graphic):
      // a Selectable anywhere up the parent chain means it is not a screen bg.
      if (t.GetComponentInParent<Selectable>() != null) continue;

      candidates.Add(t);
    }

    bool changed = false;
    foreach (Transform background in candidates)
    {
      if (background.parent != canvas.transform)
      {
        background.SetParent(canvas.transform, false);
      }
      background.SetAsFirstSibling();

      var rect = (RectTransform)background;
      rect.anchorMin = Vector2.zero;
      rect.anchorMax = Vector2.one;
      rect.offsetMin = Vector2.zero;
      rect.offsetMax = Vector2.zero;

      if (background.GetComponent<BackgroundFill>() == null)
      {
        background.gameObject.AddComponent<BackgroundFill>();
      }
      EditorUtility.SetDirty(background.gameObject);
      changed = true;
    }
    return changed;
  }

  // Gives the board visible thickness so the angled camera sees a solid soil
  // slab at its edges instead of the empty background behind the thin ground
  // plane.
  private static void AddBoardBase(GameObject root)
  {
    GridManager grid = root.GetComponentInChildren<GridManager>(true);
    if (grid == null) return;

    Transform existing = FindDeep(root.transform, "BoardBase");
    GameObject baseGo = existing != null ? existing.gameObject
      : GameObject.CreatePrimitive(PrimitiveType.Cube);
    baseGo.name = "BoardBase";
    if (existing == null) baseGo.transform.SetParent(root.transform, false);

    float worldWidth = grid.gridSize.x * grid.cellSize + 2f * BoardDecor.Margin;
    float worldDepth = grid.gridSize.y * grid.cellSize + 2f * BoardDecor.Margin;
    const float thickness = BoardDecor.SoilThickness;

    // Top sits just below the grass plane (y=0) with a matching footprint, so
    // the grass shows on top and only the soil sides are visible at the edges.
    baseGo.transform.position = new Vector3(0f, -thickness * 0.5f + BoardDecor.SoilTop, 0f);
    baseGo.transform.localScale = new Vector3(worldWidth, thickness, worldDepth);

    var renderer = baseGo.GetComponent<MeshRenderer>();
    renderer.sharedMaterial = GetOrCreateSoilMaterial();
    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

    // The board sits on top; its collider must not interfere with tower raycasts
    Collider collider = baseGo.GetComponent<Collider>();
    if (collider != null) Object.DestroyImmediate(collider);

    EditorUtility.SetDirty(baseGo);
  }

  private static void EnsureLevelDecorator(GameObject root)
  {
    if (Object.FindFirstObjectByType<LevelDecorator>() != null) return;
    var go = new GameObject("LevelDecorator");
    go.AddComponent<LevelDecorator>();
    EditorUtility.SetDirty(go);
  }

  private static Material GetOrCreateSoilMaterial()
  {
    const string path = "Assets/Materials/Environments/BoardBase.mat";
    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (material != null) return material;

    material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
    {
      color = new Color(0.28f, 0.19f, 0.12f)
    };
    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
    AssetDatabase.CreateAsset(material, path);
    return material;
  }

  private static Transform FindDeep(Transform parent, string name)
  {
    if (parent.name == name) return parent;
    foreach (Transform child in parent)
    {
      Transform found = FindDeep(child, name);
      if (found != null) return found;
    }
    return null;
  }

  // Adds a Settings button to the pause menu by cloning the existing
  // return-to-menu button, then wires it to the PauseGameScreen field.
  private static bool AddPauseSettingsButton(GameObject root)
  {
    PauseGameScreen pause = root.GetComponent<PauseGameScreen>();
    if (pause == null) return false;

    var so = new SerializedObject(pause);
    SerializedProperty settingsProp = so.FindProperty("settingsButton");
    if (settingsProp.objectReferenceValue != null) return false; // already added

    SerializedProperty returnProp = so.FindProperty("returnToMainMenuButton");
    var returnButton = returnProp.objectReferenceValue as Button;
    if (returnButton == null) return false;

    GameObject clone = Object.Instantiate(returnButton.gameObject, returnButton.transform.parent);
    clone.name = "Settings";
    // Place it between Resume and End Game
    clone.transform.SetSiblingIndex(returnButton.transform.GetSiblingIndex());

    foreach (TMPro.TextMeshProUGUI label in clone.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
    {
      label.text = "SETTINGS";
    }

    var button = clone.GetComponent<Button>();
    settingsProp.objectReferenceValue = button;
    so.ApplyModifiedPropertiesWithoutUndo();
    return true;
  }
}
