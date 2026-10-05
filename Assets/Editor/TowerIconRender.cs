using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders every tower's icon from its own prefab, on a transparent background,
// into the PNG its TowerConfig already points at (so no reference changes).
//
// The icons that shipped with the project were photos of the models on an
// opaque pale-grey backdrop. On the towers rail's dark cards each tower sat in
// a light square, which read as a missing image rather than as an icon. These
// are cut out, lit the same for all eight, and framed from measured bounds so
// every tower fills its tile the same amount.
//
// Batch: -executeMethod TowerIconRender.Render   (needs graphics, not -nographics)
public static class TowerIconRender
{
  private const int Size = 256;
  private const int Supersample = 2;

  public static void Render()
  {
    bool previous=ShaderUtil.allowAsyncCompilation;
    ShaderUtil.allowAsyncCompilation=false;
    try {RenderInternal();}
    finally {ShaderUtil.allowAsyncCompilation=previous;}
  }

  private static void RenderInternal()
  {
    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    var lightGo = new GameObject("Key");
    var key = lightGo.AddComponent<Light>();
    key.type = LightType.Directional;
    key.intensity = 1.25f;
    key.color = new Color(1f, 0.97f, 0.92f);
    lightGo.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

    var fillGo = new GameObject("Fill");
    var fill = fillGo.AddComponent<Light>();
    fill.type = LightType.Directional;
    fill.intensity = 0.45f;
    fill.color = new Color(0.75f, 0.82f, 1f);
    fillGo.transform.rotation = Quaternion.Euler(20f, 150f, 0f);

    // A cool rim from behind, so the darker towers (Inferno is glossy black)
    // keep their silhouette against the rail's dark navy cards.
    var rimGo = new GameObject("Rim");
    var rim = rimGo.AddComponent<Light>();
    rim.type = LightType.Directional;
    rim.intensity = 1.1f;
    rim.color = new Color(0.80f, 0.90f, 1f);
    rimGo.transform.rotation = Quaternion.Euler(28f, 200f, 0f);

    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight = new Color(0.50f, 0.51f, 0.57f);
    // The empty scene reflects Unity's default grey sky, which washed the
    // glossy black Inferno cap out to flat grey.
    RenderSettings.reflectionIntensity = 0.55f;

    var camGo = new GameObject("IconCam");
    var cam = camGo.AddComponent<Camera>();
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
    cam.fieldOfView = 26f;
    cam.nearClipPlane = 0.05f;
    cam.farClipPlane = 500f;

    int written = 0;
    foreach (string guid in AssetDatabase.FindAssets("t:TowerConfig"))
    {
      var config = AssetDatabase.LoadAssetAtPath<TowerConfig>(AssetDatabase.GUIDToAssetPath(guid));
      if (config == null || config.towerPrefab == null || config.towerIcon == null) continue;

      string iconPath = AssetDatabase.GetAssetPath(config.towerIcon);
      if (string.IsNullOrEmpty(iconPath) || !iconPath.EndsWith(".png")) continue;

      var go = (GameObject)PrefabUtility.InstantiatePrefab(config.towerPrefab);
      PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
      // The models face local -X (see Tower.modelYawOffset); turn that toward
      // the camera, then a little past it for a three-quarter view.
      go.transform.position = Vector3.zero;
      go.transform.rotation = Quaternion.Euler(0f, -90f + 24f, 0f);
      foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;

      Bounds b = MeasureBounds(go);
      Frame(cam, b);
      WritePng(cam, iconPath);
      Object.DestroyImmediate(go);
      written++;
    }

    AssetDatabase.Refresh();
    Debug.Log($"TOWER ICONS OK: wrote {written}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  private static Bounds MeasureBounds(GameObject go)
  {
    var renderers = go.GetComponentsInChildren<Renderer>();
    if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
    Bounds b = renderers[0].bounds;
    foreach (Renderer r in renderers)
    {
      if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
      b.Encapsulate(r.bounds);
    }
    return b;
  }

  // Looks down on the model a little, from far enough that its bounding sphere
  // fills about 86% of the frame.
  private static void Frame(Camera cam, Bounds b)
  {
    float radius = b.extents.magnitude;
    float half = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
    float distance = radius / Mathf.Sin(half) * 0.80f;
    Vector3 dir = Quaternion.Euler(16f, 0f, 0f) * Vector3.back;
    cam.transform.position = b.center + dir * distance;
    cam.transform.LookAt(b.center);
  }

  // URP does not reliably write coverage into the alpha channel of an offscreen
  // target (the first version of this read back a fully transparent image), so
  // alpha is recovered instead: the same frame over black and over white, where
  // a pixel's coverage is how little it changed between the two.
  private static Color[] Grab(Camera cam, Color background, int big)
  {
    cam.backgroundColor = background;
    var rt = new RenderTexture(big, big, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
    cam.targetTexture = rt;
    cam.aspect = 1f;
    // Warm the URP material bindings before alpha reconstruction; otherwise
    // the first icon can inherit one material tint across all of its parts.
    cam.Render();
    cam.Render();

    RenderTexture.active = rt;
    var raw = new Texture2D(big, big, TextureFormat.RGBA32, false);
    raw.ReadPixels(new Rect(0, 0, big, big), 0, 0);
    raw.Apply();
    RenderTexture.active = null;
    cam.targetTexture = null;
    rt.Release();
    Object.DestroyImmediate(rt);
    Color[] px = raw.GetPixels();
    Object.DestroyImmediate(raw);
    return px;
  }

  private static void WritePng(Camera cam, string path)
  {
    int big = Size * Supersample;
    Color[] onBlack = Grab(cam, Color.black, big);
    Color[] onWhite = Grab(cam, Color.white, big);

    // Box-filter down, premultiplied, so the cut-out edge has no dark fringe.
    var dst = new Color[Size * Size];
    for (int y = 0; y < Size; y++)
    {
      for (int x = 0; x < Size; x++)
      {
        float r = 0f, g = 0f, bl = 0f, a = 0f;
        for (int sy = 0; sy < Supersample; sy++)
        {
          for (int sx = 0; sx < Supersample; sx++)
          {
            int i = (y * Supersample + sy) * big + x * Supersample + sx;
            Color k = onBlack[i], w = onWhite[i];
            float alpha = Mathf.Clamp01(1f - ((w.r - k.r) + (w.g - k.g) + (w.b - k.b)) / 3f);
            // Over black the colour is already premultiplied by coverage.
            r += k.r; g += k.g; bl += k.b; a += alpha;
          }
        }
        dst[y * Size + x] = a > 0.004f
          ? new Color(Mathf.Clamp01(r / a), Mathf.Clamp01(g / a), Mathf.Clamp01(bl / a),
              a / (Supersample * Supersample))
          : Color.clear;
      }
    }

    var outTex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
    outTex.SetPixels(dst);
    outTex.Apply();
    File.WriteAllBytes(path, outTex.EncodeToPNG());
    Object.DestroyImmediate(outTex);
  }
}
