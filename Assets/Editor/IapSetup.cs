using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The RevenueCat keys, and the one-shot that puts the Iap component in the menu
// scene and writes them into it. The sibling of AdsSetup, for the same reason:
// a value edited only in a scene drifts from the code that expects it.
//
// These are RevenueCat **public SDK keys** (`appl_…` / `goog_…`). Like the ad
// unit IDs in AdsSetup they ship inside every build and are not secrets - the
// secret key, which is the one that can read and modify purchase data, is a
// different key that belongs on a server and must never enter this project.
//
// The component is attached to the SAME GameObject as LevelPlayAds, which is
// already DontDestroyOnLoad, so the store works from the menu and from the
// game-over screen without re-configuring the SDK per scene.
public static class IapSetup
{
  // Fill these in from RevenueCat -> Project settings -> API keys, then run
  // Tools -> IAP -> Apply Keys. Until they are set the store shows no prices
  // and every purchase button is disabled - deliberately, rather than throwing.
  private const string RevenueCatIosApiKey = "";
  private const string RevenueCatAndroidApiKey = "";

  private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

  [MenuItem("Tools/IAP/Apply Keys")]
  public static void Apply()
  {
    Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

    LevelPlayAds ads = null;
    foreach (GameObject root in scene.GetRootGameObjects())
    {
      ads = root.GetComponentInChildren<LevelPlayAds>(true);
      if (ads != null) break;
    }

    if (ads == null)
    {
      Debug.LogError("IAP SETUP: no LevelPlayAds in the menu scene to attach to.");
      if (Application.isBatchMode) EditorApplication.Exit(1);
      return;
    }

    GameObject host = ads.gameObject;

    // AddComponent pulls in Purchases and IapListener through Iap's
    // [RequireComponent], so the object ends up with the whole set.
    var iap = host.GetComponent<Iap>();
    if (iap == null) iap = host.AddComponent<Iap>();

    var so = new SerializedObject(iap);
    so.FindProperty("iosApiKey").stringValue = RevenueCatIosApiKey;
    so.FindProperty("androidApiKey").stringValue = RevenueCatAndroidApiKey;
    so.ApplyModifiedPropertiesWithoutUndo();

    EditorSceneManager.MarkSceneDirty(scene);
    EditorSceneManager.SaveScene(scene);

    bool keysSet = !string.IsNullOrWhiteSpace(RevenueCatIosApiKey)
                   || !string.IsNullOrWhiteSpace(RevenueCatAndroidApiKey);
    Debug.Log($"IAP SETUP: component attached to '{host.name}'; " +
              $"keys={(keysSet ? "written" : "EMPTY - purchases stay disabled")}");

    if (Application.isBatchMode) EditorApplication.Exit(0);
  }
}
