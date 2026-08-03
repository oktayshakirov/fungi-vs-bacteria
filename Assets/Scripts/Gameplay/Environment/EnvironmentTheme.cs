using UnityEngine;

// Applies a per-environment visual theme at level load: a rich gradient sky
// (sun/moon, clouds, stars) so the board reads as a floating island, plus a
// distinct ground texture, soil colour, and lighting for each environment.
public static class EnvironmentTheme
{
  public struct Palette
  {
    public Color skyTop, skyHorizon, skyBottom;
    public Color ambient;
    public Color lightColor;
    public float lightIntensity;
    public Vector3 lightAngles;

    public Color sunColor;
    public float sunSize, sunGlow;
    public Color hazeColor;
    public float hazeStrength;
    public Color cloudColor;
    public float cloudStrength, cloudScale;
    public float starStrength;

    public string ground;   // "SAND", "TOXIC", or a Resources texture path
    public Color groundTint;
    public Color soilColor;
    public float groundTiling;

    // Decoration (used by LevelDecorator for path, props, base and portal)
    public Color pathColor;
    public Color rockColor;
    public Color plantColor;
    public Color structureColor;
    public Color accentGlow;
    public Color woodColor;              // tree trunks
    public Color grassColor;             // grass blades
    public Color cliffTop, cliffBottom;  // strata gradient down the island cliff

    public Color fogColor;
    public float fogDensity;             // exponential-squared; 0 disables fog
  }

  private static Material skyMaterial;

  // The palette applied for the current level, read by LevelDecorator.
  public static Palette Current { get; private set; }

  public static void Apply(string environmentName)
  {
    Palette p = GetPalette(environmentName);
    Current = p;
    ApplyLighting(p);   // set the light first so the sun can align to it
    ApplySky(p);
    ApplyGround(p);
    ApplyPath(p);
  }

  // Recolours the path line. Critical for the dark toxic environment, where a
  // dark path would vanish into the dark ground.
  private static void ApplyPath(Palette p)
  {
    PathVisualizer pv = Object.FindFirstObjectByType<PathVisualizer>();
    if (pv == null) return;
    var line = pv.GetComponent<LineRenderer>();
    if (line == null) return;

    // The path material uses a near-black texture; under the Unlit shader that
    // multiplies the tint down to black. Clear the texture so the path renders
    // as its actual colour (critical on the dark environment).
    Material mat = line.material;
    mat.mainTexture = null;
    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
    mat.color = p.pathColor;
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", p.pathColor);

    // LineRenderer also multiplies by its vertex colours
    line.startColor = Color.white;
    line.endColor = Color.white;
  }

