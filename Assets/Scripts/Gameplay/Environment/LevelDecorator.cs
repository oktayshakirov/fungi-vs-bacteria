using System.Collections.Generic;
using UnityEngine;

// Dresses the level at load: scatters scenery props in the border ring, and
// builds a spawn portal at the path start and a base structure at the path end
// so the board reads as a place being defended. Runs after PathManager and
// EnvironmentTheme (execution order) so the path and palette are ready.
[DefaultExecutionOrder(100)]
public class LevelDecorator : MonoBehaviour
{
  private readonly List<GameObject> spawned = new List<GameObject>();
  private Material rockMat, plantMat, structureMat, glowMat;

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

    EnvironmentTheme.Palette p = EnvironmentTheme.Current;
    rockMat = Lit(p.rockColor);
    plantMat = Lit(p.plantColor);
    structureMat = Lit(p.structureColor);
    // Neon crystals: moderate emission keeps the hue (too high washes to white),
    // and the scene bloom adds a coloured halo.
    glowMat = Neon(p.accentGlow);

    BuildIslandCliff(p);
    BuildDistantClouds(p);
    BuildDistantIslands(p);
    BuildFloatingDebris(p);
    ScatterProps();
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

  // A tall tapering rock mass under the island — the "chunk of land torn from
  // the ground" look that gives the sense of floating high in the air.
  private void BuildIslandCliff(EnvironmentTheme.Palette p)
  {
    IslandExtent(out float halfW, out float halfD);

    // Earthy dirt near the top fading to darker rock below (grayish rock reads
    // flat; brown-to-gray reads as a torn earth cliff)
    Color rockTop = new Color(0.42f, 0.33f, 0.24f);
    Color rockBottom = new Color(0.24f, 0.24f, 0.27f);

    var rng = new System.Random(202);
    const int layers = 5;
    const float layerH = 8f;
    float top = -2.5f; // just under the soil rim slab

    for (int i = 0; i < layers; i++)
    {
      float f = Mathf.Lerp(0.92f, 0.28f, i / (float)(layers - 1)); // narrow downward
      var mat = Lit(Color.Lerp(rockTop, rockBottom, i / (float)(layers - 1)));

      // A couple of chunky, slightly offset/rotated boxes per layer for a
      // rugged rock silhouette rather than a clean cone
      int chunks = i < 2 ? 2 : 1;
      for (int c = 0; c < chunks; c++)
      {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Strip(box);
        box.transform.SetParent(transform, false);
        box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        box.transform.localScale = new Vector3(
          halfW * 2f * f * (0.8f + (float)rng.NextDouble() * 0.35f),
          layerH * 1.15f,
          halfD * 2f * f * (0.8f + (float)rng.NextDouble() * 0.35f));
        box.transform.position = new Vector3(
          (float)(rng.NextDouble() * 2 - 1) * halfW * 0.12f,
          top - i * layerH - layerH * 0.5f,
          (float)(rng.NextDouble() * 2 - 1) * halfD * 0.12f);
        box.transform.rotation = Quaternion.Euler(
          (float)(rng.NextDouble() * 8 - 4), (float)rng.NextDouble() * 30f, (float)(rng.NextDouble() * 8 - 4));
        spawned.Add(box);
      }
    }
  }

  // A wide field of soft clouds ringing the island in the distance and a little
  // below it, so they read as a far-off cloud layer in the sky rather than fog
  // clinging to the base. Kept out of the gap directly under the island.
  private void BuildDistantClouds(EnvironmentTheme.Palette p)
  {
    IslandExtent(out float halfW, out float halfD);
    Color cloudCol = Color.Lerp(p.skyHorizon, Color.white, 0.5f);
    Material cloudMat = CloudMat(cloudCol, 0.55f);

    var rng = new System.Random(909);
    int puffs = 80;
    for (int i = 0; i < puffs; i++)
    {
      // Ring from just past the island out to far away, gently below eye level
      float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
      float dist = 1.25f + (float)rng.NextDouble() * 2.6f; // 1.25x .. 3.85x island
      float x = Mathf.Cos(ang) * halfW * dist;
      float z = Mathf.Sin(ang) * halfD * dist;
      // Farther clouds sit lower, so they recede toward a horizon cloud line
      float y = -6f - (dist - 1.25f) * 8f + (float)(rng.NextDouble() * 2 - 1) * 4f;

      var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      Strip(puff);
      puff.transform.SetParent(transform, false);
      puff.GetComponent<MeshRenderer>().sharedMaterial = cloudMat;
      float s = 12f + (float)rng.NextDouble() * 20f;
      puff.transform.localScale = new Vector3(s, s * 0.4f, s);
      puff.transform.position = new Vector3(x, y, z);
      spawned.Add(puff);
    }
  }

