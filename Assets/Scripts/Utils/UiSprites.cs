using System.Collections.Generic;
using UnityEngine;

// Generates the UI's shapes in code. Every Image in the project was using
// Unity's built-in 1x1 white sprite, which is why the interface read as hard
// grey boxes; these are antialiased, 9-sliced rounded shapes that tint to any
// colour and scale without distorting their corners.
//
// Sprites are cached per shape for the session. Like StarSprite, this exists
// because the project ships no UI art and the TMP atlas is ASCII-only, so icons
// cannot be glyphs.
public static class UiSprites
{
  private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

  private static Sprite Cached(string key, System.Func<Sprite> build)
  {
    if (cache.TryGetValue(key, out Sprite existing) && existing != null) return existing;
    Sprite built = build();
    built.name = key;
    cache[key] = built;
    return built;
  }

  // A filled rounded rectangle. Use with Image.type = Sliced.
  public static Sprite Panel(int radius = 18) =>
    Cached("panel" + radius, () => RoundedRect(radius, 0f, 0f));

  // A rounded rectangle drawn as an outline only.
  public static Sprite Outline(int radius = 18, float width = 3f) =>
    Cached($"outline{radius}_{width}", () => RoundedRect(radius, width, 0f));

  // A filled rounded rectangle carrying a soft top-to-bottom shade, so buttons
  // read as slightly domed rather than flat. Image.color multiplies over it.
  public static Sprite Button(int radius = 18) =>
    Cached("button" + radius, () => RoundedRect(radius, 0f, 0.30f));

  public static Sprite Circle(int size = 64) => Cached("circle" + size, () =>
  {
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    var px = new Color32[size * size];
    float half = size * 0.5f, r = half - 0.5f;
    for (int y = 0; y < size; y++)
    {
      for (int x = 0; x < size; x++)
      {
        float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - r;
        px[y * size + x] = White(Mathf.Clamp01(0.5f - d));
      }
    }
    return Finish(tex, px, size, Vector4.zero);
  });

  // A coin: filled disc with an inset ring, so gold amounts get an icon instead
  // of the word "Gold".
  public static Sprite Coin(int size = 64) => Cached("coin" + size, () =>
  {
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    var px = new Color32[size * size];
    float half = size * 0.5f, r = half - 0.5f;
    for (int y = 0; y < size; y++)
    {
      for (int x = 0; x < size; x++)
      {
        float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
        float a = Mathf.Clamp01(0.5f - (d - r));
        // Darker band just inside the rim reads as a struck edge
        float ring = Mathf.Clamp01(0.5f - Mathf.Abs(d - r * 0.72f) + 1.2f);
        float shade = Mathf.Lerp(1f, 0.68f, Mathf.Clamp01(ring));
        px[y * size + x] = new Color32(
          (byte)(255 * shade), (byte)(255 * shade), (byte)(255 * shade), (byte)(255 * a));
      }
    }
    return Finish(tex, px, size, Vector4.zero);
  });

  // A heart, for the health readout.
  public static Sprite Heart(int size = 64) => Cached("heart" + size, () =>
  {
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    var px = new Color32[size * size];

    // Supersampled: the implicit curve has no cheap distance field, so coverage
    // is estimated by sampling a 3x3 grid inside each pixel.
    for (int y = 0; y < size; y++)
    {
      for (int x = 0; x < size; x++)
      {
        int hits = 0;
        for (int sy = 0; sy < 3; sy++)
        {
          for (int sx = 0; sx < 3; sx++)
          {
            float fx = (x + (sx + 0.5f) / 3f) / size * 2f - 1f;
            float fy = (y + (sy + 0.5f) / 3f) / size * 2f - 1f;
            // The curve spans x [-1.15,1.15], y [-1,1.25]; map the sprite onto
            // that box so the lobes and the point both stay inside
            if (InHeart(fx * 1.2f, fy * 1.15f + 0.125f)) hits++;
          }
        }
        px[y * size + x] = White(hits / 9f);
      }
    }
    return Finish(tex, px, size, Vector4.zero);
  });

  // Standard implicit heart. Texture rows run bottom-up, which already matches
  // the curve's orientation, so nothing is flipped.
  // Double chevron, for the fast-forward / game-speed control.
  public static Sprite FastForward(int size = 64) => Cached("fastforward" + size, () =>
    Shape(size, (x, y) => InTriangle(x, y, 0.04f, 0.16f, 0.04f, 0.84f, 0.48f, 0.5f)
                       || InTriangle(x, y, 0.50f, 0.16f, 0.50f, 0.84f, 0.94f, 0.5f)));

