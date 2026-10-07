using UnityEngine;
using UnityEngine.Rendering;

// The Shield booster made visible: a translucent dome over the base for as
// long as it runs. It swells in, breathes while it holds, flickers through its
// last two seconds so the end is not a surprise, and flashes whenever it
// swallows a hit (GameManager.OnShieldAbsorbed).
//
// One renderer, hidden whenever no shield is up. Uses CombatPulse's shared
// transparent material with a property block for the colour, so it adds no
// material of its own.
[RequireComponent(typeof(BaseHouse))]
public class BaseShield : MonoBehaviour
{
  private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
  private static readonly Color Tint = new Color(0.45f, 0.85f, 1f);
  private const float Alpha = 0.26f;
  private const float FadeSpeed = 4f;
  private const float WarnSeconds = 2f;
  private const float FlashSeconds = 0.35f;

  private static Mesh domeMesh;

  private Transform dome;
  private MeshRenderer domeRenderer;
  private MaterialPropertyBlock block;
  private float radius;
  private float shown;
  private float flashUntil = -1f;

  private void OnEnable() => GameManager.OnShieldAbsorbed += Flash;
  private void OnDisable() => GameManager.OnShieldAbsorbed -= Flash;

  private void Flash() => flashUntil = Time.unscaledTime + FlashSeconds;

  private void Build()
  {
    Bounds bounds = GetComponent<BaseHouse>().Bounds;
    if (bounds.size == Vector3.zero) return;

    // Wide enough to clear the house's cap, tall enough to clear its roof.
    radius = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.35f, bounds.size.y * 0.95f);

    var go = new GameObject("ShieldDome");
    // Not parented to the house: BaseFlinch squashes the house when it is hit,
    // and the shield is exactly what stops that happening.
    go.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    dome = go.transform;

    go.AddComponent<MeshFilter>().sharedMesh = Dome();
    domeRenderer = go.AddComponent<MeshRenderer>();
    domeRenderer.sharedMaterial = CombatPulse.SharedMaterial();
    domeRenderer.shadowCastingMode = ShadowCastingMode.Off;
    domeRenderer.receiveShadows = false;
    domeRenderer.enabled = false;
    block = new MaterialPropertyBlock();
  }

  private void Update()
  {
    if (dome == null)
    {
      if (!BoosterEffects.ShieldActive) return;
      Build();
      if (dome == null) return;
    }

    bool on = BoosterEffects.ShieldActive;
    shown = Mathf.MoveTowards(shown, on ? 1f : 0f, Time.unscaledDeltaTime * FadeSpeed);
    if (shown <= 0f)
    {
      if (domeRenderer.enabled) domeRenderer.enabled = false;
      return;
    }
    if (!domeRenderer.enabled) domeRenderer.enabled = true;

    float time = Time.unscaledTime;
    float alpha = Alpha * shown;

    // A slow flicker through the last seconds, so the player sees it ending.
    if (on && BoosterEffects.ShieldRemaining < WarnSeconds)
      alpha *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(time * 9f));

    // A hit it absorbed: brighter for a moment.
    float flash = flashUntil > time ? (flashUntil - time) / FlashSeconds : 0f;
    alpha = Mathf.Min(0.7f, alpha + flash * 0.35f);

    float breathe = 1f + Mathf.Sin(time * 3f) * 0.025f + flash * 0.06f;
    float size = radius * 2f * breathe * (0.8f + 0.2f * shown);
    dome.localScale = new Vector3(size, size, size);

    Color color = Color.Lerp(Tint, Color.white, flash * 0.6f);
    color.a = alpha;
    block.SetColor(BaseColorId, color);
    domeRenderer.SetPropertyBlock(block);
  }

  private void OnDestroy()
  {
    if (dome != null) Destroy(dome.gameObject);
  }

  // A unit-diameter hemisphere resting on y = 0, outward-facing. Built once.
  private static Mesh Dome()
  {
    if (domeMesh != null) return domeMesh;

    const int rings = 10;
    const int segments = 32;
    var vertices = new Vector3[(rings + 1) * (segments + 1)];
    var normals = new Vector3[vertices.Length];
    var triangles = new int[rings * segments * 6];

    for (int r = 0; r <= rings; r++)
    {
      float lat = r / (float)rings * Mathf.PI * 0.5f;   // 0 at the rim, 90 at the top
      for (int s = 0; s <= segments; s++)
      {
        float lon = s / (float)segments * Mathf.PI * 2f;
        Vector3 n = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
        int i = r * (segments + 1) + s;
        vertices[i] = n * 0.5f;
        normals[i] = n;
      }
    }

    int t = 0;
    for (int r = 0; r < rings; r++)
    {
      for (int s = 0; s < segments; s++)
      {
        int a = r * (segments + 1) + s;
        int b = a + segments + 1;
        triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
        triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
      }
    }

    domeMesh = new Mesh { name = "ShieldDome", vertices = vertices, normals = normals, triangles = triangles };
    domeMesh.RecalculateBounds();
    return domeMesh;
  }
}
