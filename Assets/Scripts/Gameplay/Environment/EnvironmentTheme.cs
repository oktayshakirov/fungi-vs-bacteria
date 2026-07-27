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
          ambient = C(0.60f, 0.50f, 0.44f),
          lightColor = C(1f, 0.78f, 0.52f),
          lightIntensity = 1.3f,
          lightAngles = new Vector3(16f, 20f, 0f),
          sunColor = C(1f, 0.55f, 0.22f),
          sunSize = 0.02f, sunGlow = 3.5f,
          hazeColor = C(1f, 0.70f, 0.40f), hazeStrength = 0.7f,
          cloudColor = C(1f, 0.78f, 0.58f), cloudStrength = 0.55f, cloudScale = 2.2f,
          starStrength = 0f,
          ground = "SAND",
          groundTint = Color.white,
          soilColor = C(0.40f, 0.26f, 0.16f),
          pathColor = C(0.55f, 0.40f, 0.26f),
          rockColor = C(0.72f, 0.56f, 0.38f),
          plantColor = C(0.52f, 0.60f, 0.30f),
          structureColor = C(0.66f, 0.50f, 0.36f),
          accentGlow = C(1f, 0.72f, 0.32f),
        };

      // Toxic / night — cool purple, moon and stars over glowing swamp
      case "Environment 3":
        return new Palette
        {
          skyTop = C(0.06f, 0.04f, 0.18f),
          skyHorizon = C(0.42f, 0.24f, 0.60f),
          skyBottom = C(0.16f, 0.10f, 0.30f),
          ambient = C(0.24f, 0.26f, 0.38f),
          lightColor = C(0.62f, 0.70f, 1f),
          lightIntensity = 0.8f,
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
          plantColor = C(0.35f, 0.70f, 0.35f),
          structureColor = C(0.42f, 0.32f, 0.56f),
          accentGlow = C(0.55f, 1f, 0.35f),
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
          ground = "Textures/Green Ground",
          groundTint = Color.white,
          soilColor = C(0.40f, 0.28f, 0.17f),
          pathColor = C(0.66f, 0.50f, 0.32f),
          rockColor = C(0.55f, 0.56f, 0.58f),
          plantColor = C(0.28f, 0.60f, 0.24f),
          structureColor = C(0.62f, 0.52f, 0.70f),
          accentGlow = C(0.55f, 0.95f, 1f),
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
      case "SAND": return GroundTextureFactory.Sand();
      case "TOXIC": return GroundTextureFactory.Toxic();
      case "DARK": return GroundTextureFactory.Dark();
      default: return Resources.Load<Texture2D>(key);
    }
  }

  private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
}