  // Camera body with a lens, for the view-angle control.
  public static Sprite Camera(int size = 64) => Cached("camera" + size, () => Shape(size, (x, y) =>
  {
    bool solid = InRoundedRect(x, y, 0.05f, 0.16f, 0.95f, 0.78f, 0.10f)
              || (x > 0.32f && x < 0.60f && y >= 0.76f && y < 0.90f);   // viewfinder bump

    // Punch a ring out of the body so a lens reads at small sizes
    float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.46f) * (y - 0.46f));
    if (d < 0.23f && d > 0.13f) solid = false;
    return solid;
  }));

  // Cogwheel, for the settings control.
  public static Sprite Gear(int size = 96) => Cached("gear" + size, () => Shape(size, (x, y) =>
  {
    float dx = x - 0.5f, dy = y - 0.5f;
    float r = Mathf.Sqrt(dx * dx + dy * dy);
    float angle = Mathf.Atan2(dy, dx);

    // Eight square teeth riding on the rim
    float rim = Mathf.Cos(angle * 8f) > 0.25f ? 0.44f : 0.34f;
    return r <= rim && r >= 0.15f;   // hollow centre
  }));

  // A padlock, for locked levels and environments.
  public static Sprite Lock(int size = 64) => Cached("lock" + size, () => Shape(size, (x, y) =>
  {
    if (InRoundedRect(x, y, 0.16f, 0.08f, 0.84f, 0.55f, 0.08f)) return true;

    // Shackle: an arc above the body
    float dx = x - 0.5f, dy = y - 0.55f;
    float r = Mathf.Sqrt(dx * dx + dy * dy);
    return dy >= 0f && r <= 0.30f && r >= 0.19f;
  }));

  // Supersampled coverage, for shapes with no cheap distance field.
  private static Sprite Shape(int size, System.Func<float, float, bool> inside)
  {
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    var px = new Color32[size * size];
    for (int y = 0; y < size; y++)
    {
      for (int x = 0; x < size; x++)
      {
        int hits = 0;
        for (int sy = 0; sy < 3; sy++)
        {
          for (int sx = 0; sx < 3; sx++)
          {
            if (inside((x + (sx + 0.5f) / 3f) / size, (y + (sy + 0.5f) / 3f) / size)) hits++;
          }
        }
        px[y * size + x] = White(hits / 9f);
      }
    }
    return Finish(tex, px, size, Vector4.zero);
  }

  private static bool InRoundedRect(float x, float y, float x0, float y0, float x1, float y1, float r)
  {
    float cx = Mathf.Clamp(x, x0 + r, x1 - r);
    float cy = Mathf.Clamp(y, y0 + r, y1 - r);
    if (x < x0 || x > x1 || y < y0 || y > y1) return false;
    float dx = x - cx, dy = y - cy;
    return dx * dx + dy * dy <= r * r || (x >= x0 + r && x <= x1 - r) || (y >= y0 + r && y <= y1 - r);
  }

  private static bool InTriangle(float px, float py,
    float ax, float ay, float bx, float by, float cx, float cy)
  {
    float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
    float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
    float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
    bool neg = d1 < 0 || d2 < 0 || d3 < 0;
    bool pos = d1 > 0 || d2 > 0 || d3 > 0;
    return !(neg && pos);
  }

  private static bool InHeart(float x, float y)
  {
    float t = x * x + y * y - 1f;
    return t * t * t - x * x * y * y * y <= 0f;
  }

  // ---------------------------------------------------------------- internals

  // shade > 0 bakes a vertical light-to-dark ramp into the RGB channels.
  private static Sprite RoundedRect(int radius, float outlineWidth, float shade)
  {
    const int pad = 4;
    int size = (radius + pad) * 2;
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    var px = new Color32[size * size];

    float half = size * 0.5f - 0.5f;
    for (int y = 0; y < size; y++)
    {
      // Row 0 is the bottom of the sprite, so invert for a top-down ramp
      float v = 1f - y / (float)(size - 1);
      float tone = shade > 0f ? Mathf.Lerp(1f, 1f - shade, v * v) : 1f;

      for (int x = 0; x < size; x++)
      {
        float d = RoundedRectDistance(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f, half, half, radius);

        float a = Mathf.Clamp01(0.5f - d);
        if (outlineWidth > 0f)
        {
          // Keep only a band at the edge
          a -= Mathf.Clamp01(0.5f - (d + outlineWidth));
          a = Mathf.Clamp01(a);
        }

        byte c = (byte)Mathf.RoundToInt(255f * tone);
        px[y * size + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(255f * a));
      }
    }

    // Only the middle 2px stretches, so corners keep their radius at any size
    float b = radius + pad - 1;
    return Finish(tex, px, size, new Vector4(b, b, b, b));
  }

  private static float RoundedRectDistance(float px, float py, float halfW, float halfH, float radius)
  {
    float qx = Mathf.Abs(px) - (halfW - radius);
    float qy = Mathf.Abs(py) - (halfH - radius);
    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
    return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
  }

  private static Color32 White(float alpha) =>
    new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alpha)));

  private static Sprite Finish(Texture2D tex, Color32[] px, int size, Vector4 border)
  {
    tex.SetPixels32(px);
    tex.Apply();
    tex.wrapMode = TextureWrapMode.Clamp;
    tex.filterMode = FilterMode.Bilinear;

    // FullRect is required for 9-slicing; 100 ppu matches the canvas reference
    // so the border maps 1:1 to pixels.
    return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
      100f, 0, SpriteMeshType.FullRect, border);
  }
}
