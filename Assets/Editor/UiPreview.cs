using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Renders the in-game HUD to a PNG so the interface can actually be looked at.
//
// CameraPreview cannot do this: the HUD canvas is ScreenSpaceOverlay, which
// draws straight to the backbuffer and never lands in a RenderTexture. This
// builds the same elements on a ScreenSpaceCamera canvas instead, and runs them
// through the real UiSkin / HudTheme / runtime-button code rather than a mock,
// so what is rendered is what the game builds.
public static class UiPreview
{
  private const string OutputDir = "Builds/UiPreview";

  // Stand-in for the board, to check UI contrast against the real backdrop
  private static readonly Color Meadow = new Color(0.36f, 0.50f, 0.22f);

  public static void Render()
  {
    Directory.CreateDirectory(OutputDir);

    // Opens MainMenu.unity itself (Single mode), so it needs to run before the
    // NewScene call below replaces the active scene for every other shot.
    ShootMainMenu();

    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    var camGo = new GameObject("PreviewCamera");
    var cam = camGo.AddComponent<Camera>();
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = Meadow;
    cam.orthographic = true;
    camGo.transform.position = new Vector3(0f, 0f, -100f);

    Shoot(cam, 2400, 1080, "hud-20x9");
    Shoot(cam, 1920, 1080, "hud-16x9");

    // The real modal prefabs, themed by the real ScreenTheme
    ShootScreen(cam, "Assets/Prefabs/Screens/PauseGameScreen.prefab", "ResumeGame", "screen-pause");
    ShootScreen(cam, "Assets/Resources/Screens/VictoryScreen.prefab", "NextLevelButton", "screen-victory");
    ShootScreen(cam, "Assets/Prefabs/Screens/GameOverScreen.prefab", "RestartNewGame", "screen-gameover");

    // Settings is themed by its own entry point rather than ScreenTheme.Apply,
    // so it needs the matching call here or the preview would not show the
    // corner the close button actually lands in.
    ShootScreen(cam, "Assets/Resources/Screens/SettingsScreen.prefab", null, "screen-settings",
      settings: true);

    ShootWallet(cam, "screen-wallet");

    // Screens whose look depends on Start() running (cards populated, sliders
    // skinned). Start is invoked by reflection rather than widening the
    // production API just for this tool.
    GameSession.SelectedEnvironment = "Environment 1";
    ShootLive(cam, "Assets/Prefabs/Screens/EnvironmentsScreen.prefab", "screen-environments");
    ShootLive(cam, "Assets/Prefabs/Screens/LevelsScreen.prefab", "screen-levels");
    ShootLive(cam, "Assets/Prefabs/Screens/LoadingScreen.prefab", "screen-loading");

    Debug.Log("UI PREVIEW OK");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  // The main menu is a SCENE, not a prefab, so it cannot go through
  // ShootScreen/ShootLive - opening it replaces whatever scene is currently
  // active, including the synthetic one the rest of this file shares one
  // camera on. Runs first and manages its own scene/camera for exactly that
  // reason, then gets out of the way so Render() can start the empty scene
  // every other shot expects.
  private static void ShootMainMenu()
  {
    const int width = 1920, height = 1080;
    const string name = "screen-mainmenu";

    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);

    Canvas canvas = null;
    foreach (GameObject root in scene.GetRootGameObjects())
    {
      canvas = root.GetComponentInChildren<Canvas>(true);
      if (canvas != null) break;
    }
    if (canvas == null)
    {
      Debug.LogError("UI PREVIEW: MainMenu.unity has no Canvas");
      return;
    }

    var camGo = new GameObject("MainMenuPreviewCamera");
    var cam = camGo.AddComponent<Camera>();
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = Color.black;
    cam.orthographic = true;
    camGo.transform.position = new Vector3(0f, 0f, -100f);

    canvas.renderMode = RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = cam;
    canvas.planeDistance = 10f;

    // Drives MainMenu.Start() (wires the buttons, builds the coin chip) the
    // same way ShootLive does for the runtime-built screens, so what is
    // rendered is what the game actually builds rather than the raw prefab.
    foreach (MonoBehaviour behaviour in canvas.GetComponentsInChildren<MonoBehaviour>(true))
    {
      var start = behaviour.GetType().GetMethod("Start",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      if (start == null || start.GetParameters().Length != 0) continue;
      try { start.Invoke(behaviour, null); }
      catch (System.Exception e) { Debug.LogWarning($"UI PREVIEW: {behaviour.GetType().Name}.Start -> {e.InnerException?.Message ?? e.Message}"); }
    }

    // Cold-launch-only and self-timed via Update, which never runs in batch -
    // it would otherwise render as an opaque splash over the whole menu.
    foreach (BootSplash splash in Object.FindObjectsByType<BootSplash>(FindObjectsSortMode.None))
    {
      Object.DestroyImmediate(splash.gameObject);
    }

    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);

    var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
    cam.targetTexture = rt;
    cam.Render();
    Canvas.ForceUpdateCanvases();
    cam.Render();

    RenderTexture previous = RenderTexture.active;
    RenderTexture.active = rt;
    var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
    shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
    shot.Apply();
    RenderTexture.active = previous;

    File.WriteAllBytes($"{OutputDir}/{name}.png", shot.EncodeToPNG());

    cam.targetTexture = null;
    Object.DestroyImmediate(rt);
  }

