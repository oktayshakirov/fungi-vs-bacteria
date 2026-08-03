using System.Collections.Generic;
using UnityEngine;

// Feedback for dropping a tower on the board: a shockwave ring racing outward
// across the ground, a puff of dust kicked up around the base, and a springy
// pop-in on the tower itself. Built at runtime, like the other effects here.
public class PlacementEffect : MonoBehaviour
{
  private const float Lifetime = 0.5f;
  private const int PuffCount = 8;

  private struct Puff
  {
    public Transform transform;
    public Vector3 velocity;
    public float startScale;
  }

  private readonly List<Puff> puffs = new List<Puff>();
  private Transform ring;
  private Material ringMaterial;
  private Material puffMaterial;
  private float age;

  public static void Spawn(Vector3 position, Color accent)
  {
    var go = new GameObject("PlacementEffect");
    go.transform.position = position;
    go.AddComponent<PlacementEffect>().Build(accent);
  }

  private void Build(Color accent)
  {
    ringMaterial = Transparent(accent);
    puffMaterial = Transparent(new Color(0.78f, 0.74f, 0.64f));

    var ringGo = new GameObject("Shockwave");
    ringGo.transform.SetParent(transform, false);
    // Just clear of the ground, or it z-fights with the grass plane
    ringGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
    ringGo.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Ring();
    var ringRenderer = ringGo.AddComponent<MeshRenderer>();
    ringRenderer.sharedMaterial = ringMaterial;
    ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    ring = ringGo.transform;

    for (int i = 0; i < PuffCount; i++)
    {
      GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      puff.name = "Dust";
      Destroy(puff.GetComponent<Collider>());
      puff.transform.SetParent(transform, false);

      var renderer = puff.GetComponent<MeshRenderer>();
      renderer.sharedMaterial = puffMaterial;
      renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

      float angle = (i / (float)PuffCount) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
      var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
      puff.transform.localPosition = outward * 0.6f;

      float scale = Random.Range(0.5f, 1.0f);
      puff.transform.localScale = Vector3.one * scale;
      puffs.Add(new Puff
      {
        transform = puff.transform,
        velocity = outward * Random.Range(3.5f, 6f) + Vector3.up * Random.Range(1.5f, 3f),
        startScale = scale
      });
    }
  }

  private void Update()
  {
    age += Time.deltaTime;
    float t = age / Lifetime;
    if (t >= 1f)
    {
      Destroy(gameObject);
      return;
    }

    // Ring expands fast then eases out, fading as it goes
    float ease = 1f - (1f - t) * (1f - t);
    float radius = Mathf.Lerp(1.5f, 9f, ease);
    ring.localScale = new Vector3(radius, 1f, radius);
    SetAlpha(ringMaterial, Mathf.Lerp(0.75f, 0f, t));

    for (int i = 0; i < puffs.Count; i++)
    {
      Puff p = puffs[i];
      p.velocity += Vector3.down * 9f * Time.deltaTime;
      p.transform.localPosition += p.velocity * Time.deltaTime;
      p.transform.localScale = Vector3.one * p.startScale * (1f - t);
    }
    SetAlpha(puffMaterial, Mathf.Lerp(0.55f, 0f, t));
  }

  private static void SetAlpha(Material material, float alpha)
  {
    Color c = material.color;
    c.a = alpha;
    material.color = c;
    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
  }

  private static Material Transparent(Color color)
  {
    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    mat.SetOverrideTag("RenderType", "Transparent");
    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    mat.SetFloat("_Surface", 1f);
    mat.SetFloat("_Blend", 0f);
    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    mat.SetInt("_ZWrite", 0);
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    color.a = 1f;
    mat.color = color;
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
    return mat;
  }

  private void OnDestroy()
  {
    if (ringMaterial != null) Destroy(ringMaterial);
    if (puffMaterial != null) Destroy(puffMaterial);
  }
}
