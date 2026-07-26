using System.Collections.Generic;
using UnityEngine;

// A short burst of shrinking fragments when an enemy dies, so kills read as a
// satisfying pop instead of the enemy simply vanishing. Built at runtime.
public class DeathEffect : MonoBehaviour
{
  private const int FragmentCount = 7;
  private const float Lifetime = 0.45f;
  private const float Gravity = 18f;

  private struct Fragment
  {
    public Transform transform;
    public Vector3 velocity;
    public Vector3 startScale;
  }

  private readonly List<Fragment> fragments = new List<Fragment>();
  private Material material;
  private float age;

  public static void Spawn(Vector3 position, Color color, float scale)
  {
    var go = new GameObject("DeathEffect");
    go.transform.position = position;
    go.AddComponent<DeathEffect>().Build(color, scale);
  }

  private void Build(Color color, float scale)
  {
    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
    material = new Material(shader) { color = color };
    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);

    float fragScale = Mathf.Max(0.3f, scale * 0.35f);

    for (int i = 0; i < FragmentCount; i++)
    {
      GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      frag.name = "Frag";
      Destroy(frag.GetComponent<Collider>());
      frag.transform.SetParent(transform, false);
      frag.GetComponent<MeshRenderer>().sharedMaterial = material;
      frag.transform.localScale = Vector3.one * fragScale;

      Vector3 dir = Random.onUnitSphere;
      dir.y = Mathf.Abs(dir.y) + 0.4f; // bias upward
      fragments.Add(new Fragment
      {
        transform = frag.transform,
        velocity = dir * Random.Range(4f, 8f),
        startScale = frag.transform.localScale
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

    for (int i = 0; i < fragments.Count; i++)
    {
      Fragment f = fragments[i];
      f.velocity += Vector3.down * Gravity * Time.deltaTime;
      f.transform.position += f.velocity * Time.deltaTime;
      f.transform.localScale = f.startScale * (1f - t);
    }
  }

  private void OnDestroy()
  {
    if (material != null) Destroy(material);
  }
}
