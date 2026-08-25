using System;
using UnityEngine;

// Decides when the wallet's "watch an ad for coins" offer is available.
//
// A rewarded ad is opt-in, so the limit is not there to protect the player from
// ads - it is there to stop the coin faucet being farmed. Two mechanisms, and
// they do different jobs:
//
//   * an escalating cooldown (1, 5, then 10 minutes) spaces out a burst, so the
//     offer stays worth coming back to rather than being drained in one sitting;
//   * a daily cap is what actually bounds the economy, because a cooldown alone
//     only limits the rate, not the total.
//
// Both reset at local midnight. PlayerPrefs is the store, so a determined player
// can move the device clock - the same trade-off LevelProgress already makes,
// and not worth a server for a single-player game.
public static class RewardedGate
{
  public const int DailyCap = 10;

  // Applied after the 1st, 2nd, and 3rd-or-later watch of the day.
  private static readonly int[] CooldownMinutes = { 1, 5, 10 };

  private const string DayKey = "Rewarded_Day";
  private const string CountKey = "Rewarded_CountToday";
  private const string NextKey = "Rewarded_NextUtcTicks";

  private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

  // Rolls the day over lazily, on read, so no update loop is needed.
  private static void SyncDay()
  {
    if (PlayerPrefs.GetString(DayKey, "") == Today) return;

    PlayerPrefs.SetString(DayKey, Today);
    PlayerPrefs.SetInt(CountKey, 0);
    PlayerPrefs.SetString(NextKey, "0");
    PlayerPrefs.Save();
  }

  public static int WatchesToday
  {
    get { SyncDay(); return PlayerPrefs.GetInt(CountKey, 0); }
  }

  public static int WatchesLeftToday => Mathf.Max(0, DailyCap - WatchesToday);

  public static bool CapReached => WatchesLeftToday <= 0;

  public static TimeSpan Remaining
  {
    get
    {
      SyncDay();
      long ticks = long.TryParse(PlayerPrefs.GetString(NextKey, "0"), out long t) ? t : 0L;
      var left = new DateTime(ticks, DateTimeKind.Utc) - DateTime.UtcNow;
      return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }
  }

  public static bool IsReady => !CapReached && Remaining <= TimeSpan.Zero;

  // The cooldown that will apply after the next watch, so the UI can tell the
  // player what they are about to trade away before they commit.
  public static int NextCooldownMinutes =>
    CooldownMinutes[Mathf.Clamp(WatchesToday, 0, CooldownMinutes.Length - 1)];

  public static void RecordWatch()
  {
    SyncDay();

    int count = PlayerPrefs.GetInt(CountKey, 0);
    int minutes = CooldownMinutes[Mathf.Clamp(count, 0, CooldownMinutes.Length - 1)];

    PlayerPrefs.SetInt(CountKey, count + 1);
    PlayerPrefs.SetString(NextKey,
      DateTime.UtcNow.AddMinutes(minutes).Ticks.ToString());
    PlayerPrefs.Save();
  }

  // mm:ss while under an hour, which every cooldown here is.
  public static string RemainingText()
  {
    TimeSpan left = Remaining;
    return $"{(int)left.TotalMinutes:0}:{left.Seconds:00}";
  }
}
