using System.Runtime.InteropServices;
using UnityEngine;

// Short tactile feedback for UI presses, on iOS and Android.
//
// Handheld.Vibrate is deliberately not used here: on iOS it maps to the 0.4s
// system vibrate (the "you have a message" buzz), which is far too heavy for a
// button tap, and on Android it has no duration control. iOS goes through
// UIFeedbackGenerator via the native plugin in Plugins/iOS/Haptics.mm; Android
// uses a short amplitude-controlled one-shot. Every other platform is a no-op,
// so callers never need to #if around this.
//
// AudioManager.Vibrate() is still the right call for the heavy "you took
// damage" buzz — it also keeps the Handheld.Vibrate reference that makes Unity
// auto-add android.permission.VIBRATE to the manifest.
public static class Haptics
{
  public enum Style
  {
    Selection, // list/toggle changes: the lightest tick there is
    Light,     // ordinary button press
    Medium,    // a press that commits to something (start wave, place tower)
    Heavy,     // a press that ends something (defeat, sell)
    Success,
    Warning,
    Failure
  }

  // Settings' vibration toggle owns this. Before AudioManager exists (first
  // frame of the menu) haptics are allowed rather than silently swallowed.
  private static bool Allowed =>
    AudioManager.Instance == null || AudioManager.Instance.IsVibrationEnabled;

#if UNITY_IOS && !UNITY_EDITOR
  [DllImport("__Internal")] private static extern void _fvbHapticSelection();
  [DllImport("__Internal")] private static extern void _fvbHapticImpact(int style);
  [DllImport("__Internal")] private static extern void _fvbHapticNotification(int type);
#endif

  public static void Play(Style style)
  {
    if (!Allowed) return;

#if UNITY_IOS && !UNITY_EDITOR
    switch (style)
    {
      case Style.Selection: _fvbHapticSelection(); break;
      case Style.Light: _fvbHapticImpact(0); break;
      case Style.Medium: _fvbHapticImpact(1); break;
      case Style.Heavy: _fvbHapticImpact(2); break;
      case Style.Success: _fvbHapticNotification(0); break;
      case Style.Warning: _fvbHapticNotification(1); break;
      case Style.Failure: _fvbHapticNotification(2); break;
    }
#elif UNITY_ANDROID && !UNITY_EDITOR
    // Android has no notification haptics, so the three notification styles are
    // mapped onto durations that read as distinct.
    switch (style)
    {
      case Style.Selection: AndroidVibrate(8, 40); break;
      case Style.Light: AndroidVibrate(12, 70); break;
      case Style.Medium: AndroidVibrate(20, 130); break;
      case Style.Heavy: AndroidVibrate(35, 200); break;
      case Style.Success: AndroidVibrate(18, 120); break;
      case Style.Warning: AndroidVibrate(28, 160); break;
      case Style.Failure: AndroidVibrate(45, 220); break;
    }
#endif
  }

  // Convenience names so call sites read as intent, not as hardware.
  public static void Select() => Play(Style.Selection);
  public static void Tap() => Play(Style.Light);
  public static void Confirm() => Play(Style.Medium);
  public static void Impact() => Play(Style.Heavy);

#if UNITY_ANDROID && !UNITY_EDITOR
  private static AndroidJavaObject vibrator;
  private static bool vibratorResolved;
  private static int sdkInt;

  private static void AndroidVibrate(long milliseconds, int amplitude)
  {
    ResolveVibrator();
    if (vibrator == null) return;

    try
    {
      if (sdkInt >= 26)
      {
        using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
        using AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
          "createOneShot", milliseconds, amplitude);
        vibrator.Call("vibrate", effect);
      }
      else
      {
        vibrator.Call("vibrate", milliseconds);
      }
    }
    catch (System.Exception e)
    {
      Debug.LogWarning($"Haptics: Android vibrate failed ({e.Message})");
      vibrator = null;
    }
  }

  // Resolved once and cached: each of these JNI lookups costs a managed->Java
  // round trip, and this runs on every button press.
  private static void ResolveVibrator()
  {
    if (vibratorResolved) return;
    vibratorResolved = true;

    try
    {
      using var version = new AndroidJavaClass("android.os.Build$VERSION");
      sdkInt = version.GetStatic<int>("SDK_INT");

      using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
      using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
      vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

      if (vibrator != null && !vibrator.Call<bool>("hasVibrator")) vibrator = null;
    }
    catch (System.Exception e)
    {
      Debug.LogWarning($"Haptics: no vibrator available ({e.Message})");
      vibrator = null;
    }
  }
#endif
}
