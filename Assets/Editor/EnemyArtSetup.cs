using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the four variety enemy prefabs by COMPOSING them out of parts taken
// from the four existing enemy models.
//
// Run: Tools/Enemies/Build Variety Prefabs, or
//   -executeMethod EnemyArtSetup.BuildVariants   (works with -nographics)
// See also Tools/Enemies/Report Base Parts, which lists what is available.
//
// The four base models are never modified. A new type is its base prefab plus
// one or more extra renderers that reference an EXISTING mesh, repositioned,
// rescaled and recoloured - so every surface in the game is authored art, and
// a new enemy cannot look hand-made or distorted, because there is nothing
// hand-made in it.
//
// This replaced a first attempt that bolted on small meshes generated in
// Blender (a carapace, a spore crown, budding lobes, a cilia fringe). They were
// cheap and they were readable, and they still looked wrong: script-made
// geometry next to detailed organic models reads as damage rather than as
// design. The generated meshes and their Blender script are gone; the pipeline
// that produced them is still written up in HANDOFF section 8 if it is ever
// wanted for something that is not a creature.
//
// Two constraints worth knowing before adding a part:
//
//  * COST. The base bodies are wildly uneven - Basic's body is 287k verts and
//    Armored's is 171k, against 552 for Fast's and 1292 for Boss's. A late
//    wave puts 30+ enemies on screen, so an extra part must come from the
//    CHEAP end. Every composition below builds out of Fast's body (552), its
//    hair (4194) or its tail (375). Duplicating a Basic or Armored body would
//    add a quarter of a million verts per enemy.
//  * BODIES, NOT WHOLE MODELS. Each model keeps its Body separate from its
//    eyes, so a part can be a bare body. Reusing a whole model as a part would
//    graft a second pair of eyes onto the enemy.
public static class EnemyArtSetup
{
  private const string PrefabDir = "Assets/Prefabs/Enemies";
  private const string MaterialDir = "Assets/Materials/Enemies";
  private const string ConfigDir = "Assets/Settings/Enemies";

  // One extra renderer on a composed enemy.
  //
  // Offsets and scales are FRACTIONS of the base body's measured bounds, never
  // world units: the four bases differ by 3x in intrinsic size and each carries
  // its own prefab scale (Basic 0.2, Armored 1.0), so a world-unit offset that
  // suits one base is meaningless on another.
  private struct Part
  {
    public string sourcePrefab;  // base prefab to borrow the mesh from
    public string meshObject;    // its child object, e.g. "Body_Body"
    public string materialName;  // asset written under MaterialDir
    // Position of the part's CENTRE. x/z are offsets from the body's centre,
    // y is measured up from the body's base; all three as shares of the body's
    // size on that axis, so y = 0.5 is the body's centre.
    public Vector3 offsetShare;
    // The part's FINAL size on each axis, as a share of the body's width. The
    // builder divides by the source mesh's own extents, so these are absolute
    // proportions and not corrections to the source's aspect ratio - a share
    // of 1 means "as wide as the body" whatever mesh is borrowed. Getting this
    // backwards is what put the first shield bubble inside its own body.
    public Vector3 scaleShare;
    public Vector3 euler;
    public Color color;          // alpha below 1 makes the material transparent
    public bool hideWhileShieldDown;
  }

  private struct Composition
  {
    public string configName;
    public string baseName;
    public bool setBodyColor;
    public Color bodyColor;
    public Part[] parts;
  }

  // Two cheap building blocks carry every composition below.
  //
  // Fast's body is a 552-vert capsule, used where a bacterial ROD is wanted.
  // It is not usable as a sphere: squashing it round exposes its facets, and
  // the first shield bubble built that way read as a chunk of faceted glass.
  //
  // For anything round, borrow an EYE. Each model's eye white is a smooth
  // 481-vert sphere - the only proper sphere in the project's art - and at a
  // flat colour nothing about it reads as an eye. It is the shield bubble and
  // the splitter's daughter cells.
  private const string FastBody = "Body_Body";
  private const string SphereMesh = "defaultMaterial.004_EyeWhite";

