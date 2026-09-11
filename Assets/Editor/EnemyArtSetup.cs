using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Builds the four variety enemy prefabs: a base body plus one authored trait
// mesh, so Swarm / Shielded / Splitter / Healer have their own silhouettes
// instead of being a tint and a scale on a shared body.
//
// Run: Tools/Enemies/Build Variety Prefabs, or
//   -executeMethod EnemyArtSetup.BuildVariants   (works with -nographics)
//
// Why a tool and not hand-authored prefabs: the placement of a trait depends on
// the base body's measured bounds, and the four base models have wildly
// different intrinsic sizes (BasicEnemy spans ~6.4 units and is scaled to 0.2
// on its prefab; ArmoredEnemy spans ~3.3 at scale 1). Eyeballing four offsets
// per trait in the inspector would bake those numbers in invisibly; measuring
// them here means re-running the tool after a base model changes is enough.
//
// It is idempotent: re-running overwrites the four prefabs and the four
// materials and rewrites the configs' prefab reference. The four BASE prefabs
// and the eight source configs' stats are never touched.
public static class EnemyArtSetup
{
  private const string TraitMeshDir = "Assets/Meshes/Enemies/Traits";
  private const string PrefabDir = "Assets/Prefabs/Enemies";
  private const string MaterialDir = "Assets/Materials/Enemies";
  private const string ConfigDir = "Assets/Settings/Enemies";

  // How a trait is fitted onto a body. Sizes and heights are FRACTIONS of the
  // body's measured bounds, never world units, so they survive a base swap.
  private struct Trait
  {
    public string configName;     // EnemyConfig asset, also names the prefab
    public string baseName;       // base prefab to build a variant of
    public string meshName;       // OBJ in TraitMeshDir
    public Color accent;
    public bool tintWithBiome;
    public bool hideWhileShieldDown;
    public float widthShare;      // trait width / body width
    public float heightShare;     // attach height / body height, from its base
    public float forwardShare;    // along -X (the models' facing axis), + is forward
    public float pitch;           // degrees about Z, to lean a trait back
    // Optional body recolour. Lives here rather than being hand-edited into
    // the .asset so the trait and the body it has to read against are chosen
    // in one place - and because hand-editing configs is how their fields end
    // up mismatched (HANDOFF section 6).
    public bool setBodyColor;
    public Color bodyColor;
  }

