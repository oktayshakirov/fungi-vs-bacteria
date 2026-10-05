using System.Collections.Generic;
using UnityEngine;

// A short burst of shrinking fragments when an enemy dies, so kills read as a
// satisfying pop instead of the enemy simply vanishing. Built at runtime.
//
// Five small fragments per pooled burst, using a shared material and a
// bounded live count so crowded waves do not flood the board.
public class DeathEffect : MonoBehaviour
{
  private const int FragmentCount = 5;
  public const int LiveLimit=12;
  private static int activeCount;
  private bool counted;
  public static int ActiveCount=>activeCount;
  private const float Lifetime = 0.45f;
  private const float Gravity = 18f;

  private struct Fragment
  {
    public Transform transform;
    public Vector3 velocity;
    public Vector3 startScale;
    public MeshRenderer renderer;
  }

  private readonly List<Fragment> fragments = new List<Fragment>();
  private static Material material;
  private MaterialPropertyBlock block;
  private float age;

  private static readonly Stack<DeathEffect> pool = new Stack<DeathEffect>();

  // Statics outlive a scene change but the GameObjects they point at do not, so
  // the pool comes back full of nulls after a level load. Mirrors Enemy.Active.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetPool() { pool.Clear();activeCount=0; }

  public static void Spawn(Vector3 position, Color color, float scale)
  {
    if(activeCount>=LiveLimit) return;
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
    effect.counted=true;activeCount++;
  }

  // One-time construction. The fragments and the material are made once and
  // then reused; only their colour, size and velocity change per kill.
  private void Build()
  {
    if(material==null)
    {
      material = new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Shared death fragments"};
      material.SetFloat("_Smoothness",.1f);
    }
    block=new MaterialPropertyBlock();

    for (int i = 0; i < FragmentCount; i++)
    {
      GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      frag.name = "Frag";
      Destroy(frag.GetComponent<Collider>());
      frag.transform.SetParent(transform, false);
      frag.GetComponent<MeshRenderer>().sharedMaterial = material;

      var renderer=frag.GetComponent<MeshRenderer>();
      renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
      fragments.Add(new Fragment { transform = frag.transform, renderer=renderer });
    }
  }

  private void Setup(Color color, float scale)
  {
    block.SetColor("_BaseColor",color);
    float fragScale = Mathf.Clamp(scale*.10f,.10f,.45f);
    age = 0f;

    for (int i = 0; i < fragments.Count; i++)
    {
      Fragment f = fragments[i];
      f.renderer.SetPropertyBlock(block);

      Vector3 dir = Random.onUnitSphere;
      dir.y = Mathf.Abs(dir.y) + 0.4f; // bias upward
      f.velocity = dir * Random.Range(2.5f, 4.5f);
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

      // Fragment is a struct: keep the integrated velocity for the next frame.
      fragments[i] = f;
    }
  }

  private void OnDisable()
  {
    if(!counted) return;
    activeCount=Mathf.Max(0,activeCount-1);counted=false;
  }
}
