using System;
using UnityEngine;

// A five day check-in, each day claimed by watching a rewarded ad.
//
// The escalating payout is the point: day five is worth more than days one to
// three combined, so the streak is worth protecting rather than something to
// dip into casually. Missing a day resets to day one - without that, there is
// no reason to come back tomorrow specifically.
//
// Deliberately separate from RewardedGate: this claim is once a day and should
// not be blocked by, or count towards, the wallet's cooldown and daily cap.
public static class DailyStreak
{
  public static readonly int[] Rewards = { 100, 150, 250, 400, 750 };

  private const string DayIndexKey = "Streak_DayIndex";     // 0-based, next to claim
  private const string LastClaimKey = "Streak_LastClaim";   // yyyy-MM-dd

  public static int Length => Rewards.Length;

  private static string Today => DateTime.Now.ToString("yyyy-MM-dd");
  private static string Yesterday => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

  private static string LastClaim => PlayerPrefs.GetString(LastClaimKey, "");

  // Resolved on read rather than tracked, so a player who closes the app for a
  // week gets the right answer without anything having run in between.
  private static int ResolvedIndex
  {
    get
    {
      int stored = PlayerPrefs.GetInt(DayIndexKey, 0);
      string last = LastClaim;

      if (string.IsNullOrEmpty(last)) return 0;
      if (last == Today) return stored;        // already claimed; stored is today's
      if (last == Yesterday) return stored + 1 >= Length ? 0 : stored + 1;
      return 0;                                // a gap - streak is broken
    }
  }

  // 1-based, for display.
  public static int CurrentDay => ResolvedIndex + 1;

  public static bool ClaimedToday => LastClaim == Today;

  public static int TodayReward => Rewards[Mathf.Clamp(ResolvedIndex, 0, Length - 1)];

  // How many days of the current streak are already banked, for the pip row.
  public static int ClaimedInStreak => ClaimedToday ? ResolvedIndex + 1 : ResolvedIndex;

  public static int Claim()
  {
    if (ClaimedToday) return 0;

    int index = ResolvedIndex;
    int reward = Rewards[Mathf.Clamp(index, 0, Length - 1)];

    PlayerPrefs.SetInt(DayIndexKey, index);
    PlayerPrefs.SetString(LastClaimKey, Today);
    PlayerPrefs.Save();

    Wallet.Add(reward);
    return reward;
  }
}
