using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Renders the gameplay camera at several device aspect ratios so the framing
// can be checked without launching the game on hardware.
public static class CameraPreview
{
  private struct Device
  {
    public string name;
    public int width;
    public int height;
  }

  private static readonly Device[] Devices =
  {
    new Device { name = "ipad-4x3", width = 1024, height = 768 },
    new Device { name = "laptop-16x9", width = 1024, height = 576 },
    new Device { name = "iphone-19.5x9", width = 1040, height = 480 },
    new Device { name = "android-20x9", width = 1067, height = 480 }
  };

  private const string OutputDir = "Builds/CameraPreview";

  public static void Render()
  {
    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);

    CameraRig rig = Object.FindFirstObjectByType<CameraRig>();
    if (rig == null)
    {
      Debug.LogError("PREVIEW FAIL: no CameraRig in MainGame scene");
      if (Application.isBatchMode) EditorApplication.Exit(1);
      return;
    }

    Camera cam = rig.GetComponent<Camera>();
    List<GameObject> temporaries = BuildTemporaryVisuals();

    Directory.CreateDirectory(OutputDir);

    foreach (Device device in Devices)
    {
      Capture(rig, cam, device, 0, "play");
    }
    Capture(rig, cam, Devices[1], 1, "intro");
    Capture(rig, cam, Devices[1], 2, "outro");

    foreach (GameObject temp in temporaries)
    {
      Object.DestroyImmediate(temp);
    }

    Debug.Log($"PREVIEW OK: wrote images to {OutputDir}");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  private static void Capture(CameraRig rig, Camera cam, Device device, int poseIndex, string label)
  {
    float aspect = (float)device.width / device.height;

    rig.EditorPreview(poseIndex, aspect);
    rig.enabled = false; // stop LateUpdate from re-framing with the editor aspect

    var rt = new RenderTexture(device.width, device.height, 24, RenderTextureFormat.ARGB32)
    {
      antiAliasing = 2
    };

    RenderTexture previousTarget = cam.targetTexture;
    RenderTexture previousActive = RenderTexture.active;

    cam.targetTexture = rt;
    cam.aspect = aspect;
    cam.Render();

    RenderTexture.active = rt;
    var texture = new Texture2D(device.width, device.height, TextureFormat.RGB24, false);
    texture.ReadPixels(new Rect(0, 0, device.width, device.height), 0, 0);

    ShadeHudBands(texture, rig.TopReserve, rig.BottomReserve);
    texture.Apply();

    File.WriteAllBytes($"{OutputDir}/{device.name}-{label}.png", texture.EncodeToPNG());

    cam.targetTexture = previousTarget;
    RenderTexture.active = previousActive;
    rt.Release();
    Object.DestroyImmediate(rt);
    Object.DestroyImmediate(texture);

    rig.enabled = true;
  }

  // Darkens the strips the HUD will occupy, so it is obvious whether the board
  // is hidden behind the stats bar or the towers panel.
  private static void ShadeHudBands(Texture2D texture, float topReserve, float bottomReserve)
  {
    int topRows = Mathf.RoundToInt(texture.height * topReserve);
    int bottomRows = Mathf.RoundToInt(texture.height * bottomReserve);

    for (int y = 0; y < texture.height; y++)
    {
      bool inBand = y < bottomRows || y >= texture.height - topRows;
      if (!inBand) continue;

      for (int x = 0; x < texture.width; x++)
      {
        Color c = texture.GetPixel(x, y);
        texture.SetPixel(x, y, Color.Lerp(c, new Color(0.9f, 0.2f, 0.4f), 0.35f));
      }
    }
  }

  // The path and towers only exist at runtime, so stand-ins are spawned to make
  // the framing legible.
  private static List<GameObject> BuildTemporaryVisuals()
  {
    var created = new List<GameObject>();

    GridManager grid = Object.FindFirstObjectByType<GridManager>();
    LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(
      "Assets/Resources/Levels/Environment1/Level01.asset");
    if (grid == null || level == null || level.pathConfig == null) return created;

    var pathMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
    {
      color = new Color(0.35f, 0.22f, 0.12f)
    };
    var towerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
    {
      color = new Color(0.55f, 0.25f, 0.7f)
    };

    var pathCells = new HashSet<Vector2Int>(level.pathConfig.pathGridCoordinates);
    foreach (Vector2Int cell in pathCells)
    {
      GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
      marker.name = "TEMP_Path";
      marker.transform.position = grid.GridToWorld(cell) + Vector3.up * 0.05f;
      marker.transform.localScale = new Vector3(grid.cellSize, 0.1f, grid.cellSize);
      marker.GetComponent<MeshRenderer>().sharedMaterial = pathMaterial;
      created.Add(marker);
    }

    // A few stand-in towers next to the path, to show scale and perspective
    int placed = 0;
    for (int x = 0; x < grid.gridSize.x && placed < 6; x++)
    {
      for (int y = 0; y < grid.gridSize.y && placed < 6; y++)
      {
        var cell = new Vector2Int(x, y);
        if (pathCells.Contains(cell)) continue;
        if (!pathCells.Contains(cell + Vector2Int.up) && !pathCells.Contains(cell + Vector2Int.down)) continue;

        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        tower.name = "TEMP_Tower";
        tower.transform.position = grid.GridToWorld(cell) + Vector3.up * 2f;
        tower.transform.localScale = new Vector3(2.4f, 2f, 2.4f);
        tower.GetComponent<MeshRenderer>().sharedMaterial = towerMaterial;
        created.Add(tower);
        placed++;
      }
    }

    return created;
  }
}