  private static Palette GetPalette(string environmentName)
  {
    switch (environmentName)
    {
      // Wetland / warm dusk — golden sunset over sand
      case "Environment 2":
        return new Palette
        {
          skyTop = C(0.24f, 0.20f, 0.52f),
          skyHorizon = C(1f, 0.60f, 0.30f),
          skyBottom = C(0.72f, 0.38f, 0.28f),
          ambient = C(0.50f, 0.47f, 0.52f),
          lightColor = C(1f, 0.86f, 0.68f),
          lightIntensity = 1.28f,
          lightAngles = new Vector3(16f, 20f, 0f),
          sunColor = C(1f, 0.55f, 0.22f),
          sunSize = 0.02f, sunGlow = 3.5f,
          hazeColor = C(1f, 0.74f, 0.48f), hazeStrength = 0.42f,
          cloudColor = C(1f, 0.78f, 0.58f), cloudStrength = 0.55f, cloudScale = 2.2f,
          starStrength = 0f,
          ground = "SAND",
          groundTint = Color.white,
          soilColor = C(0.40f, 0.26f, 0.16f),
          pathColor = C(0.55f, 0.40f, 0.26f),
          rockColor = C(0.60f, 0.52f, 0.46f),
          plantColor = C(0.52f, 0.60f, 0.30f),
          structureColor = C(0.66f, 0.50f, 0.36f),
          accentGlow = C(1f, 0.72f, 0.32f),
          woodColor = C(0.42f, 0.30f, 0.20f),
          grassColor = C(0.58f, 0.56f, 0.28f),
          cliffTop = C(0.60f, 0.44f, 0.27f),
          cliffBottom = C(0.34f, 0.27f, 0.24f),
          fogColor = C(0.92f, 0.74f, 0.58f),
          fogDensity = 0.0022f,
        };

      // Toxic / night — cool purple, moon and stars over glowing swamp
      case "Environment 3":
        return new Palette
        {
          skyTop = C(0.06f, 0.04f, 0.18f),
          skyHorizon = C(0.42f, 0.24f, 0.60f),
          skyBottom = C(0.16f, 0.10f, 0.30f),
          ambient = C(0.38f, 0.41f, 0.55f),
          lightColor = C(0.70f, 0.78f, 1f),
          lightIntensity = 1.05f,
          lightAngles = new Vector3(30f, 32f, 0f),
          sunColor = C(0.88f, 0.92f, 1f),
          sunSize = 0.02f, sunGlow = 12f,
          hazeColor = C(0.40f, 0.28f, 0.58f), hazeStrength = 0.30f,
          cloudColor = C(0.30f, 0.24f, 0.44f), cloudStrength = 0.2f, cloudScale = 2.2f,
          starStrength = 1.1f,
          ground = "DARK",
          groundTint = Color.white,
          soilColor = C(0.20f, 0.14f, 0.26f),
          groundTiling = 2f,
          // Bright warm path so it clearly stands out on the dark ground
          pathColor = C(0.92f, 0.86f, 0.60f),
          rockColor = C(0.22f, 0.26f, 0.30f),
          plantColor = C(0.26f, 0.48f, 0.32f),
          structureColor = C(0.42f, 0.32f, 0.56f),
          accentGlow = C(0.55f, 1f, 0.35f),
          woodColor = C(0.20f, 0.18f, 0.24f),
          grassColor = C(0.26f, 0.42f, 0.30f),
          cliffTop = C(0.26f, 0.22f, 0.30f),
          cliffBottom = C(0.13f, 0.12f, 0.20f),
          fogColor = C(0.32f, 0.24f, 0.46f),
          fogDensity = 0.0030f,
        };

      // Frozen tundra — bright overcast day, snow and ice
      case "Environment 4":
        return new Palette
        {
          skyTop = C(0.42f, 0.60f, 0.82f),
          skyHorizon = C(0.82f, 0.90f, 0.97f),
          skyBottom = C(0.74f, 0.82f, 0.88f),
          ambient = C(0.70f, 0.75f, 0.82f),
          lightColor = C(0.88f, 0.94f, 1f),
          lightIntensity = 1.35f,
          lightAngles = new Vector3(38f, 24f, 0f),
          sunColor = C(1f, 1f, 1f),
          sunSize = 0.014f, sunGlow = 4f,
          hazeColor = C(0.94f, 0.97f, 1f), hazeStrength = 0.6f,
          cloudColor = Color.white, cloudStrength = 0.75f, cloudScale = 2.6f,
          starStrength = 0f,
          ground = "SNOW",
          groundTint = Color.white,
          groundTiling = 4f,
          soilColor = C(0.44f, 0.48f, 0.56f),
          pathColor = C(0.55f, 0.62f, 0.72f),
          rockColor = C(0.58f, 0.63f, 0.70f),
          plantColor = C(0.24f, 0.42f, 0.40f),   // dark firs against the snow
          structureColor = C(0.66f, 0.74f, 0.84f),
          accentGlow = C(0.55f, 0.88f, 1f),
          woodColor = C(0.30f, 0.25f, 0.24f),
          grassColor = C(0.68f, 0.75f, 0.82f),
          cliffTop = C(0.60f, 0.65f, 0.72f),
          cliffBottom = C(0.30f, 0.34f, 0.42f),
          fogColor = C(0.86f, 0.92f, 0.98f),
          fogDensity = 0.0040f,
        };

      // Volcanic — ash plain under an ember sky
      case "Environment 5":
        return new Palette
        {
          skyTop = C(0.14f, 0.05f, 0.08f),
          skyHorizon = C(0.85f, 0.26f, 0.09f),
          skyBottom = C(0.42f, 0.12f, 0.08f),
          ambient = C(0.42f, 0.30f, 0.28f),
          lightColor = C(1f, 0.62f, 0.42f),
          lightIntensity = 1.05f,
          lightAngles = new Vector3(20f, 14f, 0f),
          sunColor = C(1f, 0.42f, 0.14f),
          sunSize = 0.026f, sunGlow = 6f,
          hazeColor = C(1f, 0.44f, 0.18f), hazeStrength = 0.5f,
          cloudColor = C(0.35f, 0.16f, 0.14f), cloudStrength = 0.5f, cloudScale = 2.0f,
          starStrength = 0f,
          ground = "ASH",
          groundTint = Color.white,
          groundTiling = 3f,
          soilColor = C(0.20f, 0.13f, 0.11f),
          pathColor = C(0.90f, 0.86f, 0.78f),
          rockColor = C(0.26f, 0.22f, 0.22f),
          plantColor = C(0.34f, 0.26f, 0.18f),   // scorched scrub
          structureColor = C(0.44f, 0.28f, 0.24f),
          accentGlow = C(1f, 0.52f, 0.16f),
          woodColor = C(0.18f, 0.14f, 0.13f),
          grassColor = C(0.38f, 0.28f, 0.18f),
          cliffTop = C(0.30f, 0.20f, 0.16f),
          cliffBottom = C(0.16f, 0.12f, 0.12f),
          fogColor = C(0.55f, 0.24f, 0.14f),
          fogDensity = 0.0048f,
        };

      // Alien bloom — bioluminescent growth under a teal-violet sky
      case "Environment 6":
        return new Palette
        {
          skyTop = C(0.10f, 0.05f, 0.24f),
          skyHorizon = C(0.16f, 0.62f, 0.62f),
          skyBottom = C(0.10f, 0.30f, 0.38f),
          ambient = C(0.44f, 0.52f, 0.56f),
          lightColor = C(0.78f, 0.95f, 0.98f),
          lightIntensity = 1.25f,
          lightAngles = new Vector3(34f, 40f, 0f),
          sunColor = C(0.70f, 1f, 0.95f),
          sunSize = 0.018f, sunGlow = 8f,
          hazeColor = C(0.30f, 0.75f, 0.75f), hazeStrength = 0.45f,
          cloudColor = C(0.30f, 0.50f, 0.60f), cloudStrength = 0.4f, cloudScale = 2.3f,
          starStrength = 0.7f,
          ground = "DARK",
          // Above 1 deliberately: the neutral dark ground is built for a night
          // scene, and this lifts it to a readable mid teal
          groundTint = C(1.5f, 2.1f, 2.0f),
          groundTiling = 3f,
          soilColor = C(0.16f, 0.22f, 0.24f),
          pathColor = C(0.86f, 0.92f, 0.70f),
          rockColor = C(0.30f, 0.34f, 0.42f),
          plantColor = C(0.42f, 0.24f, 0.62f),   // violet foliage
          structureColor = C(0.34f, 0.52f, 0.60f),
          accentGlow = C(1f, 0.30f, 0.85f),      // magenta bioluminescence
          woodColor = C(0.24f, 0.20f, 0.32f),
          grassColor = C(0.26f, 0.52f, 0.48f),
          cliffTop = C(0.24f, 0.30f, 0.34f),
          cliffBottom = C(0.12f, 0.18f, 0.24f),
          fogColor = C(0.16f, 0.48f, 0.52f),
          fogDensity = 0.0042f,
        };

      // Blossom grove — warm pink autumn at golden hour
      case "Environment 7":
        return new Palette
        {
          skyTop = C(0.36f, 0.52f, 0.86f),
          skyHorizon = C(1f, 0.80f, 0.76f),
          skyBottom = C(0.92f, 0.70f, 0.66f),
          ambient = C(0.68f, 0.60f, 0.60f),
          lightColor = C(1f, 0.92f, 0.82f),
          lightIntensity = 1.3f,
          lightAngles = new Vector3(28f, 34f, 0f),
          sunColor = C(1f, 0.90f, 0.72f),
          sunSize = 0.016f, sunGlow = 5f,
          hazeColor = C(1f, 0.88f, 0.86f), hazeStrength = 0.5f,
          cloudColor = C(1f, 0.94f, 0.94f), cloudStrength = 0.65f, cloudScale = 2.4f,
          starStrength = 0f,
          ground = "MEADOW",
          groundTint = C(1f, 0.90f, 0.86f),      // warms the grass toward autumn
          groundTiling = 4f,
          soilColor = C(0.42f, 0.28f, 0.24f),
          pathColor = C(0.74f, 0.56f, 0.44f),
          rockColor = C(0.62f, 0.56f, 0.56f),
          plantColor = C(0.94f, 0.55f, 0.68f),   // blossom canopies
          structureColor = C(0.72f, 0.56f, 0.62f),
          accentGlow = C(1f, 0.62f, 0.80f),
          woodColor = C(0.38f, 0.26f, 0.24f),
          grassColor = C(0.55f, 0.62f, 0.34f),
          cliffTop = C(0.52f, 0.38f, 0.30f),
          cliffBottom = C(0.30f, 0.26f, 0.28f),
          fogColor = C(1f, 0.86f, 0.82f),
          fogDensity = 0.0036f,
        };

      // Meadow / clear day — the default
      default:
        return new Palette
        {
          skyTop = C(0.16f, 0.44f, 0.86f),
          skyHorizon = C(0.53f, 0.78f, 0.95f),
          skyBottom = C(0.50f, 0.72f, 0.66f),
          ambient = C(0.62f, 0.66f, 0.68f),
          lightColor = C(1f, 0.97f, 0.86f),
          lightIntensity = 1.25f,
          lightAngles = new Vector3(32f, 28f, 0f),
          sunColor = C(1f, 0.97f, 0.80f),
          sunSize = 0.012f, sunGlow = 5f,
          hazeColor = C(0.95f, 0.98f, 1f), hazeStrength = 0.45f,
          cloudColor = Color.white, cloudStrength = 0.7f, cloudScale = 2.4f,
          starStrength = 0f,
          ground = "MEADOW",
          groundTint = Color.white,
          groundTiling = 4f,
          soilColor = C(0.40f, 0.28f, 0.17f),
          pathColor = C(0.66f, 0.50f, 0.32f),
          rockColor = C(0.55f, 0.56f, 0.58f),
          plantColor = C(0.28f, 0.60f, 0.24f),
          structureColor = C(0.62f, 0.52f, 0.70f),
          accentGlow = C(0.55f, 0.95f, 1f),
          woodColor = C(0.38f, 0.27f, 0.18f),
          grassColor = C(0.35f, 0.62f, 0.24f),
          cliffTop = C(0.46f, 0.34f, 0.23f),
          cliffBottom = C(0.28f, 0.27f, 0.30f),
          fogColor = C(0.74f, 0.86f, 0.96f),
          fogDensity = 0.0034f,
        };
    }
  }

