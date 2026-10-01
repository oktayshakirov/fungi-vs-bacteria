using System.Collections.Generic;
using UnityEngine;

// The mist that boils out of the nest's mouth when an enemy arrives, so a wave
// reads as something climbing out of the ground rather than as models blinking
// into existence on top of a ring. Half of the illusion is here and half is in
// Enemy's emergence ramp, which swells the enemy up out of nothing over the
// same third of a second; neither reads properly on its own.
//
// Pooled, exactly like DeathEffect and for the same reason: this fires once per
// enemy and a late wave spawns dozens, which is precisely when the frame is
// already busiest. Only the colour varies between spawns, so everything else is
// built once and reused.
public class SpawnEffect : MonoBehaviour
{
  // Matches Enemy.EmergeDuration with a little to spare, so the mist is still
  // thinning as the enemy finishes swelling. If the two end together the pop
  // comes back, because the last thing on screen is a full-size enemy
  // appearing as the last puff vanishes.
  private const float Lifetime = 0.62f;
  private const int PuffCount = 9;

  private struct Puff
  {
    public Transform transform;
    public Vector3 drift;
    public float startScale;
    public float endScale;
    public float delay;
  }

  private readonly List<Puff> puffs = new List<Puff>();
  private Material material;
  private Color tint;
  private float age;

  private static readonly Stack<SpawnEffect> pool = new Stack<SpawnEffect>();

  // Statics outlive a scene change but the GameObjects they point at do not, so
  // the pool comes back full of nulls after a level load. Mirrors DeathEffect.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetPool() => pool.Clear();

  // Returns the effect so the editor preview can step it by hand; the game
  // ignores the return value and lets Update drive it.
  public static SpawnEffect Spawn(Vector3 position, float scale)
  {
    SpawnEffect effect = null;
    while (pool.Count > 0 && effect == null)
    {
      effect = pool.Pop();   // entries go null across a scene load
    }

    if (effect == null)
    {
      var go = new GameObject("SpawnEffect");
      effect = go.AddComponent<SpawnEffect>();
      effect.Build();
    }

    // The mouth is at ground level; the mist starts just inside it.
    effect.transform.position = new Vector3(position.x, 0.12f, position.z);
    effect.gameObject.SetActive(true);
    effect.Setup(scale);
    return effect;
  }

  private void Build()
  {
    // UNLIT. A lit sphere picks up the key light and a terminator across its
    // surface, which is exactly what makes a ball of vapour read as a grey
    // pebble - the first pass used Lit and the puffs looked like gravel piled
    // in the mouth. Unlit gives back the flat, even value that fog has, and
    // the biome is carried by the tint instead of by the lighting.
    material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    MakeTransparent(material);

    for (int i = 0; i < PuffCount; i++)
    {
      GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      puff.name = "Puff";
      Destroy(puff.GetComponent<Collider>());
      puff.transform.SetParent(transform, false);

      var renderer = puff.GetComponent<MeshRenderer>();
      renderer.sharedMaterial = material;
      // Mist is not a solid object: it must not darken the board under it or
      // catch the key light's shadow, or the puffs read as grey pebbles.
      renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      renderer.receiveShadows = false;

      puffs.Add(new Puff { transform = puff.transform });
    }
  }

