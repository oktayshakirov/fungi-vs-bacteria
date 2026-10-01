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
  // The largest any variant uses. Puffs are built once at this count and the
  // variants wanting fewer leave the tail hidden, so a variant never allocates.
  private const int PuffCount = 11;

  // Three shapes of burst, one picked at random per spawn. Everything was
  // already jittered per puff, but the SHAPE of the burst was fixed, and a
  // late wave pushing twenty enemies through one mouth made that obvious: it
  // read as the same puff stamped twenty times. These differ in what the eye
  // actually reads at a glance - how many pieces, how wide they sit, how fast
  // they climb.
  private struct Variant
  {
    public int count;
    public float spread;        // how far off the mouth's centre they start
    public Vector2 rise;        // vertical drift
    public float outward;       // horizontal drift
    public Vector2 startSize;
    public Vector2 growth;      // multiple of startSize reached by the end
    public float stagger;
  }

  public static int VariantCount => Variants.Length;

  private static readonly Variant[] Variants =
  {
    // Billow: a round mass that swells straight up. The common case.
    new Variant { count = 9, spread = 0.50f, rise = new Vector2(2.1f, 3.0f),
                  outward = 0.85f, startSize = new Vector2(0.42f, 0.62f),
                  growth = new Vector2(2.6f, 3.8f), stagger = 0.10f },
    // Gout: fewer, bigger, faster - one hard belch out of the mouth. The
    // spread is not tighter than this on purpose: at 0.28 the six puffs landed
    // on top of each other and merged into one flawless sphere, which reads as
    // a balloon rather than as vapour. It needs just enough offset to keep a
    // lumpy outline.
    new Variant { count = 6, spread = 0.46f, rise = new Vector2(3.0f, 4.1f),
                  outward = 0.50f, startSize = new Vector2(0.50f, 0.82f),
                  growth = new Vector2(2.1f, 3.2f), stagger = 0.05f },
    // Creep: more, smaller and slower, spilling sideways and hanging low. Its
    // puffs are bigger than the first pass's and sit closer in, because spread
    // that wide left a hole over the middle and the enemy showed through it
    // while it was still half-size.
    new Variant { count = 11, spread = 0.60f, rise = new Vector2(1.2f, 1.9f),
                  outward = 1.45f, startSize = new Vector2(0.36f, 0.54f),
                  growth = new Vector2(3.0f, 4.4f), stagger = 0.15f },
  };

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
  private int used;

  private static readonly Stack<SpawnEffect> pool = new Stack<SpawnEffect>();

  // Statics outlive a scene change but the GameObjects they point at do not, so
  // the pool comes back full of nulls after a level load. Mirrors DeathEffect.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetPool() => pool.Clear();

  // Returns the effect so the editor preview can step it by hand; the game
  // ignores the return value and lets Update drive it. `variant` is -1 for the
  // random pick the game always wants, and 0-2 only so the preview can
  // photograph each one instead of hoping the dice show all three.
  public static SpawnEffect Spawn(Vector3 position, float scale, int variant = -1)
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
    effect.Setup(scale, variant);
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

  private void Setup(float scale, int variant)
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

    Variant v = Variants[variant >= 0 ? variant % Variants.Length
                                      : Random.Range(0, Variants.Length)];
    used = Mathf.Clamp(v.count, 1, puffs.Count);

    for (int i = 0; i < puffs.Count; i++)
    {
      Puff p = puffs[i];

      // Everything past the chosen variant's count is parked at zero size and
      // skipped by Update, so picking a six-puff gout costs five fewer
      // transforms than an eleven-puff creep and never allocates.
      if (i >= used)
      {
        p.transform.localScale = Vector3.zero;
        puffs[i] = p;
        continue;
      }

      // Clustered rather than spread round the rim: puffs that overlap merge
      // into one mass of vapour, where puffs sitting on a ring stay legible as
      // separate balls and read as a circle of stones.
      float ang = (i + Random.value * 0.6f) / used * Mathf.PI * 2f;
      float radius = Random.Range(0.10f, v.spread) * size;
      p.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius, 0.10f,
                                              Mathf.Sin(ang) * radius);

      // Outward and up. Fast mist reads as an explosion, which is why even the
      // gout variant climbs rather than bursts.
      p.drift = new Vector3(Mathf.Cos(ang) * Random.Range(v.outward * 0.4f, v.outward),
                            Random.Range(v.rise.x, v.rise.y),
                            Mathf.Sin(ang) * Random.Range(v.outward * 0.4f, v.outward));
      p.startScale = size * Random.Range(v.startSize.x, v.startSize.y);
      p.endScale = p.startScale * Random.Range(v.growth.x, v.growth.y);
      // Staggered, so the mist boils rather than appearing all at once.
      p.delay = i / (float)used * v.stagger;

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

    for (int i = 0; i < used; i++)
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

    // One material for every puff, so the fade is one colour write per frame
    // rather than one per puff. They are staggered in SIZE, not in opacity,
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
