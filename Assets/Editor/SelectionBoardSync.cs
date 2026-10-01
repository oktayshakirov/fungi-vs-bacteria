using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sync metadata only, never bake screenshots or rewrite scenes. Preview scenes
// let this read MainGame without closing or saving the user's current scene.
[InitializeOnLoad]
public class SelectionBoardSync : AssetPostprocessor, IPreprocessBuildWithReport
{
  const string ScenePath = "Assets/Scenes/MainGame.unity";
  const string AssetPath = "Assets/Resources/SelectionBoardSettings.asset";
  public int callbackOrder => 0;
  static SelectionBoardSync()
  {
    EditorApplication.delayCall += Sync;
    EditorSceneManager.sceneSaved += scene => { if (scene.path == ScenePath) Sync(); };
    EditorApplication.playModeStateChanged += state =>
    {
      if (state == PlayModeStateChange.ExitingEditMode) Sync();
    };
  }
  static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] old)
  {
    if (imported.Contains(ScenePath) || moved.Contains(ScenePath)) EditorApplication.delayCall += Sync;
  }
  public void OnPreprocessBuild(BuildReport report)
  {
    Sync();
    if (AssetDatabase.LoadAssetAtPath<SelectionBoardSettings>(AssetPath) == null)
      throw new BuildFailedException("Selection preview board settings could not be synchronized.");
  }
  [MenuItem("Tools/Selection/Sync Board Settings")]
  public static void Sync()
  {
    if (EditorApplication.isPlaying) return;
    Scene scene = SceneManager.GetSceneByPath(ScenePath);
    bool temporary = !scene.IsValid() || !scene.isLoaded;
    CameraRig oldRig = CameraRig.Instance;
    if (temporary) scene = EditorSceneManager.OpenPreviewScene(ScenePath);
    try
    {
      var roots = scene.GetRootGameObjects();
      GridManager grid = roots.SelectMany(r => r.GetComponentsInChildren<GridManager>(true)).FirstOrDefault();
      GroundManager ground = roots.SelectMany(r => r.GetComponentsInChildren<GroundManager>(true)).FirstOrDefault();
      Transform soil = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "BoardBase");
      if (grid == null || ground == null || soil == null)
        throw new System.InvalidOperationException("MainGame needs its grid, ground and BoardBase for selection previews.");
      var settings = AssetDatabase.LoadAssetAtPath<SelectionBoardSettings>(AssetPath);
      if (settings == null)
      {
        settings = ScriptableObject.CreateInstance<SelectionBoardSettings>();
        AssetDatabase.CreateAsset(settings, AssetPath);
      }
      string before = EditorJsonUtility.ToJson(settings);
      settings.gridSize = grid.gridSize;
      settings.cellSize = grid.cellSize;
      settings.origin = grid.originPosition;
      settings.groundHeight = ground.transform.position.y;
      settings.groundMesh = ground.GetComponent<MeshFilter>().sharedMesh;
      settings.groundRotation = ground.transform.rotation;
      settings.groundScale = ground.transform.lossyScale;
      settings.groundPosition = ground.transform.position;
      settings.soilMesh = soil.GetComponent<MeshFilter>().sharedMesh;
      settings.soilRotation = soil.rotation;
      settings.soilScale = soil.lossyScale;
      settings.soilPosition = soil.position;
      if (before != EditorJsonUtility.ToJson(settings))
      {
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
      }
    }
    finally
    {
      if (temporary)
      {
        EditorSceneManager.ClosePreviewScene(scene);
        // CameraRig executes in edit mode; opening the preview briefly sets
        // its singleton. Restore the editing session's original owner.
        typeof(CameraRig).GetProperty("Instance").SetValue(null, oldRig);
      }
    }
  }
}
