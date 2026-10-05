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
// hand-made in a creature. The small healing symbols are built separately
// as effect geometry by HealerSymbolArt and SplitterCellArt.
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
    // Emission strength. Above zero the part lights itself, and because Bloom
    // is active in DefaultVolumeProfile it also blooms. Kept modest: bloom is
    // fill-rate work on a phone and performance is still unmeasured
    // (HANDOFF Priority 3).
    public float emission;
    public bool hideWhileShieldDown;
    public EnemyTrait.Motion motion;
    public float motionSpeed;
    public float motionAmount;
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
  // Closed daughter-cell effects and camera-facing healer symbols are baked
  // separately. Eye whites have iris openings and cannot serve as closed cells.
  private const string FastBody = "Body_Body";

  private static readonly Composition[] Compositions =
  {
    // Shield identity is an opaque patterned carapace; no enclosing orb.
    new Composition
    {
      configName = "ShieldedEnemy", baseName = "ArmoredEnemy",
      setBodyColor = true, bodyColor = new Color(.38f,.70f,.60f),
      parts = new Part[0],
    },

    // SPLITTER - a shoal of glowing daughter cells orbiting the parent.
    //
    // Two buds read correctly but quietly. Six smaller orbs spread all the way
    // around, self-lit so they catch the eye, say "this one multiplies" before
    // it ever dies - and because they ring the body there is no angle from
    // which the type is unreadable.
    //
    // Positions are hand-spread rather than generated on a circle so they look
    // like cells budding at their own pace instead of a mechanical halo.
    new Composition
    {
      configName = "SplitterEnemy", baseName = "BasicEnemy",
      parts = new Part[0], // Closed buds are added by SplitterCellArt.
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
          motion = EnemyTrait.Motion.Breathe, motionSpeed = 1.3f, motionAmount = 1.4f,
        },
        new Part
        {
          sourcePrefab = "FastEnemy", meshObject = FastBody,
          materialName = "ColonyRod",
          offsetShare = new Vector3(-0.05f, 0.44f, -0.55f),
          scaleShare = new Vector3(1.07f, 0.30f, 0.30f),
          euler = new Vector3(0f, 17f, -7f),
          color = new Color(0.99f, 0.70f, 0.30f),
          motion = EnemyTrait.Motion.Breathe, motionSpeed = 1.1f, motionAmount = 1.4f,
        },
      },
    },

    // HEALER - a reaching aura, plus glowing healing crosses circling it.
    //
    // A healer acts on its NEIGHBOURS, so the silhouette reaches out of itself
    // and the crosses say what the reaching is FOR. The aura alone was read as
    // "good but unclear"; the crosses are the part that names the mechanic.
    //
    // Note the crosses are red while the body stays green, deliberately. Red
    // is the universal healing sign, but the Basic enemy is already a red
    // spiky ball - so a red-BODIED healer would collide with the most common
    // enemy in the game. Keeping the red confined to the signs gets the
    // reading without the collision. See EnemyPreview.RenderHealerOptions,
    // which renders the alternative side by side.
    new Composition
    {
      configName = "HealerEnemy", baseName = "BasicEnemy",
      parts = HealerParts(),
    },
  };

  // Keep the healer's authored tendrils. HealerSymbolArt adds the three
  // beveled, camera-facing effect symbols after the organic parts are built.
  private static Part[] HealerParts()
  {
    return new[]
    {
      new Part
      {
        sourcePrefab = "FastEnemy", meshObject = "Hair_Hair",
        materialName = "HealerAura",
        offsetShare = new Vector3(0f, 0.50f, 0f),
        scaleShare = new Vector3(1.12f, 1.08f, 1.08f),
        euler = Vector3.zero,
        color = new Color(0.86f, 1f, 0.84f),
        motion = EnemyTrait.Motion.Pulse, motionSpeed = 0.7f,
      },
    };
  }

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

      if(comp.configName == "ShieldedEnemy") body.sharedMaterial = EnemySurfaceSetup.ShieldMaterial();

      Bounds local = LocalBounds(instance.transform, body);
      float width = (local.size.x + local.size.z) * 0.5f;
      int added = 0;

      foreach (Part part in comp.parts)
      {
        // Catches the static-initialisation-order trap described on HealRed,
        // and any other part that reaches here with an unset colour: alpha 0
        // is silently valid (it means "transparent"), so without this the part
        // just quietly disappears.
        if (part.color == default)
        {
          Debug.LogError($"EnemyArtSetup: {comp.configName} part " +
                         $"'{part.materialName}' has an unset colour. If it " +
                         "came from a static field, see the note on HealRed.");
        }

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
        marker.motion = part.motion;
        marker.motionSpeed = part.motionSpeed > 0f ? part.motionSpeed : 1f;
        marker.motionAmount = part.motionAmount > 0f ? part.motionAmount : 1f;

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

      if(comp.configName=="HealerEnemy")HealerSymbolArt.Refine(instance);
      if(comp.configName=="SplitterEnemy")SplitterCellArt.Refine(instance);

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

    if (part.emission > 0f && mat.HasProperty("_EmissionColor"))
    {
      mat.SetColor("_EmissionColor", part.color * part.emission);
      // RealtimeEmissive, matching the project's existing emissive materials
      // (Towers/PoisonTower/Projectile.mat). EmissiveIsBlack is the flag that
      // tells Unity this material does not emit, and setting it alongside an
      // emission colour is how the _EMISSION keyword ends up stripped on save:
      // the colour persists, the keyword does not, and the material renders as
      // flat bright paint with no clue why.
      mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    if (part.color.a < 1f) MakeTransparent(mat);

    // Keywords LAST. Assigning mat.shader and changing surface properties both
    // re-validate a material's keyword list, so a keyword enabled earlier in
    // this method can be dropped again before the asset is written.
    if (part.emission > 0f && mat.HasProperty("_EmissionColor"))
    {
      mat.EnableKeyword("_EMISSION");
    }
    else
    {
      mat.DisableKeyword("_EMISSION");
    }

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
