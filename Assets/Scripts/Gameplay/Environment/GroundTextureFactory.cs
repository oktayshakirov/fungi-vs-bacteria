using UnityEngine;

// Generates seamless, tileable stylized ground textures at runtime so each
// environment can have a genuinely different terrain without shipping art.
// Textures are cached per biome for the session.
public static class GroundTextureFactory
{
  private const int Size = 512;

  private static Texture2D sand;
  private static Texture2D toxic;
  private static Texture2D dark;
  private static Texture2D meadow;

  // Grass with broad patches, clumping and a fine blade grain, plus bare soil
  // showing through here and there. Replaces the flat, visibly-tiling green
  // photo the meadow used to use.
  public static Texture2D Meadow()
  {
    if (meadow == null) meadow = BuildMeadow();
    return meadow;
  }

  private static Texture2D BuildMeadow()
  {
    Color deep = new Color(0.28f, 0.42f, 0.20f);
    Color mid = new Color(0.37f, 0.50f, 0.25f);
    Color light = new Color(0.46f, 0.57f, 0.30f);
    Color soil = new Color(0.48f, 0.43f, 0.27f);

    return Build((u, v) =>
    {
      // Broad grassy clearings carry the composition; fine grain is restrained
      // so it does not shimmer or compete with enemies at gameplay distance.
      float patches = SmoothStep(.28f,.72f,Fbm(u,v,2,3));
      float clumps = Fbm(u + 3.1f, v + 7.4f, 8, 2);
      float blades = Fbm(u, v, 36, 2);
      float t = Mathf.Clamp01(patches*.82f + clumps*.14f + blades*.04f);
      Color c = t < .5f ? Color.Lerp(deep,mid,t*2) : Color.Lerp(mid,light,(t-.5f)*2);
      float bare = SmoothStep(.58f,.76f,Fbm(u+11.3f,v+5.7f,3,2));
      return Color.Lerp(c,soil,bare*.30f);
    });
  }

  public static Texture2D Sand()
  {
    if (sand == null) sand = BuildSand();
    return sand;
  }

  public static Texture2D Toxic()
  {
    if (toxic == null) toxic = BuildToxic();
    return toxic;
  }

  // A clean, simple dark ground (subtle low-frequency variation only, no
  // patterns) so the night environment reads calm like the other two.
  public static Texture2D Dark()
  {
    if (dark == null) dark = BuildDark();
    return dark;
  }

  private static Texture2D snow;
  private static Texture2D ash;

  // Wind-packed snow: drifts, a faint crust and a sparse sparkle.
  public static Texture2D Snow()
  {
    if (snow == null) snow = Build((u, v) =>
    {
      Color shadow = new Color(0.72f, 0.78f, 0.88f);
      Color lit = new Color(0.97f, 0.98f, 1f);

      float drift = Fbm(u, v, 2, 3);
      float crust = Fbm(u + 4.1f, v + 2.3f, 12, 2) * 0.10f;
      Color c = Color.Lerp(shadow, lit, Mathf.Clamp01(drift * 0.90f + crust));

      // Occasional bright glints where the crust catches the light
      float glint = SmoothStep(0.93f, 0.99f, Fbm(u + 8.7f, v + 3.9f, 26, 2));
      return Color.Lerp(c, Color.white, glint * .25f);
    });
    return snow;
  }

  // Cooled lava: dark ash split by cracks with embers still glowing in them.
  public static Texture2D Ash()
  {
    if (ash == null) ash = Build((u, v) =>
    {
      Color soot = new Color(0.13f, 0.11f, 0.11f);
      Color stone = new Color(0.26f, 0.23f, 0.22f);
      Color ember = new Color(1f, 0.42f, 0.10f);

      float plates = Fbm(u, v, 2, 3);
      Color c = Color.Lerp(soot, stone, plates);

      // Ridged noise gives a crack network rather than blobs
      float veins = Mathf.Abs(Fbm(u + 2.7f, v + 6.1f, 6, 3) - 0.5f) * 2f;
      float glow = SmoothStep(0.07f, 0.0f, veins);
      return Color.Lerp(c, ember, glow * 0.48f);
    });
    return ash;
  }

  private static Texture2D marsh;

  // Toxic Marsh: boggy moss over dark peat, with acid-green pools that have a
  // bright rim and a darker centre. The biome used to share DARK with the night
  // levels, which read as a flat navy slab with nothing toxic about it. Kept in
  // the mid-dark range so the pale road and the towers still sit on top of it.
  public static Texture2D Marsh()
  {
    if (marsh == null) marsh = Build((u, v) =>
    {
      Color peat = new Color(0.13f, 0.12f, 0.17f);
      Color moss = new Color(0.20f, 0.27f, 0.17f);
      Color slime = new Color(0.46f, 0.78f, 0.18f);
      Color pool = new Color(0.20f, 0.42f, 0.10f);

      float bog = Fbm(u, v, 2, 3);
      float fleck = Fbm(u + 3.3f, v + 7.1f, 22, 2) * 0.08f;
      Color c = Color.Lerp(peat, moss, Mathf.Clamp01(bog * 1.1f - 0.1f + fleck));

      float pools = Fbm(u + 5.2f, v + 1.7f, 3, 3);
      float inside = SmoothStep(0.66f, 0.72f, pools);
      float rim = inside * (1f - SmoothStep(0.72f, 0.79f, pools));
      c = Color.Lerp(c, pool, inside * 0.6f);
      return Color.Lerp(c, slime, rim * 0.35f);
    });
    return marsh;
  }

