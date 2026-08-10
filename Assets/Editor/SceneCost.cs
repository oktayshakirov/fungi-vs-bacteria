using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reports what a built level actually costs to draw: triangles, renderers,
// unique materials, and how much of that is batchable.
//
// The environment is generated at runtime, so none of this shows up in the
// scene file — the only way to know the cost is to build it and count. Run in
// batch mode without -nographics.
public static class SceneCost
{
  // Looks up at the island from outside and below — the one angle none of the
  // gameplay camera poses reach, and the only way to judge the cliff silhouette.
  public static void RenderCliff()
  {
    EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);

    GridManager grid = Object.FindFirstObjectByType<GridManager>();
    // Theme first: LevelDecorator reads EnvironmentTheme.Current as it builds
    EnvironmentTheme.Apply("Environment 1");
    var decorGo = new GameObject("CliffDecor");
    decorGo.AddComponent<LevelDecorator>().BuildAt(PreviewPath(grid));

    Camera cam = Object.FindFirstObjectByType<Camera>();
    var rig = cam.GetComponent<CameraRig>();
    if (rig != null) rig.enabled = false;   // stop it re-framing on the board

    cam.transform.position = new Vector3(-74f, -20f, -112f);
    cam.transform.LookAt(new Vector3(0f, -16f, 0f));
    cam.fieldOfView = 40f;

    const int w = 1024, h = 576;
    var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
    cam.targetTexture = rt;
    cam.aspect = (float)w / h;
    cam.Render();

    RenderTexture.active = rt;
    var shot = new Texture2D(w, h, TextureFormat.RGB24, false);
    shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
    shot.Apply();
    RenderTexture.active = null;

    System.IO.Directory.CreateDirectory("Builds/CameraPreview");
    System.IO.File.WriteAllBytes("Builds/CameraPreview/cliff-underside.png", shot.EncodeToPNG());

    cam.targetTexture = null;
    Debug.Log("CLIFF OK");
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  public static void Report()
  {
    EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);

    GridManager grid = Object.FindFirstObjectByType<GridManager>();
    Vector3[] path = PreviewPath(grid);

    var decorGo = new GameObject("CostDecor");
    var decor = decorGo.AddComponent<LevelDecorator>();

    foreach (string env in new[] { "Environment 1", "Environment 5" })
    {
      EnvironmentTheme.Apply(env);
      decor.BuildAt(path);
      Measure(env, decorGo);
    }

    Object.DestroyImmediate(decorGo);
    if (Application.isBatchMode) EditorApplication.Exit(0);
  }

  private static void Measure(string label, GameObject root)
  {
    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);

    var materials = new HashSet<Material>();
    var meshes = new HashSet<Mesh>();
    var batches = new HashSet<(Mesh, Material)>();

    foreach (MeshRenderer renderer in renderers)
    {
      var filter = renderer.GetComponent<MeshFilter>();
      Mesh mesh = filter != null ? filter.sharedMesh : null;
      if (mesh != null) meshes.Add(mesh);

      foreach (Material m in renderer.sharedMaterials)
      {
        if (m == null) continue;
        materials.Add(m);
        batches.Add((mesh, m));
      }
    }

    // Summed over DISTINCT meshes. Static batching makes many renderers share
    // one merged mesh, so adding up per-renderer would count it over and over.
    long triangles = meshes.Sum(m => (long)(m.triangles.Length / 3));

    Debug.Log($"COST [{label}] renderers={renderers.Length} distinctMeshes={meshes.Count} " +
              $"triangles={triangles:N0} materials={materials.Count} " +
              $"estimatedBatches={batches.Count}");
  }

  private static Vector3[] PreviewPath(GridManager grid)
  {
    var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(
      "Assets/Resources/Levels/Environment1/Level01.asset");
    if (grid == null || level == null || level.pathConfig == null) return null;

    var cells = level.pathConfig.pathGridCoordinates;
    var points = new Vector3[cells.Count];
    for (int i = 0; i < cells.Count; i++)
    {
      points[i] = grid.GridToWorld(cells[i]);
      points[i].y = 0f;
    }
    return points;
  }
}
