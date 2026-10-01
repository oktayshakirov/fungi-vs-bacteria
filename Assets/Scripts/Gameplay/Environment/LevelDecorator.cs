using System.Collections.Generic;
using UnityEngine;

// Dresses the level at load: sculpts the island cliff, scatters scenery in the
// border ring, and builds a spawn portal at the path start and a base structure
// at the path end so the board reads as a place being defended. Runs after
// PathManager and EnvironmentTheme (execution order) so the path and palette
// are ready.
//
// All geometry comes from MeshFactory — noise-displaced, flat-shaded meshes
// rather than Unity primitives.
[DefaultExecutionOrder(100)]
public class LevelDecorator : MonoBehaviour
{
  // How many distinct meshes are generated per prop type. Props pick one at
  // random, so the scatter looks varied while staying cheap to batch.
  private const int Variants = 5;

  // Props in front of the board hide it. The camera looks down +Z, so a prop's
  // normalised depth (0 = nearest the camera, 1 = far edge) decides how tall it
  // is allowed to be: only the back of the island gets trees.
  private const float ShortBand = 0.40f;   // below this, foreground — keep it low
  private const float TallBand = 0.58f;    // above this, trees are allowed

  private readonly List<GameObject> spawned = new List<GameObject>();
  private Material[] rockMats, plantMats, woodMats;
  private Material grassMat, structureMat, glowMat;

  // Derived from the level so every level lays its scenery out differently,
  // while staying identical each time that level is replayed.
  private int levelSeed;

  private void Start()
  {
    Vector3[] pts = PathManager.Instance != null ? PathManager.Instance.GetPathPoints() : null;
    BuildAt(pts);
  }

  // Path points are passed in so the same build works at runtime (from
  // PathManager) and in the editor preview tool.
  public void BuildAt(Vector3[] pathPoints)
  {
    Clear();
    levelSeed = LevelSeed();

    EnvironmentTheme.Palette p = EnvironmentTheme.Current;
    // Slight per-instance colour jitter, so a scatter of rocks or bushes does
    // not read as one flat block of colour.
    rockMats = Shades(p.rockColor, 4, 0.16f);
    plantMats = Shades(p.plantColor, 5, 0.22f);
    woodMats = Shades(p.woodColor, 3, 0.14f);
    structureMat = Lit(p.structureColor);
    grassMat = GrassMat(p.grassColor);
    // Neon crystals: moderate emission keeps the hue (too high washes to white),
    // and the scene bloom adds a coloured halo.
    glowMat = Neon(p.accentGlow);

    landmarks = pathPoints != null && pathPoints.Length >= 2
      ? new[] { pathPoints[0], pathPoints[pathPoints.Length - 1] }
      : new Vector3[0];

    BuildIslandCliff(p);
    BuildDistantClouds(p);
    BuildDistantIslands(p);
    BuildFloatingDebris(p);
    ScatterProps(p);
    ScatterGrass();
    ScatterNeonOrbs(p);
    if (pathPoints != null && pathPoints.Length >= 2)
    {
      // The nest faces along the path it feeds.
      BuildPortal(pathPoints[0], pathPoints[0] - pathPoints[1]);
    }

    // ~200 scenery renderers is ~200 draw calls, which dwarfs the rest of the
    // frame on mobile. None of this moves once built, so merge it by material:
    // that collapses the scenery to roughly one batch per material.
    //
    // Must run last — combined objects can no longer be moved or reparented.
    StaticBatchingUtility.Combine(gameObject);

    // The house is built AFTER the merge, because it moves: it flinches when
    // an enemy reaches it (BaseFlinch). Six renderers, a few draw calls.
    if (pathPoints != null && pathPoints.Length >= 2)
    {
      int last = pathPoints.Length - 1;
      // Faces back up the path, toward what is coming.
      GameObject house = BuildBase(pathPoints[last], pathPoints[last - 1] - pathPoints[last]);
      if (house != null && Application.isPlaying) house.AddComponent<BaseFlinch>();
    }
  }

  private void IslandExtent(out float halfW, out float halfD)
  {
    GridManager grid = GridManager.Instance;
#if UNITY_EDITOR
    if (grid == null) grid = FindFirstObjectByType<GridManager>();
#endif
    if (grid == null) { halfW = 40f; halfD = 20f; return; }
    halfW = grid.gridSize.x * grid.cellSize * 0.5f + BoardDecor.Margin;
    halfD = grid.gridSize.y * grid.cellSize * 0.5f + BoardDecor.Margin;
  }

  // The mass of rock the island is torn from: one sculpted shell that tapers to
  // a ragged tip, textured with rock strata running top to bottom. Its top ring
  // matches the board footprint exactly, so it seams with the soil slab above.
  private void BuildIslandCliff(EnvironmentTheme.Palette p)
  {
    IslandExtent(out float halfW, out float halfD);

    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetTexture("_BaseMap", MeshFactory.StrataTexture(p.cliffTop, p.cliffBottom));
    mat.SetColor("_BaseColor", Color.white);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.04f);