  private static Light keyLight;

  private static void ApplyLighting(Palette p)
  {
    keyLight = null;
    foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
    {
      if (light.type == LightType.Directional) { keyLight = light; break; }
    }
    if (keyLight == null) return;

    keyLight.color = p.lightColor;
    keyLight.intensity = p.lightIntensity;
    keyLight.transform.rotation = Quaternion.Euler(p.lightAngles);
    keyLight.shadows = LightShadows.Soft;
    keyLight.shadowStrength = 0.72f;

    // Atmospheric depth: the distant clouds and floating islands fade toward the
    // horizon colour, which is most of what sells the "high in the sky" look.
    // Tuned so the play area itself is barely touched.
    RenderSettings.fog = p.fogDensity > 0f;
    RenderSettings.fogMode = FogMode.ExponentialSquared;
    RenderSettings.fogColor = p.fogColor;
    RenderSettings.fogDensity = p.fogDensity;
  }

  private static void ApplySky(Palette p)
  {
    if (skyMaterial == null)
    {
      Shader shader = Shader.Find("FungiVsBacteria/GradientSky");
      if (shader != null) skyMaterial = new Material(shader);
    }

    if (skyMaterial != null)
    {
      skyMaterial.SetColor("_Top", p.skyTop);
      skyMaterial.SetColor("_Horizon", p.skyHorizon);
      skyMaterial.SetColor("_Bottom", p.skyBottom);

      // The sun sits opposite the light's facing direction, up in the sky
      Vector3 sunDir = keyLight != null ? -keyLight.transform.forward
                                        : Quaternion.Euler(p.lightAngles) * Vector3.back;
      skyMaterial.SetVector("_SunDir", sunDir);
      skyMaterial.SetColor("_SunColor", p.sunColor);
      skyMaterial.SetFloat("_SunSize", p.sunSize);
      skyMaterial.SetFloat("_SunGlow", p.sunGlow);

      skyMaterial.SetColor("_HazeColor", p.hazeColor);
      skyMaterial.SetFloat("_HazeStrength", p.hazeStrength);
      skyMaterial.SetColor("_CloudColor", p.cloudColor);
      skyMaterial.SetFloat("_CloudStrength", p.cloudStrength);
      skyMaterial.SetFloat("_CloudScale", p.cloudScale);
      skyMaterial.SetFloat("_StarStrength", p.starStrength);

      RenderSettings.skybox = skyMaterial;
    }

    Camera cam = Camera.main;
    if (cam != null)
    {
      cam.clearFlags = skyMaterial != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
      cam.backgroundColor = p.skyHorizon;
    }

    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight = p.ambient;
  }

