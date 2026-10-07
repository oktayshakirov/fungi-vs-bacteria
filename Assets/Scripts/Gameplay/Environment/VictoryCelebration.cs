using UnityEngine;
using UnityEngine.Rendering;

// Fireworks and confetti over the base when a level is won, while the camera
// swings in around it (CameraRig.PlayVictoryView) and before the victory
// screen comes up.
//
// Every spark and every scrap of confetti is a quad in one of two meshes that
// are rewritten each frame from preallocated arrays: two draw calls and no
// allocation for the whole show, and no particle shader that a build might
// strip. "Sprites/Default" is always included in a build and takes vertex
// colour and alpha, which is all a spark needs.
//
// Unscaled time: the game pauses under the victory screen, and the show keeps
// going behind it.
public class VictoryCelebration : MonoBehaviour
{
  private const int Rockets = 6;
  private const int SparksPerBurst = 44;
  private const int TrailPerRocket = 6;
  private const int ConfettiCount = 140;
  private const float Lifetime = 6f;

  private static readonly Color32[] Palette =
  {
    new Color32(255, 214, 74, 255),   // gold
    new Color32(255, 92, 170, 255),   // magenta
    new Color32(92, 220, 255, 255),   // cyan
    new Color32(190, 236, 40, 255),   // lime, the game's own primary
    new Color32(255, 140, 60, 255),   // orange
    new Color32(255, 255, 255, 255),
  };

  private struct Rocket
  {
    public Vector3 from, to;
    public float launchAt, flight;
    public Color32 color;
    public bool burst;
  }

  private struct Bit
  {
    public Vector3 position, velocity;
    public Quaternion rotation, spin;
    public float bornAt, life, size;
    public Color32 color;
  }

  private Rocket[] rockets;
  private Bit[] sparks;
  private Bit[] confetti;
  private Mesh sparkMesh, confettiMesh;
  private Vector3[] sparkVerts, confettiVerts;
  private Color32[] sparkColors, confettiColors;
  private float startedAt;
  private float scale;
  private System.Random rng;

  public static void Play(Vector3 basePosition, float worldScale)
  {
    var go = new GameObject("VictoryCelebration");
    go.transform.position = Vector3.zero;
    go.AddComponent<VictoryCelebration>().Begin(basePosition, Mathf.Max(0.5f, worldScale));
  }