  // Every trait opts OUT of the biome tint, deliberately.
  //
  // The first pass tinted them like bodies and two of the four vanished: the
  // splitter's lobes were purple on a purple body and the healer's crown was
  // green on a green body, so at a glance both read as a plain Basic enemy in a
  // different colour - exactly the problem the traits exist to solve. The body
  // still carries EnvironmentTheme.EnemyTint, so the cast still belongs to its
  // biome; the trait is the part that has to stay the same everywhere, because
  // it is the type's identity rather than its surroundings.
  private static readonly Trait[] Traits =
  {
    // Swarm rides on FastEnemy and is scaled to 0.65, so its fringe has to be
    // proportionally wide or it disappears: a swarm enemy is the smallest thing
    // on the board and the fringe is the only reason it is not just "a small
    // Fast enemy". Sat low and swept back, reading as motion.
    new Trait
    {
      configName = "SwarmEnemy", baseName = "FastEnemy", meshName = "CiliaFringe",
      accent = new Color(0.99f, 0.96f, 0.72f), tintWithBiome = false,
      widthShare = 0.95f, heightShare = 0.22f, forwardShare = -0.10f, pitch = 0f,
    },
    // Shielded rides on ArmoredEnemy, which is already the bulkiest body - the
    // carapace arches over its back and is the one trait that toggles, so a
    // broken shield is visible before the health bar is read.
    new Trait
    {
      configName = "ShieldedEnemy", baseName = "ArmoredEnemy", meshName = "Carapace",
      accent = new Color(0.62f, 0.84f, 1f), tintWithBiome = false,
      hideWhileShieldDown = true,
      // Was 1.12: a share above 1 turned the carapace into an umbrella that
      // covered the body, its eyes and its spikes, so the type read as a white
      // dome rather than as an armoured enemy carrying a shield.
      // Sizing note, because this one is not intuitive: the authored mesh's
      // bounding box is the arc's CHORD, not its radius, so the shell sits at
      // roughly 0.55 of the box. A share of 0.82 therefore put the shell
      // radius INSIDE the body's and it vanished behind it; 1.12 put it 23%
      // outside and it read as an umbrella covering the eyes. Just over 1 is
      // the band where it caps the body and still clears it.
      // The carapace is the only trait whose geometry is an arc around an
      // implied centre, so it wants to be CONCENTRIC with the body: heightShare
      // 0.50 is the body's own centre, and widthShare just over 1 puts the
      // shell's radius a few percent outside the body's. Both matter. At 0.42
      // the shell rode the body's widest point and showed only as slivers
      // behind the silhouette; at 0.68 it rode the crown and became a lid.
      widthShare = 1.14f, heightShare = 0.50f, forwardShare = -0.06f, pitch = -8f,
      // Armored and Shielded were both saturated blue spiky spheres, so the
      // shell was doing 100% of the work of telling them apart. The body is
      // therefore recoloured too - but DARK, not pale: the first attempt at a
      // pale steel body landed within a few percent of the shell's own colour
      // and the shell stopped reading as a separate object at all.
      setBodyColor = true, bodyColor = new Color(0.24f, 0.34f, 0.54f),
    },
    // Splitter and Healer both ride on BasicEnemy and were previously
    // distinguishable only by a purple-vs-green tint at two similar scales.
    // These two traits are therefore the ones that have to differ MOST: lobes
    // bulge sideways and low, the crown sits high and central.
    new Trait
    {
      configName = "SplitterEnemy", baseName = "BasicEnemy", meshName = "BudLobes",
      // Amber against a purple body, and pushed out to roughly the body's own
      // radius so the lobes break the silhouette instead of sitting inside it -
      // at forwardShare -0.28 they were buried under the body's spikes and
      // invisible from every angle.
      accent = new Color(0.99f, 0.80f, 0.38f), tintWithBiome = false,
      widthShare = 0.70f, heightShare = 0.28f, forwardShare = -0.50f, pitch = 0f,
    },
    new Trait
    {
      configName = "HealerEnemy", baseName = "BasicEnemy", meshName = "SporeCrown",
      // A pale cap, not a green one: the healer body is green, so a green
      // crown disappeared into it.
      accent = new Color(0.97f, 0.94f, 0.86f), tintWithBiome = false,
      // heightShare counts from the body's BASE and the crown is itself 0.83
      // of its own box tall, so a share chosen to look like "near the top"
      // (0.72) stacked the two and left the cap hovering clear of the body.
      // 0.50 seats it in among the body's spikes.
      widthShare = 0.62f, heightShare = 0.50f, forwardShare = 0f, pitch = 0f,
    },
  };