  // Small grass-topped rock islets floating in the distance around the play
  // island — the strongest "we are high in a sky full of floating lands" cue.
  private void BuildDistantIslands(EnvironmentTheme.Palette p)
  {
    IslandExtent(out float halfW, out float halfD);
    Material grassMat = Lit(p.groundTint == Color.white ? new Color(0.45f, 0.62f, 0.28f) : p.groundTint * 0.7f);
    Material rockMat2 = Lit(new Color(0.34f, 0.30f, 0.28f));

    var rng = new System.Random(555);
    int count = 6;
    for (int i = 0; i < count; i++)
    {
      float ang = (i / (float)count) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f;
      float dist = 1.9f + (float)rng.NextDouble() * 1.8f;
      float x = Mathf.Cos(ang) * halfW * dist;
      float z = Mathf.Sin(ang) * halfD * dist;
      float y = -4f + (float)(rng.NextDouble() * 2 - 1) * 12f;

      var root = new GameObject("DistantIsland");
      root.transform.SetParent(transform, false);
      root.transform.position = new Vector3(x, y, z);
      root.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
      spawned.Add(root);

      float w = 6f + (float)rng.NextDouble() * 10f;

      var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
      Strip(top);
      top.transform.SetParent(root.transform, false);
      top.transform.localScale = new Vector3(w, 2f, w * 0.8f);
      top.transform.localPosition = Vector3.zero;
      top.GetComponent<MeshRenderer>().sharedMaterial = grassMat;

      var bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
      Strip(bottom);
      bottom.transform.SetParent(root.transform, false);
      bottom.transform.localScale = new Vector3(w * 0.7f, 6f, w * 0.55f);
      bottom.transform.localPosition = new Vector3(0f, -3.5f, 0f);
      bottom.transform.localRotation = Quaternion.Euler(4f, 12f, 3f);
      bottom.GetComponent<MeshRenderer>().sharedMaterial = rockMat2;
    }
  }

