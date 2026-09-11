using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

  public static void Render()
  {
    EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
    Directory.CreateDirectory(OutputDir);

    var camGo = new GameObject("EnemyPreviewCam");
    Camera cam = camGo.AddComponent<Camera>();
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0.14f, 0.16f, 0.19f);
    cam.fieldOfView = 32f;

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
          float d = Mathf.Max(1.2f, p.radius) * 3.1f;
          Vector3 at = p.go.transform.position + Vector3.up * p.radius * 0.55f;
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
    public float radius;
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
      x += previousHalf + half + 0.9f;
      previousHalf = half;

      // Sit the body ON the ground rather than centred at y=0, so the shot
      // matches how it is seen in play.
      go.transform.position = new Vector3(x, -b.min.y, 0f);

      cast.Add(new Posed { go = go, name = name, radius = half });
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