  // Instantiates a prefab and drives its Start(), so screens that build their
  // content at runtime can be captured as the player actually sees them.
  private static void ShootLive(Camera cam, string prefabPath, string name)
  {
    const int width = 1920, height = 1080;
    ClearCanvases();

    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (prefab == null) { Debug.LogError("UI PREVIEW: missing " + prefabPath); return; }

    var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
    cam.targetTexture = rt;

    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    go.SetActive(true);

    // Unity silently refuses to reparent a child of a live Prefab Instance to
    // an object outside the prefab's own structure while in EDIT mode (no
    // exception, no warning - Transform.SetParent just no-ops). That is
    // exactly what ScreenTheme.EnsureSafeArea does (wraps existing children in
    // a freshly-created SafeArea), so under the instance this created it
    // silently failed and the preview understated the very bug it exists to
    // catch — the screen looked fine here while the notch-safety fix quietly
    // never took effect. This restriction is editor/edit-mode only: it does
    // not exist in Play Mode or a build, where there is no "prefab instance"
    // concept at runtime at all, so the real game was never affected — only
    // this tool's ability to verify it. Unpacking removes the connection
    // entirely, which is fine here since this instance is destroyed right
    // after the shot and never saved.
    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

    // LevelsScreen and EnvironmentsScreen carry no Canvas of their own — the
    // game instantiates them under the main menu's canvas — so host them in one
    // here or they render nothing at all.
    var canvas = go.GetComponentInChildren<Canvas>(true);
    GameObject host = null;
    if (canvas == null)
    {
      host = HostCanvas(cam);
      go.transform.SetParent(host.transform, false);
      UiSkin.Stretch((RectTransform)go.transform);
      canvas = host.GetComponent<Canvas>();
    }
    else
    {
      canvas.renderMode = RenderMode.ScreenSpaceCamera;
      canvas.worldCamera = cam;
      canvas.planeDistance = 10f;
    }

    foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
    {
      var start = behaviour.GetType().GetMethod("Start",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      if (start == null || start.GetParameters().Length != 0) continue;
      try { start.Invoke(behaviour, null); }
      catch (System.Exception e) { Debug.LogWarning($"UI PREVIEW: {behaviour.GetType().Name}.Start -> {e.InnerException?.Message ?? e.Message}"); }
    }

    foreach (BackgroundFill fill in go.GetComponentsInChildren<BackgroundFill>(true))
    {
      fill.enabled = false;
      fill.enabled = true;
    }

    // The fade-in starts fully black and clears itself in Update, which never
    // runs in batch mode — it would render as a black frame.
    foreach (ScreenFade fade in Object.FindObjectsByType<ScreenFade>(FindObjectsSortMode.None))
    {
      Object.DestroyImmediate(fade.gameObject);
    }

    Canvas.ForceUpdateCanvases();
    if (canvas != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
    cam.Render();
    Canvas.ForceUpdateCanvases();
    cam.Render();

    RenderTexture previous = RenderTexture.active;
    RenderTexture.active = rt;
    var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
    shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
    shot.Apply();
    RenderTexture.active = previous;

    File.WriteAllBytes($"{OutputDir}/{name}.png", shot.EncodeToPNG());

    cam.targetTexture = null;
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(host != null ? host : go);
  }

  private static void Shoot(Camera cam, int width, int height, string name)
  {
    ClearCanvases();
    // The render target must exist before the canvas is built, so the canvas
    // sizes itself to the right resolution from the start
    var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
    cam.targetTexture = rt;

    GameObject canvasGo = BuildHud(cam, width, height);

    // Rendered twice: the first pass settles the canvas at the new resolution,
    // and only the second is captured. A single pass left stale geometry from
    // the pre-resize layout smeared into the image.
    cam.Render();
    Canvas.ForceUpdateCanvases();
    cam.Render();

    RenderTexture previous = RenderTexture.active;
    RenderTexture.active = rt;
    var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
    shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
    shot.Apply();
    RenderTexture.active = previous;

    File.WriteAllBytes($"{OutputDir}/{name}.png", shot.EncodeToPNG());

    cam.targetTexture = null;
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(canvasGo);
  }

  // Instantiates a real screen prefab and themes it. Initialize() is skipped —
  // it wires up clicks and needs a live GameManager — so ScreenTheme is invoked
  // directly, which is the same call Initialize makes.
  // Anything left over from a previous shot would render into the next one.
  private static void ClearCanvases()
  {
    foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
    {
      if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
    }
  }

  // The wallet has no prefab — it is built entirely in code — so it is
  // constructed here the same way the game constructs it.
  private static void ShootWallet(Camera cam, string name)
  {
    const int width = 1920, height = 1080;
    ClearCanvases();

    // Must happen BEFORE the wallet builds itself: WalletScreen.Panel() sizes
    // the card off its parent's actual rect, and a ScreenSpaceCamera canvas
    // with no render target falls back to the batch-mode default game view
    // size (640x480) rather than this texture's 1920x1080 - the card would
    // size itself for a canvas four times smaller than the one it actually
    // renders into.
    var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
    cam.targetTexture = rt;

    GameObject host = HostCanvas(cam);
    WalletScreen.Open(host.transform);

    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)host.transform);

    cam.Render();
    Canvas.ForceUpdateCanvases();
    cam.Render();

    RenderTexture previous = RenderTexture.active;
    RenderTexture.active = rt;
    var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
    shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
    shot.Apply();
    RenderTexture.active = previous;

    File.WriteAllBytes($"{OutputDir}/{name}.png", shot.EncodeToPNG());

    cam.targetTexture = null;
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(host);
  }

