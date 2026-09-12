using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Post-processing values that the game's look depends on, in code.
//
// Same reason DisplaySetup and AdsSetup exist: a value that lives only inside
// an .asset drifts silently, and the handoff already lists bugs caused by
// exactly that. Re-run Tools/Render/Apply Post Processing after any profile
// reset and the asset agrees with this file again.
//
// Run: -executeMethod RenderSetup.ApplyPostProcessing   (works with -nographics)
public static class RenderSetup
{
  private const string ProfilePath = "Assets/Settings_URP/DefaultVolumeProfile.asset";

  // Bloom was present and ACTIVE in the profile from the start, with an
  // intensity of zero - so nothing in the game glowed and it looked as though
  // emissive materials were being ignored. It is on now, low, because the only
  // thing meant to glow is genuinely self-lit geometry.
  //
  // Threshold above 1 is the load-bearing part: it means ONLY colours pushed
  // past white by an emissive material bloom. At the stock 0.9 every bright
  // surface in the game would join in - the enemies' white eyes, the sky, the
  // neon UI cues - and the result is haze rather than glow.
  private const float BloomIntensity = 0.55f;
  private const float BloomThreshold = 1.05f;
  private const float BloomScatter = 0.62f;

  [MenuItem("Tools/Render/Apply Post Processing")]
  public static void ApplyPostProcessing()
  {
    var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
    if (profile == null)
    {
      Debug.LogError($"RenderSetup: no volume profile at {ProfilePath}");
      return;
    }

    if (!profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
    {
      Debug.LogError("RenderSetup: the profile has no Bloom override");
      return;
    }

    bloom.active = true;
    bloom.intensity.overrideState = true;
    bloom.intensity.value = BloomIntensity;
    bloom.threshold.overrideState = true;
    bloom.threshold.value = BloomThreshold;
    bloom.scatter.overrideState = true;
    bloom.scatter.value = BloomScatter;
    // Off on purpose: high-quality filtering is extra full-screen passes, and
    // this is a mobile-first game whose device performance has never been
    // re-measured (HANDOFF Priority 3).
    bloom.highQualityFiltering.overrideState = true;
    bloom.highQualityFiltering.value = false;

    EditorUtility.SetDirty(bloom);
    EditorUtility.SetDirty(profile);
    AssetDatabase.SaveAssets();
    Debug.Log($"RenderSetup: bloom intensity={BloomIntensity} " +
              $"threshold={BloomThreshold} scatter={BloomScatter}");
  }
}
