using System.Collections.Generic;
using UnityEngine;

// Builds the scenery geometry in code. Everything here is real generated mesh —
// subdivided, noise-displaced and flat-shaded — rather than Unity primitives, so
// rocks read as weathered boulders and the island sits on one sculpted cliff
// instead of a stack of boxes.
//
// Meshes are cached by key, so scattering fifty props reuses a handful of
// variants and keeps the batching cheap on mobile.
public static class MeshFactory
{
  private static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

  private static Mesh Cached(string key, System.Func<Mesh> build)
  {
    if (cache.TryGetValue(key, out Mesh existing) && existing != null) return existing;
    Mesh built = build();
    built.name = key;
    cache[key] = built;
    return built;
  }

  // ---------------------------------------------------------------- props

  // A weathered boulder: a subdivided sphere pushed around by 3D noise, squashed
  // on the underside so it rests on the ground instead of looking half-buried.
  public static Mesh Boulder(int variant) => Cached("boulder" + variant, () =>
  {
    int seed = 7919 * (variant + 1);
    var rng = new System.Random(seed);
    var stretch = new Vector3(
      1f + (float)rng.NextDouble() * 0.55f,
      0.60f + (float)rng.NextDouble() * 0.38f,
      0.85f + (float)rng.NextDouble() * 0.55f);
    var offset = new Vector3(seed % 17, seed % 23, seed % 31);

    Sphere(2, out List<Vector3> pts, out List<int> idx);
    for (int i = 0; i < pts.Count; i++)
    {
      Vector3 dir = pts[i];
      float n = Fbm(dir * 1.7f + offset, 3, seed);
      Vector3 v = Vector3.Scale(dir * (0.74f + n * 0.62f), stretch);
      if (v.y < 0f) v.y *= 0.45f;
      pts[i] = v;
    }
    return FlatShade(pts, idx, true);
  });

  // A leafy clump: a few overlapping noise-displaced blobs merged into one mesh.
  public static Mesh Bush(int variant) => Cached("bush" + variant, () =>
  {
    int seed = 6131 * (variant + 1);
    var rng = new System.Random(seed);
    var pts = new List<Vector3>();
    var idx = new List<int>();

    int blobs = 3 + rng.Next(3);
    for (int b = 0; b < blobs; b++)
    {
      Sphere(1, out List<Vector3> bp, out List<int> bi);
      var centre = new Vector3(
        (float)(rng.NextDouble() * 2 - 1) * 0.55f,
        (float)rng.NextDouble() * 0.45f,
        (float)(rng.NextDouble() * 2 - 1) * 0.55f);
      float radius = 0.5f + (float)rng.NextDouble() * 0.35f;
      var noiseOffset = new Vector3(b * 13.7f, b * 5.1f, b * 9.3f);

      int start = pts.Count;
      for (int i = 0; i < bp.Count; i++)
      {
        Vector3 dir = bp[i];
        float n = Fbm(dir * 2.6f + noiseOffset, 2, seed + b);
        pts.Add(centre + dir * radius * (0.78f + n * 0.5f));
      }
      for (int i = 0; i < bi.Count; i++) idx.Add(bi[i] + start);
    }
    return FlatShade(pts, idx, true);
  });

  // A faceted crystal shard: a tapered prism with a slight twist and a point.
  public static Mesh Crystal(int variant) => Cached("crystal" + variant, () =>
  {
    int seed = 4703 * (variant + 1);
    var rng = new System.Random(seed);
    int sides = 5 + rng.Next(2);
    float twist = (float)(rng.NextDouble() * 0.5 - 0.25);
    float lean = (float)(rng.NextDouble() * 0.22);

    var b = new Builder();
    // Widest a little above the base, then tapering to a tip
    float[] levels = { 0f, 0.18f, 0.72f, 1f };
    float[] radii = { 0.34f, 0.5f, 0.28f, 0f };

    for (int l = 0; l < levels.Length - 1; l++)
    {
      for (int s = 0; s < sides; s++)
      {
        Vector3 a0 = Shard(s, sides, levels[l], radii[l], twist, lean);
        Vector3 a1 = Shard(s + 1, sides, levels[l], radii[l], twist, lean);
        Vector3 b0 = Shard(s, sides, levels[l + 1], radii[l + 1], twist, lean);
        Vector3 b1 = Shard(s + 1, sides, levels[l + 1], radii[l + 1], twist, lean);
        if (radii[l + 1] <= 0f) b.Face(a0, a1, b0, UV(a0), UV(a1), UV(b0));
        else b.Quad(a0, a1, b1, b0, UV(a0), UV(a1), UV(b1), UV(b0));
      }
    }
    return b.ToMesh();
  });

