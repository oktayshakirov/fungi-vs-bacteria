using System;
using UnityEngine;

// The only ads API the game talks to. Everything that shows an ad goes through
// here, and the actual SDK sits behind IAdProvider.
//
// Two reasons for the indirection rather than calling LevelPlay directly from
// the screens:
//   * the game compiles and plays with no ad SDK present at all (in the editor,
//     and before the packages are resolved) — every call simply no-ops;
//   * the interstitial pacing rules are game design, not SDK behaviour, so they
//     live here where they can be reasoned about without an ad network.
public static class Ads
{
  // How often an interstitial is allowed. These are the numbers that decide
  // whether the game feels ad-supported or ad-infested; they are the first
  // thing to tune after a playtest.
  private const int LevelEndsBetweenInterstitials = 3;
  private const float MinSecondsBetweenInterstitials = 90f;
  private const int GraceLevelEnds = 3; // no interstitials at all for a new player

  private const string LevelEndCountKey = "Ads_LevelEndCount";

  public interface IAdProvider
  {
    bool IsInitialized { get; }
    bool IsRewardedReady { get; }
    bool IsRewardedLoading { get; }
    bool IsAnyAdShowing { get; }
    void ShowRewarded(Action<int> onReward, Action onFailed);
    void ShowInterstitial();
    void Prewarm();
  }

  private static IAdProvider provider;
  private static float lastInterstitialTime = float.NegativeInfinity;
  private static int levelEndsSinceInterstitial;

  // Raised when a rewarded ad becomes available or stops being available, so
  // buttons offering one can show or hide themselves.
  public static event Action OnRewardedAvailabilityChanged;

  public static void Register(IAdProvider newProvider)
  {
    provider = newProvider;
    NotifyRewardedAvailability();
  }

  public static bool IsInitialized => provider != null && provider.IsInitialized;

  public static bool IsRewardedReady => provider != null && provider.IsRewardedReady;

  // Distinguishes "an ad is on its way" from "there is no ad". Without it, UI
  // offering a rewarded ad has to choose between claiming it is loading
  // forever or claiming it is unavailable while a load is in flight.
  public static bool IsRewardedLoading => provider != null && provider.IsRewardedLoading;

  // Raised after any full-screen ad is dismissed. Ads take over the iOS audio
  // session, so the game has to restore it afterwards.
  public static event Action OnFullScreenAdClosed;

  public static void NotifyFullScreenAdClosed() => OnFullScreenAdClosed?.Invoke();

  // Raised immediately before a full-screen ad is presented, so the game can
  // fade its own audio out rather than being cut off mid-note.
  public static event Action OnFullScreenAdWillShow;

  public static void NotifyFullScreenAdWillShow() => OnFullScreenAdWillShow?.Invoke();

  // Called when the game is about to need an ad - opening a screen that offers
  // one, or reaching a point where one is likely. Retry backoff is there to
  // stop pointless background polling, not to make a player wait once they
  // have actually arrived at the offer, so this collapses the wait and tries
  // now.
  public static void Prewarm() => provider?.Prewarm();

  public static void NotifyRewardedAvailability() => OnRewardedAvailabilityChanged?.Invoke();

  // onReward carries the amount the mediation dashboard specified, so the
  // payout can be changed without shipping a build. onFailed fires when no ad
  // could be shown, and the caller must leave the player no worse off.
  public static void ShowRewarded(Action<int> onReward, Action onFailed = null)
  {
    if (provider == null || !provider.IsRewardedReady)
    {
      onFailed?.Invoke();
      return;
    }

    provider.ShowRewarded(onReward, onFailed);
  }

  // Called at the end of every level, win or lose. Whether an ad actually shows
  // is decided here, so call sites do not each need their own counter.
  public static void OnLevelEnded()
  {
    int total = PlayerPrefs.GetInt(LevelEndCountKey, 0) + 1;
    PlayerPrefs.SetInt(LevelEndCountKey, total);
    PlayerPrefs.Save();

    levelEndsSinceInterstitial++;

    if (total <= GraceLevelEnds) return;
    if (levelEndsSinceInterstitial < LevelEndsBetweenInterstitials) return;
    if (Time.realtimeSinceStartup - lastInterstitialTime < MinSecondsBetweenInterstitials) return;
    if (provider == null || !provider.IsInitialized || provider.IsAnyAdShowing) return;

    levelEndsSinceInterstitial = 0;
    lastInterstitialTime = Time.realtimeSinceStartup;
    provider.ShowInterstitial();
  }

  // Watching a rewarded ad buys the player out of the next interstitial: back
  // to back full-screen ads is the fastest way to make someone uninstall.
  public static void DeferInterstitial()
  {
    lastInterstitialTime = Time.realtimeSinceStartup;
    levelEndsSinceInterstitial = 0;
  }
}