    Mesh mesh = MeshFactory.Cliff(halfW, halfD, 13f);
    Piece("IslandCliff", mesh, mat, new Vector3(0f, BoardDecor.CliffTop, 0f));
  }

  // A wide field of soft clouds ringing the island in the distance and a little
  // below it, so they read as a far-off cloud layer in the sky rather than fog
  // clinging to the base. Kept out of the gap directly under the island.
  private void BuildDistantClouds(EnvironmentTheme.Palette p)
  {
    IslandExtent(out float halfW, out float halfD);
    Material cloudMat = CloudMat(p);

    System.Random rng = Rng(4);
    for (int i = 0; i < 30; i++)
    {
      // Ring from just past the island out to far away, gently below eye level
      float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
      float dist = 1.5f + (float)rng.NextDouble() * 2.8f; // 1.5x .. 4.3x island
      float x = Mathf.Cos(ang) * halfW * dist;
      float z = Mathf.Sin(ang) * halfD * dist;
      // Farther clouds sit lower, so they recede toward a horizon cloud line
      float y = -6f - (dist - 1.25f) * 8f + (float)(rng.NextDouble() * 2 - 1) * 4f;

      GameObject cloud = Piece("Cloud", MeshFactory.Cloud(rng.Next(Variants)), cloudMat,
        new Vector3(x, y, z));
      float s = 14f + (float)rng.NextDouble() * 22f;
      cloud.transform.localScale = new Vector3(s, s * (0.42f + (float)rng.NextDouble() * 0.3f), s * 0.8f);
      cloud.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
      // Far-off clouds throwing shadows across the board would be pure noise
      cloud.GetComponent<MeshRenderer>().shadowCastingMode =
        UnityEngine.Rendering.ShadowCastingMode.Off;
    }
  }

  // Small grass-topped islets floating in the distance around the play island —
  // the strongest "we are high in a sky full of floating lands" cue. Each is a
  // low mound of turf on a tapering shard of rock.
  private void BuildDistantIslands(EnvironmentTheme.Palette p)
  {
    IslandExtent(out float halfW, out float halfD);
    Material turfMat = Lit(p.grassColor);
    Material stoneMat = Lit(Color.Lerp(p.cliffBottom, p.cliffTop, 0.55f));

    System.Random rng = Rng(5);
    const int count = 6;
    for (int i = 0; i < count; i++)
    {
      float ang = (i / (float)count) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f;
      float dist = 1.9f + (float)rng.NextDouble() * 1.8f;
      var pos = new Vector3(
        Mathf.Cos(ang) * halfW * dist,
        -4f + (float)(rng.NextDouble() * 2 - 1) * 12f,
        Mathf.Sin(ang) * halfD * dist);

      var root = new GameObject("DistantIsland");
      root.transform.SetParent(transform, false);
      root.transform.position = pos;
      root.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
      spawned.Add(root);

      float w = 6f + (float)rng.NextDouble() * 10f;
      int v = rng.Next(Variants);

      GameObject turf = Piece("Turf", MeshFactory.Mound(v), turfMat, pos, root.transform);
      turf.transform.localPosition = Vector3.zero;
      turf.transform.localScale = new Vector3(w, w * 0.5f, w * 0.8f);

      // A crystal shard upside down makes a good island underside: wide where it
      // meets the turf, tapering to a point below.
      GameObject rock = Piece("Rock", MeshFactory.Crystal(v), stoneMat, pos, root.transform);
      rock.transform.localPosition = Vector3.zero;
      rock.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
      rock.transform.localScale = new Vector3(w * 1.5f, w * 1.1f, w * 1.2f);
    }
  }

  // A few small rock chunks drifting around/below the island for depth and scale.
  private void BuildFloatingDebris(EnvironmentTheme.Palette p)
  {
    IslandExtent(out float halfW, out float halfD);
    Material debrisMat = Lit(p.rockColor * 0.6f);

    System.Random rng = Rng(6);
    const int chunks = 7;
    for (int i = 0; i < chunks; i++)
    {
      float ang = (i / (float)chunks) * Mathf.PI * 2f + (float)rng.NextDouble();
      float dist = 1.35f + (float)rng.NextDouble() * 0.7f;
      var pos = new Vector3(
        Mathf.Cos(ang) * halfW * dist,
        -6f - (float)rng.NextDouble() * 22f,
        Mathf.Sin(ang) * halfD * dist);

      GameObject rock = Piece("Debris", MeshFactory.Boulder(rng.Next(Variants)), debrisMat, pos);
      rock.transform.localScale = Vector3.one * (2.5f + (float)rng.NextDouble() * 5f);
      rock.transform.rotation = Quaternion.Euler(
        (float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f);
    }
  }

  // Props live only in the border ring outside the play grid, so they never
  // interfere with tower placement.
  private void ScatterProps(EnvironmentTheme.Palette p)
  {
    if (!Ring(out float halfW, out float halfD, out float outerW, out float outerD)) return;

    System.Random rng = Rng(1);
    const int count = 56;
    int placed = 0, attempts = 0;

    while (placed < count && attempts < count * 12)
    {
      attempts++;
      float x = (float)(rng.NextDouble() * 2 - 1) * outerW;
      float z = (float)(rng.NextDouble() * 2 - 1) * outerD;
      if (Mathf.Abs(x) < halfW - 0.5f && Mathf.Abs(z) < halfD - 0.5f) continue;
      if (NearLandmark(new Vector3(x, 0f, z))) continue;

      SpawnProp(new Vector3(x, 0f, z), Mathf.InverseLerp(-outerD, outerD, z), rng);
      placed++;
    }

    // A treeline hugging the back edge, framing the board the way the reference
    // does. Safe to be tall: it is the farthest thing from the camera.
    for (int i = 0; i < 9; i++)
    {
      float x = Mathf.Lerp(-outerW, outerW, (i + 0.5f) / 9f) + (float)(rng.NextDouble() * 2 - 1) * 3f;
      float z = outerD - (float)rng.NextDouble() * 3.5f;
      if (NearLandmark(new Vector3(x, 0f, z))) continue;
      SpawnTree(new Vector3(x, 0f, z), rng);
    }

    // Low swells of turf along the border, so the grass plane is not dead flat
    // where it meets the cliff edge.
    Material moundMat = Lit(p.grassColor * 0.92f);
    placed = 0; attempts = 0;
    while (placed < 14 && attempts < 200)
    {
      attempts++;
      float x = (float)(rng.NextDouble() * 2 - 1) * outerW;
      float z = (float)(rng.NextDouble() * 2 - 1) * outerD;
      if (Mathf.Abs(x) < halfW + 1f && Mathf.Abs(z) < halfD + 1f) continue;
      if (NearLandmark(new Vector3(x, 0f, z))) continue;

      GameObject mound = Piece("Mound", MeshFactory.Mound(rng.Next(Variants)), moundMat, new Vector3(x, 0.02f, z));
      // Flattened hard toward the camera: a swell of turf in the foreground
      // becomes a green blob sitting over the board.
      float depth = Mathf.InverseLerp(-outerD, outerD, z);
      float s = (2.2f + (float)rng.NextDouble() * 2.4f) * Mathf.Lerp(0.6f, 1f, depth);
      mound.transform.localScale = new Vector3(s, s * 0.28f * Mathf.Lerp(0.45f, 1f, depth), s * (0.7f + (float)rng.NextDouble() * 0.6f));
      mound.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
      placed++;
    }
  }

  // Tufts of grass blades, baked a patch at a time so each patch is one draw
  // call. Blades are single-sided geometry, so the material renders both faces.
  private void ScatterGrass()
  {
    if (!Ring(out float halfW, out float halfD, out float outerW, out float outerD)) return;

    System.Random rng = Rng(2);
    int placed = 0, attempts = 0;
    while (placed < 34 && attempts < 500)
    {
      attempts++;
      float x = (float)(rng.NextDouble() * 2 - 1) * outerW;
      float z = (float)(rng.NextDouble() * 2 - 1) * outerD;
      if (Mathf.Abs(x) < halfW - 0.5f && Mathf.Abs(z) < halfD - 0.5f) continue;
      if (NearLandmark(new Vector3(x, 0f, z))) continue;

      GameObject patch = Piece("Grass", MeshFactory.GrassPatch(rng.Next(Variants)), grassMat, new Vector3(x, 0f, z));
      float s = 1.1f + (float)rng.NextDouble() * 1.1f;
      patch.transform.localScale = new Vector3(s, s * (0.9f + (float)rng.NextDouble() * 0.7f), s);
      patch.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
      // Thousands of thin blades casting shadows is noisy and expensive
      patch.GetComponent<MeshRenderer>().shadowCastingMode =
        UnityEngine.Rendering.ShadowCastingMode.Off;
      placed++;
    }
  }

  private bool Ring(out float halfW, out float halfD, out float outerW, out float outerD)
  {
    GridManager grid = GridManager.Instance;
#if UNITY_EDITOR
    if (grid == null) grid = FindFirstObjectByType<GridManager>();
#endif
    if (grid == null) { halfW = halfD = outerW = outerD = 0f; return false; }

    halfW = grid.gridSize.x * grid.cellSize * 0.5f;
    halfD = grid.gridSize.y * grid.cellSize * 0.5f;
    outerW = halfW + BoardDecor.Margin;
    outerD = halfD + BoardDecor.Margin;
    return true;
  }

  // Clusters of glowing shards in the biome's own accent, around the border.
  //
  // These used to be twelve lone shards in a fixed magenta / cyan / amber mix
  // on every biome, including right along the front edge. At play distance they
  // read as pastel paper cones scattered over the board, the same three colours
  // on the meadow as on the lava. Grouping them makes each one read as a
  // crystal outcrop, the accent ties it to the place, and keeping the nearest
  // band clear stops them sitting over the first row of cells.
  private void ScatterNeonOrbs(EnvironmentTheme.Palette p)
  {
    if (!Ring(out float halfW, out float halfD, out float outerW, out float outerD)) return;

    Color accent = p.accentGlow;
    Color.RGBToHSV(accent, out float h, out float sat, out float val);
    Color[] neons =
    {
      accent,
      Color.HSVToRGB(Mathf.Repeat(h + 0.06f, 1f), sat * 0.8f, Mathf.Min(1f, val * 1.05f)),
      Color.HSVToRGB(Mathf.Repeat(h - 0.05f, 1f), Mathf.Min(1f, sat * 1.1f), val * 0.9f),
    };
    var orbMats = new Material[neons.Length];
    for (int i = 0; i < neons.Length; i++) orbMats[i] = Neon(neons[i]);

    System.Random rng = Rng(3);
    int clusters = 0, attempts = 0;
    while (clusters < 5 && attempts < 300)
    {
      attempts++;
      float x = (float)(rng.NextDouble() * 2 - 1) * outerW;
      float z = (float)(rng.NextDouble() * 2 - 1) * outerD;
      if (Mathf.Abs(x) < halfW + 0.5f && Mathf.Abs(z) < halfD + 0.5f) continue;
      float depth = Mathf.InverseLerp(-outerD, outerD, z);
      if (depth < 0.22f) continue;
      if (NearLandmark(new Vector3(x, 0f, z))) continue;

      Material mat = orbMats[rng.Next(orbMats.Length)];
      int shards = 2 + rng.Next(2);
      for (int k = 0; k < shards; k++)
      {
        Vector3 offset = k == 0 ? Vector3.zero : new Vector3(
          (float)(rng.NextDouble() * 2 - 1) * 1.1f, 0f, (float)(rng.NextDouble() * 2 - 1) * 1.1f);
        GameObject orb = Piece("NeonShard", MeshFactory.Crystal(rng.Next(Variants)), mat,
          new Vector3(x, 0f, z) + offset);
        float sc = (k == 0 ? 1.5f : 0.8f + (float)rng.NextDouble() * 0.5f) * Mathf.Lerp(0.75f, 1f, depth);
        orb.transform.localScale = new Vector3(sc, sc * 1.9f, sc);
        orb.transform.rotation = Quaternion.Euler(
          (float)(rng.NextDouble() * 30 - 15), (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() * 30 - 15));
      }
      clusters++;
    }
  }

  // depth is 0 at the near edge and 1 at the far edge. Foreground props are
  // capped low and tall ones are held back, so nothing grows up over the board.
  private void SpawnProp(Vector3 pos, float depth, System.Random rng)
  {
    int kind = rng.Next(100);

    if (depth < ShortBand)
    {
      // Foreground: ground clutter only
      if (kind < 62) SpawnRock(pos, rng, 0.5f);
      else SpawnBush(pos, rng, 0.5f);
      return;
    }

    if (depth < TallBand)
    {
      if (kind < 44) SpawnRock(pos, rng, 0.8f);
      else if (kind < 84) SpawnBush(pos, rng, 0.75f);
      else SpawnCrystal(pos, rng, 0.7f);
      return;
    }

    if (kind < 26) SpawnRock(pos, rng, 1f);
    else if (kind < 50) SpawnBush(pos, rng, 1f);
    else if (kind < 88) SpawnTree(pos, rng);
    else SpawnCrystal(pos, rng, 1f);
  }

  private void SpawnRock(Vector3 pos, System.Random rng, float scale)
  {
    GameObject go = Piece("Boulder", MeshFactory.Boulder(rng.Next(Variants)), Pick(rockMats, rng), pos);
    float s = (1.0f + (float)rng.NextDouble() * 2.0f) * scale;
    go.transform.localScale = new Vector3(s, s * (0.7f + (float)rng.NextDouble() * 0.5f), s);
    go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
  }

  private void SpawnBush(Vector3 pos, System.Random rng, float scale)
  {
    GameObject go = Piece("Bush", MeshFactory.Bush(rng.Next(Variants)), Pick(plantMats, rng), pos);
    float s = (1.1f + (float)rng.NextDouble() * 1.2f) * scale;
    go.transform.localScale = new Vector3(s, s * 0.85f, s);
    go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
  }

  // Trunk and canopy are separate children so each takes its own material.
  private void SpawnTree(Vector3 pos, System.Random rng)
  {
    var root = new GameObject("Tree");
    root.transform.SetParent(transform, false);
    root.transform.position = pos;
    root.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
    spawned.Add(root);

    int v = rng.Next(Variants);
    float height = 3.4f + (float)rng.NextDouble() * 2.0f;
    float girth = height * 0.55f;

    GameObject trunk = Piece("Trunk", MeshFactory.TreeTrunk(v), Pick(woodMats, rng), pos, root.transform);
    trunk.transform.localPosition = Vector3.zero;
    trunk.transform.localScale = new Vector3(girth, height, girth);

    GameObject crown = Piece("Crown", MeshFactory.TreeFoliage(v), Pick(plantMats, rng), pos, root.transform);
    // The trunk bends as it rises, so the crown follows its tip
    crown.transform.localPosition = new Vector3(0f, height * 0.94f, 0f);
    crown.transform.localScale = Vector3.one * (height * (0.52f + (float)rng.NextDouble() * 0.18f));
  }

  private void SpawnCrystal(Vector3 pos, System.Random rng, float scale)
  {
    GameObject go = Piece("Crystal", MeshFactory.Crystal(rng.Next(Variants)), glowMat, pos);
    float h = (1.9f + (float)rng.NextDouble() * 2.0f) * scale;
    go.transform.localScale = new Vector3(h * 0.5f, h, h * 0.5f);
    go.transform.rotation = Quaternion.Euler(
      (float)(rng.NextDouble() * 24 - 12), (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() * 24 - 12));
  }

  // The bacteria's nest at the path start: a lumpy crater with a glowing pool
  // and cilia round the rim. Authored in Blender (Tools/Blender/structures.py)
  // because the old ring of primitives - a black disc and white shards - read as
  // a hole in the texture at play distance.
  private void BuildPortal(Vector3 pos, Vector3 facing)
  {
    EnvironmentTheme.Palette p = EnvironmentTheme.Current;
    string model = string.IsNullOrEmpty(p.nestModel) ? "Structures/NestMeadow" : p.nestModel;
    NestSkin skin = NestSkinFor(model, p);
    Landmark("SpawnPortal", model, pos, facing, part =>
    {
      switch (part)
      {
        case "Rim": return Lit(skin.rim);
        case "Pool": return Neon(skin.pool, skin.poolEmission);
        case "Spikes": return Lit(skin.spikes);
        case "Bubbles": return Neon(skin.bubbles, skin.bubbleEmission);
        case "Crust": return Lit(skin.crust);
        case "Glow": return Neon(skin.glow, skin.glowEmission);
        default: return structureMat;
      }
    });
  }

  // The nest's counterpart to BaseSkin. Same arrangement, same reason for
  // keying it on the model: the tundra's Crust is snow lying on heaved ice and
  // the blossom grove's is a ring of petals, and neither would take the
  // other's colour.
  private struct NestSkin
  {
    public Color rim, pool, spikes, bubbles, crust, glow;
    public float poolEmission, bubbleEmission, glowEmission;
  }

  // The OOZE IS THE SAME MAGENTA ON EVERY BIOME, and that is the point. It
  // mirrors the bases' warm windows: one colour that never takes the
  // environment, so the two ends of the path always read as home and not-home
  // however far the rest of the board moves. The ring around it - the rim, the
  // shards, the crust - is where the biome goes.
  //
  // Deep, and only softly emissive: at full Neon strength the bloom washes the
  // pool out to pastel pink.
  private static readonly Color Ooze = new Color(0.78f, 0.10f, 0.42f);

  private static NestSkin NestSkinFor(string model, EnvironmentTheme.Palette p)
  {
    // Shared by every nest, so a biome can override only what it needs to.
    var skin = new NestSkin
    {
      rim = Color.Lerp(p.soilColor, new Color(0.36f, 0.10f, 0.24f), 0.6f),
      pool = Ooze, poolEmission = 0.55f,
      spikes = new Color(0.72f, 0.22f, 0.44f),
      bubbles = Color.Lerp(Ooze, new Color(1f, 0.55f, 0.8f), 0.5f), bubbleEmission = 0.7f,
      crust = Color.Lerp(p.rockColor, new Color(0.40f, 0.14f, 0.26f), 0.35f),
      glow = Ooze, glowEmission = 0.6f,
    };

    switch (model)
    {
      // Sand drifted into a berm. The crust plates are bleached dry sand -
      // the one biome where the ring is paler than the ground it sits on.
      case "Structures/NestDunes":
        // Stained sand, not mud: lerped toward a lighter sand than the biome's
        // own soil, which came back the colour of chocolate on a pale board.
        skin.rim = Color.Lerp(p.soilColor, new Color(0.66f, 0.50f, 0.32f), 0.6f);
        skin.crust = new Color(0.80f, 0.68f, 0.48f);
        return skin;

      // The bog vent, on the darkest board in the game: the ooze running over
      // its lip is doing the work the pool alone could not.
      case "Structures/NestMarsh":
        skin.rim = Color.Lerp(p.soilColor, new Color(0.30f, 0.12f, 0.30f), 0.65f);
        skin.spikes = new Color(0.62f, 0.26f, 0.46f);
        skin.glowEmission = 0.75f;
        return skin;

      // Heaved pack ice with snow still on it. The shards stay ICE coloured
      // rather than taking the ooze: magenta showing up through cracked white
      // is nastier than a ring that is already the colour of the thing in it.
      case "Structures/NestTundra":
        skin.rim = Color.Lerp(p.rockColor, new Color(0.46f, 0.58f, 0.72f), 0.6f);
        skin.crust = new Color(0.95f, 0.97f, 1f);
        skin.spikes = new Color(0.72f, 0.87f, 0.97f);
        return skin;

      // Basalt columns round the vent, lava in the gaps. The columns echo the
      // ember base's footing, so both landmarks read as the same geology.
      case "Structures/NestEmber":
        skin.rim = new Color(0.17f, 0.14f, 0.15f);
        skin.crust = new Color(0.30f, 0.24f, 0.23f);
        skin.spikes = new Color(0.13f, 0.11f, 0.13f);
        // Lava, not ooze - and below 1, for the reason the ember base's
        // fissures are.
        skin.glow = p.accentGlow; skin.glowEmission = 0.7f;
        return skin;

      // Egg sacs, built the way the bloom base's pods are.
      case "Structures/NestBloom":
        skin.rim = Color.Lerp(p.plantColor, new Color(0.18f, 0.44f, 0.48f), 0.30f);
        skin.spikes = new Color(0.26f, 0.56f, 0.50f);
        skin.glow = p.accentGlow; skin.glowEmission = 0.8f;
        return skin;

      // A carrion flower. The petals are a sickly flesh pink: near enough the
      // biome's blossom to belong to it, far enough off to be wrong.
      case "Structures/NestBlossom":
        skin.rim = new Color(0.46f, 0.24f, 0.30f);
        skin.crust = new Color(0.82f, 0.48f, 0.56f);
        skin.spikes = new Color(0.92f, 0.82f, 0.64f);
        return skin;

      // The meadow crater, and the fallback: colour for colour as it was
      // before the other six existed.
      default:
        return skin;
    }
  }

  // The fungi's base at the path end: the thing the player is defending, with
  // lit windows. It was a lilac dome on a cylinder, which nobody could have
  // named, then one mushroom house recoloured per biome - so a biome changed
  // hue and never shape. Each environment now has its OWN model, named by
  // Palette.baseModel and authored in Tools/Blender/structures.py.
  private GameObject BuildBase(Vector3 pos, Vector3 facing)
  {
    EnvironmentTheme.Palette p = EnvironmentTheme.Current;
    // Palettes built before baseModel existed fall back to the original, the
    // same way EnemyTint guards its own late addition.
    string model = string.IsNullOrEmpty(p.baseModel) ? "Structures/BaseMeadow" : p.baseModel;
    BaseSkin skin = SkinFor(model, p);
    return Landmark("BaseStructure", model, pos, facing, part =>
    {
      switch (part)
      {
        case "Cap": return Lit(skin.cap);
        case "Spots": return Lit(skin.spots);
        case "Stem": return Lit(skin.stem);
        case "Door": return Lit(skin.door);
        case "Windows": return Neon(skin.windows, skin.windowEmission);
        case "Plinth": return Lit(skin.plinth);
        case "Trim": return Lit(skin.trim);
        case "Glow": return Neon(skin.glow, skin.glowEmission);
        default: return structureMat;
      }
    });
  }

  // All seven bases name their parts from the same vocabulary, so one table
  // skins every one of them. Trim is whatever structural accent that model
  // carries - an awning, icicles, tendrils, eaves - and Glow its emissive
  // biome accent; a model uses them or it does not.
  private struct BaseSkin
  {
    public Color cap, spots, stem, door, windows, plinth, trim, glow;
    public float windowEmission, glowEmission;
  }

  // Keyed on the MODEL rather than on the environment, because what a part
  // means is a property of the model: the marsh's Trim is reeds and the
  // tundra's is icicles, and they would not take each other's colour.
  //
  // The windows are WARM on every biome, including the cold ones and the toxic
  // one, and that is deliberate: a light on inside is the single cue that says
  // this end of the path is home, against the nest at the other end. The
  // biome is carried by the cap, the stem and the glow.
  private static BaseSkin SkinFor(string model, EnvironmentTheme.Palette p)
  {
    switch (model)
    {
      // Sand at dusk: a sun-bleached parasol over sandstone, with faded
      // canvas for the awning - the one woven thing in the set.
      case "Structures/BaseDunes":
        return new BaseSkin
        {
          cap = Color.Lerp(new Color(0.80f, 0.50f, 0.26f), p.accentGlow, 0.18f),
          // Weathering, not markings: against a bright ochre cap a cream spot
          // reads as a white scratch rather than as sun-cracked skin.
          spots = new Color(0.88f, 0.70f, 0.46f),
          stem = new Color(0.91f, 0.82f, 0.64f),
          door = p.woodColor * 1.45f,
          windows = new Color(1f, 0.76f, 0.36f), windowEmission = 0.95f,
          plinth = p.rockColor,
          trim = new Color(0.82f, 0.44f, 0.31f),
          glow = p.accentGlow, glowEmission = 1f,
        };

      // Toxic night. The darkest biome in the game, so this base leans hardest
      // on emission: the hanging pods take the biome's acid green and the
      // gills are pale, or the underside of the bell closes the silhouette
      // into one black mass.
      case "Structures/BaseMarsh":
        return new BaseSkin
        {
          cap = new Color(0.36f, 0.21f, 0.48f),
          spots = new Color(0.66f, 0.86f, 0.56f),
          stem = new Color(0.70f, 0.64f, 0.76f),
          door = p.woodColor * 1.9f,
          windows = new Color(1f, 0.80f, 0.40f), windowEmission = 1.05f,
          plinth = p.rockColor,
          trim = p.plantColor,
          // Held below 1 for the same reason the volcanic fissures are: the
          // pods came back pale lime, because emission past the bloom
          // threshold bleaches a hue toward white instead of deepening it.
          glow = p.accentGlow, glowEmission = 0.65f,
        };

      // Snow. The cap is a cold slate so the settled snow on it reads as snow
      // and not as a toadstool's spots, and the windows are the warmest in the
      // game against it.
      case "Structures/BaseTundra":
        return new BaseSkin
        {
          cap = new Color(0.38f, 0.49f, 0.62f),
          spots = new Color(0.97f, 0.98f, 1f),
          stem = new Color(0.86f, 0.88f, 0.92f),
          door = p.woodColor * 1.55f,
          windows = new Color(1f, 0.78f, 0.38f), windowEmission = 1.1f,
          plinth = p.rockColor,
          trim = Color.Lerp(p.accentGlow, Color.white, 0.35f),
          glow = p.accentGlow, glowEmission = 0.9f,
        };

      // Volcanic. Nearly black, and lit entirely by what is glowing through
      // it: the fissures carry the biome and the rock only catches the rim.
      case "Structures/BaseEmber":
        return new BaseSkin
        {
          // Near black, and cool. The biome's key light is a strong orange, so
          // a warm dark grey came back mid-brown and sat in the same value as
          // the boulders around it.
          cap = new Color(0.13f, 0.12f, 0.14f),
          spots = new Color(0.62f, 0.26f, 0.13f),
          stem = new Color(0.24f, 0.22f, 0.24f),
          door = p.woodColor * 1.9f,
          windows = new Color(1f, 0.66f, 0.26f), windowEmission = 0.9f,
          plinth = p.rockColor,
          trim = new Color(0.34f, 0.30f, 0.30f),
          // Emission BELOW 1 here, against the instinct that lava should be
          // the brightest thing on the board: the bloom threshold is just
          // above white, so pushing an orange past it bleaches the hue out and
          // the fissures came back as pale yellow streaks.
          glow = p.accentGlow, glowEmission = 0.7f,
        };

      // Alien bloom. The pods take the biome's violet foliage pulled toward
      // its teal ground, and the magenta pores do the work the windows do
      // elsewhere - which is why the windows here are the smallest.
      case "Structures/BaseBloom":
        return new BaseSkin
        {
          cap = Color.Lerp(p.plantColor, new Color(0.18f, 0.44f, 0.48f), 0.18f),
          spots = new Color(0.72f, 0.93f, 0.86f),
          stem = new Color(0.28f, 0.50f, 0.50f),
          door = new Color(0.24f, 0.20f, 0.32f),
          windows = new Color(1f, 0.80f, 0.46f), windowEmission = 0.95f,
          plinth = p.rockColor,
          trim = new Color(0.26f, 0.56f, 0.50f),
          glow = p.accentGlow, glowEmission = 0.8f,
        };

      // Blossom grove. A deep plum roof over a cream stem: the biome is
      // already made of soft pink canopies, so a pink base would sink into it.
      case "Structures/BaseBlossom":
        return new BaseSkin
        {
          cap = new Color(0.58f, 0.28f, 0.36f),
          spots = new Color(1f, 0.86f, 0.90f),
          stem = new Color(0.95f, 0.90f, 0.80f),
          door = p.woodColor * 1.35f,
          windows = new Color(1f, 0.80f, 0.40f), windowEmission = 0.9f,
          plinth = p.rockColor,
          trim = p.woodColor,
          glow = p.accentGlow, glowEmission = 1f,
        };

      // The meadow, and the fallback: the original red toadstool cottage,
      // colour for colour as it was before the other six existed.
      default:
        return new BaseSkin
        {
          cap = Color.Lerp(new Color(0.86f, 0.20f, 0.19f), p.accentGlow, 0.12f),
          spots = new Color(0.98f, 0.95f, 0.88f),
          stem = new Color(0.94f, 0.87f, 0.74f),
          door = p.woodColor * 1.15f,
          windows = new Color(1f, 0.78f, 0.34f), windowEmission = 0.9f,
          plinth = p.rockColor,
          trim = p.woodColor * 0.95f,
          glow = p.accentGlow, glowEmission = 1f,
        };
    }
  }

  // Instances each named part of an authored model as its own renderer, so the
  // parts take biome colours and still go through the same static batching as
  // the rest of the scenery. The model's door (and the nest's front) is its -Z,
  // turned to face `facing`.
  private GameObject Landmark(string name, string resource, Vector3 pos, Vector3 facing,
    System.Func<string, Material> materialFor)
  {
    var model = Resources.Load<GameObject>(resource);
    if (model == null)
    {
      Debug.LogWarning($"LevelDecorator: missing {resource}");
      return null;
    }

    var root = new GameObject(name);
    root.transform.SetParent(transform, false);
    root.transform.position = pos;
    // Turned halfway between the path and the camera (which looks down +Z), so
    // the front - the house's door and windows - is never hidden round the back.
    facing.y = 0f;
    if (facing.sqrMagnitude > 0.0001f) facing.Normalize();
    Vector3 front = facing + Vector3.back * 1.2f;
    front.y = 0f;
    // The model's front is its local +Z after import (measured in a render).
    if (front.sqrMagnitude > 0.0001f) root.transform.rotation = Quaternion.LookRotation(front.normalized);
    spawned.Add(root);

    // Unity's OBJ importer merges the parts into one mesh; each part survives
    // as a submesh whose imported material is named after it.
    foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
    {
      var source = filter.GetComponent<MeshRenderer>();
      Material[] imported = source != null ? source.sharedMaterials : new Material[0];
      var mats = new Material[filter.sharedMesh.subMeshCount];
      for (int i = 0; i < mats.Length; i++)
      {
        string part = i < imported.Length && imported[i] != null ? imported[i].name : "";
        mats[i] = materialFor(part);
      }

      GameObject piece = Piece(filter.name, filter.sharedMesh, mats[0],
        root.transform.position, root.transform);
      piece.GetComponent<MeshRenderer>().sharedMaterials = mats;
      piece.transform.localPosition = filter.transform.localPosition;
      piece.transform.localRotation = filter.transform.localRotation;
      piece.transform.localScale = filter.transform.localScale;
    }
    return root;
  }

  // Props kept this far from the nest and the base, so a boulder or a bush is
  // never scattered on top of either (one sat half over the nest's mouth).
  private const float LandmarkClearance = 4.2f;
  private Vector3[] landmarks = new Vector3[0];

  private bool NearLandmark(Vector3 pos)
  {
    foreach (Vector3 l in landmarks)
    {
      float dx = pos.x - l.x, dz = pos.z - l.z;
      if (dx * dx + dz * dz < LandmarkClearance * LandmarkClearance) return true;
    }
    return false;
  }

  // Builds a renderer for a generated mesh. Children (parent != null) are freed
  // with their root, so only roots go in the cleanup list.
  private GameObject Piece(string name, Mesh mesh, Material mat, Vector3 pos, Transform parent = null)
  {
    var go = new GameObject(name);
    go.transform.SetParent(parent != null ? parent : transform, false);
    go.transform.position = pos;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    if (parent == null) spawned.Add(go);
    return go;
  }

  // A stable per-level seed. String.GetHashCode is not guaranteed stable across
  // runtimes, so the environment name is folded in by hand — otherwise a level
  // could re-scatter itself differently between sessions.
  private static int LevelSeed()
  {
    LevelConfig cfg = GameSession.SelectedLevel;
    if (cfg == null) return 12345;

    int h = 17 + cfg.levelNumber * 7919;
    if (!string.IsNullOrEmpty(cfg.environmentName))
    {
      foreach (char c in cfg.environmentName) h = unchecked(h * 31 + c);
    }
    return h;
  }

  // Masked positive: System.Random rejects int.MinValue as a seed.
  private System.Random Rng(int salt) => new System.Random((levelSeed ^ (salt * 7919)) & 0x7FFFFFFF);

  private static Material Pick(Material[] mats, System.Random rng) => mats[rng.Next(mats.Length)];

  // A few materials around a base colour, varying brightness and saturation a
  // little so scattered props do not all match exactly.
  private static Material[] Shades(Color baseColor, int count, float spread)
  {
    var mats = new Material[count];
    Color.RGBToHSV(baseColor, out float h, out float s, out float v);
    for (int i = 0; i < count; i++)
    {
      float t = count == 1 ? 0f : (i / (float)(count - 1)) * 2f - 1f; // -1..1
      Color c = Color.HSVToRGB(
        Mathf.Repeat(h + t * spread * 0.06f, 1f),
        Mathf.Clamp01(s + t * spread * 0.35f),
        Mathf.Clamp01(v + t * spread));
      mats[i] = Lit(c);
    }
    return mats;
  }

  private static void Strip(GameObject go)
  {
    var c = go.GetComponent<Collider>();
    if (c == null) return;
    if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
  }

  private static Material Lit(Color color)
  {
    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
    // Anything static batching cannot merge can still be instanced
    mat.enableInstancing = true;
    return mat;
  }

  // Grass blades are flat single-sided geometry, so back faces must render too
  // or half of every tuft disappears depending on the viewing angle.
  private static Material GrassMat(Color color)
  {
    Material mat = Lit(color);
    if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
    mat.doubleSidedGI = true;
    return mat;
  }

  // Clouds are lit, not unlit: an unlit cloud is one flat colour and reads as a
  // paper cut-out however lumpy its silhouette is. The key light gives the lobes
  // form, and a baked vertical ramp darkens the underside toward the sky colour
  // the way a real cumulus shades from white top to grey base.
  private static Material CloudMat(EnvironmentTheme.Palette p)
  {
    Color top = Color.Lerp(p.skyHorizon, Color.white, 0.88f);
    Color bottom = Color.Lerp(p.skyHorizon, p.skyBottom, 0.55f) * 0.82f;
    bottom.a = 1f;

    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetTexture("_BaseMap", MeshFactory.VerticalGradient(bottom, top));
    mat.SetColor("_BaseColor", Color.white);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    return mat;
  }

  // A vibrant emissive material: the albedo carries the hue and a modest
  // emission (kept ~1) gives a coloured bloom halo without blowing out to white.
  private static Material Neon(Color color, float emission = 1.1f)
  {
    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
    mat.EnableKeyword("_EMISSION");
    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * emission);
    mat.enableInstancing = true;
    return mat;
  }

  private void Clear()
  {
    foreach (var go in spawned)
    {
      if (go == null) continue;
      if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }
    spawned.Clear();
  }
}
