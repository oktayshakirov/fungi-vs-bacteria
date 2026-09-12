using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Renders the whole enemy cast in a row, at gameplay scale, so trait placement
// can be judged without a device.
//
// Run: -executeMethod EnemyPreview.Render   (needs graphics, NOT -nographics)
//
// This exists for the same reason UiPreview does: the thing being changed is
// how something LOOKS, and the alternative is guessing. CameraPreview renders
// the board and its towers but never the enemy cast, and the four variety types
// are precisely the ones whose appearance is now code-and-tool-driven rather
// than authored - EnemyArtSetup measures each base body's bounds and places a
// trait from a fraction, so the only way to know a fraction is right is to look.
//
// Writes Builds/EnemyPreview/lineup-<env>.png plus one close-up per type.
public static class EnemyPreview
{
  private const string OutputDir = "Builds/EnemyPreview";
  private const string ConfigDir = "Assets/Settings/Enemies";

  // Ordered so the four variety types sit next to the base body they reuse:
  // the whole question is whether each is distinguishable from its neighbour.
  private static readonly string[] Order =
  {
    "BasicEnemy", "SplitterEnemy", "HealerEnemy",
    "FastEnemy", "SwarmEnemy",
    "ArmoredEnemy", "ShieldedEnemy",
    "BossEnemy",
  };


