using System.Collections.Generic;
using UnityEngine;

// A short burst of shrinking fragments when an enemy dies, so kills read as a
// satisfying pop instead of the enemy simply vanishing. Built at runtime.
//
// Pooled, for the same reason FloatingText is: an unpooled burst allocated a
// GameObject, seven sphere primitives (mesh + renderer each) and a Material per
// kill, then destroyed all nine 0.45s later. That is the heaviest per-kill
// allocation left in the game, and it lands exactly when a wave is at its
// busiest. Only what actually varies between kills - the colour and the
// fragment size - is set on reuse.
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

  private static readonly Stack<DeathEffect> pool = new Stack<DeathEffect>();

  // Statics outlive a scene change but the GameObjects they point at do not, so
  // the pool comes back full of nulls after a level load. Mirrors Enemy.Active.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetPool() => pool.Clear();

  public static void Spawn(Vector3 position, Color color, float scale)
  {
    DeathEffect effect = null;
    while (pool.Count > 0 && effect == null)
    {
      effect = pool.Pop();   // entries go null across a scene load
    }

    if (effect == null)
    {
      var go = new GameObject("DeathEffect");
      effect = go.AddComponent<DeathEffect>();
      effect.Build();
    }

    effect.transform.position = position;
    effect.gameObject.SetActive(true);
    effect.Setup(color, scale);
  }

  // One-time construction. The fragments and the material are made once and
  // then reused; only their colour, size and velocity change per kill.
  private void Build()
  {
    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
    material = new Material(shader);
    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);

    for (int i = 0; i < FragmentCount; i++)
    {
      GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      frag.name = "Frag";
      Destroy(frag.GetComponent<Collider>());
      frag.transform.SetParent(transform, false);
      frag.GetComponent<MeshRenderer>().sharedMaterial = material;

      fragments.Add(new Fragment { transform = frag.transform });
    }
  }

  private void Setup(Color color, float scale)
  {
    // Each pooled effect owns its own material, so writing the colour here
    // cannot bleed into another burst on screen at the same time.
    if (material != null) material.color = color;

    float fragScale = Mathf.Max(0.3f, scale * 0.35f);
    age = 0f;

    for (int i = 0; i < fragments.Count; i++)
    {
      Fragment f = fragments[i];

      Vector3 dir = Random.onUnitSphere;
      dir.y = Mathf.Abs(dir.y) + 0.4f; // bias upward
      f.velocity = dir * Random.Range(4f, 8f);
      f.startScale = Vector3.one * fragScale;

      // Update drives world position and shrinks the scale to zero, so both
      // have to be put back or a reused fragment starts where the last one
      // died, at zero size.
      f.transform.localPosition = Vector3.zero;
      f.transform.localScale = f.startScale;

      fragments[i] = f;
    }
  }

  private void Update()
  {
    age += Time.deltaTime;
    float t = age / Lifetime;
    if (t >= 1f)
    {
      gameObject.SetActive(false);
      pool.Push(this);
      return;
    }

    for (int i = 0; i < fragments.Count; i++)
    {
      Fragment f = fragments[i];
      f.velocity += Vector3.down * Gravity * Time.deltaTime;
      f.transform.position += f.velocity * Time.deltaTime;
      f.transform.localScale = f.startScale * (1f - t);

      // NOTE, this changes how the burst LOOKS. Fragment is a struct, so `f` is
      // a copy; the previous version never wrote it back, which meant the
      // velocity integration above was thrown away every frame and gravity
      // never accumulated. Fragments flew off in near-straight lines. They now
      // arc and fall, which is what the code always said it wanted. If the new
      // pop reads worse on a device, delete this one line rather than unpooling
      // anything - the two changes are independent.
      fragments[i] = f;
    }
  }

  private void OnDestroy()
  {
    if (material != null) Destroy(material);
  }
}