  private void Setup(float scale)
  {
    // The mouth's own neutral carried a good way toward the biome's own fog, so
    // it belongs to the board it is standing on: ash-orange on the volcano,
    // bruised purple on the marsh, white on the snow. At a lower blend it was
    // the same stark white everywhere and read as a flashbulb on the dark
    // biomes rather than as something venting.
    tint = Color.Lerp(EnvironmentTheme.NestMist, EnvironmentTheme.Current.fogColor, 0.40f);
    if (material != null) material.color = tint;

    float size = Mathf.Max(0.5f, scale);
    age = 0f;

    for (int i = 0; i < puffs.Count; i++)
    {
      Puff p = puffs[i];

      // Round the rim rather than out of the middle: mist rising from the whole
      // mouth reads as the mouth venting, where one plume from dead centre
      // reads as a jet and points straight at the spawn position.
      // Clustered tightly rather than spread round the rim: puffs that overlap
      // merge into one mass of vapour, where puffs on a ring stay legible as
      // nine separate balls and read as a circle of stones.
      float ang = (i + Random.value * 0.6f) / puffs.Count * Mathf.PI * 2f;
      float radius = Random.Range(0.10f, 0.50f) * size;
      p.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius, 0.10f,
                                              Mathf.Sin(ang) * radius);

      // Outward and up, slowly. Fast mist reads as an explosion.
      p.drift = new Vector3(Mathf.Cos(ang) * Random.Range(0.35f, 0.85f),
                            Random.Range(2.1f, 3.0f),
                            Mathf.Sin(ang) * Random.Range(0.35f, 0.85f));
      p.startScale = size * Random.Range(0.42f, 0.62f);
      p.endScale = p.startScale * Random.Range(2.6f, 3.8f);
      // Staggered, so the mist boils rather than appearing as one ring.
      p.delay = i / (float)puffs.Count * 0.10f;

      p.transform.localScale = Vector3.zero;
      puffs[i] = p;
    }
  }

  private void Update() => Step(Time.deltaTime);

  // Split out of Update so the whole effect is a function of elapsed time that
  // something other than the engine can drive. CameraPreview.RenderSpawn steps
  // it frame by frame to photograph it, which is the only way a batch render -
  // one still at t=0 - can show an effect that is nothing but motion.
  public void Step(float dt)
  {
    age += dt;
    if (age >= Lifetime)
    {
      gameObject.SetActive(false);
      pool.Push(this);
      return;
    }

    for (int i = 0; i < puffs.Count; i++)
    {
      Puff p = puffs[i];
      float t = (age - p.delay) / (Lifetime - p.delay);
      if (t <= 0f)
      {
        p.transform.localScale = Vector3.zero;
        continue;
      }
      t = Mathf.Clamp01(t);

      // Swells fast and then holds, which is how a puff of vapour behaves and
      // also what keeps it covering the enemy for as long as possible.
      float grow = 1f - (1f - t) * (1f - t);
      p.transform.localScale = Vector3.one * Mathf.Lerp(p.startScale, p.endScale, grow);
      p.transform.localPosition += p.drift * dt;
    }

    // One material for all nine puffs, so the fade is one colour write per
    // frame rather than nine. They are staggered in SIZE, not in opacity,
    // which is what the stagger is for.
    if (material != null)
    {
      float fade = age / Lifetime;
      Color c = tint;
      // Thickens FAST and then thins, rather than decaying from full. The
      // first pass started at its most opaque, which is the one moment the
      // puffs are still too small to hide anything, and by the time they had
      // grown over the enemy they were already half gone. It has to be at its
      // densest around a third of the way in, which is when the enemy is
      // halfway out of the ground.
      const float Peak = 0.24f;
      c.a = 0.88f * (fade < Peak
        ? fade / Peak
        : 1f - (fade - Peak) / (1f - Peak));
      material.color = c;
    }
  }

  // URP's Lit shader needs all of this to actually blend; setting only _Surface
  // leaves it opaque. Same sequence as EnemyArtSetup.MakeTransparent, which is
  // editor-side and so cannot be shared from here.
  private static void MakeTransparent(Material mat)
  {
    mat.SetFloat("_Surface", 1f);
    mat.SetFloat("_Blend", 0f);
    mat.SetFloat("_ZWrite", 0f);
    mat.SetFloat("_AlphaClip", 0f);
    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    mat.DisableKeyword("_ALPHATEST_ON");
    mat.SetOverrideTag("RenderType", "Transparent");
    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
  }

  private void OnDestroy()
  {
    if (material != null) Destroy(material);
  }
}