  private static void ShootScreen(Camera cam, string prefabPath, string primaryName, string name,
    bool settings = false)
  {
    const int width = 1920, height = 1080;
    ClearCanvases();

    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (prefab == null) { Debug.LogError("UI PREVIEW: missing " + prefabPath); return; }

    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    go.SetActive(true);
    // See ShootLive: ScreenTheme.Dim() reparents this prefab's Background out
    // to the canvas root, which the same edit-mode instance restriction blocks.
    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

    // The prefab canvas is ScreenSpaceOverlay, which never lands in a
    // RenderTexture; retarget it at the preview camera.
    var canvas = go.GetComponentInChildren<Canvas>(true);
    if (canvas != null)
    {
      canvas.renderMode = RenderMode.ScreenSpaceCamera;
      canvas.worldCamera = cam;
      canvas.planeDistance = 10f;
      ((RectTransform)canvas.transform).sizeDelta = new Vector2(width, height);
    }

    Button primary = null;
    foreach (Button b in go.GetComponentsInChildren<Button>(true))
    {
      if (b.name == primaryName) primary = b;
    }

    // Game over builds its continue offer in Initialize(), so calling the real
    // entry point is the only way to see the screen the player actually gets.
    // Initialize also runs ScreenTheme itself, so nothing else is needed here.
    var gameOver = go.GetComponent<GameOverScreen>();
    if (gameOver != null)
    {
      gameOver.Initialize();
    }
    else if (settings)
    {
      Button close = null;
      foreach (Button b in go.GetComponentsInChildren<Button>(true))
      {
        if (b.name == "Close") close = b;
      }
      ScreenTheme.ApplySettingsScreen(go.transform, close);
    }
    else
    {
      ScreenTheme.Apply(go.transform, primary,
        name.Contains("gameover") ? UiSkin.Danger : (Color?)null);
    }

    Canvas.ForceUpdateCanvases();
    if (canvas != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);

    // BackgroundFill sizes itself in OnEnable, which ran while the prefab was
    // still on the old canvas. In play mode its Update() corrects that on the
    // first frame; batch mode has no update loop, so re-trigger OnEnable here
    // or the scrim renders at the wrong size.
    foreach (BackgroundFill fill in go.GetComponentsInChildren<BackgroundFill>(true))
    {
      fill.enabled = false;
      fill.enabled = true;
    }
    Canvas.ForceUpdateCanvases();

    var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
    cam.targetTexture = rt;
    cam.Render();

    RenderTexture previous = RenderTexture.active;
    RenderTexture.active = rt;
    var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
    shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
    shot.Apply();
    RenderTexture.active = previous;

    File.WriteAllBytes($"{OutputDir}/{name}.png", shot.EncodeToPNG());

    cam.targetTexture = null;
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(go);
  }