  private static void ApplyGround(Palette p)
  {
    if (GroundManager.Instance != null)
    {
      var renderer = GroundManager.Instance.GetComponent<MeshRenderer>();
      if (renderer != null)
      {
        Texture tex = ResolveGround(p.ground);
        Material mat = renderer.material; // instance, so the shared asset is untouched
        if (tex != null)
        {
          float tiling = p.groundTiling > 0f ? p.groundTiling : 3f;
          mat.SetTexture("_BaseMap", tex);
          mat.mainTextureScale = new Vector2(tiling, tiling);
        }
        mat.SetColor("_BaseColor", p.groundTint);
      }
    }

    GameObject baseGo = GameObject.Find("BoardBase");
    if (baseGo != null)
    {
      var renderer = baseGo.GetComponent<MeshRenderer>();
      if (renderer != null) renderer.material.SetColor("_BaseColor", p.soilColor);
    }
  }

  private static Texture ResolveGround(string key)
  {
    switch (key)
    {
      case "MEADOW": return GroundTextureFactory.Meadow();
      case "SNOW": return GroundTextureFactory.Snow();
      case "ASH": return GroundTextureFactory.Ash();
      case "SAND": return GroundTextureFactory.Sand();
      case "TOXIC": return GroundTextureFactory.Toxic();
      case "DARK": return GroundTextureFactory.Dark();
      default: return Resources.Load<Texture2D>(key);
    }
  }

  private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
}