  private static readonly Composition[] Compositions =
  {
    // SHIELDED - a full bubble around the Armored body.
    //
    // The shield has to read as protection on EVERY side. A partial shell,
    // which is what the first attempt used, reads as damage or as a hat, and
    // the review said so. A closed translucent bubble is symmetric by
    // construction, needs no orientation, and gives the clearest possible cue
    // when it pops: the enemy is visibly naked afterwards.
    new Composition
    {
      configName = "ShieldedEnemy", baseName = "ArmoredEnemy",
      // Dark body under a pale bubble. Armored and Shielded were both
      // saturated blue spheres, so the body is recoloured too - and it stays
      // recoloured with the bubble gone, which is what keeps the two types
      // apart while the shield is regenerating.
      setBodyColor = true, bodyColor = new Color(0.26f, 0.36f, 0.56f),
      parts = new[]
      {
        new Part
        {
          sourcePrefab = "BasicEnemy", meshObject = SphereMesh,
          materialName = "ShieldBubble",
          offsetShare = new Vector3(0f, 0.5f, 0f),
          // Round, and a little wider than the body so it encloses the spikes.
          scaleShare = new Vector3(1.22f, 1.22f, 1.22f),
          euler = Vector3.zero,
          color = new Color(0.58f, 0.84f, 1f, 0.28f),
          hideWhileShieldDown = true,
        },
      },
    },

    // SPLITTER - two daughter cells already budding out of the parent.
    //
    // Smooth round cells against the parent's spiky shell: the contrast is
    // what makes them read as separate organisms rather than as lumps, and it
    // tells the player what is about to happen before the parent dies.
    new Composition
    {
      configName = "SplitterEnemy", baseName = "BasicEnemy",
      parts = new[]
      {
        new Part
        {
          sourcePrefab = "BasicEnemy", meshObject = SphereMesh,
          materialName = "DaughterCell",
          // The two cells sit on roughly OPPOSITE sides, and each one's centre
          // is pushed past the body's own radius. Both matter. An enemy turns
          // to follow the path, so a pair of cells clustered on one side is
          // hidden for half of every corner - the first version put both on
          // -X and they were invisible from the camera. And a cell centred
          // inside the spike field is swallowed by it whatever its colour.
          offsetShare = new Vector3(0.52f, 0.46f, 0.16f),
          scaleShare = new Vector3(0.42f, 0.42f, 0.42f),
          euler = Vector3.zero,
          // Bright amber against a purple parent. A same-hue cell disappears,
          // which is what the review of the previous attempt was pointing at.
          color = new Color(1f, 0.82f, 0.38f),
        },
        new Part
        {
          sourcePrefab = "BasicEnemy", meshObject = SphereMesh,
          materialName = "DaughterCell",
          offsetShare = new Vector3(-0.30f, 0.26f, -0.48f),
          scaleShare = new Vector3(0.33f, 0.33f, 0.33f),
          euler = Vector3.zero,
          color = new Color(1f, 0.76f, 0.32f),
        },
      },
    },

    // SWARM - a colony of three rods rather than one small enemy.
    //
    // Swarm's problem was never colour, it was that a shrunken Fast enemy is
    // still a Fast enemy. Repeating its body twice more, off-axis and smaller,
    // changes the SHAPE into a clump - and a clump is what the mechanic is.
    new Composition
    {
      configName = "SwarmEnemy", baseName = "FastEnemy",
      parts = new[]
      {
        new Part
        {
          sourcePrefab = "FastEnemy", meshObject = FastBody,
          materialName = "ColonyRod",
          offsetShare = new Vector3(0.08f, 0.52f, 0.52f),
          scaleShare = new Vector3(1.28f, 0.36f, 0.36f),
          euler = new Vector3(0f, -22f, 9f),
          color = new Color(0.97f, 0.58f, 0.22f),
        },
        new Part
        {
          sourcePrefab = "FastEnemy", meshObject = FastBody,
          materialName = "ColonyRod",
          offsetShare = new Vector3(-0.05f, 0.44f, -0.55f),
          scaleShare = new Vector3(1.07f, 0.30f, 0.30f),
          euler = new Vector3(0f, 17f, -7f),
          color = new Color(0.99f, 0.70f, 0.30f),
        },
      },
    },

    // HEALER - the Fast model's tendril mesh, wrapped around the Basic body as
    // an aura that reaches outward.
    //
    // A healer acts on its NEIGHBOURS, so the silhouette should reach out of
    // itself. Tendrils do that and are authored organic geometry, so they sit
    // on a detailed body without looking bolted on - which the cone they
    // replaced did not.
    new Composition
    {
      configName = "HealerEnemy", baseName = "BasicEnemy",
      parts = new[]
      {
        new Part
        {
          sourcePrefab = "FastEnemy", meshObject = "Hair_Hair",
          materialName = "HealerAura",
          offsetShare = new Vector3(0f, 0.50f, 0f),
          // Has to be BIGGER than the body, not the same size. At 1.1 the
          // tendrils ended inside the spike field and read as tangle rather
          // than as reach; the whole point is a silhouette that extends past
          // the creature towards its neighbours.
          scaleShare = new Vector3(1.55f, 1.45f, 1.45f),
          euler = Vector3.zero,
          // Near-white, not green. A green aura on a green body is the same
          // mistake as a purple cell on a purple body.
          color = new Color(0.97f, 1f, 0.92f),
        },
      },
    },
  };