  [MenuItem("Tools/Enemies/Build Variety Prefabs")]
  public static void BuildVariants()
  {
    Directory.CreateDirectory(MaterialDir);
    var report = new List<string>();

    foreach (Trait trait in Traits)
    {
      string basePath = $"{PrefabDir}/{trait.baseName}.prefab";
      GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
      if (basePrefab == null)
      {
        Debug.LogError($"EnemyArtSetup: base prefab missing at {basePath}");
        continue;
      }

      string meshPath = $"{TraitMeshDir}/{trait.meshName}.obj";
      Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
      if (mesh == null)
      {
        // The OBJ importer names the mesh after the object inside the file, so
        // the top-level Mesh load can miss it; fall back to a sub-asset sweep.
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(meshPath))
        {
          if (o is Mesh m) { mesh = m; break; }
        }
      }
      if (mesh == null)
      {
        Debug.LogError($"EnemyArtSetup: trait mesh missing at {meshPath}. " +
                       "Re-run Tools/Blender/enemy_traits.py.");
        continue;
      }

      // Instantiating the base prefab and saving that instance under a new name
      // produces a prefab VARIANT, so the variety prefabs keep inheriting the
      // base body's Enemy component, scale and material assignments.
      GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
      instance.name = trait.configName;

      MeshRenderer body = Enemy.FindBodyRenderer(instance);
      if (body == null)
      {
        Debug.LogError($"EnemyArtSetup: no body renderer under {trait.baseName}");
        Object.DestroyImmediate(instance);
        continue;
      }

      // Bounds have to be measured in the ROOT's local space: the trait is
      // parented to the root, so it inherits the root's scale, and renderer
      // bounds are in world space. Converting through the root's inverse matrix
      // is what makes widthShare/heightShare mean the same thing on a body
      // scaled 0.2 and one scaled 1.
      Bounds local = LocalBounds(instance.transform, body);

      GameObject traitObject = new GameObject(trait.meshName);
      traitObject.transform.SetParent(instance.transform, false);
      traitObject.AddComponent<MeshFilter>().sharedMesh = mesh;
      MeshRenderer renderer = traitObject.AddComponent<MeshRenderer>();
      renderer.sharedMaterial = TraitMaterial(trait);
      // Traits are small and never the silhouette that matters for a shadow;
      // the board already draws 30+ enemies at once.
      renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

      EnemyTrait marker = traitObject.AddComponent<EnemyTrait>();
      marker.accentColor = trait.accent;
      marker.tintWithBiome = trait.tintWithBiome;
      marker.hideWhileShieldDown = trait.hideWhileShieldDown;

      // The authored mesh is a unit box centred in X/Z and sitting on Y=0, so
      // one uniform scale maps it onto a share of the body's width.
      //
      // The metric is the MEAN of the two horizontal extents, not the max.
      // FastEnemy is elongated - 7.07 along X against 2.27 along Z - so the max
      // sized Swarm's fringe against the body's LENGTH and produced a fringe
      // three times wider than the body it wraps. The mean tracks the body a
      // trait actually has to sit on.
      float width = (local.size.x + local.size.z) * 0.5f;
      float scale = Mathf.Max(0.0001f, width * trait.widthShare);
      traitObject.transform.localScale = Vector3.one * scale;
      traitObject.transform.localPosition = new Vector3(
        local.center.x - trait.forwardShare * local.size.x,
        local.min.y + local.size.y * trait.heightShare,
        local.center.z);
      traitObject.transform.localRotation = Quaternion.Euler(0f, 0f, trait.pitch);

      // Read anything wanted for the report BEFORE destroying the instance -
      // the trait transform belongs to it and goes away with it.
      Vector3 placedAt = traitObject.transform.localPosition;

      string prefabPath = $"{PrefabDir}/{trait.configName}.prefab";
      GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
      Object.DestroyImmediate(instance);

      // Point the config at its own prefab. Splitter and Healer both used
      // BasicEnemy, so before this they shared a pool AND a silhouette.
      var config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(
        $"{ConfigDir}/{trait.configName}.asset");
      if (config == null)
      {
        Debug.LogError($"EnemyArtSetup: config missing for {trait.configName}");
      }
      else
      {
        config.prefab = saved;
        if (trait.setBodyColor)
        {
          config.overrideBodyColor = true;
          config.bodyColor = trait.bodyColor;
        }
        EditorUtility.SetDirty(config);
      }

      report.Add($"{trait.configName,-16} base={trait.baseName,-13} " +
                 $"trait={trait.meshName,-12} scale={scale:F3} " +
                 $"pos={placedAt} " +
                 $"bodyLocal(size={local.size}, min.y={local.min.y:F3})");
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
    Debug.Log("EnemyArtSetup built:\n" + string.Join("\n", report));
  }

  // World-space renderer bounds, expressed in the root's local space. Uses the
  // renderer's own transform because a base body can sit at an offset under the
  // root, which is exactly the case that a root-relative guess gets wrong.
  private static Bounds LocalBounds(Transform root, MeshRenderer body)
  {
    Mesh mesh = body.GetComponent<MeshFilter>()?.sharedMesh;
    Bounds source = mesh != null ? mesh.bounds : body.localBounds;
    Matrix4x4 toRoot = root.worldToLocalMatrix * body.transform.localToWorldMatrix;

    Vector3 c = source.center, e = source.extents;
    Bounds result = new Bounds(toRoot.MultiplyPoint3x4(c), Vector3.zero);
    for (int i = 0; i < 8; i++)
    {
      Vector3 corner = c + new Vector3(
        (i & 1) == 0 ? -e.x : e.x,
        (i & 2) == 0 ? -e.y : e.y,
        (i & 4) == 0 ? -e.z : e.z);
      result.Encapsulate(toRoot.MultiplyPoint3x4(corner));
    }
    return result;
  }

  // One material per trait, so the accent has somewhere to live and the
  // MaterialPropertyBlock in EnemyTrait has a _BaseColor to override. Written
  // as an asset rather than created at runtime so the prefab can reference it.
  private static Material TraitMaterial(Trait trait)
  {
    string path = $"{MaterialDir}/{trait.meshName}.mat";
    Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");
    Material mat = existing ?? new Material(shader);
    mat.shader = shader;
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", trait.accent);
    if (mat.HasProperty("_Color")) mat.color = trait.accent;
    // Slightly glossy: the bodies are matte, so a little specular separation
    // keeps a trait from reading as a lump of the same material.
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    if (existing == null) AssetDatabase.CreateAsset(mat, path);
    else EditorUtility.SetDirty(mat);
    return mat;
  }
}
