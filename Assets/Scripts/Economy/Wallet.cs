using System;
using UnityEngine;

// The persistent meta currency, kept deliberately separate from GameManager's
// `currentGold`. Gold is per-level and resets every run; coins survive across
// runs and are what rewarded ads pay out.
//
// PlayerPrefs is the store, matching LevelProgress. This is client-side and
// trivially editable by a determined player — acceptable for a single-player
// game with no server, and the same trade-off LevelProgress already makes.
public static class Wallet
{
  private const string CoinsKey = "Wallet_Coins";

  // Coins granted the first time a level is cleared, indexed by star rating.
  // Playing has to pay something, or the only way to earn is to watch ads and
  // the currency reads as a paywall rather than a reward.
  private static readonly int[] StarPayout = { 0, 10, 20, 35 };

  public static event Action<int> OnCoinsChanged;

  public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);

  public static void Add(int amount)
  {
    if (amount <= 0) return;
    Set(Coins + amount);
  }

  public static bool TrySpend(int amount)
  {
    if (amount <= 0 || Coins < amount) return false;
    Set(Coins - amount);
    return true;
  }

  public static bool CanAfford(int amount) => Coins >= amount;

  // Pays out for a level result, and only for the part that is new: clearing a
  // level you already 3-starred pays nothing, but improving 1 star to 3 pays
  // the difference. Without this, replaying the easiest level is the optimal
  // way to farm coins.
  public static int AwardForLevel(string environment, int levelNumber, int stars, int previousStars)
  {
    stars = Mathf.Clamp(stars, 0, 3);
    previousStars = Mathf.Clamp(previousStars, 0, 3);
    if (stars <= previousStars) return 0;

    int payout = StarPayout[stars] - StarPayout[previousStars];
    Add(payout);
    return payout;
  }

  private static void Set(int amount)
  {
    PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, amount));
    PlayerPrefs.Save();
    OnCoinsChanged?.Invoke(Coins);
  }
}
