using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The ad identifiers, and the one-shot that writes them where the SDKs read
// them from.
//
// They live in code for the same reason DisplaySetup's camera and board values
// do: a value edited only in the scene or only in an .asset silently drifts,
// and the handoff already lists three bugs caused by exactly that. Re-run
// "Tools/Ads/Apply Ad Keys" after any scene reset and both places agree again.
//
// These are not secrets. Ad unit IDs ship inside every APK and IPA and are
// readable by anyone who unzips one; the dashboards are what actually
// authenticate. Do not add anything here that is genuinely private.
public static class AdsSetup
{
  // --- ironSource / LevelPlay -------------------------------------------
  private const string LevelPlayAndroidAppKey = "278709fa5";
  private const string LevelPlayIosAppKey = "27870661d";

  private const string LevelPlayAndroidInterstitial = "c47fie8fgtkqqwes";
  private const string LevelPlayAndroidRewarded = "2g3ezdg4mymfssc8";
  private const string LevelPlayIosInterstitial = "iuhic3ldrsi2p2o1";
  private const string LevelPlayIosRewarded = "7juzu67ubc1el0b3";

  // --- AdMob -------------------------------------------------------------
  // App IDs only. The AdMob *ad unit* IDs are entered on the LevelPlay
  // dashboard when AdMob is added as a network, never here — see ADS.md.
  private const string AdMobAndroidAppId = "ca-app-pub-5852582960793521~5375331742";
  private const string AdMobIosAppId = "ca-app-pub-5852582960793521~8390056055";

  private const string AttUsageDescription =
    "This identifier will be used to deliver personalized ads to you.";

  private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

  [MenuItem("Tools/Ads/Apply Ad Keys")]
  public static void Apply()
  {
    bool sceneOk = ApplyToScene();
    bool adMobOk = ApplyAdMobSettings();

    Debug.Log($"ADS SETUP: scene keys={(sceneOk ? "written" : "FAILED")}, " +
              $"AdMob app IDs={(adMobOk ? "written" : "FAILED")}");

    if (Application.isBatchMode) EditorApplication.Exit(sceneOk && adMobOk ? 0 : 1);
  }

  private static bool ApplyToScene()
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
      Debug.LogError("ADS SETUP: no LevelPlayAds in the menu scene. " +
                     "Run Tools/Display/Apply Camera + Safe Area Setup first.");
      return false;
    }

    // SerializedObject rather than public fields: the keys stay [SerializeField]
    // private, so the component's own API surface does not grow just to let a
    // build tool write to it.
    var so = new SerializedObject(ads);
    so.FindProperty("androidAppKey").stringValue = LevelPlayAndroidAppKey;
    so.FindProperty("iosAppKey").stringValue = LevelPlayIosAppKey;
    so.FindProperty("androidInterstitialAdUnitId").stringValue = LevelPlayAndroidInterstitial;
    so.FindProperty("iosInterstitialAdUnitId").stringValue = LevelPlayIosInterstitial;
    so.FindProperty("androidRewardedAdUnitId").stringValue = LevelPlayAndroidRewarded;
    so.FindProperty("iosRewardedAdUnitId").stringValue = LevelPlayIosRewarded;
    so.ApplyModifiedPropertiesWithoutUndo();

    EditorSceneManager.MarkSceneDirty(scene);
    EditorSceneManager.SaveScene(scene);
    return true;
  }

  // GoogleMobileAdsSettings is internal to the plugin's editor assembly, and
  // LoadInstance() is the only thing that creates the asset in the right place
  // with the right script GUID. Reflection is the supported-behaviour-through-
  // an-unsupported-door option, and it beats hand-writing the YAML.
  private static bool ApplyAdMobSettings()
  {
    Type type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
    if (type == null)
    {
      Debug.LogError("ADS SETUP: GoogleMobileAdsSettings not found; is com.google.ads.mobile installed?");
      return false;
    }

    MethodInfo load = type.GetMethod("LoadInstance",
      BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
    var settings = load?.Invoke(null, null) as ScriptableObject;
    if (settings == null)
    {
      Debug.LogError("ADS SETUP: could not load or create GoogleMobileAdsSettings.");
      return false;
    }

    Set(type, settings, "GoogleMobileAdsAndroidAppId", AdMobAndroidAppId);
    Set(type, settings, "GoogleMobileAdsIOSAppId", AdMobIosAppId);
    Set(type, settings, "UserTrackingUsageDescription", AttUsageDescription);

    EditorUtility.SetDirty(settings);
    AssetDatabase.SaveAssets();
    return true;
  }

  private static void Set(Type type, object target, string property, string value)
  {
    PropertyInfo info = type.GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
    if (info == null)
    {
      Debug.LogWarning($"ADS SETUP: GoogleMobileAdsSettings has no {property}; skipped.");
      return;
    }
    info.SetValue(target, value);
  }
}