  // A bare `new GameObject` + Camera has no UniversalAdditionalCameraData, so
  // URP runs it with post-processing OFF and logs a warning rather than an
  // error. That silently disables Bloom, which is the whole reason an emissive
  // part looks like flat bright paint in a preview and glows in the game. Any
  // preview camera that needs to judge emission must go through here.
  private static Camera MakePreviewCamera(string name)
  {
    var go = new GameObject(name);
    Camera cam = go.AddComponent<Camera>();
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0.14f, 0.16f, 0.19f);
    cam.fieldOfView = 32f;
    cam.allowHDR = true;
    UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
    data.renderPostProcessing = true;
    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    return cam;
  }

  public static void Render()
  {
    EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
    Directory.CreateDirectory(OutputDir);

    Camera cam = MakePreviewCamera("EnemyPreviewCam");
    GameObject camGo = cam.gameObject;

    // A dedicated light: MainGame's own rig is themed per environment and the
    // point here is to read silhouette, not lighting.
    var lightGo = new GameObject("EnemyPreviewLight");
    Light key = lightGo.AddComponent<Light>();
    key.type = LightType.Directional;
    key.intensity = 1.25f;
    key.transform.rotation = Quaternion.Euler(42f, 150f, 0f);

    foreach (string env in new[] { "Environment 1", "Environment 3", "Environment 6" })
    {
      EnvironmentTheme.Apply(env);
      List<Posed> cast = BuildLineup(out float span);

      // Frame the row from the gameplay three-quarter angle, far enough back
      // to hold all eight. The distance is derived from the row's own span so
      // adding a type cannot silently push the last one out of frame - the
      // first version used a fixed gap and cropped the boss in half.
      var focus = new Vector3(span * 0.5f, 1.4f, 0f);
      cam.transform.position = focus + new Vector3(0f, span * 0.30f, -span * 0.78f);
      cam.transform.LookAt(focus);
      Capture(cam, 1800, 700, $"lineup-{env.Replace(" ", "").ToLower()}");

      if (env == "Environment 1")
      {
        // Close-ups: a trait that reads in a wide shot can still be wrong up
        // close, and vice versa. Distance scales with the subject, because the
        // cast spans a ~4x size range and one fixed offset put the camera
        // inside the bigger bodies.
        foreach (Posed p in cast)
        {
          // Aim at the measured bounds centre, not at the transform plus a
          // guessed lift: FastEnemy is 3x longer than it is tall, so a lift
          // scaled by the LARGEST half-extent pointed the camera at bare
          // ground above the Swarm enemy entirely.
          float d = Mathf.Max(1.2f, p.framing) * 3.1f;
          Vector3 at = p.centre;
          cam.transform.position = at + new Vector3(d * 0.42f, d * 0.45f, -d * 0.80f);
          cam.transform.LookAt(at);
          Capture(cam, 560, 560, $"close-{p.name}");

          // The carapace hides while the shield pool is empty, which is the
          // only in-world cue that the regen delay is running. Worth rendering
          // rather than assuming: with the shell off, a Shielded enemy has to
          // still be distinguishable from a plain Armored one, and that is
          // exactly what the recoloured body is for.
          EnemyTrait[] hideable = p.go.GetComponentsInChildren<EnemyTrait>(true);
          bool any = false;
          foreach (EnemyTrait t in hideable)
          {
            if (!t.hideWhileShieldDown) continue;
            t.SetShieldUp(false);
            any = true;
          }
          if (any)
          {
            Capture(cam, 560, 560, $"close-{p.name}-shielddown");
            foreach (EnemyTrait t in hideable) t.SetShieldUp(true);
          }
        }
      }

      foreach (Posed p in cast) Object.DestroyImmediate(p.go);
    }

    Object.DestroyImmediate(camGo);
    Object.DestroyImmediate(lightGo);
    Debug.Log($"EnemyPreview wrote {OutputDir}");
  }

  private struct Posed
  {
    public GameObject go;
    public string name;
    public float radius;   // horizontal half-extent, used for spacing
    public float framing;  // largest half-extent, used for camera distance
    public Vector3 centre; // world bounds centre, used as the look-at target
  }

  // Candidate bubble colours for the Shielded enemy, rendered side by side so
  // the choice is made by looking rather than by imagining. The bubble's
  // colour is one number in EnemyArtSetup; this only previews it.
  private static readonly (string name, Color bubble, Color body)[] ShieldOptions =
  {
    ("cyan",   new Color(0.58f, 0.84f, 1.00f, 0.28f), new Color(0.26f, 0.36f, 0.56f)),
    ("mint",   new Color(0.62f, 1.00f, 0.86f, 0.28f), new Color(0.20f, 0.40f, 0.40f)),
    ("violet", new Color(0.78f, 0.68f, 1.00f, 0.28f), new Color(0.30f, 0.26f, 0.50f)),
    ("gold",   new Color(1.00f, 0.90f, 0.55f, 0.28f), new Color(0.40f, 0.32f, 0.22f)),
  };

  // Renders the Shielded enemy once per candidate colour, in one wide shot.
  public static void RenderShieldColors()
  {
    EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
    Directory.CreateDirectory(OutputDir);

    Camera cam = MakePreviewCamera("ShieldPreviewCam");
    GameObject camGo = cam.gameObject;

    var lightGo = new GameObject("ShieldPreviewLight");
    Light key = lightGo.AddComponent<Light>();
    key.type = LightType.Directional;
    key.intensity = 1.25f;
    key.transform.rotation = Quaternion.Euler(42f, 150f, 0f);

    EnvironmentTheme.Apply("Environment 1");
    var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>($"{ConfigDir}/ShieldedEnemy.asset");
    var made = new List<GameObject>();
    float x = 0f;

    foreach ((string name, Color bubble, Color body) option in ShieldOptions)
    {
      var go = (GameObject)PrefabUtility.InstantiatePrefab(cfg.prefab);
      PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                        InteractionMode.AutomatedAction);
      go.transform.localScale *= UnitScale.Enemy * Mathf.Max(0.01f, cfg.scaleMultiplier);
      go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

      MeshRenderer bodyRenderer = Enemy.FindBodyRenderer(go);
      if (bodyRenderer != null)
      {
        var block = new MaterialPropertyBlock();
        bodyRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", option.body);
        block.SetColor("_Color", option.body);
        bodyRenderer.SetPropertyBlock(block);
      }

      foreach (EnemyTrait trait in go.GetComponentsInChildren<EnemyTrait>(true))
      {
        typeof(EnemyTrait)
          .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
          ?.Invoke(trait, null);
        trait.accentColor = option.bubble;
        trait.ApplyTint(Color.white);
      }

      Bounds b = WorldBounds(go);
      x += Mathf.Max(b.extents.x, b.extents.z) * 2.1f + 1.2f;
      go.transform.position = new Vector3(x, -b.min.y, 0f);
      made.Add(go);
    }

    var focus = new Vector3(x * 0.5f, 1.6f, 0f);
    cam.transform.position = focus + new Vector3(0f, x * 0.26f, -x * 0.70f);
    cam.transform.LookAt(focus);
    Capture(cam, 1600, 560, "shield-colours");

    foreach (GameObject go in made) Object.DestroyImmediate(go);
    Object.DestroyImmediate(camGo);
    Object.DestroyImmediate(lightGo);
    Debug.Log("EnemyPreview wrote shield-colours (left to right: " +
              string.Join(", ", System.Array.ConvertAll(ShieldOptions, o => o.name)) + ")");
  }

  // Renders one enemy at four points in its walk cycle, side by side.
  //
  // Animation is the one thing a normal preview cannot check: a still frame at
  // an arbitrary phase looks like any other still, so an amplitude that is far
  // too strong or effectively zero both pass unnoticed. Posing the same enemy
  // at four phases makes the travel visible in a single image, and it uses
  // Enemy's own WaddleScale/WaddleRoll so it cannot drift from the game.
  public static void RenderMotion()
  {
    EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
    Directory.CreateDirectory(OutputDir);

    Camera cam = MakePreviewCamera("MotionPreviewCam");
    var lightGo = new GameObject("MotionPreviewLight");
    Light key = lightGo.AddComponent<Light>();
    key.type = LightType.Directional;
    key.intensity = 1.25f;
    key.transform.rotation = Quaternion.Euler(42f, 150f, 0f);

    EnvironmentTheme.Apply("Environment 1");

    foreach (string name in new[] { "SplitterEnemy", "HealerEnemy", "ShieldedEnemy", "SwarmEnemy" })
    {
      var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>($"{ConfigDir}/{name}.asset");
      if (cfg == null || cfg.prefab == null) continue;

      var made = new List<GameObject>();
      float x = 0f;
      float half = 1f;

      // A whole cycle of the SLOWEST thing on the enemy, so the orbit is
      // sampled across a full revolution rather than four adjacent frames.
      for (int i = 0; i < 4; i++)
      {
        float time = i / 4f * (Mathf.PI * 2f / Enemy.WaddleSpeed) + i * 1.55f;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(cfg.prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                          InteractionMode.AutomatedAction);
        Vector3 rest = go.transform.localScale *
                       (UnitScale.Enemy * Mathf.Max(0.01f, cfg.scaleMultiplier));
        go.transform.localScale = Enemy.WaddleScale(rest, time, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * Enemy.WaddleRoll(time, 0f);

        TintBody(go, cfg);
        TintTraits(go);
        foreach (EnemyTrait trait in go.GetComponentsInChildren<EnemyTrait>(true))
        {
          trait.Animate(time);
        }

        Bounds b = WorldBounds(go);
        half = Mathf.Max(b.extents.x, b.extents.z);
        x += half * 2f + 0.9f;
        go.transform.position = new Vector3(x, -b.min.y, 0f);
        made.Add(go);
      }

      var focus = new Vector3(x * 0.5f, half * 0.9f, 0f);
      cam.transform.position = focus + new Vector3(0f, x * 0.22f, -x * 0.62f);
      cam.transform.LookAt(focus);
      Capture(cam, 1500, 520, $"motion-{name}");

      foreach (GameObject go in made) Object.DestroyImmediate(go);
    }

    Object.DestroyImmediate(cam.gameObject);
    Object.DestroyImmediate(lightGo);
    Debug.Log($"EnemyPreview wrote motion sheets to {OutputDir}");
  }

  // Healer variants, side by side: the question is whether the RED belongs on
  // the signs or on the creature. It cannot be answered in the abstract,
  // because the Basic enemy is already a red spiky ball and the healer shares
  // its body mesh - so a red-bodied healer risks reading as a Basic enemy with
  // decorations. The rightmost option puts a Basic enemy next to it to make
  // that collision visible rather than theoretical.
  public static void RenderHealerOptions()
  {
    EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
    Directory.CreateDirectory(OutputDir);

    Camera cam = MakePreviewCamera("HealerPreviewCam");
    var lightGo = new GameObject("HealerPreviewLight");
    Light key = lightGo.AddComponent<Light>();
    key.type = LightType.Directional;
    key.intensity = 1.25f;
    key.transform.rotation = Quaternion.Euler(42f, 150f, 0f);

    EnvironmentTheme.Apply("Environment 1");

    var green = new Color(0.35f, 0.9f, 0.45f);
    var red = new Color(0.88f, 0.20f, 0.18f);
    var signRed = new Color(1f, 0.13f, 0.10f);
    var signWhite = new Color(1f, 0.97f, 0.94f);

    // name, healer body, sign colour, aura colour, and whether to stand a
    // Basic enemy beside it for comparison.
    var options = new (string name, Color body, Color sign, Color aura, bool withBasic)[]
    {
      ("green body, red signs", green, signRed, new Color(0.86f, 1f, 0.84f), false),
      ("red body, white signs", red, signWhite, new Color(1f, 0.94f, 0.92f), false),
      ("red body, red signs", red, signRed, new Color(1f, 0.90f, 0.88f), true),
    };

    var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>($"{ConfigDir}/HealerEnemy.asset");
    var basicCfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>($"{ConfigDir}/BasicEnemy.asset");
    var made = new List<GameObject>();
    float x = 0f;
    float half = 1f;

    foreach (var option in options)
    {
      GameObject go = Pose(cfg, option.body, option.sign, option.aura);
      Bounds b = WorldBounds(go);
      half = Mathf.Max(b.extents.x, b.extents.z);
      x += half + 1.0f;
      go.transform.position = new Vector3(x, -b.min.y, 0f);
      x += half;
      made.Add(go);

      if (!option.withBasic || basicCfg == null) continue;
      GameObject basic = Pose(basicCfg, Color.clear, Color.clear, Color.clear);
      Bounds bb = WorldBounds(basic);
      float bh = Mathf.Max(bb.extents.x, bb.extents.z);
      x += bh + 0.5f;
      basic.transform.position = new Vector3(x, -bb.min.y, 0f);
      x += bh;
      made.Add(basic);
    }

    var focus = new Vector3(x * 0.5f, half * 0.8f, 0f);
    cam.transform.position = focus + new Vector3(0f, x * 0.24f, -x * 0.66f);
    cam.transform.LookAt(focus);
    Capture(cam, 1700, 600, "healer-options");

    foreach (GameObject go in made) Object.DestroyImmediate(go);
    Object.DestroyImmediate(cam.gameObject);
    Object.DestroyImmediate(lightGo);
    Debug.Log("EnemyPreview wrote healer-options (left to right: " +
              string.Join(", ", System.Array.ConvertAll(options, o => o.name)) +
              ", then a Basic enemy for comparison)");
  }

  // Instantiates an enemy at gameplay scale. A body colour with zero alpha
  // means "leave the config's own colour alone", which is how the Basic enemy
  // is posed unchanged beside the recoloured healers.
  private static GameObject Pose(EnemyConfig cfg, Color body, Color sign, Color aura)
  {
    var go = (GameObject)PrefabUtility.InstantiatePrefab(cfg.prefab);
    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                      InteractionMode.AutomatedAction);
    go.transform.localScale *= UnitScale.Enemy * Mathf.Max(0.01f, cfg.scaleMultiplier);
    go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

    if (body.a > 0f)
    {
      MeshRenderer bodyRenderer = Enemy.FindBodyRenderer(go);
      if (bodyRenderer != null)
      {
        var block = new MaterialPropertyBlock();
        bodyRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", body);
        block.SetColor("_Color", body);
        bodyRenderer.SetPropertyBlock(block);
      }
    }
    else
    {
      TintBody(go, cfg);
    }

    foreach (EnemyTrait trait in go.GetComponentsInChildren<EnemyTrait>(true))
    {
      typeof(EnemyTrait)
        .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
        ?.Invoke(trait, null);
      if (sign.a > 0f)
      {
        // The aura is the part that pulses; the signs are the ones that orbit.
        bool isSign = trait.motion == EnemyTrait.Motion.Orbit;
        trait.accentColor = isSign ? sign : aura;
      }
      trait.ApplyTint(Color.white);
    }
    return go;
  }

  private static List<Posed> BuildLineup(out float span)
  {
    var cast = new List<Posed>();
    float x = 0f;
    float previousHalf = 0f;

    foreach (string name in Order)
    {
      var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>($"{ConfigDir}/{name}.asset");
      if (cfg == null || cfg.prefab == null)
      {
        Debug.LogError($"EnemyPreview: no config/prefab for {name}");
        continue;
      }

      var go = (GameObject)PrefabUtility.InstantiatePrefab(cfg.prefab);
      // Unity refuses to reparent a live prefab instance's children in EDIT
      // mode and silently no-ops (HANDOFF section 6). Nothing here reparents,
      // but unpacking also makes the instance safe to mutate freely.
      PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                        InteractionMode.AutomatedAction);

      // Mirror what EnemyPool + Enemy.ApplyAppearance do, without running the
      // whole Enemy.Initialize path (which wants waypoints and a GameManager).
      go.transform.localScale *= UnitScale.Enemy * Mathf.Max(0.01f, cfg.scaleMultiplier);
      go.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // face the camera-ish
      go.transform.position = Vector3.zero;

      TintBody(go, cfg);
      TintTraits(go);

      // Space by measured size, not a constant: the cast runs from a 0.65x
      // Swarm to a 1.5x Boss and a fixed gap overlaps the big end.
      Bounds b = WorldBounds(go);
      float half = Mathf.Max(0.3f, Mathf.Max(b.extents.x, b.extents.z));
      // Framing radius includes HEIGHT, spacing does not. The healer's crown
      // reaches well above its body, and a radius taken from the horizontal
      // extents alone cropped the cap out of its own close-up - the one shot
      // whose whole purpose is to show it.
      float framing = Mathf.Max(half, b.extents.y);
      x += previousHalf + half + 0.9f;
      previousHalf = half;

      // Sit the body ON the ground rather than centred at y=0, so the shot
      // matches how it is seen in play.
      go.transform.position = new Vector3(x, -b.min.y, 0f);

      // Re-measure after the final placement: the bounds above were taken at
      // the origin, and the look-at wants the real centre.
      Bounds placed = WorldBounds(go);
      cast.Add(new Posed
      {
        go = go, name = name, radius = half, framing = framing,
        centre = placed.center,
      });
    }

    span = Mathf.Max(1f, x + previousHalf);
    return cast;
  }

  private static Bounds WorldBounds(GameObject go)
  {
    Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
    if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
    Bounds b = rs[0].bounds;
    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
    return b;
  }

  private static void TintBody(GameObject go, EnemyConfig cfg)
  {
    MeshRenderer body = Enemy.FindBodyRenderer(go);
    if (body == null || body.sharedMaterial == null) return;

    Color own = cfg.overrideBodyColor
      ? cfg.bodyColor
      : (body.sharedMaterial.HasProperty("_BaseColor")
          ? body.sharedMaterial.GetColor("_BaseColor")
          : body.sharedMaterial.color);
    Color t = EnvironmentTheme.EnemyTint;
    var final = new Color(own.r * t.r, own.g * t.g, own.b * t.b, own.a);

    var block = new MaterialPropertyBlock();
    body.GetPropertyBlock(block);
    if (body.sharedMaterial.HasProperty("_BaseColor")) block.SetColor("_BaseColor", final);
    if (body.sharedMaterial.HasProperty("_Color")) block.SetColor("_Color", final);
    body.SetPropertyBlock(block);
  }

  private static void TintTraits(GameObject go)
  {
    foreach (EnemyTrait trait in go.GetComponentsInChildren<EnemyTrait>(true))
    {
      // -executeMethod runs in EDIT mode, so Awake() never fired and the
      // trait's renderer cache is null - ApplyTint would return early and the
      // preview would show the material's authored colour instead of the
      // tinted one. Same reflection trick UiPreview uses for the tutorial.
      typeof(EnemyTrait)
        .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
        ?.Invoke(trait, null);
      trait.ApplyTint(EnvironmentTheme.EnemyTint);
    }
  }

  private static void Capture(Camera cam, int width, int height, string label)
  {
    var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
    {
      antiAliasing = 4
    };
    RenderTexture previous = RenderTexture.active;

    cam.targetTexture = rt;
    cam.aspect = (float)width / height;
    cam.Render();

    RenderTexture.active = rt;
    var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
    tex.Apply();
    File.WriteAllBytes($"{OutputDir}/{label}.png", tex.EncodeToPNG());

    cam.targetTexture = null;
    RenderTexture.active = previous;
    rt.Release();
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(tex);
  }
}
