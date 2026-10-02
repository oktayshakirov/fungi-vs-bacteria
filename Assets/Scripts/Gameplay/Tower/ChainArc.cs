using System.Collections.Generic;
using UnityEngine;

// One jump of a Shock bolt: a short jagged line from the enemy that was hit to
// the next one in the chain, gone in a tenth of a second.
//
// It is the only thing that tells the player chaining happened at all. Without
// it a Shock tower looks exactly like a single-target tower that occasionally
// kills three enemies for no visible reason - which is how the splash it
// replaced read, and the reason the card's "chains" was never believed.
//
// Pooled, and for a harder reason than DeathEffect's: a chain of three fires
// three of these per SHOT, and Shock is the fastest-firing tower in the game at
// two shots a second. Unpooled that is six GameObjects, six LineRenderers and
// six Materials a second from a single tower.
//
// A LineRenderer, not stretched quads: it already billboards towards the camera
// every frame, which is exactly what a bolt wants and what a quad would need
// hand-written code to do.
public class ChainArc : MonoBehaviour
{
  // Enough to read as a bolt rather than a wire; more joints just makes it
  // noise at the distance the play camera sits at.
  private const int Joints = 6;
  private const float Lifetime = 0.11f;
  private const float Jitter = 0.28f;
  private const float Width = 0.09f;

  private LineRenderer line;
  private Material material;
  private float age;
  private Color color;

  private static readonly Stack<ChainArc> pool = new Stack<ChainArc>();

  // Statics outlive a scene change but the GameObjects they point at do not, so
  // the pool comes back full of nulls after a level load. Mirrors DeathEffect.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetPool() => pool.Clear();

  public static void Spawn(Vector3 from, Vector3 to, Color color)
  {
    ChainArc arc = null;
    while (pool.Count > 0 && arc == null)
    {
      arc = pool.Pop();   // entries go null across a scene load
    }

    if (arc == null)
    {
      var go = new GameObject("ChainArc");
      arc = go.AddComponent<ChainArc>();
      arc.Build();
    }

    arc.gameObject.SetActive(true);
    arc.Setup(from, to, color);
  }

  private void Build()
  {
    line = gameObject.AddComponent<LineRenderer>();
    line.positionCount = Joints;
    line.useWorldSpace = true;
    line.numCapVertices = 0;
    line.alignment = LineAlignment.View;
    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    line.receiveShadows = false;
    line.textureMode = LineTextureMode.Stretch;

    // Unlit and additive: a bolt is light, so it has to brighten whatever is
    // behind it rather than being shaded by the biome's key light - a Lit line
    // picks up the terminator and reads as a bent grey stick (the same mistake
    // SpawnEffect's first pass made with its mist).
    material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    material.SetOverrideTag("RenderType", "Transparent");
    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    material.SetFloat("_Surface", 1f);
    material.SetFloat("_Blend", 1f);
    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
    material.SetInt("_ZWrite", 0);
    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    line.sharedMaterial = material;
  }

  private void Setup(Vector3 from, Vector3 to, Color tint)
  {
    color = tint;
    age = 0f;

    // Lifted off both ends so the bolt leaves the body rather than starting
    // inside it, where the mesh would swallow the first joint.
    from += Vector3.up * 0.35f;
    to += Vector3.up * 0.35f;

    Vector3 along = to - from;
    // Perpendicular in the horizontal plane. Jittering along a world axis
    // instead would collapse to a straight line for any bolt that happens to
    // run along that axis, which on a grid-aligned path is most of them.
    Vector3 side = Vector3.Cross(along.normalized, Vector3.up);
    if (side.sqrMagnitude < 0.001f) side = Vector3.right;
    side.Normalize();

    for (int i = 0; i < Joints; i++)
    {
      float t = i / (float)(Joints - 1);
      // Ends are pinned: a bolt whose tips wander looks like it missed.
      float wobble = Mathf.Sin(t * Mathf.PI) * Jitter;
      Vector3 offset = side * Random.Range(-wobble, wobble) +
                       Vector3.up * Random.Range(-wobble, wobble) * 0.5f;
      line.SetPosition(i, from + along * t + offset);
    }

    ApplyFade(0f);
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
    ApplyFade(t);
  }

  // Thins and dims together, so the bolt snaps out instead of shrinking into a
  // visible thread.
  private void ApplyFade(float t)
  {
    float remaining = 1f - t;
    float width = Width * remaining;
    line.startWidth = width;
    line.endWidth = width * 0.6f;

    Color c = color * remaining;
    c.a = remaining;
    material.color = c;
    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
  }
}