  private void Begin(Vector3 origin, float worldScale)
  {
    scale = worldScale;
    rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));
    startedAt = Time.unscaledTime;

    rockets = new Rocket[Rockets];
    for (int i = 0; i < Rockets; i++)
    {
      float angle = (i / (float)Rockets) * Mathf.PI * 2f + Range(-0.4f, 0.4f);
      float reach = Range(1.2f, 3.2f) * scale;
      Vector3 from = origin + new Vector3(Mathf.Cos(angle) * reach * 0.4f, 0f, Mathf.Sin(angle) * reach * 0.4f);
      rockets[i] = new Rocket
      {
        from = from,
        to = origin + new Vector3(Mathf.Cos(angle) * reach, Range(5.5f, 8f) * scale, Mathf.Sin(angle) * reach),
        launchAt = 0.25f + i * 0.42f + Range(0f, 0.15f),
        flight = Range(0.55f, 0.75f),
        color = Palette[i % Palette.Length],
      };
    }

    sparks = new Bit[Rockets * (SparksPerBurst + TrailPerRocket)];
    confetti = new Bit[ConfettiCount];
    for (int i = 0; i < ConfettiCount; i++)
    {
      Vector2 disc = Random.insideUnitCircle * 2.2f * scale;
      confetti[i] = new Bit
      {
        position = origin + new Vector3(disc.x, Range(5f, 7.5f) * scale, disc.y),
        velocity = new Vector3(disc.x * 0.9f, Range(1.5f, 4f) * scale, disc.y * 0.9f),
        rotation = Random.rotation,
        spin = Quaternion.Euler(Range(-260f, 260f), Range(-260f, 260f), Range(-260f, 260f)),
        bornAt = Range(0.05f, 0.5f),
        life = Range(3.6f, 4.8f),
        size = Range(0.16f, 0.24f) * scale,
        color = Palette[rng.Next(Palette.Length)],
      };
    }

    sparkMesh = BuildMesh("Sparks", sparks.Length, out sparkVerts, out sparkColors);
    confettiMesh = BuildMesh("Confetti", confetti.Length, out confettiVerts, out confettiColors);
    Attach("SparksRenderer", sparkMesh, 1.8f);
    Attach("ConfettiRenderer", confettiMesh, 1f);
  }

  private void Update()
  {
    float age = Time.unscaledTime - startedAt;
    if (age > Lifetime)
    {
      Destroy(gameObject);
      return;
    }

    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
    Camera view = Camera.main;
    Vector3 right = view != null ? view.transform.right : Vector3.right;
    Vector3 up = view != null ? view.transform.up : Vector3.up;

    UpdateRockets(age);
    WriteSparks(age, dt, right, up);
    WriteConfetti(age, dt);

    sparkMesh.SetVertices(sparkVerts);
    sparkMesh.SetColors(sparkColors);
    confettiMesh.SetVertices(confettiVerts);
    confettiMesh.SetColors(confettiColors);
  }

  // Each rocket climbs as a short streak of trail sparks, then bursts into a
  // sphere of sparks in its own colour.
  private void UpdateRockets(float age)
  {
    for (int r = 0; r < rockets.Length; r++)
    {
      Rocket rocket = rockets[r];
      int baseIndex = r * (SparksPerBurst + TrailPerRocket);
      float t = (age - rocket.launchAt) / rocket.flight;

      if (t >= 0f && t < 1f)
      {
        // Ease out, so it slows to a stop at the top like a real shell.
        float eased = 1f - (1f - t) * (1f - t);
        Vector3 head = Vector3.Lerp(rocket.from, rocket.to, eased);
        for (int i = 0; i < TrailPerRocket; i++)
        {
          float behind = Mathf.Max(0f, eased - i * 0.035f);
          sparks[baseIndex + i] = new Bit
          {
            position = Vector3.Lerp(rocket.from, rocket.to, behind),
            bornAt = age, life = 0.12f,
            size = (0.22f - i * 0.025f) * scale,
            color = new Color32(255, 236, 190, 255),
          };
        }
        sparks[baseIndex].position = head;
      }

      if (t >= 1f && !rocket.burst)
      {
        rocket.burst = true;
        for (int i = 0; i < SparksPerBurst; i++)
        {
          sparks[baseIndex + TrailPerRocket + i] = new Bit
          {
            position = rocket.to,
            velocity = Random.onUnitSphere * Range(3.2f, 4.6f) * scale,
            bornAt = age,
            life = Range(1.1f, 1.6f),
            size = Range(0.16f, 0.26f) * scale,
            color = i % 5 == 0 ? Palette[5] : rocket.color,
          };
        }
        Haptics.Play(Haptics.Style.Light);
      }

      rockets[r] = rocket;
    }
  }

  private void WriteSparks(float age, float dt, Vector3 right, Vector3 up)
  {
    for (int i = 0; i < sparks.Length; i++)
    {
      Bit bit = sparks[i];
      float t = bit.life > 0f ? (age - bit.bornAt) / bit.life : 2f;
      if (t < 0f || t > 1f)
      {
        Collapse(sparkVerts, sparkColors, i);
        continue;
      }

      // Gravity and air drag: bursts open fast, then droop and slow.
      bit.velocity += Vector3.down * 3.5f * scale * dt;
      bit.velocity *= 1f - 1.6f * dt;
      bit.position += bit.velocity * dt;
      sparks[i] = bit;

      float size = bit.size * (1f - t * 0.6f);
      Color32 color = bit.color;
      color.a = (byte)(255f * (1f - t * t));
      Quad(sparkVerts, sparkColors, i, bit.position, right * size, up * size, color);
    }
  }

  private void WriteConfetti(float age, float dt)
  {
    for (int i = 0; i < confetti.Length; i++)
    {
      Bit bit = confetti[i];
      float t = (age - bit.bornAt) / bit.life;
      if (t < 0f || t > 1f)
      {
        Collapse(confettiVerts, confettiColors, i);
        continue;
      }

      // Heavy drag gives paper its slow terminal fall; the sway is the flutter.
      bit.velocity += Vector3.down * 4f * scale * dt;
      bit.velocity *= 1f - 2.4f * dt;
      float sway = Mathf.Sin((age + i) * 3.1f) * 0.9f * scale;
      bit.position += (bit.velocity + new Vector3(sway, 0f, sway * 0.5f)) * dt;
      bit.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, bit.spin, dt) * bit.rotation;
      confetti[i] = bit;

      Color32 color = bit.color;
      color.a = (byte)(255f * Mathf.Clamp01((1f - t) * 6f));
      Vector3 w = bit.rotation * Vector3.right * bit.size;
      Vector3 h = bit.rotation * Vector3.up * bit.size * 0.55f;
      Quad(confettiVerts, confettiColors, i, bit.position, w, h, color);
    }
  }

  private static void Quad(Vector3[] verts, Color32[] colors, int index, Vector3 centre,
    Vector3 halfRight, Vector3 halfUp, Color32 color)
  {
    int v = index * 4;
    verts[v] = centre - halfRight - halfUp;
    verts[v + 1] = centre - halfRight + halfUp;
    verts[v + 2] = centre + halfRight + halfUp;
    verts[v + 3] = centre + halfRight - halfUp;
    colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
  }

  private static void Collapse(Vector3[] verts, Color32[] colors, int index)
  {
    int v = index * 4;
    verts[v] = verts[v + 1] = verts[v + 2] = verts[v + 3] = Vector3.zero;
    colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = new Color32(0, 0, 0, 0);
  }

  private static Mesh BuildMesh(string name, int quads, out Vector3[] verts, out Color32[] colors)
  {
    verts = new Vector3[quads * 4];
    colors = new Color32[quads * 4];
    var triangles = new int[quads * 6];
    for (int q = 0; q < quads; q++)
    {
      int v = q * 4, t = q * 6;
      triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
      triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
    }

    var mesh = new Mesh { name = name };
    mesh.MarkDynamic();
    mesh.vertices = verts;
    mesh.colors32 = colors;
    mesh.triangles = triangles;
    // Big and fixed: the bits fly well outside where they start, and a mesh
    // culled on stale bounds would wink out mid-burst.
    mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
    return mesh;
  }

  private void Attach(string name, Mesh mesh, float glow)
  {
    var go = new GameObject(name);
    go.transform.SetParent(transform, false);
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    var renderer = go.AddComponent<MeshRenderer>();
    renderer.shadowCastingMode = ShadowCastingMode.Off;
    renderer.receiveShadows = false;
    renderer.sharedMaterial = Material(glow);
  }

  private Material ownedSparks, ownedConfetti;

  private Material Material(float glow)
  {
    Shader shader = Shader.Find("Sprites/Default");
    if (shader == null) shader = Shader.Find("UI/Default");
    var material = new Material(shader) { name = "Victory celebration" };
    // Above 1 on purpose for the sparks: with HDR on, the scene bloom picks
    // them up and they glow.
    if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(glow, glow, glow, 1f));
    if (ownedSparks == null) ownedSparks = material; else ownedConfetti = material;
    return material;
  }

  private float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

  private void OnDestroy()
  {
    if (sparkMesh != null) Destroy(sparkMesh);
    if (confettiMesh != null) Destroy(confettiMesh);
    if (ownedSparks != null) Destroy(ownedSparks);
    if (ownedConfetti != null) Destroy(ownedConfetti);
  }
}
