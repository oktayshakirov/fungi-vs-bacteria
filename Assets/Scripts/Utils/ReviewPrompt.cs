using System.Collections;
using UnityEngine;

// Asks for an App Store rating at a good moment: a 3-star win, once the
// player has some levels behind them. iOS draws the system prompt itself
// (SKStoreReviewController) and shows it at most three times a year whatever
// we ask, so the caps here are only about not asking at a bad time.
// Android has no built-in equivalent (Play's in-app review needs its own
// package), so everywhere but an iOS device this is a no-op.
public static class ReviewPrompt
{
  private const int MinLevelsCompleted = 5;
  private const int MaxAsks = 3;
  private const double MinDaysBetweenAsks = 30;

  // After the stars and the coin payout have counted up on the victory screen.
  private const float DelaySeconds = 2.5f;

  private const string AskCountKey = "Review_AskCount";
  private const string LastAskKey = "Review_LastAskUtcTicks";

  // Run on the victory screen, so leaving it before the delay cancels the ask.
  public static void AfterVictory(MonoBehaviour host, int stars)
  {
#if UNITY_IOS && !UNITY_EDITOR
    if (host == null || !IsDue(stars)) return;
    host.StartCoroutine(AskAfterDelay());
#endif
  }

  private static bool IsDue(int stars)
  {
    if (stars < 3) return false;
    if (CompletedLevels() < MinLevelsCompleted) return false;

    int asks = PlayerPrefs.GetInt(AskCountKey, 0);
    if (asks >= MaxAsks) return false;

    if (asks > 0 && long.TryParse(PlayerPrefs.GetString(LastAskKey, ""), out long ticks))
    {
      double days = (System.DateTime.UtcNow - new System.DateTime(ticks, System.DateTimeKind.Utc)).TotalDays;
      if (days < MinDaysBetweenAsks) return false;
    }
    return true;
  }

  private static int CompletedLevels()
  {
    int total = 0;
    for (int n = 1; n <= 7; n++) total += LevelProgress.GetHighestCompletedLevel($"Environment {n}");
    return total;
  }

  private static IEnumerator AskAfterDelay()
  {
    // The game is paused behind the victory screen, so real time.
    yield return new WaitForSecondsRealtime(DelaySeconds);
    if (Ads.IsAnyAdShowing) yield break;

#if UNITY_IOS && !UNITY_EDITOR
    if (!UnityEngine.iOS.Device.RequestStoreReview()) yield break;
#endif

    PlayerPrefs.SetInt(AskCountKey, PlayerPrefs.GetInt(AskCountKey, 0) + 1);
    PlayerPrefs.SetString(LastAskKey, System.DateTime.UtcNow.Ticks.ToString());
    PlayerPrefs.Save();
    Debug.Log("[Review] Rating prompt requested.");
  }
}