  private static GameObject HostCanvas(Camera cam)
  {
    var go = new GameObject("PreviewCanvas", typeof(RectTransform));
    var canvas = go.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = cam;
    canvas.planeDistance = 10f;

    var scaler = go.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920f, 1080f);
    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
    scaler.matchWidthOrHeight = 1f;
    go.AddComponent<GraphicRaycaster>();
    return go;
  }

  // Mirrors the MainGame HUD hierarchy: SafeArea holding StatsPanel (health +
  // gold), WaveText, TimerText, PauseGameButton, TowersPanel, StartWaveButton.
  private static GameObject BuildHud(Camera cam, int width, int height)
  {
    var canvasGo = new GameObject("HUD", typeof(RectTransform));
    var canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = cam;
    canvas.planeDistance = 10f;

    var scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920f, 1080f);
    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
    scaler.matchWidthOrHeight = 1f; // match height, as the game does in landscape
    canvasGo.AddComponent<GraphicRaycaster>();

    // The canvas rect is driven by the camera's target texture; setting it by
    // hand makes layout and rendering disagree about the width.
    var canvasRect = (RectTransform)canvasGo.transform;

    var safeArea = Child(canvasRect, "SafeArea");
    UiSkin.Stretch(safeArea);

    // Anchors below are copied from MainGame.unity so the preview reflects the
    // real HUD layout rather than invented positions.

    // --- stats (top-left) ---
    RectTransform statsPanel = Place(safeArea, "StatsPanel",
      new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
      new Vector2(0f, 0f), new Vector2(270f, 136f));
    statsPanel.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

    TMP_Text health = Text(statsPanel, "HealthText", "100");
    TMP_Text gold = Text(statsPanel, "GoldText", "515");

    // --- wave + timer (top, right of centre) ---
    RectTransform waveRect = Place(safeArea, "WaveText",
      new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
      new Vector2(-450f, 0f), new Vector2(220f, 50f));
    TMP_Text wave = waveRect.gameObject.AddComponent<TextMeshProUGUI>();
    wave.text = "Wave 3/12";

    RectTransform timerRect = Place(safeArea, "TimerText",
      new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
      new Vector2(-450f, -70f), new Vector2(220f, 50f));
    TMP_Text timer = timerRect.gameObject.AddComponent<TextMeshProUGUI>();
    timer.text = "Next Wave in: 8";

    // --- pause (top-right) ---
    Button pause = MakeButton(safeArea, "PauseGameButton", "PAUSE",
      new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
      new Vector2(10f, 0f), new Vector2(330f, 75f));

    // --- towers panel (right, vertically stretched) ---
    RectTransform towers = Place(safeArea, "TowersPanel",
      new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f),
      new Vector2(10f, -85f), new Vector2(330f, -170f));
    BuildTowerCards(towers);

    // --- start wave (bottom-right) ---
    Button startWave = MakeButton(safeArea, "StartWaveButton", "START WAVE",
      new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
      new Vector2(10f, 0f), new Vector2(330f, 75f));

    HudTheme.Apply(statsPanel, gold, health, wave, timer, startWave, pause, towers);

    // The real runtime buttons, built by their own code
    GameSpeedButton.Create(safeArea, statsPanel, 0);
    CameraViewButton.Create(safeArea, statsPanel, 1);

    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
    return canvasGo;
  }

  // Approximates TowerSelectionButton's styled card. The real component needs a
  // live GameManager for affordability, so the two states are faked here; the
  // styling calls are the same ones it makes.
  private static void BuildTowerCards(RectTransform panel)
  {
    var grid = panel.gameObject.AddComponent<GridLayoutGroup>();
    // Matches the real TowersPanel's GridLayoutGroup (MainGame.unity) exactly,
    // not an approximation - HudTheme.StyleTowersPanel branches on
    // FixedColumnCount specifically, and a preview using Flexible instead
    // would exercise a different code path than the real HUD does.
    grid.cellSize = new Vector2(160f, 160f);
    grid.spacing = new Vector2(0f, 0f);
    grid.padding = new RectOffset(10, 10, 10, 10);
    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
    grid.constraintCount = 2;

    (string name, int cost, bool affordable)[] cards =
    {
      ("Archer", 100, true), ("Ice", 150, true),
      ("Inferno", 175, true), ("Sniper", 175, true),
      ("Poison", 200, true), ("Shock", 225, false),
      ("Aura", 250, false), ("Defense", 275, false),
    };

    foreach (var card in cards)
    {
      RectTransform rect = Child(panel, card.name + "Card");
      var bg = rect.gameObject.AddComponent<Image>();
      UiSkin.Panel(bg, card.affordable ? UiSkin.PanelRaised
        : new Color(UiSkin.PanelDark.r, UiSkin.PanelDark.g, UiSkin.PanelDark.b, 0.75f),
        UiSkin.RadiusButton);

      RectTransform nameRect = Child(rect, "TowerNameText");
      Anchor(nameRect, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(120f, 24f));
      TMP_Text label = nameRect.gameObject.AddComponent<TextMeshProUGUI>();
      label.text = card.name;
      label.alignment = TextAlignmentOptions.Center;
      UiSkin.Label(label, UiSkin.Role.Caption);

      // Stand-in for the tower art
      RectTransform iconRect = Child(rect, "TowerIcon");
      Anchor(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(64f, 64f));
      var icon = iconRect.gameObject.AddComponent<Image>();
      icon.sprite = UiSprites.Circle();
      icon.color = new Color(0.62f, 0.45f, 0.85f, card.affordable ? 1f : 0.45f);

      RectTransform costRect = Child(rect, "TowerCostText");
      Anchor(costRect, new Vector2(0.5f, 0f), new Vector2(16f, 16f), new Vector2(58f, 28f));
      TMP_Text cost = costRect.gameObject.AddComponent<TextMeshProUGUI>();
      cost.text = card.cost.ToString();
      cost.alignment = TextAlignmentOptions.MidlineLeft;
      UiSkin.Label(cost, UiSkin.Role.Value, card.affordable ? UiSkin.Gold : UiSkin.Danger);

      Image coin = UiSkin.Icon(rect, UiSprites.Coin(),
        card.affordable ? UiSkin.Gold : UiSkin.Danger, 22f);
      Anchor((RectTransform)coin.transform, new Vector2(0.5f, 0f), new Vector2(-26f, 19f), new Vector2(22f, 22f));
    }
  }

  // ---------------------------------------------------------------- helpers

  private static RectTransform Child(Transform parent, string name)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    return (RectTransform)go.transform;
  }

  private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
  {
    rect.anchorMin = anchor;
    rect.anchorMax = anchor;
    rect.pivot = anchor;
    rect.anchoredPosition = position;
    rect.sizeDelta = size;
  }

  private static RectTransform Place(Transform parent, string name,
    Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
  {
    RectTransform rect = Child(parent, name);
    rect.anchorMin = min;
    rect.anchorMax = max;
    rect.pivot = pivot;
    rect.anchoredPosition = position;
    rect.sizeDelta = size;
    return rect;
  }

  private static TMP_Text Text(Transform parent, string name, string value)
  {
    RectTransform rect = Child(parent, name);
    rect.sizeDelta = new Vector2(110f, 40f);
    var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
    label.text = value;
    return label;
  }

  private static Button MakeButton(Transform parent, string name, string text,
    Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
  {
    RectTransform rect = Place(parent, name, min, max, pivot, position, size);
    rect.gameObject.AddComponent<Image>();
    var button = rect.gameObject.AddComponent<Button>();

    RectTransform labelRect = Child(rect, "Label");
    UiSkin.Stretch(labelRect);
    var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
    label.text = text;
    label.alignment = TextAlignmentOptions.Center;
    return button;
  }
}