  [MenuItem("Tools/Enemies/Report Base Parts")]
  public static void ReportBaseParts()
  {
    var lines = new List<string>();
    foreach (string name in new[] { "BasicEnemy", "FastEnemy", "ArmoredEnemy", "BossEnemy" })
    {
      GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
      if (prefab == null) { lines.Add($"{name}: MISSING"); continue; }
      lines.Add($"--- {name} (prefab scale {prefab.transform.localScale.x:F3})");
      foreach (MeshRenderer r in prefab.GetComponentsInChildren<MeshRenderer>(true))
      {
        Mesh m = r.GetComponent<MeshFilter>()?.sharedMesh;
        lines.Add($"    obj={r.gameObject.name,-36} verts={(m == null ? 0 : m.vertexCount),-8} " +
                  $"mat={(r.sharedMaterial == null ? "none" : r.sharedMaterial.name),-12} " +
                  $"bounds={(m == null ? default : m.bounds.size)}");
      }
    }
    Debug.Log("BASE PARTS:\n" + string.Join("\n", lines));
  }

  [MenuItem("Tools/Enemies/Build Variety Prefabs")]
  public static void BuildVariants()
  {
    Directory.CreateDirectory(MaterialDir);
    var report = new List<string>();

    foreach (Composition comp in Compositions)
    {
      GameObject basePrefab =
        AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{comp.baseName}.prefab");
      if (basePrefab == null)
      {
        Debug.LogError($"EnemyArtSetup: base prefab missing: {comp.baseName}");
        continue;
      }

      // Instantiating the base prefab and saving that instance under a new name
      // produces a prefab VARIANT, so each variety prefab keeps inheriting the
      // base body's Enemy component, prefab scale and material assignments.
      GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
      instance.name = comp.configName;

      MeshRenderer body = Enemy.FindBodyRenderer(instance);
      if (body == null)
      {
        Debug.LogError($"EnemyArtSetup: no body renderer under {comp.baseName}");
        Object.DestroyImmediate(instance);
        continue;
      }

      Bounds local = LocalBounds(instance.transform, body);
      float width = (local.size.x + local.size.z) * 0.5f;
      int added = 0;

      foreach (Part part in comp.parts)
      {
        Mesh mesh = FindPartMesh(part.sourcePrefab, part.meshObject);
        if (mesh == null)
        {
          Debug.LogError($"EnemyArtSetup: part {part.sourcePrefab}/{part.meshObject} " +
                         "not found. Run Tools/Enemies/Report Base Parts.");
          continue;
        }

        var go = new GameObject($"{part.materialName}{++added}");
        go.transform.SetParent(instance.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = PartMaterial(part);
        // Added parts are never the silhouette a shadow needs to describe, and
        // the board already draws 30+ enemies.
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        EnemyTrait marker = go.AddComponent<EnemyTrait>();
        marker.accentColor = part.color;
        marker.tintWithBiome = false;
        marker.hideWhileShieldDown = part.hideWhileShieldDown;

        // The mesh is scaled RELATIVE TO ITS OWN SIZE so that a scaleShare of
        // 1 means "as wide as the body". Without dividing by the source mesh's
        // extents, every share would be in units of whatever that mesh
        // happened to measure, and the numbers above would mean nothing.
        Vector3 src = mesh.bounds.size;
        go.transform.localScale = new Vector3(
          width * part.scaleShare.x / Mathf.Max(0.0001f, src.x),
          width * part.scaleShare.y / Mathf.Max(0.0001f, src.y),
          width * part.scaleShare.z / Mathf.Max(0.0001f, src.z));
        go.transform.localRotation = Quaternion.Euler(part.euler);
        go.transform.localPosition = new Vector3(
          local.center.x + part.offsetShare.x * local.size.x,
          local.min.y + part.offsetShare.y * local.size.y,
          local.center.z + part.offsetShare.z * local.size.z);

        // The source mesh is not centred on its own pivot, so a part placed by
        // its transform alone lands off by its pivot offset - most visible on
        // Fast's hair, whose pivot sits well outside the tendrils.
        go.transform.localPosition -= go.transform.localRotation *
          Vector3.Scale(mesh.bounds.center, go.transform.localScale);
      }

      string prefabPath = $"{PrefabDir}/{comp.configName}.prefab";
      GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
      Object.DestroyImmediate(instance);

      var config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(
        $"{ConfigDir}/{comp.configName}.asset");
      if (config == null)
      {
        Debug.LogError($"EnemyArtSetup: config missing for {comp.configName}");
      }
      else
      {
        config.prefab = saved;
        if (comp.setBodyColor)
        {
          config.overrideBodyColor = true;
          config.bodyColor = comp.bodyColor;
        }
        EditorUtility.SetDirty(config);
      }

      report.Add($"{comp.configName,-16} base={comp.baseName,-13} parts={added} " +
                 $"bodyWidth={width:F2} bodyLocal(size={local.size})");
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
    Debug.Log("EnemyArtSetup built:\n" + string.Join("\n", report));
  }

  private static Mesh FindPartMesh(string prefabName, string objectName)
  {
    GameObject prefab =
      AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
    if (prefab == null) return null;
    foreach (MeshRenderer r in prefab.GetComponentsInChildren<MeshRenderer>(true))
    {
      if (r.gameObject.name != objectName) continue;
      return r.GetComponent<MeshFilter>()?.sharedMesh;
    }
    return null;
  }

  // World-space renderer bounds expressed in the root's local space. Uses the
  // renderer's own transform because a base body can sit at an offset under the
  // root, which is exactly the case a root-relative guess gets wrong.
  private static Bounds LocalBounds(Transform root, MeshRenderer body)
  {
    Mesh mesh = body.GetComponent<MeshFilter>()?.sharedMesh;
    Bounds source = mesh != null ? mesh.bounds : body.localBounds;
    Matrix4x4 toRoot = root.worldToLocalMatrix * body.transform.localToWorldMatrix;

    Vector3 c = source.center, e = source.extents;
    var result = new Bounds(toRoot.MultiplyPoint3x4(c), Vector3.zero);
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

  // One material per part role. Written as an asset rather than created at
  // runtime so the prefab can reference it, and so EnemyTrait's
  // MaterialPropertyBlock has a _BaseColor to override.
  private static Material PartMaterial(Part part)
  {
    string path = $"{MaterialDir}/{part.materialName}.mat";
    Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");
    Material mat = existing ?? new Material(shader);
    mat.shader = shader;

    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", part.color);
    if (mat.HasProperty("_Color")) mat.color = part.color;
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    // A translucent part wants LOW smoothness: at high smoothness the shield
    // bubble read as polished glass rather than as a membrane.
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", part.color.a < 1f ? 0.25f : 0.4f);

    if (part.color.a < 1f) MakeTransparent(mat);

    if (existing == null) AssetDatabase.CreateAsset(mat, path);
    else EditorUtility.SetDirty(mat);
    return mat;
  }

  // URP's transparency is not one property: the surface mode, the blend
  // factors, depth writing, the render queue AND a shader keyword all have to
  // agree. Setting only _Surface leaves the material opaque at runtime, which
  // looks like the alpha being ignored.
  private static void MakeTransparent(Material mat)
  {
    mat.SetFloat("_Surface", 1f);
    mat.SetFloat("_Blend", 0f);
    mat.SetFloat("_ZWrite", 0f);
    mat.SetFloat("_AlphaClip", 0f);
    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
    mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    mat.DisableKeyword("_ALPHATEST_ON");
    mat.SetOverrideTag("RenderType", "Transparent");
    mat.renderQueue = (int)RenderQueue.Transparent;
  }
}