  // A few small rock chunks drifting around/below the island for depth and scale.
  private void BuildFloatingDebris(EnvironmentTheme.Palette p)
  {
    IslandExtent(out float halfW, out float halfD);
    Material rockMatDbg = Lit(p.rockColor * 0.6f);

    var rng = new System.Random(313);
    int chunks = 7;
    for (int i = 0; i < chunks; i++)
    {
      float ang = (i / (float)chunks) * Mathf.PI * 2f + (float)rng.NextDouble();
      float dist = 1.35f + (float)rng.NextDouble() * 0.7f;
      float x = Mathf.Cos(ang) * halfW * dist;
      float z = Mathf.Sin(ang) * halfD * dist;
      float y = -6f - (float)rng.NextDouble() * 22f;

      var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
      Strip(rock);
      rock.transform.SetParent(transform, false);
      rock.GetComponent<MeshRenderer>().sharedMaterial = rockMatDbg;
      float s = 2.5f + (float)rng.NextDouble() * 5f;
      rock.transform.localScale = new Vector3(s, s * (0.7f + (float)rng.NextDouble() * 0.6f), s * 0.9f);
      rock.transform.position = new Vector3(x, y, z);
      rock.transform.rotation = Quaternion.Euler(
        (float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f);
      spawned.Add(rock);
    }
  }

  // Unlit transparent material for soft, foggy clouds.
  private static Material CloudMat(Color color, float alpha)
  {
    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    mat.SetOverrideTag("RenderType", "Transparent");
    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    mat.SetFloat("_Surface", 1f);          // 0 opaque, 1 transparent
    mat.SetFloat("_Blend", 0f);            // alpha blend
    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    mat.SetInt("_ZWrite", 0);
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    Color c = color; c.a = alpha;
    mat.color = c;
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
    return mat;
  }

  // Scattered neon glowing orbs in varied vibrant colours (like the combat
  // hit-effects) for extra vibrancy in the border ring.
  private void ScatterNeonOrbs(EnvironmentTheme.Palette p)
  {
    GridManager grid = GridManager.Instance;
#if UNITY_EDITOR
    if (grid == null) grid = FindFirstObjectByType<GridManager>();
#endif
    if (grid == null) return;

    Color[] neons =
    {
      p.accentGlow,
      new Color(1f, 0.25f, 0.85f),  // magenta
      new Color(0.25f, 0.85f, 1f),  // cyan
      new Color(1f, 0.85f, 0.2f),   // amber
    };
    var orbMats = new Material[neons.Length];
    for (int i = 0; i < neons.Length; i++) orbMats[i] = Neon(neons[i]);

    float halfW = grid.gridSize.x * grid.cellSize * 0.5f;
    float halfD = grid.gridSize.y * grid.cellSize * 0.5f;
    var rng = new System.Random(4242);
    int orbs = 12;
    int placed = 0, attempts = 0;
    while (placed < orbs && attempts < orbs * 12)
    {
      attempts++;
      float x = (float)(rng.NextDouble() * 2 - 1) * (halfW + BoardDecor.Margin);
      float z = (float)(rng.NextDouble() * 2 - 1) * (halfD + BoardDecor.Margin);
      if (Mathf.Abs(x) < halfW - 0.5f && Mathf.Abs(z) < halfD - 0.5f) continue;

      var orb = Primitive(PrimitiveType.Sphere, orbMats[rng.Next(orbMats.Length)], new Vector3(x, 0.7f, z));
      float s = 0.7f + (float)rng.NextDouble() * 0.7f;
      orb.transform.localScale = Vector3.one * s;
      placed++;
    }
  }

  // Props live only in the border ring outside the play grid, so they never
  // interfere with tower placement.
  private void ScatterProps()
  {
    GridManager grid = GridManager.Instance;
#if UNITY_EDITOR
    if (grid == null) grid = FindFirstObjectByType<GridManager>();
#endif
    if (grid == null) return;

    float halfW = grid.gridSize.x * grid.cellSize * 0.5f;
    float halfD = grid.gridSize.y * grid.cellSize * 0.5f;
    float outerW = halfW + BoardDecor.Margin;
    float outerD = halfD + BoardDecor.Margin;

    var rng = new System.Random(12345);
    int count = 46;
    int placed = 0, attempts = 0;

    while (placed < count && attempts < count * 12)
    {
      attempts++;
      float x = (float)(rng.NextDouble() * 2 - 1) * outerW;
      float z = (float)(rng.NextDouble() * 2 - 1) * outerD;

      // Keep props in the border ring, not on the play grid
      bool insideGrid = Mathf.Abs(x) < halfW - 0.5f && Mathf.Abs(z) < halfD - 0.5f;
      if (insideGrid) continue;

      SpawnProp(new Vector3(x, 0f, z), rng);
      placed++;
    }
  }

  private void SpawnProp(Vector3 pos, System.Random rng)
  {
    int kind = rng.Next(100);
    if (kind < 45) SpawnRock(pos, rng);
    else if (kind < 85) SpawnPlant(pos, rng);
    else SpawnCrystal(pos, rng);
  }

  private void SpawnRock(Vector3 pos, System.Random rng)
  {
    var go = Primitive(PrimitiveType.Cube, rockMat, pos);
    float s = 1.2f + (float)rng.NextDouble() * 2.6f;
    go.transform.localScale = new Vector3(s, s * 0.7f, s * (0.8f + (float)rng.NextDouble() * 0.4f));
    go.transform.rotation = Quaternion.Euler(
      (float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f);
    go.transform.position = pos + Vector3.up * s * 0.3f;
  }

  private void SpawnPlant(Vector3 pos, System.Random rng)
  {
    // A little clustered bush of squashed spheres
    int blobs = 2 + rng.Next(3);
    var root = new GameObject("Bush");
    root.transform.SetParent(transform, false);
    root.transform.position = pos;
    spawned.Add(root);
    for (int i = 0; i < blobs; i++)
    {
      var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      Strip(blob);
      blob.transform.SetParent(root.transform, false);
      blob.GetComponent<MeshRenderer>().sharedMaterial = plantMat;
      float s = 1.2f + (float)rng.NextDouble() * 1.4f;
      blob.transform.localScale = new Vector3(s, s * 0.8f, s);
      blob.transform.localPosition = new Vector3(
        (float)(rng.NextDouble() * 2 - 1) * 0.9f, s * 0.35f, (float)(rng.NextDouble() * 2 - 1) * 0.9f);
    }
  }

  private void SpawnCrystal(Vector3 pos, System.Random rng)
  {
    var go = Primitive(PrimitiveType.Cube, glowMat, pos);
    float h = 2.2f + (float)rng.NextDouble() * 2.4f;
    go.transform.localScale = new Vector3(0.7f, h, 0.7f);
    go.transform.rotation = Quaternion.Euler(
      (float)(rng.NextDouble() * 24 - 12), (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() * 24 - 12));
    go.transform.position = pos + Vector3.up * h * 0.4f;
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
  }

  private GameObject Primitive(PrimitiveType type, Material mat, Vector3 pos)
  {
    var go = GameObject.CreatePrimitive(type);
    Strip(go);
    go.transform.SetParent(transform, false);
    go.transform.position = pos;
    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    spawned.Add(go);
    return go;
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
