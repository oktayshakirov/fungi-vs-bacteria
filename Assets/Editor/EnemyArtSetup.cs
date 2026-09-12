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
      // Mint, chosen in review over cyan, violet and gold: cyan sat too close
      // to the Armored enemy's blue, violet to the splitter's purple and the
      // boss's magenta, and gold to the splitter's amber orbs.
      setBodyColor = true, bodyColor = new Color(0.20f, 0.40f, 0.40f),
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
          color = new Color(0.62f, 1f, 0.86f, 0.28f),
          hideWhileShieldDown = true,
          motion = EnemyTrait.Motion.Breathe, motionSpeed = 0.55f,
        },
      },
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
      parts = new[]
      {
        Orb(new Vector3(0.54f, 0.52f, 0.14f), 0.30f),
        Orb(new Vector3(-0.32f, 0.26f, -0.50f), 0.24f),
        Orb(new Vector3(0.16f, 0.18f, 0.55f), 0.21f),
        Orb(new Vector3(-0.50f, 0.62f, 0.22f), 0.18f),
        Orb(new Vector3(0.30f, 0.78f, -0.34f), 0.16f),
        Orb(new Vector3(-0.14f, 0.86f, 0.12f), 0.13f),
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

  // A glowing daughter cell. Amber against the splitter's purple body, which
  // is the contrast rule from Priority 2b: a same-hue part disappears.
  private static Part Orb(Vector3 offsetShare, float size) => new Part
  {
    sourcePrefab = "BasicEnemy", meshObject = SphereMesh,
    materialName = "DaughterCell",
    offsetShare = offsetShare,
    scaleShare = new Vector3(size, size, size),
    euler = Vector3.zero,
    // Deeper amber than it looks like it should be, because emission
    // MULTIPLIES and anything over 1 clips per channel. A light amber at
    // emission 1.5 clipped to near-white and the orbs stopped reading as
    // amber at all; starting darker means the clipped result is still amber.
    color = new Color(1f, 0.58f, 0.12f),
    // Tuned to look right with NO bloom, deliberately. Emission is verified
    // working here (the material carries _EMISSION and the orbs self-light),
    // but bloom's halo could not be verified: URP's post-processing does not
    // run for a camera driven by Camera.Render() from an editor batch method,
    // so no preview in this project can show it. Anything above ~2 clips to
    // white and the orbs lose their amber identity, which is a real regression
    // if bloom turns out to be off on a device.
    emission = 1.35f,
    // Slow: these are meant to drift around the parent, and anything faster
    // reads as a spinning prop bolted to it.
    motion = EnemyTrait.Motion.Orbit, motionSpeed = 0.16f,
  };

  // A glowing healing cross, built as two crossed capsules.
  //
  // It lies FLAT, in the horizontal plane, and that is the whole trick: a plus
  // is symmetric under a quarter turn, so a horizontal one still reads as a
  // plus no matter which way the enemy is facing. An upright cross would need
  // to billboard towards the camera - otherwise it degenerates into a single
  // bar every time the enemy turns side-on - and billboarding means feeding a
  // camera into every part's animation for a decoration.
  //
  // The game camera looks down at roughly 30-40 degrees, so a flat cross is
  // foreshortened rather than square. It still reads; a bar does not.
  //
  // Both bars share the same offsetShare, which is what keeps them together
  // while orbiting: Orbit preserves each part's own radius and bearing, so two
  // parts authored at the same position travel as one object.
  private static Part[] HealerParts()
  {
    var parts = new List<Part>
    {
      new Part
      {
        sourcePrefab = "FastEnemy", meshObject = "Hair_Hair",
        materialName = "HealerAura",
        offsetShare = new Vector3(0f, 0.50f, 0f),
        // Pulled in from 1.32 to leave the crosses somewhere to sit. With the
        // aura at its old size the signs were inside the tendrils and read as
        // red specks caught in them.
        scaleShare = new Vector3(1.12f, 1.08f, 1.08f),
        euler = Vector3.zero,
        color = new Color(0.86f, 1f, 0.84f),
        motion = EnemyTrait.Motion.Pulse, motionSpeed = 0.7f,
      },
    };

    // Three crosses at different bearings and heights, so at least one is on
    // the camera side at any point in the orbit.
    // Radii are deliberately OUTSIDE the aura's 1.12 half-width (so beyond
    // ~0.56 of the body's size), and the arms are long: a small cross at this
    // camera's elevation is foreshortened into an unreadable speck, which is
    // exactly what the first attempt produced.
    parts.AddRange(Cross(new Vector3(0.84f, 0.62f, 0.14f), 0.32f, HealRed));
    parts.AddRange(Cross(new Vector3(-0.42f, 0.36f, -0.72f), 0.27f, HealRed));
    parts.AddRange(Cross(new Vector3(-0.20f, 0.94f, 0.60f), 0.23f, HealRed));
    return parts.ToArray();
  }

  // Deep red, not a light one: emission multiplies and clips per channel, so a
  // pale red washes out to pink-white at any useful emission strength.
  //
  // A PROPERTY, not a static readonly field, and that matters. `Compositions`
  // is a static initialiser that calls HealerParts(), so it runs before any
  // static field declared later in the file - a `static readonly Color` here
  // was still (0,0,0,0) when the parts were built. The signs came out
  // transparent black, and because alpha 0 also routes the material through
  // MakeTransparent they rendered as invisible smudges rather than as anything
  // that looked like a colour mistake. A property is evaluated on use, so
  // declaration order stops mattering.
  private static Color HealRed => new Color(0.94f, 0.07f, 0.06f);

  private static Part[] Cross(Vector3 offsetShare, float size, Color color)
  {
    Part Bar(Vector3 scaleShare) => new Part
    {
      sourcePrefab = "FastEnemy", meshObject = FastBody,
      materialName = "HealSign",
      offsetShare = offsetShare,
      scaleShare = scaleShare,
      euler = Vector3.zero,
      color = color,
      // Low. Emission ADDS to the lit colour, so a strong value lifts the
      // green and blue channels too and a saturated red turns salmon - which
      // is the opposite of what a healing sign needs. Just enough to look
      // self-lit.
      emission = 0.8f,
      motion = EnemyTrait.Motion.Orbit, motionSpeed = 0.12f,
    };

    float arm = size;
    // Thin enough that the two bars read as a cross rather than as a blob.
    // The bars are capsules, so their rounded ends eat into the apparent arm
    // length; at 0.24 the two of them merged into a fat lozenge.
    float thick = size * 0.15f;
    return new[]
    {
      Bar(new Vector3(arm, thick, thick)),
      Bar(new Vector3(thick, thick, arm)),
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