  private static Texture2D BuildDark()
  {
    // Night, but still readable: at the old values the play area rendered as
    // near-black and towers/path had nothing to sit against.
    Color a = new Color(0.17f, 0.19f, 0.25f);
    Color b = new Color(0.26f, 0.28f, 0.35f);
    return Build((u, v) =>
    {
      float n = Fbm(u, v, 2, 3);
      float growth = SmoothStep(.6f,.8f,Fbm(u+2.1f,v+4.7f,4,2));
      return Color.Lerp(Color.Lerp(a,b,n), new Color(.25f,.33f,.34f),growth*.35f);
    });
  }

  // Warm desert/savanna sand with soft dunes and grain
  private static Texture2D BuildSand()
  {
    Color dark = new Color(0.62f, 0.47f, 0.28f);
    Color light = new Color(0.92f, 0.82f, 0.56f);

    return Build((u, v) =>
    {
      float dune = Fbm(u, v, 2, 3);
      float grain = Fbm(u, v, 24, 2) * 0.06f;
      float ripple = 0.5f + 0.5f * Mathf.Sin((v * 8f + dune * 2f) * Mathf.PI * 2f);
      float t = Mathf.Clamp01(dune * 0.90f + grain + ripple * 0.04f);
      return Color.Lerp(dark, light, t);
    });
  }

  // Dark toxic swamp with acid-green bioluminescent pools
  private static Texture2D BuildToxic()
  {
    // A calm, mostly-dark swamp: only a few soft, dim glow patches so the
    // ground does not read as busy/noisy and the path stays legible on top.
    Color deep = new Color(0.05f, 0.08f, 0.07f);
    Color mid = new Color(0.09f, 0.15f, 0.11f);
    Color glow = new Color(0.18f, 0.38f, 0.15f);

    return Build((u, v) =>
    {
      float lumps = Fbm(u, v, 3, 4);
      Color baseCol = Color.Lerp(deep, mid, Mathf.Clamp01(lumps));

      // Few, soft, dim glow patches (much subtler than before)
      float pools = Fbm(u + 5.2f, v + 1.7f, 3, 3);
      float glowMask = SmoothStep(0.74f, 0.90f, pools) * 0.55f;
      return Color.Lerp(baseCol, glow, glowMask);
    });
  }

  private static Texture2D Build(System.Func<float, float, Color> sample)
  {
    var tex = new Texture2D(Size, Size, TextureFormat.RGB24, true);
    var pixels = new Color[Size * Size];
    for (int y = 0; y < Size; y++)
    {
      for (int x = 0; x < Size; x++)
      {
        float u = (float)x / Size;
        float v = (float)y / Size;
        pixels[y * Size + x] = sample(u, v);
      }
    }
    tex.SetPixels(pixels);
    tex.Apply(true);
    tex.wrapMode = TextureWrapMode.Repeat;
    tex.anisoLevel = 4;
    return tex;
  }

  // Tileable value noise: the lattice wraps at `freq`, so it repeats seamlessly
  // across the [0,1) texture. Summed over octaves for detail.
  private static float Fbm(float u, float v, int freq, int octaves)
  {
    float sum = 0f;
    float amp = 0.5f;
    float total = 0f;
    int f = freq;
    for (int o = 0; o < octaves; o++)
    {
      sum += amp * TileNoise(u * f, v * f, f);
      total += amp;
      amp *= 0.5f;
      f *= 2;
    }
    return sum / total;
  }

  private static float TileNoise(float x, float y, int period)
  {
    int xi = Mathf.FloorToInt(x);
    int yi = Mathf.FloorToInt(y);
    float xf = x - xi;
    float yf = y - yi;
    xf = xf * xf * (3f - 2f * xf);
    yf = yf * yf * (3f - 2f * yf);

    float a = Hash(xi, yi, period);
    float b = Hash(xi + 1, yi, period);
    float c = Hash(xi, yi + 1, period);
    float d = Hash(xi + 1, yi + 1, period);
    return Mathf.Lerp(Mathf.Lerp(a, b, xf), Mathf.Lerp(c, d, xf), yf);
  }

  // GLSL-style smoothstep: 0 below edge0, 1 above edge1, smooth between.
  // (Unity's Mathf.SmoothStep interpolates BETWEEN the edges instead.)
  private static float SmoothStep(float edge0, float edge1, float x)
  {
    float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
    return t * t * (3f - 2f * t);
  }

  private static float Hash(int x, int y, int period)
  {
    // Wrap the lattice so opposite edges of the texture match
    x = ((x % period) + period) % period;
    y = ((y % period) + period) % period;
    int h = x * 374761393 + y * 668265263;
    h = (h ^ (h >> 13)) * 1274126177;
    h = h ^ (h >> 16);
    return (h & 0xFFFF) / 65535f;
  }
}
