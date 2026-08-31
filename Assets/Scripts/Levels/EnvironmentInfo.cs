using System.Collections.Generic;
using UnityEngine;

// Names and card art for the seven environments.
//
// IMPORTANT: `LevelConfig.environmentName` ("Environment 3") is a PERSISTENCE
// KEY — it is baked into every level asset and into the PlayerPrefs keys
// `HighestCompletedLevel_<name>` and `Stars_<name>_<level>`. Renaming it would
// silently wipe every player's progress. So the pretty name lives here as a
// display layer on top of the key, and the key never changes.
//
// Card art is generated from the biome's OWN palette and ground texture rather
// than a rendered screenshot: it is a few kilobytes instead of a 640x400 PNG,
// it can never drift out of sync with how the level actually looks, and a flat
// sky-over-ground band reads as a place at thumbnail size where a 3D render of
// a tower board just reads as clutter.
public static class EnvironmentInfo
{
  private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
  {
    { "Environment 1", "Verdant Meadow" },
    { "Environment 2", "Sunset Wetland" },
    { "Environment 3", "Toxic Marsh" },
    { "Environment 4", "Frozen Tundra" },
    { "Environment 5", "Volcanic Ashlands" },
    { "Environment 6", "Alien Bloom" },
    { "Environment 7", "Blossom Grove" },
  };

  public static string DisplayName(string environmentName)
  {
    if (environmentName != null && Names.TryGetValue(environmentName, out string pretty))
    {
      return pretty;
    }
    return environmentName ?? "Unknown";
  }

  // The biome's signature colour, used for its name banner and for the level
  // tiles inside it so the two screens read as one place.
  //
  // Chosen here rather than derived from Palette.accentGlow: those are tuned to
  // bloom against a dark 3D scene, and reading them straight into UI gave the
  // meadow an ice-blue banner all but identical to the tundra's. These are
  // picked to name the biome at a glance and to stay distinct from each other.
  private static readonly Dictionary<string, Color> Accents = new Dictionary<string, Color>
  {
    { "Environment 1", new Color(0.44f, 0.79f, 0.31f) },  // grass green
    { "Environment 2", new Color(0.98f, 0.66f, 0.20f) },  // sunset amber
    { "Environment 3", new Color(0.66f, 0.94f, 0.26f) },  // acid lime
    { "Environment 4", new Color(0.55f, 0.82f, 0.98f) },  // ice blue
    { "Environment 5", new Color(0.95f, 0.36f, 0.20f) },  // lava red
    { "Environment 6", new Color(0.92f, 0.35f, 0.86f) },  // bioluminescent magenta
    { "Environment 7", new Color(0.99f, 0.60f, 0.74f) },  // blossom pink
  };

  public static Color AccentFor(string environmentName)
  {
    if (environmentName != null && Accents.TryGetValue(environmentName, out Color accent))
    {
      return accent;
    }
    return UiSkin.Primary;
  }

  private const int CardWidth = 320;
  private const int CardHeight = 180;
  private static readonly Dictionary<string, Sprite> CardCache = new Dictionary<string, Sprite>();

  // A small landscape of the biome: its sky gradient above, its real ground
  // texture below, divided by the haze band the level itself uses.
  public static Sprite CardArt(string environmentName)
  {
    string key = environmentName ?? "Environment 1";
    if (CardCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

    EnvironmentTheme.Palette p = EnvironmentTheme.PaletteFor(key);
    var tex = new Texture2D(CardWidth, CardHeight, TextureFormat.RGBA32, false)
    {
      wrapMode = TextureWrapMode.Clamp,
      filterMode = FilterMode.Bilinear,
    };

    Texture2D ground = EnvironmentTheme.ResolveGround(p.ground) as Texture2D;
    // Env 6 lifts a near-black ground with a tint ABOVE 1, so this has to be
    // applied the same way here or the card renders as a black slab.
    Color tint = p.groundTint;

    const float horizon = 0.46f;   // share of the card height that is sky
    var pixels = new Color[CardWidth * CardHeight];

    for (int y = 0; y < CardHeight; y++)
    {
      float v = (float)y / (CardHeight - 1);   // 0 at the bottom
      for (int x = 0; x < CardWidth; x++)
      {
        Color c;
        if (v >= horizon)
        {
          // Sky: horizon colour up to the zenith.
          float t = Mathf.InverseLerp(horizon, 1f, v);
          c = Color.Lerp(p.skyHorizon, p.skyTop, t * t);
        }
        else
        {
          // Ground: the real texture, tinted, darkening toward the front edge
          // so the band has depth instead of reading as flat wallpaper.
          float t = Mathf.InverseLerp(horizon, 0f, v);
          Color soil = p.soilColor;
          if (ground != null && ground.isReadable)
          {
            // Sample with increasing scale toward the viewer for a cheap sense
            // of perspective.
            float u = (float)x / CardWidth * (1f + t * 2f);
            float gv = t * 0.55f;
            soil = ground.GetPixelBilinear(u, gv);
            soil = new Color(soil.r * tint.r, soil.g * tint.g, soil.b * tint.b, 1f);
          }
          c = Color.Lerp(soil, soil * 0.72f, t);

          // Blend the first few rows into the haze so there is no hard seam.
          float blend = Mathf.InverseLerp(horizon - 0.06f, horizon, v);
          c = Color.Lerp(c, p.hazeColor, blend * 0.55f);
        }

        // A soft vignette keeps the thumbnail from fighting the card border.
        float dx = (x / (float)CardWidth - 0.5f) * 2f;
        float dy = (v - 0.5f) * 2f;
        float vig = 1f - 0.18f * Mathf.Clamp01(dx * dx + dy * dy - 0.35f);
        c *= vig;

        c.a = 1f;
        pixels[y * CardWidth + x] = c;
      }
    }

    tex.SetPixels(pixels);
    tex.Apply(false, false);

    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, CardWidth, CardHeight),
      new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    CardCache[key] = sprite;
    return sprite;
  }
}