  private static Vector3 Shard(int side, int sides, float t, float radius, float twist, float lean)
  {
    float ang = (side / (float)sides) * Mathf.PI * 2f + t * twist;
    return new Vector3(Mathf.Cos(ang) * radius + t * t * lean, t, Mathf.Sin(ang) * radius);
  }

  // A bent, tapering trunk. Unit height, so callers scale it freely.
  public static Mesh TreeTrunk(int variant) => Cached("trunk" + variant, () =>
  {
    int seed = 3559 * (variant + 1);
    var rng = new System.Random(seed);
    float bendX = (float)(rng.NextDouble() * 0.24 - 0.12);
    float bendZ = (float)(rng.NextDouble() * 0.24 - 0.12);
    const int rings = 7, sides = 7;

    var b = new Builder();
    for (int r = 0; r < rings; r++)
    {
      float t0 = r / (float)rings;
      float t1 = (r + 1) / (float)rings;
      for (int s = 0; s < sides; s++)
      {
        Vector3 a0 = Trunk(s, sides, t0, bendX, bendZ, seed);
        Vector3 a1 = Trunk(s + 1, sides, t0, bendX, bendZ, seed);
        Vector3 c0 = Trunk(s, sides, t1, bendX, bendZ, seed);
        Vector3 c1 = Trunk(s + 1, sides, t1, bendX, bendZ, seed);
        b.Quad(a0, a1, c1, c0, new Vector2(0f, t0), new Vector2(1f, t0), new Vector2(1f, t1), new Vector2(0f, t1));
      }
    }
    return b.ToMesh();
  });

  private static Vector3 Trunk(int side, int sides, float t, float bendX, float bendZ, int seed)
  {
    float ang = (side / (float)sides) * Mathf.PI * 2f;
    // Thick, flared base tapering to a thin top, with a little bark wobble
    float radius = Mathf.Lerp(0.16f, 0.07f, Mathf.Sqrt(t));
    radius *= 1f + 0.16f * (Noise(new Vector3(Mathf.Cos(ang) * 2f, t * 6f, Mathf.Sin(ang) * 2f), seed) - 0.5f);
    return new Vector3(Mathf.Cos(ang) * radius + bendX * t * t, t, Mathf.Sin(ang) * radius + bendZ * t * t);
  }

  // The canopy that sits on a trunk: overlapping displaced blobs, wider than
  // tall so it reads as a leafy crown rather than a ball.
  public static Mesh TreeFoliage(int variant) => Cached("foliage" + variant, () =>
  {
    int seed = 2477 * (variant + 1);
    var rng = new System.Random(seed);
    var pts = new List<Vector3>();
    var idx = new List<int>();

    int blobs = 3 + rng.Next(2);
    for (int b = 0; b < blobs; b++)
    {
      Sphere(2, out List<Vector3> bp, out List<int> bi);
      var centre = new Vector3(
        (float)(rng.NextDouble() * 2 - 1) * 0.34f,
        (float)(rng.NextDouble() * 2 - 1) * 0.26f,
        (float)(rng.NextDouble() * 2 - 1) * 0.34f);
      float radius = 0.52f + (float)rng.NextDouble() * 0.28f;
      var noiseOffset = new Vector3(b * 21.3f, b * 7.7f, b * 15.1f);

      int start = pts.Count;
      for (int i = 0; i < bp.Count; i++)
      {
        Vector3 dir = bp[i];
        float n = Fbm(dir * 2.2f + noiseOffset, 3, seed + b);
        Vector3 v = dir * radius * (0.8f + n * 0.42f);
        v.y *= 0.82f;
        pts.Add(centre + v);
      }
      for (int i = 0; i < bi.Count; i++) idx.Add(bi[i] + start);
    }
    return FlatShade(pts, idx, false);
  });

