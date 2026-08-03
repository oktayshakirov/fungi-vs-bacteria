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

    BuildIslandCliff(p);
    BuildDistantClouds(p);
    BuildDistantIslands(p);
    BuildFloatingDebris(p);
    ScatterProps(p);
    ScatterGrass();
    ScatterNeonOrbs(p);
    if (pathPoints != null && pathPoints.Length >= 2)
    {
      BuildPortal(pathPoints[0]);
      BuildBase(pathPoints[pathPoints.Length - 1]);
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

    Mesh mesh = MeshFactory.Cliff(halfW, halfD, 46f);
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

      SpawnProp(new Vector3(x, 0f, z), Mathf.InverseLerp(-outerD, outerD, z), rng);
      placed++;
    }

    // A treeline hugging the back edge, framing the board the way the reference
    // does. Safe to be tall: it is the farthest thing from the camera.
    for (int i = 0; i < 9; i++)
    {
      float x = Mathf.Lerp(-outerW, outerW, (i + 0.5f) / 9f) + (float)(rng.NextDouble() * 2 - 1) * 3f;
      float z = outerD - (float)rng.NextDouble() * 3.5f;
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

      GameObject mound = Piece("Mound", MeshFactory.Mound(rng.Next(Variants)), moundMat, new Vector3(x, 0.02f, z));
      // Flattened hard toward the camera: a swell of turf in the foreground
      // becomes a green blob sitting over the board.
      float depth = Mathf.InverseLerp(-outerD, outerD, z);
      float s = (3f + (float)rng.NextDouble() * 3.5f) * Mathf.Lerp(0.6f, 1f, depth);
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

      GameObject patch = Piece("Grass", MeshFactory.GrassPatch(rng.Next(Variants)), grassMat, new Vector3(x, 0f, z));
      float s = 1.6f + (float)rng.NextDouble() * 1.6f;
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

  // Scattered neon glowing shards in varied vibrant colours (like the combat
  // hit-effects) for extra vibrancy in the border ring.
  private void ScatterNeonOrbs(EnvironmentTheme.Palette p)
  {
    if (!Ring(out float halfW, out float halfD, out float outerW, out float outerD)) return;

    Color[] neons =
    {
      p.accentGlow,
      new Color(1f, 0.25f, 0.85f),  // magenta
      new Color(0.25f, 0.85f, 1f),  // cyan
      new Color(1f, 0.85f, 0.2f),   // amber
    };
    var orbMats = new Material[neons.Length];
    for (int i = 0; i < neons.Length; i++) orbMats[i] = Neon(neons[i]);

    System.Random rng = Rng(3);
    int placed = 0, attempts = 0;
    while (placed < 12 && attempts < 200)
    {
      attempts++;
      float x = (float)(rng.NextDouble() * 2 - 1) * outerW;
      float z = (float)(rng.NextDouble() * 2 - 1) * outerD;
      if (Mathf.Abs(x) < halfW - 0.5f && Mathf.Abs(z) < halfD - 0.5f) continue;

      GameObject orb = Piece("NeonShard", MeshFactory.Crystal(rng.Next(Variants)),
        orbMats[rng.Next(orbMats.Length)], new Vector3(x, 0f, z));
      float s = 1.4f + (float)rng.NextDouble() * 1.4f;
      orb.transform.localScale = new Vector3(s, s * 1.8f, s);
      orb.transform.rotation = Quaternion.Euler(
        (float)(rng.NextDouble() * 16 - 8), (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() * 16 - 8));
      placed++;
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
    float s = (1.4f + (float)rng.NextDouble() * 3.2f) * scale;
    go.transform.localScale = new Vector3(s, s * (0.7f + (float)rng.NextDouble() * 0.5f), s);
    go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
  }

  private void SpawnBush(Vector3 pos, System.Random rng, float scale)
  {
    GameObject go = Piece("Bush", MeshFactory.Bush(rng.Next(Variants)), Pick(plantMats, rng), pos);
    float s = (1.6f + (float)rng.NextDouble() * 1.8f) * scale;
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
    float height = 4.4f + (float)rng.NextDouble() * 2.8f;
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
    float h = (2.6f + (float)rng.NextDouble() * 2.8f) * scale;
    go.transform.localScale = new Vector3(h * 0.5f, h, h * 0.5f);
    go.transform.rotation = Quaternion.Euler(
      (float)(rng.NextDouble() * 24 - 12), (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() * 24 - 12));
  }

  // A dark ring the enemies emerge from, at the path start
  private void BuildPortal(Vector3 pos)
  {
    var root = new GameObject("SpawnPortal");
    root.transform.SetParent(transform, false);
    root.transform.position = pos + Vector3.up * 0.1f;
    spawned.Add(root);

    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    Strip(ring);
    ring.transform.SetParent(root.transform, false);
    ring.transform.localScale = new Vector3(3.4f, 0.15f, 3.4f);
    ring.GetComponent<MeshRenderer>().sharedMaterial = glowMat;

    var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    Strip(inner);
    inner.transform.SetParent(root.transform, false);
    inner.transform.localScale = new Vector3(2.6f, 0.2f, 2.6f);
    inner.transform.localPosition = Vector3.up * 0.05f;
    inner.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(0.05f, 0.05f, 0.08f));

    // A ring of shards around the mouth, so the spawn point reads as built
    var rng = new System.Random(77);
    for (int i = 0; i < 7; i++)
    {
      float ang = (i / 7f) * Mathf.PI * 2f;
      GameObject shard = Piece("PortalShard", MeshFactory.Crystal(i % Variants), glowMat,
        root.transform.position, root.transform);
      shard.transform.localPosition = new Vector3(Mathf.Cos(ang) * 2.1f, 0f, Mathf.Sin(ang) * 2.1f);
      float h = 1.2f + (float)rng.NextDouble() * 1.1f;
      shard.transform.localScale = new Vector3(h * 0.45f, h, h * 0.45f);
      shard.transform.localRotation = Quaternion.Euler(22f * Mathf.Cos(ang), 0f, -22f * Mathf.Sin(ang));
    }
  }

  // A stacked structure that the enemies march toward (the base that takes damage)
  private void BuildBase(Vector3 pos)
  {
    var root = new GameObject("BaseStructure");
    root.transform.SetParent(transform, false);
    root.transform.position = pos;
    spawned.Add(root);

    var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    Strip(plinth);
    plinth.transform.SetParent(root.transform, false);
    plinth.transform.localScale = new Vector3(4.2f, 0.6f, 4.2f);
    plinth.transform.localPosition = Vector3.up * 0.6f;
    plinth.GetComponent<MeshRenderer>().sharedMaterial = structureMat;

    var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    Strip(dome);
    dome.transform.SetParent(root.transform, false);
    dome.transform.localScale = new Vector3(3.4f, 3.0f, 3.4f);
    dome.transform.localPosition = Vector3.up * 1.6f;
    dome.GetComponent<MeshRenderer>().sharedMaterial = structureMat;

    var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    Strip(core);
    core.transform.SetParent(root.transform, false);
    core.transform.localScale = Vector3.one * 1.6f;
    core.transform.localPosition = Vector3.up * 3.3f;
    core.GetComponent<MeshRenderer>().sharedMaterial = glowMat;

    // Buttress shards leaning against the plinth
    for (int i = 0; i < 6; i++)
    {
      float ang = (i / 6f) * Mathf.PI * 2f + 0.4f;
      GameObject buttress = Piece("Buttress", MeshFactory.Crystal(i % Variants), structureMat,
        root.transform.position, root.transform);
      buttress.transform.localPosition = new Vector3(Mathf.Cos(ang) * 2.3f, 0f, Mathf.Sin(ang) * 2.3f);
      buttress.transform.localScale = new Vector3(0.9f, 2.4f, 0.9f);
      buttress.transform.localRotation = Quaternion.Euler(18f * Mathf.Sin(ang), 0f, -18f * Mathf.Cos(ang));
    }
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
  private static Material Neon(Color color)
  {
    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
    mat.EnableKeyword("_EMISSION");
    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 1.1f);
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