  // A patch of grass blades baked into a single mesh — one draw call per patch
  // rather than per blade. Blades are single-sided, so the material must render
  // double-sided (_Cull = Off).
  public static Mesh GrassPatch(int variant) => Cached("grass" + variant, () =>
  {
    int seed = 8171 * (variant + 1);
    var rng = new System.Random(seed);
    var b = new Builder();

    const int blades = 34;
    for (int i = 0; i < blades; i++)
    {
      // Scattered over a unit disc, denser toward the middle
      float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
      float dist = Mathf.Sqrt((float)rng.NextDouble());
      var root = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);

      float yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
      var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
      var lean = new Vector3(-side.z, 0f, side.x) * (float)(rng.NextDouble() * 0.7 + 0.2);
      float height = 0.55f + (float)rng.NextDouble() * 0.65f;
      float width = 0.055f + (float)rng.NextDouble() * 0.035f;

      const int segments = 3;
      for (int s = 0; s < segments; s++)
      {
        float t0 = s / (float)segments;
        float t1 = (s + 1) / (float)segments;
        Vector3 p0 = root + Vector3.up * (height * t0) + lean * (t0 * t0);
        Vector3 p1 = root + Vector3.up * (height * t1) + lean * (t1 * t1);
        float w0 = width * (1f - t0);
        float w1 = width * (1f - t1);

        if (w1 <= 0.001f)
          b.Face(p0 - side * w0, p0 + side * w0, p1, new Vector2(0f, t0), new Vector2(1f, t0), new Vector2(0.5f, t1));
        else
          b.Quad(p0 - side * w0, p0 + side * w0, p1 + side * w1, p1 - side * w1,
            new Vector2(0f, t0), new Vector2(1f, t0), new Vector2(1f, t1), new Vector2(0f, t1));
      }
    }
    return b.ToMesh();
  });

  // A cumulus puff: a cluster of overlapping noise-displaced lobes, wider than
  // tall and flattened underneath, the way a real cloud sits on its own base.
  // UV.y runs 0 at the base to 1 at the top so a gradient can shade the
  // underside — that vertical shading is most of what makes it read as fluffy
  // rather than as a flat blob.
  public static Mesh Cloud(int variant) => Cached("cloud" + variant, () =>
  {
    int seed = 9391 * (variant + 1);
    var rng = new System.Random(seed);
    var verts = new List<Vector3>();
    var norms = new List<Vector3>();
    var tris = new List<int>();

    int lobes = 8 + rng.Next(5);
    for (int b = 0; b < lobes; b++)
    {
      // Low subdivision per lobe: the lumpy silhouette comes from the number of
      // lobes, not from detail within one, and smooth normals hide the facets.
      Sphere(1, out List<Vector3> bp, out List<int> bi);

      var centre = new Vector3(
        (float)(rng.NextDouble() * 2 - 1) * 1.4f,
        (float)rng.NextDouble() * 0.36f,
        (float)(rng.NextDouble() * 2 - 1) * 0.7f);

      // Lobes bunch up in the middle and shrink toward the ends, giving the
      // cloud a heaped silhouette instead of an even sausage.
      float radius = 0.40f + (float)rng.NextDouble() * 0.38f;
      radius *= Mathf.Lerp(1.2f, 0.62f, Mathf.Abs(centre.x) / 1.4f);
      var noiseOffset = new Vector3(b * 17.9f, b * 6.3f, b * 11.7f);

      int start = verts.Count;
      for (int i = 0; i < bp.Count; i++)
      {
        Vector3 dir = bp[i];
        float n = Fbm(dir * 2.4f + noiseOffset, 3, seed + b);
        Vector3 v = centre + dir * radius * (0.82f + n * 0.4f);
        if (v.y < 0f) v.y *= 0.34f;   // flat base
        verts.Add(v);
        // Smooth normals, unlike every other mesh here: faceting reads as
        // carved stone, and a cloud has to read as soft.
        norms.Add(dir);
      }
      for (int i = 0; i < bi.Count; i++) tris.Add(bi[i] + start);
    }

    // Normalise to roughly a unit-diameter footprint sitting on y=0, so callers
    // can scale it in world units like any other primitive.
    float extent = 1e-4f, minY = float.MaxValue, maxY = float.MinValue;
    for (int i = 0; i < verts.Count; i++)
    {
      extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(verts[i].x), Mathf.Abs(verts[i].z)));
      minY = Mathf.Min(minY, verts[i].y);
      maxY = Mathf.Max(maxY, verts[i].y);
    }
    float k = 0.5f / extent;
    float height = Mathf.Max((maxY - minY) * k, 1e-4f);

    var uvs = new List<Vector2>(verts.Count);
    for (int i = 0; i < verts.Count; i++)
    {
      Vector3 v = verts[i];
      v = new Vector3(v.x * k, (v.y - minY) * k, v.z * k);
      verts[i] = v;
      uvs.Add(new Vector2(0.5f, Mathf.Clamp01(v.y / height)));
    }

    var mesh = new Mesh();
    mesh.SetVertices(verts);
    mesh.SetNormals(norms);
    mesh.SetUVs(0, uvs);
    mesh.SetTriangles(tris, 0);
    mesh.RecalculateBounds();
    return mesh;
  });

  // A vertical two-colour ramp, sampled by meshes whose UV.y encodes height.
  public static Texture2D VerticalGradient(Color bottom, Color top)
  {
    const int h = 128;
    var tex = new Texture2D(4, h, TextureFormat.RGB24, true);
    var pixels = new Color[4 * h];
    for (int y = 0; y < h; y++)
    {
      Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y / (float)(h - 1)));
      for (int x = 0; x < 4; x++) pixels[y * 4 + x] = c;
    }
    tex.SetPixels(pixels);
    tex.Apply(true);
    tex.wrapMode = TextureWrapMode.Clamp;
    return tex;
  }

  // A low, wide swell of ground — used along the island border to break up the
  // dead-flat grass plane.
  public static Mesh Mound(int variant) => Cached("mound" + variant, () =>
  {
    int seed = 5273 * (variant + 1);
    var offset = new Vector3(seed % 19, seed % 29, seed % 37);

    Sphere(2, out List<Vector3> pts, out List<int> idx);
    for (int i = 0; i < pts.Count; i++)
    {
      Vector3 dir = pts[i];
      float n = Fbm(dir * 1.3f + offset, 2, seed);
      Vector3 v = dir * (0.8f + n * 0.4f);
      v.y *= 0.3f;
      // Everything below the ground plane collapses flat so the mound merges
      // into the terrain instead of showing a buried hemisphere edge.
      if (v.y < 0f) v.y = 0f;
      pts[i] = v;
    }
    return FlatShade(pts, idx, false);
  });

  // ---------------------------------------------------------------- cliff

  // The mass of rock under the island: one sculpted, tapering, layered shell
  // rather than stacked boxes. The top ring exactly matches the board footprint
  // so it seams with the soil slab above it; UV.y runs 0 (top) to 1 (tip) so a
  // strata texture can shade it top to bottom.
  public static Mesh Cliff(float halfW, float halfD, float depth) =>
    Cached($"cliff{halfW:F1}x{halfD:F1}x{depth:F1}", () =>
    {
      const int sides = 48, rows = 12, seed = 1301;
      var b = new Builder();

      // The underside closes with a broad, nearly flat floor — a deeper dome
      // put a visible crease down the middle and read as a boat hull.
      var floorCentre = new Vector3(0f, -depth * 1.04f, 0f);

      for (int r = 0; r < rows; r++)
      {
        float t0 = r / (float)rows;
        float t1 = (r + 1) / (float)rows;
        bool last = r == rows - 1;

        for (int s = 0; s < sides; s++)
        {
          Vector3 a0 = CliffPoint(s, sides, t0, halfW, halfD, depth, seed);
          Vector3 a1 = CliffPoint(s + 1, sides, t0, halfW, halfD, depth, seed);
          Vector3 c0 = CliffPoint(s, sides, t1, halfW, halfD, depth, seed);
          Vector3 c1 = CliffPoint(s + 1, sides, t1, halfW, halfD, depth, seed);

          var u0 = new Vector2(s / (float)sides, t0);
          var u1 = new Vector2((s + 1) / (float)sides, t0);
          var v1 = new Vector2((s + 1) / (float)sides, t1);
          var v0 = new Vector2(s / (float)sides, t1);

          b.Quad(a0, a1, c1, c0, u0, u1, v1, v0);

          // Fan the final ring into the centre to close the floor
          if (last) b.Face(c1, c0, floorCentre, v1, v0, new Vector2(0.5f, 1f));
        }
      }
      return b.ToMesh();
    });

  private static Vector3 CliffPoint(int side, int sides, float t, float halfW, float halfD, float depth, int seed)
  {
    float ang = (side / (float)sides) * Mathf.PI * 2f;
    float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);

    // Rounded-rectangle footprint (superellipse) matching the board's shape
    const float power = 4f;
    float fx = Mathf.Sign(cos) * Mathf.Pow(Mathf.Abs(cos), 2f / power);
    float fz = Mathf.Sign(sin) * Mathf.Pow(Mathf.Abs(sin), 2f / power);

    // An elliptical profile: near-vertical where it meets the turf, then
    // curving under to a wide floor (about 0.42 of the island's footprint)
    // instead of running down to a point. Shallow and slightly domed, rather
    // than a deep spike hanging below the board.
    float taper = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t * 0.58f));

    // Detail runs VERTICALLY: ridges and gullies around the circumference that
    // hold their shape all the way down, so the face reads as carved columns.
    // Two frequencies, both well under the segment count to avoid aliasing.
    float ridges = 1f
      + 0.10f * Mathf.Sin(ang * 9f)
      + 0.04f * Mathf.Sin(ang * 19f + 1.3f);

    // The noise varies quickly with angle but only slowly with depth, so it
    // roughens the silhouette without introducing horizontal bumps.
    float rough = 1f + (Fbm(new Vector3(cos * 2.6f, t * 1.2f, sin * 2.6f), 3, seed) - 0.5f) * 0.16f;

    // The very top stays clean so it meets the soil slab without a seam
    float ramp = Mathf.Clamp01(t / 0.06f);
    float scale = taper * Mathf.Lerp(1f, ridges * rough, ramp);
    return new Vector3(fx * halfW * scale, -t * depth, fz * halfD * scale);
  }

  // A flat annulus lying in the XZ plane, outer radius 0.5. Used for the
  // expanding shockwave when a tower is placed.
  public static Mesh Ring(float innerRatio = 0.72f) => Cached($"ring{innerRatio:F2}", () =>
  {
    const int segments = 48;
    var b = new Builder();
    float outer = 0.5f, inner = 0.5f * Mathf.Clamp01(innerRatio);

    for (int s = 0; s < segments; s++)
    {
      float a0 = (s / (float)segments) * Mathf.PI * 2f;
      float a1 = ((s + 1) / (float)segments) * Mathf.PI * 2f;

      var o0 = new Vector3(Mathf.Cos(a0) * outer, 0f, Mathf.Sin(a0) * outer);
      var o1 = new Vector3(Mathf.Cos(a1) * outer, 0f, Mathf.Sin(a1) * outer);
      var i0 = new Vector3(Mathf.Cos(a0) * inner, 0f, Mathf.Sin(a0) * inner);
      var i1 = new Vector3(Mathf.Cos(a1) * inner, 0f, Mathf.Sin(a1) * inner);

      // Wound so the face points up
      b.Quad(i0, o0, o1, i1, UV(i0), UV(o0), UV(o1), UV(i1));
    }
    return b.ToMesh();
  });

  // A vertical strata texture for the cliff: banded rock layers running top to
  // bottom, so the cliff face reads as sedimentary rock rather than flat colour.
  public static Texture2D StrataTexture(Color top, Color bottom)
  {
    const int w = 64, h = 256;
    var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
    var pixels = new Color[w * h];
    for (int y = 0; y < h; y++)
    {
      // Row 0 of the texture is UV.y = 0, which is the top of the cliff
      float t = y / (float)(h - 1);
      Color layer = Color.Lerp(top, bottom, Mathf.SmoothStep(0f, 1f, t));

      // A dark line right under the turf, so the grass rim reads as sitting on
      // the rock rather than being painted onto it
      float rim = 1f - 0.38f * Mathf.Exp(-t * 34f);

      for (int x = 0; x < w; x++)
      {
        // Streaks run down the face (they vary with x, which is the angle
        // around the cliff). Horizontal banding was the texture equivalent of
        // the ledges and read as sedimentary shelves.
        float streak = Noise(new Vector3(x * 1.6f, 0f, 0f), 733);
        float fine = Noise(new Vector3(x * 5.5f, 0f, 0f), 517);
        float grain = Noise(new Vector3(x * 0.35f, y * 0.6f, 0f), 991);

        float shade = (0.78f + streak * 0.26f + fine * 0.08f + (grain - 0.5f) * 0.10f) * rim;
        pixels[y * w + x] = layer * shade;
      }
    }
    tex.SetPixels(pixels);
    tex.Apply(true);
    tex.wrapMode = TextureWrapMode.Repeat;
    return tex;
  }

  // ---------------------------------------------------------------- geometry

  // Flat-shaded output: every triangle gets its own three vertices and one face
  // normal, which is what gives the faceted, hand-carved look.
  private static Mesh FlatShade(List<Vector3> pts, List<int> idx, bool restOnGround, bool verticalUv = false)
  {
    if (restOnGround)
    {
      float minY = float.MaxValue;
      for (int i = 0; i < pts.Count; i++) minY = Mathf.Min(minY, pts[i].y);
      for (int i = 0; i < pts.Count; i++) pts[i] -= new Vector3(0f, minY, 0f);
    }

    // UV.y = normalised height, for meshes shaded by a vertical gradient
    float height = 1f;
    if (verticalUv)
    {
      float maxY = 0f;
      for (int i = 0; i < pts.Count; i++) maxY = Mathf.Max(maxY, pts[i].y);
      height = Mathf.Max(maxY, 1e-4f);
    }

    var b = new Builder();
    for (int i = 0; i < idx.Count; i += 3)
    {
      Vector3 a = pts[idx[i]], c = pts[idx[i + 1]], d = pts[idx[i + 2]];
      if (verticalUv) b.Face(a, c, d, VUV(a, height), VUV(c, height), VUV(d, height));
      else b.Face(a, c, d, UV(a), UV(c), UV(d));
    }
    return b.ToMesh();
  }

  private static Vector2 VUV(Vector3 p, float height) => new Vector2(0.5f, Mathf.Clamp01(p.y / height));

  private static Vector2 UV(Vector3 p) => new Vector2(p.x * 0.5f + 0.5f, p.z * 0.5f + 0.5f);

  // An icosphere: uniform triangles with no pole pinching, which matters once
  // the vertices get displaced by noise.
  private static void Sphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
  {
    float g = (1f + Mathf.Sqrt(5f)) / 2f;
    verts = new List<Vector3>
    {
      new Vector3(-1f, g, 0f), new Vector3(1f, g, 0f), new Vector3(-1f, -g, 0f), new Vector3(1f, -g, 0f),
      new Vector3(0f, -1f, g), new Vector3(0f, 1f, g), new Vector3(0f, -1f, -g), new Vector3(0f, 1f, -g),
      new Vector3(g, 0f, -1f), new Vector3(g, 0f, 1f), new Vector3(-g, 0f, -1f), new Vector3(-g, 0f, 1f),
    };
    for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;

    tris = new List<int>
    {
      0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
      1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
      3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
      4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
    };

    for (int s = 0; s < subdivisions; s++)
    {
      var midpoints = new Dictionary<long, int>();
      var next = new List<int>(tris.Count * 4);
      for (int i = 0; i < tris.Count; i += 3)
      {
        int a = tris[i], b = tris[i + 1], c = tris[i + 2];
        int ab = Midpoint(a, b, verts, midpoints);
        int bc = Midpoint(b, c, verts, midpoints);
        int ca = Midpoint(c, a, verts, midpoints);
        next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
      }
      tris = next;
    }
  }

  private static int Midpoint(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
  {
    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
    if (cache.TryGetValue(key, out int found)) return found;
    verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
    int index = verts.Count - 1;
    cache[key] = index;
    return index;
  }

  // ---------------------------------------------------------------- noise

  private static float Fbm(Vector3 p, int octaves, int seed)
  {
    float sum = 0f, amp = 0.5f, total = 0f;
    for (int o = 0; o < octaves; o++)
    {
      sum += amp * Noise(p, seed + o * 131);
      total += amp;
      amp *= 0.5f;
      p *= 2f;
    }
    return sum / total;
  }

  private static float Noise(Vector3 p, int seed)
  {
    int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
    float xf = Smooth(p.x - xi), yf = Smooth(p.y - yi), zf = Smooth(p.z - zi);

    float x00 = Mathf.Lerp(Hash(xi, yi, zi, seed), Hash(xi + 1, yi, zi, seed), xf);
    float x10 = Mathf.Lerp(Hash(xi, yi + 1, zi, seed), Hash(xi + 1, yi + 1, zi, seed), xf);
    float x01 = Mathf.Lerp(Hash(xi, yi, zi + 1, seed), Hash(xi + 1, yi, zi + 1, seed), xf);
    float x11 = Mathf.Lerp(Hash(xi, yi + 1, zi + 1, seed), Hash(xi + 1, yi + 1, zi + 1, seed), xf);

    return Mathf.Lerp(Mathf.Lerp(x00, x10, yf), Mathf.Lerp(x01, x11, yf), zf);
  }

  private static float Smooth(float t) => t * t * (3f - 2f * t);

  private static float Hash(int x, int y, int z, int seed)
  {
    int h = x * 374761393 + y * 668265263 + z * 1440662683 + seed * 1013904223;
    h = (h ^ (h >> 13)) * 1274126177;
    h ^= h >> 16;
    return (h & 0xFFFF) / 65535f;
  }

  // ---------------------------------------------------------------- builder

  // Accumulates flat-shaded triangles. Each Face() emits three fresh vertices
  // sharing one normal, so nothing is smoothed across an edge.
  private class Builder
  {
    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> tris = new List<int>();

    public void Face(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
    {
      Vector3 n = Vector3.Cross(b - a, c - a);
      n = n.sqrMagnitude < 1e-12f ? Vector3.up : n.normalized;

      int i = verts.Count;
      verts.Add(a); verts.Add(b); verts.Add(c);
      normals.Add(n); normals.Add(n); normals.Add(n);
      uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
      tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
    {
      Face(a, b, c, ua, ub, uc);
      Face(a, c, d, ua, uc, ud);
    }

    public Mesh ToMesh()
    {
      var mesh = new Mesh();
      if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      mesh.SetVertices(verts);
      mesh.SetNormals(normals);
      mesh.SetUVs(0, uvs);
      mesh.SetTriangles(tris, 0);
      mesh.RecalculateBounds();
      return mesh;
    }
  }
}
